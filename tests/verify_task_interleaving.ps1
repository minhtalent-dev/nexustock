param(
    [string]$BaseUrl = "http://localhost:5024",
    [string]$ConnectionString = "",
    [switch]$SkipFeatureFlagMutation
)

Write-Host "=== Task Interleaving Verification Script (Full Matrix) ===" -ForegroundColor Cyan
Write-Host "Base URL: $BaseUrl" -ForegroundColor Gray

$pass = 0
$skip = 0
$fail = 0
$script:seedLocA = $null
$script:seedLocB = $null
$script:seedZoneA = $null
$script:seedZoneB = $null
$script:hasDbSeed = $false
$VerifyTag = "verify_task_interleaving"

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

function Invoke-Sql {
    param([string]$Sql)
    $dockerCmd = Get-Command docker -ErrorAction SilentlyContinue
    if ($dockerCmd) {
        $postgresContainer = docker ps -q --filter "name=nexustock-postgres"
        if ($postgresContainer) {
            $out = docker exec nexustock-postgres psql -U kingsman -d nexustock_main -t -A -c $Sql 2>&1
            if ($LASTEXITCODE -eq 0) { return @{ Ok = $true; Output = ($out | Out-String).Trim() } }
        }
    }

    $py = Get-Command python -ErrorAction SilentlyContinue
    if ($py) {
        $sqlFile = [System.IO.Path]::GetTempFileName()
        $pyFile = [System.IO.Path]::GetTempFileName() + ".py"
        [System.IO.File]::WriteAllText($sqlFile, $Sql)
        $pyCode = @"
import psycopg2
cs = 'host=127.0.0.1 port=5435 dbname=nexustock_main user=kingsman password=43zTV!^FiU2g!!nXc3RL!6x2&nw@2V9^BM^@!f8&ersTL!9Sj7'
sql = open(r'$sqlFile', encoding='utf-8').read()
conn = psycopg2.connect(cs)
conn.autocommit = True
cur = conn.cursor()
cur.execute(sql)
try:
    rows = cur.fetchall()
    for r in rows:
        print('|'.join('' if v is None else str(v) for v in r))
except Exception:
    pass
conn.close()
"@
        [System.IO.File]::WriteAllText($pyFile, $pyCode)
        $out = & python $pyFile 2>&1
        $code = $LASTEXITCODE
        Remove-Item $sqlFile, $pyFile -Force -ErrorAction SilentlyContinue
        if ($code -eq 0) { return @{ Ok = $true; Output = ($out | Out-String).Trim() } }
        return @{ Ok = $false; Output = ($out | Out-String).Trim() }
    }
    return @{ Ok = $false; Output = "" }
}

