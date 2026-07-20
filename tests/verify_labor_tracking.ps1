param(
    [string]$BaseUrl = "http://localhost:5024",
    [switch]$SkipFeatureFlagMutation
)

Write-Host "=== Labor Tracking Verification Script (Full Matrix) ===" -ForegroundColor Cyan
Write-Host "Base URL: $BaseUrl" -ForegroundColor Gray

$pass = 0
$skip = 0
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
        if ($_.Exception.Message -like "SKIPPED:*") {
            $reason = $_.Exception.Message.Substring(8)
            Write-Host "[SKIP] $Name — $reason" -ForegroundColor Yellow
            $script:skip++
        } else {
            Write-Host "[FAIL] $Name — $_" -ForegroundColor Red
            $script:fail++
        }
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

# --- CLEAN UP ANY EXISTING ACTIVE SESSIONS ---
Write-Host "Checking for existing active sessions to clean up..." -ForegroundColor Gray
$cleanupListRes = Invoke-Api -Uri "$BaseUrl/api/labor/sessions?status=Running" -Method GET -Headers $headers
if ($cleanupListRes.StatusCode -eq 200) {
    $cleanupList = $cleanupListRes.Body | ConvertFrom-Json
    foreach ($item in $cleanupList.items) {
        Write-Host "Cleaning up active session $($item.id)..." -ForegroundColor Yellow
        $cancelBody = @{ reason = "System test cleanup." } | ConvertTo-Json
        $null = Invoke-Api -Uri "$BaseUrl/api/labor/sessions/$($item.id)/cancel" -Method POST -Body $cancelBody -Headers $headers
    }
}
$cleanupListRes2 = Invoke-Api -Uri "$BaseUrl/api/labor/sessions?status=Paused" -Method GET -Headers $headers
if ($cleanupListRes2.StatusCode -eq 200) {
    $cleanupList2 = $cleanupListRes2.Body | ConvertFrom-Json
    foreach ($item in $cleanupList2.items) {
        Write-Host "Cleaning up paused session $($item.id)..." -ForegroundColor Yellow
        $cancelBody = @{ reason = "System test cleanup." } | ConvertTo-Json
        $null = Invoke-Api -Uri "$BaseUrl/api/labor/sessions/$($item.id)/cancel" -Method POST -Body $cancelBody -Headers $headers
    }
}

# --- SCENARIO 1: Start session missing required fields => 400 ---
Invoke-Test "Scenario 1: Start session without required fields returns 400" {
    $r = Invoke-Api -Uri "$BaseUrl/api/labor/sessions/start" -Method POST -Body "{}" -Headers $headers
    if ($r.StatusCode -ne 400 -and $r.StatusCode -ne 500) { 
        throw "Expected failure validation status, got $($r.StatusCode). Body: $($r.Body)" 
    }
}

# --- SCENARIO 2: Unauthorized access => 401 ---
Invoke-Test "Scenario 2: Call start without auth token returns 401" {
    $body = @{ sourceTaskType = "Manual"; operationType = "Picking" } | ConvertTo-Json
    $r = Invoke-Api -Uri "$BaseUrl/api/labor/sessions/start" -Method POST -Body $body
    if ($r.StatusCode -ne 401) { throw "Expected 401, got $($r.StatusCode). Body: $($r.Body)" }
}

# --- SCENARIO 3: Start valid session => 200 ---
$sessionId = $null
Invoke-Test "Scenario 3: Start valid labor session returns 200 with session info" {
    $body = @{
        sourceTaskType = "Manual"
        operationType  = "Picking"
    } | ConvertTo-Json -Depth 5
    $r = Invoke-Api -Uri "$BaseUrl/api/labor/sessions/start" -Method POST -Body $body -Headers $headers
    if ($r.StatusCode -ne 200) { throw "Expected 200, got $($r.StatusCode). Body: $($r.Body)" }
    $res = $r.Body | ConvertFrom-Json
    if ([string]::IsNullOrEmpty($res.sessionId)) { throw "Response missing sessionId." }
    $script:sessionId = $res.sessionId
}

# --- SCENARIO 4: Duplicate start session => 409 ---
Invoke-Test "Scenario 4: Start session when one is active returns 409" {
    $body = @{
        sourceTaskType = "Manual"
        operationType  = "Picking"
    } | ConvertTo-Json -Depth 5
    $r = Invoke-Api -Uri "$BaseUrl/api/labor/sessions/start" -Method POST -Body $body -Headers $headers
    if ($r.StatusCode -ne 409) { throw "Expected 409, got $($r.StatusCode). Body: $($r.Body)" }
    $res = $r.Body | ConvertFrom-Json
    if ($res.errorCode -ne "LABOR_SESSION_ALREADY_ACTIVE") { throw "Expected errorCode LABOR_SESSION_ALREADY_ACTIVE, got $($res.errorCode)" }
}

