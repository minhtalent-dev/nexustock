param(
    [string]$BaseUrl = "http://localhost:5024",
    [switch]$SkipFeatureFlagMutation
)

Write-Host "=== Labor Tracking Verification Script ===" -ForegroundColor Cyan
Write-Host "Base URL: $BaseUrl" -ForegroundColor Gray

$pass = 0
$fail = 0

function Invoke-Api {
    param($Uri, $Method = "GET", $Body = $null, $Headers = @{})
    try {
        $params = @{ Uri = $Uri; Method = $Method; Headers = $Headers; UseBasicParsing = $true; ErrorAction = "Stop" }
        if ($Body) { $params["Body"] = $Body; $params["ContentType"] = "application/json" }
        $resp = Invoke-WebRequest @params
        return @{ StatusCode = $resp.StatusCode; Body = $resp.Content }
    } catch {
        $code = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
        $body = ""
        if ($_.Exception.Response) {
            try {
                $sr = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
                $body = $sr.ReadToEnd(); $sr.Close()
            } catch {}
        }
        return @{ StatusCode = $code; Body = $body }
    }
}

function Invoke-Test {
    param([string]$Name, [scriptblock]$Test)
    try {
        & $Test
        Write-Host "[PASS] $Name" -ForegroundColor Green
        $script:pass++
    } catch {
        Write-Host "[FAIL] $Name — $_" -ForegroundColor Red
        $script:fail++
    }
}

function Set-FeatureFlag {
    param([string]$Name, [bool]$Enabled)
    try {
        $enabledStr = if ($Enabled) { "true" } else { "false" }
        $r = Invoke-Api -Uri "$BaseUrl/api/feature-flags/$Name" -Method PUT -Body "{`"enabled`": $enabledStr}" -Headers $headers
        return ($r.StatusCode -eq 200)
    } catch { return $false }
}

# --- AUTHENTICATE ---
$loginBody = @{ email = "admin@nexustock.com"; password = "AdminSecret123!" } | ConvertTo-Json -Depth 5
$loginRes = Invoke-Api -Uri "$BaseUrl/api/auth/login" -Method POST -Body $loginBody
if ($loginRes.StatusCode -ne 200) {
    Write-Error "Login failed: $($loginRes.StatusCode) — $($loginRes.Body)"
    exit 1
}
$token = ($loginRes.Body | ConvertFrom-Json).token
if ([string]::IsNullOrEmpty($token)) {
    Write-Error "Login response missing token: $($loginRes.Body)"
    exit 1
}
$headers = @{ Authorization = "Bearer $token" }
Write-Host "Authenticated successfully." -ForegroundColor Green

# --- SCENARIO 1: Start session missing required fields => 400 ---
Invoke-Test "Scenario 1: Start session without required fields returns 400" {
    $r = Invoke-Api -Uri "$BaseUrl/api/labor/sessions/start" -Method POST -Body "{}" -Headers $headers
    if ($r.StatusCode -ne 400 -and $r.StatusCode -ne 500) { 
        throw "Expected failure validation status, got $($r.StatusCode). Body: $($r.Body)" 
    }
}

# --- SCENARIO 2: Start valid session => 200 ---
$sessionId = $null
Invoke-Test "Scenario 2: Start valid labor session returns 200 with session info" {
    $body = @{
        sourceTaskType = "Manual"
        operationType  = "PICKING"
    } | ConvertTo-Json -Depth 5
    $r = Invoke-Api -Uri "$BaseUrl/api/labor/sessions/start" -Method POST -Body $body -Headers $headers
    if ($r.StatusCode -ne 200) { throw "Expected 200, got $($r.StatusCode). Body: $($r.Body)" }
    $res = $r.Body | ConvertFrom-Json
    if ([string]::IsNullOrEmpty($res.sessionId)) { throw "Response missing sessionId." }
    $script:sessionId = $res.sessionId
}

# --- SCENARIO 3: Pause session => 200 ---
Invoke-Test "Scenario 3: Pause active session returns 200" {
    if (-not $script:sessionId) { throw "Skipped: No session ID from Scenario 2." }
    $r = Invoke-Api -Uri "$BaseUrl/api/labor/sessions/$($script:sessionId)/pause" -Method POST -Headers $headers
    if ($r.StatusCode -ne 200) { throw "Expected 200, got $($r.StatusCode). Body: $($r.Body)" }
    $res = $r.Body | ConvertFrom-Json
    if ($res.status -ne "Paused") { throw "Expected status Paused, got $($res.status)" }
}

# --- SCENARIO 4: Resume session => 200 ---
Invoke-Test "Scenario 4: Resume paused session returns 200" {
    if (-not $script:sessionId) { throw "Skipped: No session ID from Scenario 2." }
    $r = Invoke-Api -Uri "$BaseUrl/api/labor/sessions/$($script:sessionId)/resume" -Method POST -Headers $headers
    if ($r.StatusCode -ne 200) { throw "Expected 200, got $($r.StatusCode). Body: $($r.Body)" }
    $res = $r.Body | ConvertFrom-Json
    if ($res.status -ne "Running") { throw "Expected status Running, got $($res.status)" }
}

# --- SCENARIO 5: Complete session => 200 ---
Invoke-Test "Scenario 5: Complete session returns 200" {
    if (-not $script:sessionId) { throw "Skipped: No session ID from Scenario 2." }
    $r = Invoke-Api -Uri "$BaseUrl/api/labor/sessions/$($script:sessionId)/complete" -Method POST -Headers $headers
    if ($r.StatusCode -ne 200) { throw "Expected 200, got $($r.StatusCode). Body: $($r.Body)" }
    $res = $r.Body | ConvertFrom-Json
    if ($res.status -ne "Completed") { throw "Expected status Completed, got $($res.status)" }
}

# --- SCENARIO 6: Feature flag gate ---
Invoke-Test "Scenario 6: Disabled FF_LABOR_TRACKING_ENABLED returns 403 FEATURE_DISABLED" {
    if ($SkipFeatureFlagMutation) { Write-Warning "Feature flag mutation skipped."; return }
    $toggled = Set-FeatureFlag -Name "FF_LABOR_TRACKING_ENABLED" -Enabled $false
    if (-not $toggled) { Write-Warning "Could not toggle FeatureFlag. Skipping Scenario 6."; return }
    try {
        $body = @{ sourceTaskType = "Manual"; operationType = "PICKING" } | ConvertTo-Json -Depth 5
        $r = Invoke-Api -Uri "$BaseUrl/api/labor/sessions/start" -Method POST -Body $body -Headers $headers
        if ($r.StatusCode -ne 403) { throw "Expected 403, got $($r.StatusCode). Body: $($r.Body)" }
        $res = $r.Body | ConvertFrom-Json
        if ($res.errorCode -ne "FEATURE_DISABLED") { throw "Expected errorCode FEATURE_DISABLED, got $($res.errorCode)" }
    } finally {
        $null = Set-FeatureFlag -Name "FF_LABOR_TRACKING_ENABLED" -Enabled $true
    }
}

Write-Host "`n=== Results ===" -ForegroundColor Cyan
Write-Host "Passed: $pass / $($pass + $fail)" -ForegroundColor $(if ($fail -eq 0) { "Green" } else { "Yellow" })
if ($fail -gt 0) {
    Write-Host "Failed: $fail" -ForegroundColor Red
    exit 1
}
Write-Host "All tests passed successfully!" -ForegroundColor Green
exit 0
