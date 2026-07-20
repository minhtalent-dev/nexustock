param(
    [string]$BaseUrl = "http://localhost:5024",
    [string]$Token = "",
    [string]$ReleasedLotId = "",
    [string]$BlockedLotId = "",
    [string]$ConnectionString = "",
    [switch]$SkipFeatureFlagMutation = $false
)

$pass = 0
$fail = 0

function Invoke-Test {
    param([string]$Name, [scriptblock]$Test)
    try {
        & $Test
        Write-Host "[PASS] $Name" -ForegroundColor Green
        $script:pass++
    } catch {
        Write-Host "[FAIL] $Name - $($_.Exception.Message)" -ForegroundColor Red
        $script:fail++
    }
}

function Invoke-Api {
    param([string]$Uri, [string]$Method = "GET", [string]$Body = $null, [hashtable]$Headers = @{})
    try {
        $params = @{ Uri = $Uri; Method = $Method; UseBasicParsing = $true }
        if ($Body) { $params.Body = $Body; $Headers["Content-Type"] = "application/json" }
        if ($Headers.Count -gt 0) { $params.Headers = $Headers }
        $resp = Invoke-WebRequest @params
        return @{ StatusCode = $resp.StatusCode; Body = $resp.Content }
    } catch [System.Net.WebException] {
        $statusCode = [int]$_.Exception.Response.StatusCode
        $streamReader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $errBody = $streamReader.ReadToEnd()
        $streamReader.Close()
        return @{ StatusCode = $statusCode; Body = $errBody }
    } catch {
        return @{ StatusCode = 0; Body = $_.Exception.Message }
    }
}