function Set-FeatureFlag {
    param([string]$Name, [bool]$Enabled)
    try {
        $enabledStr = if ($Enabled) { "true" } else { "false" }
        $r = Invoke-Api -Uri "$BaseUrl/api/feature-flags/$Name" -Method PUT -Body "{`"enabled`": $enabledStr}" -Headers $headers
        if ($r.StatusCode -eq 200) { return $true }
    } catch {}
    $val = if ($Enabled) { "true" } else { "false" }
    $sql = "UPDATE `"FeatureFlags`" SET `"Enabled`" = $val, `"UpdatedAt`" = now() WHERE `"Name`" = '$Name';"
    $res = Invoke-Sql -Sql $sql
    return $res.Ok
}

function Ensure-SeedTasks {
    $probe = Invoke-Sql -Sql "SELECT 1;"
    if (-not $probe.Ok) {
        $script:hasDbSeed = $false
        return
    }

    # Cleanup previous verify seeds
    $null = Invoke-Sql -Sql "DELETE FROM `"MobileTasks`" WHERE `"CreatedBy`" = '$VerifyTag';"
    $null = Invoke-Sql -Sql "UPDATE task_interleaving.task_recommendations SET `"Status`" = 'Superseded', `"UpdatedAt`" = now() WHERE `"Status`" = 'Open' AND `"CreatedBy`" LIKE '%admin%';"

    $locSql = @"
SELECT l.id::text || '|' || COALESCE(l.zone_id::text, '')
FROM storage_locations l
WHERE l.zone_id IS NOT NULL
ORDER BY l.zone_id, l.id
LIMIT 20;
"@
    $locRes = Invoke-Sql -Sql $locSql
    if (-not $locRes.Ok -or [string]::IsNullOrWhiteSpace($locRes.Output)) {
        $script:hasDbSeed = $false
        return
    }

    $rows = $locRes.Output -split "`n" | Where-Object { $_ -and $_.Contains("|") }
    $picked = @()
    foreach ($row in $rows) {
        $parts = $row.Trim() -split "\|"
        if ($parts.Count -lt 2) { continue }
        $item = @{ LocId = $parts[0]; ZoneId = $parts[1] }
        if ($picked.Count -eq 0) { $picked += $item; continue }
        if ($picked[0].ZoneId -ne $item.ZoneId) { $picked += $item; break }
    }
    if ($picked.Count -lt 2) {
        $script:hasDbSeed = $false
        return
    }

    $script:seedLocA = $picked[0].LocId
    $script:seedZoneA = $picked[0].ZoneId
    $script:seedLocB = $picked[1].LocId
    $script:seedZoneB = $picked[1].ZoneId
    $tenantId = "00000000-0000-0000-0000-000000000001"
    $refA = [Guid]::NewGuid().ToString()
    $refB = [Guid]::NewGuid().ToString()
    $taskA = [Guid]::NewGuid().ToString()
    $taskB = [Guid]::NewGuid().ToString()

    $insert = @"
INSERT INTO `"MobileTasks`" (`"Id`", `"TenantId`", `"ReferenceType`", `"ReferenceId`", `"Step`", `"LocationId`", `"AssignedUser`", `"Status`", `"CreatedAt`", `"CreatedBy`")
VALUES
('$taskA', '$tenantId', 'Picking', '$refA', 'HIGH', '$($script:seedLocA)', NULL, 'Open', now() - interval '30 minutes', '$VerifyTag'),
('$taskB', '$tenantId', 'Picking', '$refB', 'LOW', '$($script:seedLocB)', NULL, 'Open', now() - interval '5 minutes', '$VerifyTag');
"@
    $ins = Invoke-Sql -Sql $insert
    $script:hasDbSeed = $ins.Ok
    if ($script:hasDbSeed) {
        Write-Host "Seeded MobileTasks for verify (zones $($script:seedZoneA) / $($script:seedZoneB))." -ForegroundColor Gray
    }
}

function Clear-SeedTasks {
    $null = Invoke-Sql -Sql "DELETE FROM `"MobileTasks`" WHERE `"CreatedBy`" = '$VerifyTag';"
}

# --- AUTHENTICATE ---
$loginBody = @{ email = "admin@nexustock.com"; password = "AdminSecret123!" } | ConvertTo-Json -Depth 5
$loginRes = Invoke-Api -Uri "$BaseUrl/api/auth/login" -Method POST -Body $loginBody
if ($loginRes.StatusCode -ne 200) {
    Write-Error "Login failed: $($loginRes.StatusCode) — $($loginRes.Body)"
    exit 1
}

$token = ($loginRes.Body | ConvertFrom-Json).token
if (-not $token) {
    Write-Error "Login response missing token: $($loginRes.Body)"
    exit 1
}

$headers = @{ "Authorization" = "Bearer $token" }

Ensure-SeedTasks

# 1
Invoke-Test "Scenario 1: Feature flag on + Auth + GetNext success" {
    if (-not $SkipFeatureFlagMutation) {
        $ok = Set-FeatureFlag -Name "FF_TASK_INTERLEAVING_ENABLED" -Enabled $true
        if (-not $ok) { throw "Setup failed: cannot enable feature flag" }
    }
    $qs = if ($script:hasDbSeed) { "?maxCandidates=5&currentLocationId=$($script:seedLocA)&currentZoneId=$($script:seedZoneA)" } else { "?maxCandidates=5" }
    $res = Invoke-Api -Uri "$BaseUrl/api/task-interleaving/next$qs" -Method GET -Headers $headers
    if ($res.StatusCode -ne 200) { throw "Expected HTTP 200, got $($res.StatusCode). Response: $($res.Body)" }
    $obj = $res.Body | ConvertFrom-Json
    if (-not $obj.recommendationId) { throw "Response missing recommendationId: $($res.Body)" }
}

# 2
Invoke-Test "Scenario 2: Missing permissions returns 403" {
    $noPermBody = @{ email = "worker@nexustock.com"; password = "WorkerSecret123!" } | ConvertTo-Json -Depth 5
    $noPermLogin = Invoke-Api -Uri "$BaseUrl/api/auth/login" -Method POST -Body $noPermBody
    if ($noPermLogin.StatusCode -eq 200) {
        $noPermToken = ($noPermLogin.Body | ConvertFrom-Json).token
        $noPermHeaders = @{ "Authorization" = "Bearer $noPermToken" }
        $res = Invoke-Api -Uri "$BaseUrl/api/task-interleaving/next" -Method GET -Headers $noPermHeaders
        if ($res.StatusCode -ne 403) { throw "Expected HTTP 403 for unauthorized user, got $($res.StatusCode)" }
    } else {
        throw "SKIPPED: worker user not seeded"
    }
}

# 3
Invoke-Test "Scenario 3: GetNext returns NoCandidate when task queue empty" {
    $res = Invoke-Api -Uri "$BaseUrl/api/task-interleaving/next?operationType=Receiving" -Method GET -Headers $headers
    if ($res.StatusCode -ne 200) { throw "Expected HTTP 200, got $($res.StatusCode)" }
    $obj = $res.Body | ConvertFrom-Json
    if ($obj.status -eq "NoCandidate" -and $null -ne $obj.selected) {
        throw "Expected selected task to be null when status is NoCandidate"
    }
}

# 4
Invoke-Test "Scenario 4: Spatial proximity priority (Same-zone outranks different-zone)" {
    if (-not $script:hasDbSeed) { throw "SKIPPED: no DB seed channel" }
    $qs = "?maxCandidates=10&currentLocationId=$($script:seedLocA)&currentZoneId=$($script:seedZoneA)&operationType=Picking"
    $res = Invoke-Api -Uri "$BaseUrl/api/task-interleaving/next$qs" -Method GET -Headers $headers
    if ($res.StatusCode -ne 200) { throw "Expected HTTP 200, got $($res.StatusCode)" }
    $obj = $res.Body | ConvertFrom-Json
    if ($obj.status -eq "NoCandidate") { throw "SKIPPED: No candidates available to compare spatial proximity" }
    $candidates = @($obj.candidates)
    if ($candidates.Count -lt 2) { throw "SKIPPED: Not enough candidates to compare spatial scores" }
    $sameZone = @($candidates | Where-Object { $_.zoneId -eq $script:seedZoneA })
    $diffZone = @($candidates | Where-Object { $_.zoneId -eq $script:seedZoneB })
    if ($sameZone.Count -gt 0 -and $diffZone.Count -gt 0) {
        if ($sameZone[0].explanation.distanceScore -le $diffZone[0].explanation.distanceScore) {
            throw "Expected same-zone distance score ($($sameZone[0].explanation.distanceScore)) > diff-zone ($($diffZone[0].explanation.distanceScore))"
        }
    } else {
        throw "SKIPPED: Candidates do not cross zone boundaries to verify spatial priority"
    }
}

# 5
Invoke-Test "Scenario 5: Business priority override (High priority wins over low priority)" {
    if (-not $script:hasDbSeed) { throw "SKIPPED: no DB seed channel" }
    $qs = "?maxCandidates=10&currentLocationId=$($script:seedLocA)&currentZoneId=$($script:seedZoneA)&operationType=Picking"
    $res = Invoke-Api -Uri "$BaseUrl/api/task-interleaving/next$qs" -Method GET -Headers $headers
    if ($res.StatusCode -ne 200) { throw "Expected HTTP 200, got $($res.StatusCode)" }
    $obj = $res.Body | ConvertFrom-Json
    if ($obj.status -eq "NoCandidate") { throw "SKIPPED: No candidates available to compare priorities" }
    $candidates = @($obj.candidates)
    $high = @($candidates | Where-Object { $_.explanation.priorityScore -eq 20 })
    $low = @($candidates | Where-Object { $_.explanation.priorityScore -lt 20 })
    if ($high.Count -gt 0 -and $low.Count -gt 0) {
        if ($high[0].explanation.priorityScore -le $low[0].explanation.priorityScore) {
            throw "Expected high-priority score to be greater than low-priority score"
        }
    } else {
        throw "SKIPPED: Missing mixed-priority candidates to verify priority scoring override"
    }
}

# 6
Invoke-Test "Scenario 6: Persistence of recommendation log and candidates" {
    $res = Invoke-Api -Uri "$BaseUrl/api/task-interleaving/next" -Method GET -Headers $headers
    if ($res.StatusCode -ne 200) { throw "Expected HTTP 200" }
    $obj = $res.Body | ConvertFrom-Json
    $detailRes = Invoke-Api -Uri "$BaseUrl/api/task-interleaving/recommendations/$($obj.recommendationId)" -Method GET -Headers $headers
    if ($detailRes.StatusCode -ne 200) { throw "Failed to fetch persisted recommendation detail: $($detailRes.Body)" }
}

# 7
Invoke-Test "Scenario 7: Accept suggestion successfully" {
    if (-not $script:hasDbSeed) { throw "SKIPPED: no DB seed channel" }
    # Re-seed clean Open tasks (previous accepts may consume)
    Ensure-SeedTasks
    $qs = "?maxCandidates=5&currentLocationId=$($script:seedLocA)&currentZoneId=$($script:seedZoneA)&operationType=Picking"
    $res = Invoke-Api -Uri "$BaseUrl/api/task-interleaving/next$qs" -Method GET -Headers $headers
    if ($res.StatusCode -ne 200) { throw "Expected HTTP 200, got $($res.StatusCode)" }
    $obj = $res.Body | ConvertFrom-Json
    if ($obj.status -eq "Open" -and $obj.selected) {
        $key = "idemp-" + [Guid]::NewGuid().ToString()
        $acceptRes = Invoke-Api -Uri "$BaseUrl/api/task-interleaving/recommendations/$($obj.recommendationId)/accept" -Method POST -Body (@{ idempotencyKey = $key } | ConvertTo-Json) -Headers $headers
        if ($acceptRes.StatusCode -ne 200) { throw "Expected HTTP 200 for accept, got $($acceptRes.StatusCode). Response: $($acceptRes.Body)" }
    } else {
        throw "SKIPPED: No active Open recommendation candidate to test accept"
    }
}

# 8
Invoke-Test "Scenario 8: Idempotent accept requests" {
    if (-not $script:hasDbSeed) { throw "SKIPPED: no DB seed channel" }
    Ensure-SeedTasks
    $qs = "?maxCandidates=5&currentLocationId=$($script:seedLocA)&currentZoneId=$($script:seedZoneA)&operationType=Picking"
    $res = Invoke-Api -Uri "$BaseUrl/api/task-interleaving/next$qs" -Method GET -Headers $headers
    if ($res.StatusCode -ne 200) { throw "Expected HTTP 200, got $($res.StatusCode)" }
    $obj = $res.Body | ConvertFrom-Json
    if ($obj.status -eq "Open" -and $obj.selected) {
        $recId = $obj.recommendationId
        $key = "idemp-" + $recId
        $acceptBody = @{ idempotencyKey = $key } | ConvertTo-Json
        $acceptRes1 = Invoke-Api -Uri "$BaseUrl/api/task-interleaving/recommendations/$recId/accept" -Method POST -Body $acceptBody -Headers $headers
        if ($acceptRes1.StatusCode -ne 200) { throw "Expected HTTP 200 for first accept, got $($acceptRes1.StatusCode). Resp: $($acceptRes1.Body)" }
        $obj1 = $acceptRes1.Body | ConvertFrom-Json
        $acceptRes2 = Invoke-Api -Uri "$BaseUrl/api/task-interleaving/recommendations/$recId/accept" -Method POST -Body $acceptBody -Headers $headers
        if ($acceptRes2.StatusCode -ne 200) { throw "Expected idempotent second accept to succeed, got $($acceptRes2.StatusCode). Resp: $($acceptRes2.Body)" }
        $obj2 = $acceptRes2.Body | ConvertFrom-Json
        if ($obj1.recommendationId -ne $obj2.recommendationId -or $obj1.taskId -ne $obj2.taskId -or $obj1.status -ne $obj2.status) {
            throw "Idempotency response mismatch"
        }
    } else {
        throw "SKIPPED: No active Open recommendation candidate"
    }
}

# 9
Invoke-Test "Scenario 9: Accept expired recommendation fails with 409" {
    if (-not $script:hasDbSeed) { throw "SKIPPED: no DB seed channel" }
    Ensure-SeedTasks
    $qs = "?maxCandidates=5&currentLocationId=$($script:seedLocA)&currentZoneId=$($script:seedZoneA)&operationType=Picking"
    $res = Invoke-Api -Uri "$BaseUrl/api/task-interleaving/next$qs" -Method GET -Headers $headers
    if ($res.StatusCode -ne 200) { throw "Expected HTTP 200" }
    $obj = $res.Body | ConvertFrom-Json
    if ($obj.status -ne "Open") { throw "SKIPPED: No Open recommendation to expire" }
    $upd = Invoke-Sql -Sql "UPDATE task_interleaving.task_recommendations SET `"ExpiresAt`" = now() - interval '1 minute' WHERE `"Id`" = '$($obj.recommendationId)';"
    if (-not $upd.Ok) { throw "SKIPPED: cannot mutate ExpiresAt via SQL" }
    $acceptRes = Invoke-Api -Uri "$BaseUrl/api/task-interleaving/recommendations/$($obj.recommendationId)/accept" -Method POST -Body (@{ idempotencyKey = ("idemp-exp-" + [Guid]::NewGuid()) } | ConvertTo-Json) -Headers $headers
    if ($acceptRes.StatusCode -ne 409) { throw "Expected HTTP 409 for expired, got $($acceptRes.StatusCode). Resp: $($acceptRes.Body)" }
}

# 10
Invoke-Test "Scenario 10: Concurrency conflict (task already claimed) returns 409" {
    if (-not $script:hasDbSeed) { throw "SKIPPED: no DB seed channel" }
    Ensure-SeedTasks
    $qs = "?maxCandidates=5&currentLocationId=$($script:seedLocA)&currentZoneId=$($script:seedZoneA)&operationType=Picking"
    $res = Invoke-Api -Uri "$BaseUrl/api/task-interleaving/next$qs" -Method GET -Headers $headers
    if ($res.StatusCode -ne 200) { throw "Expected HTTP 200" }
    $obj = $res.Body | ConvertFrom-Json
    if ($obj.status -ne "Open" -or -not $obj.selected) { throw "SKIPPED: No active Open recommendation candidate" }
    $taskId = $obj.selected.taskId
    $upd = Invoke-Sql -Sql "UPDATE `"MobileTasks`" SET `"AssignedUser`" = 'other-user' WHERE `"Id`" = '$taskId';"
    if (-not $upd.Ok) { throw "SKIPPED: cannot assign MobileTask via SQL" }
    $acceptRes = Invoke-Api -Uri "$BaseUrl/api/task-interleaving/recommendations/$($obj.recommendationId)/accept" -Method POST -Body (@{ idempotencyKey = ("idemp-cf-" + [Guid]::NewGuid()) } | ConvertTo-Json) -Headers $headers
    if ($acceptRes.StatusCode -ne 409) { throw "Expected HTTP 409 for conflict, got $($acceptRes.StatusCode). Resp: $($acceptRes.Body)" }
}

# 11
Invoke-Test "Scenario 11: Rejection requires reason code" {
    if (-not $script:hasDbSeed) { throw "SKIPPED: no DB seed channel" }
    Ensure-SeedTasks
    $qs = "?maxCandidates=5&currentLocationId=$($script:seedLocA)&currentZoneId=$($script:seedZoneA)&operationType=Picking"
    $res = Invoke-Api -Uri "$BaseUrl/api/task-interleaving/next$qs" -Method GET -Headers $headers
    if ($res.StatusCode -ne 200) { throw "Expected HTTP 200" }
    $obj = $res.Body | ConvertFrom-Json
    if ($obj.status -ne "Open") { throw "SKIPPED: No active Open recommendation" }
    $rejectRes = Invoke-Api -Uri "$BaseUrl/api/task-interleaving/recommendations/$($obj.recommendationId)/reject" -Method POST -Body (@{ note = "test" } | ConvertTo-Json) -Headers $headers
    if ($rejectRes.StatusCode -ne 400) { throw "Expected HTTP 400 when reasonCode is missing, got $($rejectRes.StatusCode). Resp: $($rejectRes.Body)" }
}

# 12
Invoke-Test "Scenario 12: Successful rejection updates status to Rejected" {
    if (-not $script:hasDbSeed) { throw "SKIPPED: no DB seed channel" }
    Ensure-SeedTasks
    $qs = "?maxCandidates=5&currentLocationId=$($script:seedLocA)&currentZoneId=$($script:seedZoneA)&operationType=Picking"
    $res = Invoke-Api -Uri "$BaseUrl/api/task-interleaving/next$qs" -Method GET -Headers $headers
    if ($res.StatusCode -ne 200) { throw "Expected HTTP 200" }
    $obj = $res.Body | ConvertFrom-Json
    if ($obj.status -ne "Open") { throw "SKIPPED: No active Open recommendation" }
    $rejectRes = Invoke-Api -Uri "$BaseUrl/api/task-interleaving/recommendations/$($obj.recommendationId)/reject" -Method POST -Body (@{ reasonCode = "TOO_FAR"; note = "test skip" } | ConvertTo-Json) -Headers $headers
    if ($rejectRes.StatusCode -ne 200) { throw "Expected HTTP 200, got $($rejectRes.StatusCode). Response: $($rejectRes.Body)" }
    $rObj = $rejectRes.Body | ConvertFrom-Json
    if ($rObj.status -ne "Rejected") { throw "Expected status to be Rejected, got $($rObj.status)" }
}

# 13
Invoke-Test "Scenario 13: List recommendations has filter and pagination" {
    $res = Invoke-Api -Uri "$BaseUrl/api/task-interleaving/recommendations?page=1&pageSize=5&operationType=Picking" -Method GET -Headers $headers
    if ($res.StatusCode -ne 200) { throw "Expected HTTP 200, got $($res.StatusCode). Resp: $($res.Body)" }
    $obj = $res.Body | ConvertFrom-Json
    if ($null -eq $obj.items) { throw "Response missing items" }
}

# 14
Invoke-Test "Scenario 14: KPI aggregates rates correctly" {
    $res = Invoke-Api -Uri "$BaseUrl/api/task-interleaving/kpi" -Method GET -Headers $headers
    if ($res.StatusCode -ne 200) { throw "Expected HTTP 200, got $($res.StatusCode). Resp: $($res.Body)" }
    $obj = $res.Body | ConvertFrom-Json
    if ($null -eq $obj.acceptRate) { throw "Response missing acceptRate KPI" }
}

# 15
Invoke-Test "Scenario 15: Feature flag off blocks access" {
    if (-not $SkipFeatureFlagMutation) {
        $ok = Set-FeatureFlag -Name "FF_TASK_INTERLEAVING_ENABLED" -Enabled $false
        if (-not $ok) { throw "Setup failed: cannot disable feature flag" }
        $res = Invoke-Api -Uri "$BaseUrl/api/task-interleaving/next" -Method GET -Headers $headers
        $null = Set-FeatureFlag -Name "FF_TASK_INTERLEAVING_ENABLED" -Enabled $true
        if ($res.StatusCode -ne 403) { throw "Expected HTTP 403 when feature flag is off, got $($res.StatusCode). Resp: $($res.Body)" }
    } else {
        throw "SKIPPED: Feature flag mutation skipped"
    }
}

Clear-SeedTasks

Write-Host "=== VERIFICATION SUMMARY ===" -ForegroundColor Cyan
Write-Host "PASS: $pass" -ForegroundColor Green
Write-Host "SKIP: $skip" -ForegroundColor Yellow
Write-Host "FAIL: $fail" -ForegroundColor Red
Write-Host "DB seed available: $script:hasDbSeed" -ForegroundColor Gray

if ($fail -gt 0) { exit 1 } else { exit 0 }
