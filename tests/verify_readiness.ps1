param(
    [string]$BaseUrl = "http://localhost:5024",
    [switch]$SkipFeatureFlagMutation
)

Write-Host "=== Readiness Gate Verification Script ===" -ForegroundColor Cyan
Write-Host "Base URL: $BaseUrl" -ForegroundColor Gray

$pass = 0
$skip = 0
$fail = 0
$script:uatId = $null
$script:laborSessionId = $null

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

# --- AUTH ---
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

# Ensure flags on
$null = Set-FeatureFlag -Name "FF_READINESS_GATE_ENABLED" -Enabled $true
$null = Set-FeatureFlag -Name "FF_CUTOVER_FREEZE_ENABLED" -Enabled $true
$null = Set-FeatureFlag -Name "FF_LABOR_TRACKING_ENABLED" -Enabled $true

# Ensure unfrozen before matrix
$null = Invoke-Api -Uri "$BaseUrl/api/admin/cutover/unfreeze" -Method POST -Body "{}" -Headers $headers

Invoke-Test "Scenario 1: GET readiness probe returns 200" {
    $r = Invoke-Api -Uri "$BaseUrl/api/admin/readiness" -Method GET -Headers $headers
    if ($r.StatusCode -ne 200) { throw "Expected 200, got $($r.StatusCode) $($r.Body)" }
    $json = $r.Body | ConvertFrom-Json
    if (-not $json.overallStatus) { throw "Missing overallStatus" }
    $sap = $json.components | Where-Object { $_.name -eq "SAP" } | Select-Object -First 1
    if ($sap -and $sap.status -notin @("Skipped", "Up", "Degraded")) {
        throw "Unexpected SAP status: $($sap.status)"
    }
}

Invoke-Test "Scenario 2: Missing auth returns 401/403" {
    $r = Invoke-Api -Uri "$BaseUrl/api/admin/readiness" -Method GET
    if ($r.StatusCode -notin @(401, 403)) { throw "Expected 401/403, got $($r.StatusCode)" }
}

Invoke-Test "Scenario 3: Freeze blocks labor session start with 423 CUTOVER_FROZEN" {
    $fr = Invoke-Api -Uri "$BaseUrl/api/admin/cutover/freeze" -Method POST -Body '{"reason":"verify_readiness"}' -Headers $headers
    if ($fr.StatusCode -ne 200) { throw "Freeze failed: $($fr.StatusCode) $($fr.Body)" }

    $r = Invoke-Api -Uri "$BaseUrl/api/labor/sessions/start" -Method POST -Body "{}" -Headers $headers
    if ($r.StatusCode -ne 423) { throw "Expected 423, got $($r.StatusCode) $($r.Body)" }
    if ($r.Body -notmatch "CUTOVER_FROZEN") { throw "Expected CUTOVER_FROZEN in body: $($r.Body)" }
}

Invoke-Test "Scenario 4: Unfreeze allows labor session start" {
    $uf = Invoke-Api -Uri "$BaseUrl/api/admin/cutover/unfreeze" -Method POST -Body "{}" -Headers $headers
    if ($uf.StatusCode -ne 200) { throw "Unfreeze failed: $($uf.StatusCode) $($uf.Body)" }

    $r = Invoke-Api -Uri "$BaseUrl/api/labor/sessions/start" -Method POST -Body "{}" -Headers $headers
    if ($r.StatusCode -eq 423) { throw "Still frozen after unfreeze" }
    if ($r.StatusCode -notin @(200, 201)) {
        # labor may return 400 if session already running — still proves not frozen
        if ($r.Body -match "CUTOVER_FROZEN") { throw "Still returning CUTOVER_FROZEN" }
        if ($r.StatusCode -ge 500) { throw "Server error $($r.StatusCode) $($r.Body)" }
    }
    try {
        $json = $r.Body | ConvertFrom-Json
        if ($json.id) {
            $script:laborSessionId = $json.id
            $null = Invoke-Api -Uri "$BaseUrl/api/labor/sessions/$($json.id)/cancel" -Method POST -Body '{"reason":"verify_cleanup"}' -Headers $headers
        }
    } catch {}
}