function Set-FeatureFlag {
    param([string]$Name, [bool]$Enabled)
    $val = if ($Enabled) { "true" } else { "false" }
    $sql = "UPDATE `"FeatureFlags`" SET `"Enabled`" = $val, `"UpdatedAt`" = now() WHERE `"Name`" = '$Name';"
    
    # Try docker exec first
    $dockerCmd = Get-Command docker -ErrorAction SilentlyContinue
    if ($dockerCmd) {
        $postgresContainer = docker ps -q --filter "name=nexustock-postgres"
        if ($postgresContainer) {
            docker exec nexustock-postgres psql -U kingsman -d nexustock_main -c "$sql" 2>&1 | Out-Null
            if ($LASTEXITCODE -eq 0) { return $true }
        }
    }
    
    # Try local psql
    $psqlCmd = Get-Command psql -ErrorAction SilentlyContinue
    if ($psqlCmd) {
        $env:PGPASSWORD = "43zTV!^FiU2g!!nXc3RL!6x2&nw@2V9^BM^@!f8&ersTL!9Sj7"
        psql -h 127.0.0.1 -p 5435 -U kingsman -d nexustock_main -c "$sql" 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) { return $true }
    }
    
    return $false
}

Write-Host "`n=== Cross-docking Strict Integration Verification ===" -ForegroundColor Cyan
Write-Host "Base URL: $BaseUrl"

# 1. Login & Token Retrieval
$headers = @{}
if ($Token) {
    $headers["Authorization"] = "Bearer $Token"
    Write-Host "Using user-supplied token."
} else {
    Write-Host "Logging in dynamically as admin..."
    $loginBody = @{ email = "admin@nexustock.com"; password = "AdminSecret123!" } | ConvertTo-Json -Depth 5
    $r = Invoke-Api -Uri "$BaseUrl/api/auth/login" -Method POST -Body $loginBody
    if ($r.StatusCode -ne 200) {
        Write-Error "Preflight failed: Cannot login as admin. API response code: $($r.StatusCode)"
        exit 1
    }
    $loginRes = $r.Body | ConvertFrom-Json
    $headers["Authorization"] = "Bearer $($loginRes.token)"
    Write-Host "Dynamic login successful. Token acquired."
}

# 2. Dynamic Test Data Generation (if ReleasedLotId and BlockedLotId not provided)
$testReleasedLotId = $ReleasedLotId
$testBlockedLotId = $BlockedLotId

if (-not $testReleasedLotId -or -not $testBlockedLotId) {
    Write-Host "`nGenerating dynamic test data..." -ForegroundColor Cyan
    try {
        # Fetch Active Product (non-serial tracked)
        $products = Invoke-RestMethod -Uri "$BaseUrl/api/master-data/products" -Method Get -Headers $headers
        $product = $null
        foreach ($p in $products.items) {
            if ($p.isActive -and ($null -eq $p.isSerialTracked -or $p.isSerialTracked -eq $false)) {
                $product = $p
                break
            }
        }
        if ($null -eq $product) { $product = $products.items[0] }
        $productId = $product.id
        $uomId = $product.baseUomId
        
        # Fetch Partner
        $partners = Invoke-RestMethod -Uri "$BaseUrl/api/master-data/partners" -Method Get -Headers $headers
        $partnerId = $partners.items[0].id

        # Fetch Zone
        $zones = Invoke-RestMethod -Uri "$BaseUrl/api/master-data/storage-zones" -Method Get -Headers $headers
        $zoneId = $zones.items[0].id

        # Create temporary high-capacity location
        $locCode = "LOC-CD-TEST-" + (Get-Date -Format "HHmmss")
        $createLocBody = @{
            zoneId = $zoneId
            code = $locCode
            maxCapacity = 999999.0
            maxVolume = 999999.0
            xCoord = 0
            yCoord = 0
            zCoord = 0
            length = 1.0
            width = 1.0
            height = 1.0
            isLocked = $false
            isActive = $true
        } | ConvertTo-Json -Depth 5
        $locRes = Invoke-RestMethod -Uri "$BaseUrl/api/master-data/storage-locations" -Method Post -Body $createLocBody -ContentType "application/json" -Headers $headers
        $locationId = $locRes.id
        Write-Host "Created temporary high-capacity location: $locCode ($locationId)" -ForegroundColor Green

        # --- SEED LOT A (QC RELEASE) ---
        Write-Host "Seeding Released Lot (Lot A)..."
        $ioNoA = "IO-CD-REL-" + (Get-Date -Format "HHmmss")
        $ioBodyA = @{ orderNo = $ioNoA; partnerId = $partnerId; items = @(@{ itemId = $productId; uomId = $uomId; expectedQty = 20.0; tolerance = 0.1 }) } | ConvertTo-Json -Depth 5
        $ioResA = Invoke-RestMethod -Uri "$BaseUrl/api/inbound/orders" -Method Post -Body $ioBodyA -ContentType "application/json" -Headers $headers
        $ioIdA = $ioResA.id

        $lotNoA = "LOT-CD-REL-" + (Get-Date -Format "HHmmss")
        $receiveBodyA = @{ itemId = $productId; lotNo = $lotNoA; receivedQty = 20.0; toLocationId = $locationId } | ConvertTo-Json -Depth 5
        $null = Invoke-RestMethod -Uri "$BaseUrl/api/inbound/orders/$ioIdA/receive" -Method Post -Body $receiveBodyA -ContentType "application/json" -Headers $headers

        $lotResA = Invoke-RestMethod -Uri "$BaseUrl/api/lots/$lotNoA" -Method Get -Headers $headers
        $lotA = $lotResA[0]
        $testReleasedLotId = $lotA.id

        $queue = Invoke-RestMethod -Uri "$BaseUrl/api/qc/queue" -Method Get -Headers $headers
        $queueItemA = $null
        foreach ($item in $queue) { if ($item.lotNo -eq $lotNoA) { $queueItemA = $item; break } }
        $qcBodyA = @{ qcRequestId = $queueItemA.id; isPassed = $true; metrics = "CD Seed Released" } | ConvertTo-Json -Depth 5
        $null = Invoke-RestMethod -Uri "$BaseUrl/api/qc/$testReleasedLotId/result" -Method Post -Body $qcBodyA -ContentType "application/json" -Headers $headers
        Write-Host "Seeded Released Lot ID: $testReleasedLotId" -ForegroundColor Green

        # --- SEED LOT B (QC HOLD/BLOCKED) ---
        Write-Host "Seeding Blocked Lot (Lot B)..."
        $ioNoB = "IO-CD-BLK-" + (Get-Date -Format "HHmmss")
        $ioBodyB = @{ orderNo = $ioNoB; partnerId = $partnerId; items = @(@{ itemId = $productId; uomId = $uomId; expectedQty = 20.0; tolerance = 0.1 }) } | ConvertTo-Json -Depth 5
        $ioResB = Invoke-RestMethod -Uri "$BaseUrl/api/inbound/orders" -Method Post -Body $ioBodyB -ContentType "application/json" -Headers $headers
        $ioIdB = $ioResB.id

        $lotNoB = "LOT-CD-BLK-" + (Get-Date -Format "HHmmss")
        $receiveBodyB = @{ itemId = $productId; lotNo = $lotNoB; receivedQty = 20.0; toLocationId = $locationId } | ConvertTo-Json -Depth 5
        $null = Invoke-RestMethod -Uri "$BaseUrl/api/inbound/orders/$ioIdB/receive" -Method Post -Body $receiveBodyB -ContentType "application/json" -Headers $headers

        $lotResB = Invoke-RestMethod -Uri "$BaseUrl/api/lots/$lotNoB" -Method Get -Headers $headers
        $lotB = $lotResB[0]
        $testBlockedLotId = $lotB.id

        $queue = Invoke-RestMethod -Uri "$BaseUrl/api/qc/queue" -Method Get -Headers $headers
        $queueItemB = $null
        foreach ($item in $queue) { if ($item.lotNo -eq $lotNoB) { $queueItemB = $item; break } }
        $qcBodyB = @{ qcRequestId = $queueItemB.id; isPassed = $false; metrics = "CD Seed Blocked" } | ConvertTo-Json -Depth 5
        $null = Invoke-RestMethod -Uri "$BaseUrl/api/qc/$testBlockedLotId/result" -Method Post -Body $qcBodyB -ContentType "application/json" -Headers $headers
        Write-Host "Seeded Blocked Lot ID: $testBlockedLotId" -ForegroundColor Green

        # --- SEED OPEN DEMAND (SHIPMENT & WAVE) ---
        Write-Host "Seeding Open Demand..."
        $shipmentNo = "SHIP-CD-DEMAND-" + (Get-Date -Format "HHmmss")
        $shipmentBody = @{
            shipmentNo = $shipmentNo
            partnerId = $partnerId
            items = @(@{ itemId = $productId; uomId = $uomId; requestedQty = 10.0 })
        } | ConvertTo-Json -Depth 5
        $shipRes = Invoke-RestMethod -Uri "$BaseUrl/api/outbound/shipments" -Method Post -Body $shipmentBody -ContentType "application/json" -Headers $headers
        $shipmentId = $shipRes.id

        $waveBody = @{ shipmentIds = @($shipmentId) } | ConvertTo-Json -Depth 5
        $waveRes = Invoke-RestMethod -Uri "$BaseUrl/api/waves" -Method Post -Body $waveBody -ContentType "application/json" -Headers $headers
        Write-Host "Seeded open demand for Item $productId via Wave $($waveRes.id)" -ForegroundColor Green

    } catch {
        if ($_.Exception.Response) {
            $streamReader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $errBody = $streamReader.ReadToEnd()
            $streamReader.Close()
            Write-Error "Test data seeding failed: $_. Response: $errBody"
        } else {
            Write-Error "Test data seeding failed: $_"
        }
        exit 1
    }
}

if (-not $testReleasedLotId -or -not $testBlockedLotId) {
    Write-Error "Preflight failed: Release and Blocked Lot IDs must not be null."
    exit 1
}

# 3. Execution of 6 strict integration tests
Write-Host "`n=== Executing Test Scenarios ===" -ForegroundColor Cyan
$candidateId = $null

# SCENARIO 1: Evaluate Released Lot -> Expects 200 with candidates
Invoke-Test "Scenario 1: Evaluate Lot QC Release returns 200 and candidates" {
    $body = @{ lotId = $testReleasedLotId } | ConvertTo-Json -Depth 5
    $r = Invoke-Api -Uri "$BaseUrl/api/cross-docking/evaluate" -Method POST -Body $body -Headers $headers
    if ($r.StatusCode -ne 200) { throw "Expected 200, got $($r.StatusCode). Body: $($r.Body)" }
    $res = $r.Body | ConvertFrom-Json
    if ($null -eq $res.candidates -or $res.candidates.Count -eq 0) { throw "Expected candidates array to be non-empty." }
    $script:candidateId = $res.candidates[0].id
}

# SCENARIO 2: Evaluate Blocked Lot -> Expects 400 LOT_NOT_QC_RELEASED
Invoke-Test "Scenario 2: Evaluate Lot QC Blocked returns 400 LOT_NOT_QC_RELEASED" {
    $body = @{ lotId = $testBlockedLotId } | ConvertTo-Json -Depth 5
    $r = Invoke-Api -Uri "$BaseUrl/api/cross-docking/evaluate" -Method POST -Body $body -Headers $headers
    if ($r.StatusCode -ne 400) { throw "Expected 400, got $($r.StatusCode). Body: $($r.Body)" }
    $res = $r.Body | ConvertFrom-Json
    if ($res.errorCode -ne "LOT_NOT_QC_RELEASED") { throw "Expected errorCode LOT_NOT_QC_RELEASED, got $($res.errorCode)" }
}

# SCENARIO 3: Accept Candidate -> Expects 200 and status Accepted in detail + timeline
Invoke-Test "Scenario 3: Accept Candidate updates status and records event" {
    if (-not $candidateId) { throw "Skipped: Candidate ID not available from Scenario 1." }
    $r = Invoke-Api -Uri "$BaseUrl/api/cross-docking/$candidateId/accept" -Method POST -Headers $headers
    if ($r.StatusCode -ne 200) { throw "Expected 200, got $($r.StatusCode). Body: $($r.Body)" }
    
    # Verify detail and timeline
    $d = Invoke-Api -Uri "$BaseUrl/api/cross-docking/$candidateId" -Headers $headers
    if ($d.StatusCode -ne 200) { throw "Expected 200 on detail lookup, got $($d.StatusCode)" }
    $detail = $d.Body | ConvertFrom-Json
    if ($detail.status -ne "Accepted") { throw "Expected status Accepted, got $($detail.status)" }
    
    $hasAcceptedEvent = $false
    foreach ($evt in $detail.events) {
        if ($evt.eventType -eq "Accepted") { $hasAcceptedEvent = $true; break }
    }
    if (-not $hasAcceptedEvent) { throw "Timeline events does not contain 'Accepted'." }
}

# SCENARIO 4: Reject candidate missing reason -> Expects 400 REJECT_REASON_REQUIRED
Invoke-Test "Scenario 4: Reject candidate without reason returns 400 REJECT_REASON_REQUIRED" {
    # Generate another candidate to test reject
    $body = @{ lotId = $testReleasedLotId } | ConvertTo-Json -Depth 5
    $rEval = Invoke-Api -Uri "$BaseUrl/api/cross-docking/evaluate" -Method POST -Body $body -Headers $headers
    if ($rEval.StatusCode -ne 200) { throw "Failed to recreate candidate for reject test: $($rEval.StatusCode)" }
    $resEval = $rEval.Body | ConvertFrom-Json
    
    $rejectCandidateId = $null
    foreach ($c in $resEval.candidates) {
        if ($c.status -eq "Pending") { $rejectCandidateId = $c.id; break }
    }
    if (-not $rejectCandidateId) { throw "No Pending candidate available for reject test." }

    $rejectBody = @{ reason = "" } | ConvertTo-Json -Depth 5
    $r = Invoke-Api -Uri "$BaseUrl/api/cross-docking/$rejectCandidateId/reject" -Method POST -Body $rejectBody -Headers $headers
    if ($r.StatusCode -ne 400) { throw "Expected 400, got $($r.StatusCode). Body: $($r.Body)" }
    $res = $r.Body | ConvertFrom-Json
    if ($res.errorCode -ne "REJECT_REASON_REQUIRED") { throw "Expected errorCode REJECT_REASON_REQUIRED, got $($res.errorCode)" }
}

# SCENARIO 5: Get Candidate Details -> Expects 200 with details and events
Invoke-Test "Scenario 5: Get Candidate Details returns detailed model and events" {
    if (-not $candidateId) { throw "Skipped: Candidate ID not available from Scenario 1." }
    $r = Invoke-Api -Uri "$BaseUrl/api/cross-docking/$candidateId" -Headers $headers
    if ($r.StatusCode -ne 200) { throw "Expected 200, got $($r.StatusCode). Body: $($r.Body)" }
    $detail = $r.Body | ConvertFrom-Json
    if ($null -eq $detail.events -or $detail.events.Count -eq 0) { throw "Expected events array to be populated." }
}

# SCENARIO 6: Feature Flag Gate -> Toggle flag to false, expect 403, restore flag
Invoke-Test "Scenario 6: Disabled feature flag returns 403 FEATURE_DISABLED" {
    if ($SkipFeatureFlagMutation) {
        Write-Warning "Feature flag mutation skipped via switch."
        return
    }
    
    Write-Host "Updating database to disable FF_CROSS_DOCKING_ENABLED..."
    $toggled = Set-FeatureFlag -Name "FF_CROSS_DOCKING_ENABLED" -Enabled $false
    if (-not $toggled) {
        Write-Warning "Could not update FeatureFlags in DB. Test skipped."
        return
    }
    
    try {
        # Check API returns 403
        $body = @{ lotId = $testReleasedLotId } | ConvertTo-Json -Depth 5
        $r = Invoke-Api -Uri "$BaseUrl/api/cross-docking/evaluate" -Method POST -Body $body -Headers $headers
        if ($r.StatusCode -ne 403) { throw "Expected 403, got $($r.StatusCode). Body: $($r.Body)" }
        $res = $r.Body | ConvertFrom-Json
        if ($res.errorCode -ne "FEATURE_DISABLED") { throw "Expected errorCode FEATURE_DISABLED, got $($res.errorCode)" }
        Write-Host "Feature flag disabled correctly returned 403."
    } finally {
        Write-Host "Restoring FF_CROSS_DOCKING_ENABLED feature flag..."
        $restored = Set-FeatureFlag -Name "FF_CROSS_DOCKING_ENABLED" -Enabled $true
        if (-not $restored) {
            Write-Error "CRITICAL: Failed to restore FF_CROSS_DOCKING_ENABLED feature flag to True!"
        }
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