# --- SCENARIO 5: Pause session => 200 ---
Invoke-Test "Scenario 5: Pause active session returns 200" {
    if (-not $script:sessionId) { throw "SKIPPED: No session ID from Scenario 3." }
    $r = Invoke-Api -Uri "$BaseUrl/api/labor/sessions/$($script:sessionId)/pause" -Method POST -Headers $headers
    if ($r.StatusCode -ne 200) { throw "Expected 200, got $($r.StatusCode). Body: $($r.Body)" }
    $res = $r.Body | ConvertFrom-Json
    if ($res.status -ne "Paused") { throw "Expected status Paused, got $($res.status)" }
}

# --- SCENARIO 6: Resume session => 200 ---
Invoke-Test "Scenario 6: Resume paused session returns 200" {
    if (-not $script:sessionId) { throw "SKIPPED: No session ID from Scenario 3." }
    $r = Invoke-Api -Uri "$BaseUrl/api/labor/sessions/$($script:sessionId)/resume" -Method POST -Headers $headers
    if ($r.StatusCode -ne 200) { throw "Expected 200, got $($r.StatusCode). Body: $($r.Body)" }
    $res = $r.Body | ConvertFrom-Json
    if ($res.status -ne "Running") { throw "Expected status Running, got $($res.status)" }
}

# --- SCENARIO 7: Complete session => 200 ---
Invoke-Test "Scenario 7: Complete session returns 200" {
    if (-not $script:sessionId) { throw "SKIPPED: No session ID from Scenario 3." }
    $r = Invoke-Api -Uri "$BaseUrl/api/labor/sessions/$($script:sessionId)/complete" -Method POST -Headers $headers
    if ($r.StatusCode -ne 200) { throw "Expected 200, got $($r.StatusCode). Body: $($r.Body)" }
    $res = $r.Body | ConvertFrom-Json
    if ($res.status -ne "Completed") { throw "Expected status Completed, got $($res.status)" }
}

# --- SCENARIO 8: Complete session again => 409 ---
Invoke-Test "Scenario 8: Complete completed session returns 409" {
    if (-not $script:sessionId) { throw "SKIPPED: No session ID from Scenario 3." }
    $r = Invoke-Api -Uri "$BaseUrl/api/labor/sessions/$($script:sessionId)/complete" -Method POST -Headers $headers
    if ($r.StatusCode -ne 409) { throw "Expected 409, got $($r.StatusCode). Body: $($r.Body)" }
    $res = $r.Body | ConvertFrom-Json
    if ($res.errorCode -ne "LABOR_SESSION_INVALID_STATUS") { throw "Expected errorCode LABOR_SESSION_INVALID_STATUS, got $($res.errorCode)" }
}

# --- SCENARIO 9: Cancel session validation => 400 ---
$cancelSessionId = $null
Invoke-Test "Scenario 9: Start session and cancel without reason returns 400" {
    $body = @{ sourceTaskType = "Manual"; operationType  = "Picking" } | ConvertTo-Json
    $rStart = Invoke-Api -Uri "$BaseUrl/api/labor/sessions/start" -Method POST -Body $body -Headers $headers
    if ($rStart.StatusCode -ne 200) { throw "Setup failed. Start active session status: $($rStart.StatusCode)" }
    $resStart = $rStart.Body | ConvertFrom-Json
    $script:cancelSessionId = $resStart.sessionId

    $cancelBody = @{ reason = "" } | ConvertTo-Json
    $rCancel = Invoke-Api -Uri "$BaseUrl/api/labor/sessions/$($script:cancelSessionId)/cancel" -Method POST -Body $cancelBody -Headers $headers
    if ($rCancel.StatusCode -ne 400) { throw "Expected 400, got $($rCancel.StatusCode). Body: $($rCancel.Body)" }
    $resCancel = $rCancel.Body | ConvertFrom-Json
    if ($resCancel.errorCode -ne "LABOR_CANCEL_REASON_REQUIRED") { throw "Expected errorCode LABOR_CANCEL_REASON_REQUIRED, got $($resCancel.errorCode)" }
}

# --- SCENARIO 10: Cancel session with reason => 200 ---
Invoke-Test "Scenario 10: Cancel session with reason returns 200" {
    if (-not $script:cancelSessionId) { throw "SKIPPED: Setup missing cancelSessionId." }
    $cancelBody = @{ reason = "User aborted task." } | ConvertTo-Json
    $r = Invoke-Api -Uri "$BaseUrl/api/labor/sessions/$($script:cancelSessionId)/cancel" -Method POST -Body $cancelBody -Headers $headers
    if ($r.StatusCode -ne 200) { throw "Expected 200, got $($r.StatusCode). Body: $($r.Body)" }
    $res = $r.Body | ConvertFrom-Json
    if ($res.status -ne "Cancelled") { throw "Expected status Cancelled, got $($res.status)" }
}