Invoke-Test "Scenario 5: Create UAT run + signoff" {
    $create = Invoke-Api -Uri "$BaseUrl/api/admin/readiness/uat-runs" -Method POST -Body '{"scenarioCode":"INBOUND","status":"Passed"}' -Headers $headers
    if ($create.StatusCode -ne 200) { throw "Create UAT failed: $($create.StatusCode) $($create.Body)" }
    $uat = $create.Body | ConvertFrom-Json
    $script:uatId = $uat.id
    $sign = Invoke-Api -Uri "$BaseUrl/api/admin/readiness/uat-runs/$($uat.id)/signoff" -Method POST -Body "{}" -Headers $headers
    if ($sign.StatusCode -ne 200) { throw "Signoff failed: $($sign.StatusCode) $($sign.Body)" }
    $signed = $sign.Body | ConvertFrom-Json
    if ($signed.status -ne "SignedOff") { throw "Expected SignedOff, got $($signed.status)" }
}

Invoke-Test "Scenario 6: Create incident drill with rtoMinutes" {
    $r = Invoke-Api -Uri "$BaseUrl/api/admin/readiness/incident-drills" -Method POST -Body '{"scenarioCode":"DB_DOWN","rtoMinutes":45,"passed":true}' -Headers $headers
    if ($r.StatusCode -ne 200) { throw "Drill failed: $($r.StatusCode) $($r.Body)" }
    $json = $r.Body | ConvertFrom-Json
    if ($json.rtoMinutes -ne 45) { throw "Expected rtoMinutes=45" }
}

Invoke-Test "Scenario 7: List cutover logs pagination" {
    $r = Invoke-Api -Uri "$BaseUrl/api/admin/cutover/logs?page=1&pageSize=5" -Method GET -Headers $headers
    if ($r.StatusCode -ne 200) { throw "List logs failed: $($r.StatusCode) $($r.Body)" }
    $json = $r.Body | ConvertFrom-Json
    if ($null -eq $json.items) { throw "Missing items" }
    if ($json.pageSize -ne 5) { throw "Expected pageSize 5" }
}

Invoke-Test "Scenario 8: Flag off disables readiness API" {
    if ($SkipFeatureFlagMutation) { throw "SKIPPED: SkipFeatureFlagMutation" }
    $ok = Set-FeatureFlag -Name "FF_READINESS_GATE_ENABLED" -Enabled $false
    if (-not $ok) { throw "SKIPPED: Cannot mutate feature flag" }
    try {
        $r = Invoke-Api -Uri "$BaseUrl/api/admin/readiness" -Method GET -Headers $headers
        if ($r.StatusCode -ne 403) { throw "Expected 403, got $($r.StatusCode)" }
        if ($r.Body -notmatch "READINESS_DISABLED") { throw "Expected READINESS_DISABLED: $($r.Body)" }
    } finally {
        $null = Set-FeatureFlag -Name "FF_READINESS_GATE_ENABLED" -Enabled $true
    }
}

Invoke-Test "Scenario 9: SAP skipped does not force NotReady when DB up" {
    $r = Invoke-Api -Uri "$BaseUrl/api/admin/readiness" -Method GET -Headers $headers
    if ($r.StatusCode -ne 200) { throw "Probe failed: $($r.StatusCode)" }
    $json = $r.Body | ConvertFrom-Json
    if ($json.overallStatus -eq "NotReady") {
        $db = $json.components | Where-Object { $_.name -eq "Database" } | Select-Object -First 1
        if ($db.status -eq "Up") { throw "DB Up but overall NotReady" }
    }
}

Write-Host ""
Write-Host "=== Summary: PASS=$pass SKIP=$skip FAIL=$fail ===" -ForegroundColor Cyan
if ($fail -gt 0) { exit 1 } else { exit 0 }