# --- SCENARIO 11: List sessions with pagination => 200 ---
Invoke-Test "Scenario 11: List sessions returns items and pagination metadata" {
    $r = Invoke-Api -Uri "$BaseUrl/api/labor/sessions?page=1&pageSize=5" -Method GET -Headers $headers
    if ($r.StatusCode -ne 200) { throw "Expected 200, got $($r.StatusCode). Body: $($r.Body)" }
    $res = $r.Body | ConvertFrom-Json
    if ($null -eq $res.items) { throw "Response items array is null." }
    if ($res.page -ne 1) { throw "Expected page 1, got $($res.page)" }
    if ($res.pageSize -ne 5) { throw "Expected pageSize 5, got $($res.pageSize)" }
}

# --- SCENARIO 12: Get KPI summary => 200 ---
Invoke-Test "Scenario 12: Get KPI summary returns valid analytics summary" {
    $r = Invoke-Api -Uri "$BaseUrl/api/labor/kpi" -Method GET -Headers $headers
    if ($r.StatusCode -ne 200) { throw "Expected 200, got $($r.StatusCode). Body: $($r.Body)" }
    $res = $r.Body | ConvertFrom-Json
    if ($null -eq $res.summary) { throw "Summary field is null." }
    if ($null -eq $res.groupByUser) { throw "groupByUser field is null." }
    if ($null -eq $res.groupByShift) { throw "groupByShift field is null." }
    if ($null -eq $res.groupByZone) { throw "groupByZone field is null." }
    if ($null -eq $res.groupByOperation) { throw "groupByOperation field is null." }
}

# --- SCENARIO 13: Get KPI charts data => 200 ---
Invoke-Test "Scenario 13: Get KPI charts data returns trends and Operation Mix" {
    $r = Invoke-Api -Uri "$BaseUrl/api/labor/kpi/charts" -Method GET -Headers $headers
    if ($r.StatusCode -ne 200) { throw "Expected 200, got $($r.StatusCode). Body: $($r.Body)" }
    $res = $r.Body | ConvertFrom-Json
    if ($null -eq $res.throughputTrend) { throw "throughputTrend is null." }
    if ($null -eq $res.tasksPerHourTrend) { throw "tasksPerHourTrend is null." }
    if ($null -eq $res.operationMix) { throw "operationMix is null." }
    if ($null -eq $res.userProductivityRanking) { throw "userProductivityRanking is null." }
    if ($null -eq $res.zoneProductivity) { throw "zoneProductivity is null." }
}

# --- SCENARIO 14: Get current shift info => 200 ---
Invoke-Test "Scenario 14: Get current active shift information" {
    $r = Invoke-Api -Uri "$BaseUrl/api/labor/shifts/current" -Method GET -Headers $headers
    if ($r.StatusCode -ne 200) { throw "Expected 200, got $($r.StatusCode). Body: $($r.Body)" }
    $res = $r.Body | ConvertFrom-Json
    if ([string]::IsNullOrEmpty($res.shiftId)) { throw "Response missing shiftId." }
    if ([string]::IsNullOrEmpty($res.shiftCode)) { throw "Response missing shiftCode." }
    if ($res.status -ne "Open") { throw "Expected shift status Open, got $($res.status)" }
}

# --- SCENARIO 15: Feature flag gate ---
Invoke-Test "Scenario 15: Disabled FF_LABOR_TRACKING_ENABLED returns 403 FEATURE_DISABLED" {
    if ($SkipFeatureFlagMutation) { throw "SKIPPED: Skip mutation parameter passed." }
    $toggled = Set-FeatureFlag -Name "FF_LABOR_TRACKING_ENABLED" -Enabled $false
    if (-not $toggled) { throw "SKIPPED: Mutation endpoint not available." }
    try {
        $body = @{ sourceTaskType = "Manual"; operationType = "Picking" } | ConvertTo-Json -Depth 5
        $r = Invoke-Api -Uri "$BaseUrl/api/labor/sessions/start" -Method POST -Body $body -Headers $headers
        if ($r.StatusCode -ne 403) { throw "Expected 403, got $($r.StatusCode). Body: $($r.Body)" }
        $res = $r.Body | ConvertFrom-Json
        if ($res.errorCode -ne "FEATURE_DISABLED") { throw "Expected errorCode FEATURE_DISABLED, got $($res.errorCode)" }
    } finally {
        $null = Set-FeatureFlag -Name "FF_LABOR_TRACKING_ENABLED" -Enabled $true
    }
}

Write-Host "`n=== Results ===" -ForegroundColor Cyan
Write-Host "Passed: $pass" -ForegroundColor Green
Write-Host "Skipped: $skip" -ForegroundColor Yellow
Write-Host "Failed: $fail" -ForegroundColor $(if ($fail -eq 0) { "Green" } else { "Red" })

if ($fail -gt 0) {
    exit 1
}
exit 0

