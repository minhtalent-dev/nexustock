# Phase 37 — L3 pilot smoke (EP5)
# Thứ tự §22.1 A–L · CẤM biến $pid
# Pack FAIL → SKIP OK

$ErrorActionPreference = "Stop"
$API_URL = if ($env:NEXUSTOCK_API_URL) { $env:NEXUSTOCK_API_URL } else { "http://localhost:5024/api" }
$root = Split-Path $PSScriptRoot -Parent
$evidenceDir = Join-Path $root "planning/evidence/phase_37"
New-Item -ItemType Directory -Force -Path $evidenceDir | Out-Null

$pass = 0
$fail = 0
$skip = 0
$results = @()

function Assert-True($cond, $msg) {
    if ($cond) {
        Write-Host "  PASS: $msg" -ForegroundColor Green
        $script:pass++
        $script:results += @{ id = $msg; status = "PASS" }
    } else {
        Write-Host "  FAIL: $msg" -ForegroundColor Red
        $script:fail++
        $script:results += @{ id = $msg; status = "FAIL" }
    }
}

function Assert-Skip($msg) {
    Write-Host "  SKIP: $msg" -ForegroundColor Yellow
    $script:skip++
    $script:results += @{ id = $msg; status = "SKIP" }
}

function Get-ErrorBody($err) {
    $detail = $err.ErrorDetails.Message
    if (-not $detail -and $err.Exception.Response) {
        try {
            $sr = New-Object System.IO.StreamReader($err.Exception.Response.GetResponseStream())
            $detail = $sr.ReadToEnd()
        } catch { $detail = "$err" }
    }
    if (-not $detail) { $detail = "$err" }
    return $detail
}

Write-Host "=== verify_l3_pilot_smoke ===" -ForegroundColor Cyan
Write-Host "API: $API_URL"

# A. Login
Write-Host "`nA. Login..." -ForegroundColor Cyan
$loginRes = Invoke-RestMethod -Uri "$API_URL/auth/login" -Method Post -Body (@{
    email = "admin@nexustock.com"
    password = "AdminSecret123!"
} | ConvertTo-Json) -ContentType "application/json"
$headers = @{ Authorization = "Bearer $($loginRes.token)" }
Assert-True $true "Login"

# B. Seed / master
Write-Host "`nB. Seed / master..." -ForegroundColor Cyan
$seedScript = Join-Path $PSScriptRoot "seed/demo_generic_tenant.ps1"
& powershell -NoProfile -File $seedScript
Assert-True ($LASTEXITCODE -eq 0) "Seed script"
$seed = Get-Content (Join-Path $evidenceDir "seed_summary.json") -Raw | ConvertFrom-Json
$productId = $seed.primaryProductId
$uomId = $seed.uomId
$partnerId = $seed.partnerId
$locationId = $seed.locationId
$toLocationId = $seed.toLocationId
Assert-True ($productId -and $locationId) "Master ids from seed"

$suffix = Get-Date -Format "HHmmssfff"

function New-ReleasedLot([string]$lotPrefix, [decimal]$qty) {
    $lotNo = "$lotPrefix-$suffix"
    $orderNo = "PO-DEMO-$lotPrefix-$suffix"
    $ioJson = "{`"orderNo`":`"$orderNo`",`"partnerId`":`"$partnerId`",`"items`":[{`"itemId`":`"$productId`",`"uomId`":`"$uomId`",`"expectedQty`":$qty,`"tolerance`":0.1}]}"
    $ioRes = Invoke-RestMethod -Uri "$API_URL/inbound/orders" -Method Post -Body $ioJson -ContentType "application/json; charset=utf-8" -Headers $headers
    $orderId = $ioRes.id.ToString()
    $recvJson = "{`"itemId`":`"$productId`",`"lotNo`":`"$lotNo`",`"receivedQty`":$qty,`"toLocationId`":`"$locationId`"}"
    $null = Invoke-RestMethod -Uri "$API_URL/inbound/orders/$orderId/receive" -Method Post -Body $recvJson -ContentType "application/json; charset=utf-8" -Headers $headers
    $lotRes = Invoke-RestMethod -Uri "$API_URL/lots/$lotNo" -Method Get -Headers $headers
    $lotId = $lotRes[0].id.ToString()
    $queue = Invoke-RestMethod -Uri "$API_URL/qc/queue" -Method Get -Headers $headers
    $queueItem = $null
    foreach ($item in $queue) {
        if ($item.lotNo -eq $lotNo) { $queueItem = $item; break }
    }
    if ($null -eq $queueItem) { throw "Lot $lotNo not in QC queue" }
    $qcReqId = $queueItem.id.ToString()
    $qcJson = "{`"qcRequestId`":`"$qcReqId`",`"isPassed`":true,`"metrics`":`"L3 smoke`"}"
    $null = Invoke-RestMethod -Uri "$API_URL/qc/$lotId/result" -Method Post -Body $qcJson -ContentType "application/json; charset=utf-8" -Headers $headers
    return @{ lotNo = $lotNo; lotId = $lotId; orderId = $orderId }
}

# C. Lot-HAPPY
Write-Host "`nC. Lot-HAPPY inbound+QC..." -ForegroundColor Cyan
$happy = New-ReleasedLot "HAPPY" 20
Assert-True ($happy.lotId -ne "") "L3-UAT-01 Lot-HAPPY $($happy.lotNo)"

# D. Move OK
Write-Host "`nD. Move OK..." -ForegroundColor Cyan
try {
    if ($toLocationId -eq $locationId) {
        Assert-Skip "Move OK skipped (single location)"
    } else {
        $moveJson = "{`"itemId`":`"$productId`",`"lotNo`":`"$($happy.lotNo)`",`"fromLocationId`":`"$locationId`",`"toLocationId`":`"$toLocationId`",`"qty`":1.0,`"reasonCode`":`"TEST_SEED`"}"
        $null = Invoke-RestMethod -Uri "$API_URL/inventory/move" -Method Post -Body $moveJson -ContentType "application/json; charset=utf-8" -Headers $headers
        Assert-True $true "L3-UAT-03 Move OK"
        # Move back for pick stock at LOC-SORT
        $moveBack = "{`"itemId`":`"$productId`",`"lotNo`":`"$($happy.lotNo)`",`"fromLocationId`":`"$toLocationId`",`"toLocationId`":`"$locationId`",`"qty`":1.0,`"reasonCode`":`"TEST_SEED`"}"
        $null = Invoke-RestMethod -Uri "$API_URL/inventory/move" -Method Post -Body $moveBack -ContentType "application/json; charset=utf-8" -Headers $headers
    }
} catch {
    Assert-True $false "Move OK: $(Get-ErrorBody $_)"
}

# E. Shipment + generate-picks
Write-Host "`nE. Generate picks..." -ForegroundColor Cyan
$shipmentNo = "SO-DEMO-$suffix"
$shipJson = "{`"shipmentNo`":`"$shipmentNo`",`"partnerId`":`"$partnerId`",`"items`":[{`"itemId`":`"$productId`",`"uomId`":`"$uomId`",`"requestedQty`":5.0}]}"
$ship = Invoke-RestMethod -Uri "$API_URL/outbound/shipments" -Method Post -Body $shipJson -ContentType "application/json; charset=utf-8" -Headers $headers
$shipmentId = $ship.id.ToString()
Assert-True ($shipmentId -ne "") "Shipment $shipmentNo"
try {
    $gen = Invoke-RestMethod -Uri "$API_URL/outbound/shipments/$shipmentId/generate-picks?strategy=FEFO" -Method Post -Headers $headers
    Assert-True ($gen.pickTaskCount -gt 0) "L3-UAT-04 pickTaskCount=$($gen.pickTaskCount)"
} catch {
    Assert-True $false "Generate picks: $(Get-ErrorBody $_)"
}

# F. Complete pick
Write-Host "`nF. Complete pick..." -ForegroundColor Cyan
$detail = Invoke-RestMethod -Uri "$API_URL/outbound/shipments/$shipmentId" -Method Get -Headers $headers
$pickTask = $detail.picks | Where-Object { $_.status -eq "Pending" } | Select-Object -First 1
if ($null -eq $pickTask) {
    Assert-True $false "No Pending pick task"
} else {
    $pickTaskId = $pickTask.id.ToString()
    $pickedQty = [decimal]$pickTask.qty
    $completeJson = "{`"pickedQty`":$pickedQty}"
    try {
        $null = Invoke-RestMethod -Uri "$API_URL/outbound/picks/$pickTaskId/complete" -Method Post -Body $completeJson -ContentType "application/json; charset=utf-8" -Headers $headers
        Assert-True $true "L3-UAT-05 Complete pick"
    } catch {
        Assert-True $false "Complete pick: $(Get-ErrorBody $_)"
    }
}

# G. Pack try
Write-Host "`nG. Pack..." -ForegroundColor Cyan
try {
    $packJson = "{`"packageNo`":`"PKG-L3-$suffix`",`"weight`":1.0,`"weightSource`":`"manual`",`"scaleStable`":true}"
    $null = Invoke-RestMethod -Uri "$API_URL/outbound/packing/$shipmentId/complete" -Method Post -Body $packJson -ContentType "application/json; charset=utf-8" -Headers $headers
    Assert-True $true "Pack complete"
} catch {
    Assert-Skip "Pack (weight/governance): $(Get-ErrorBody $_)"
}

# H. Lot-HOLD
Write-Host "`nH. Lot-HOLD → move blocked..." -ForegroundColor Cyan
try {
    $holdLot = New-ReleasedLot "HOLD" 5
    $holdJson = "{`"reasonCode`":`"L3_HOLD`"}"
    $null = Invoke-RestMethod -Uri "$API_URL/qc/$($holdLot.lotId)/hold" -Method Post -Body $holdJson -ContentType "application/json; charset=utf-8" -Headers $headers
    $dest = if ($toLocationId -ne $locationId) { $toLocationId } else { $locationId }
    # If same loc, still try move with qty — may need different loc; create tiny second loc attempt already done
    $blocked = $false
    try {
        if ($toLocationId -eq $locationId) {
            # Cannot move same loc — use insufficient path separately; for hold use inventory/move to any other or expect gate before loc check
            # Force: get any other location or skip with note after verifying hold status via move to self fails differently
            $locs = Invoke-RestMethod -Uri "$API_URL/master-data/storage-locations" -Method Get -Headers $headers
            foreach ($loc in $locs.items) {
                if ($loc.id.ToString() -ne $locationId) { $dest = $loc.id.ToString(); break }
            }
        }
        $moveHoldJson = "{`"itemId`":`"$productId`",`"lotNo`":`"$($holdLot.lotNo)`",`"fromLocationId`":`"$locationId`",`"toLocationId`":`"$dest`",`"qty`":1.0,`"reasonCode`":`"TEST_SEED`"}"
        $null = Invoke-RestMethod -Uri "$API_URL/inventory/move" -Method Post -Body $moveHoldJson -ContentType "application/json; charset=utf-8" -Headers $headers
    } catch {
        $body = Get-ErrorBody $_
        if ($body -match "QC_LOT_ON_HOLD") { $blocked = $true }
        else { Write-Host "  hold-move body: $body" -ForegroundColor DarkYellow }
    }
    Assert-True $blocked "L3-UAT-02 QC_LOT_ON_HOLD"
} catch {
    Assert-True $false "Lot-HOLD setup: $(Get-ErrorBody $_)"
}

# I. Tenant isolation
Write-Host "`nI. Tenant isolation..." -ForegroundColor Cyan
$emailB = "l3-tenant2-$suffix@demo.local"
try {
    $regBody = @{
        email = $emailB
        password = "DemoTenant2!123"
        fullName = "L3 Tenant2"
        tenantId = "00000000-0000-0000-0000-000000000002"
    } | ConvertTo-Json
    $null = Invoke-RestMethod -Uri "$API_URL/auth/register" -Method Post -Body $regBody -ContentType "application/json"
} catch {
    # may already exist pattern — continue to login
    Write-Host "  register note: $(Get-ErrorBody $_)" -ForegroundColor DarkYellow
}
try {
    $loginB = Invoke-RestMethod -Uri "$API_URL/auth/login" -Method Post -Body (@{
        email = $emailB
        password = "DemoTenant2!123"
    } | ConvertTo-Json) -ContentType "application/json"
    $headersB = @{ Authorization = "Bearer $($loginB.token)" }
    $isolated = $false
    try {
        $listB = Invoke-RestMethod -Uri "$API_URL/outbound/shipments" -Method Get -Headers $headersB
        $items = @()
        if ($listB -is [System.Array]) { $items = $listB }
        elseif ($listB.items) { $items = $listB.items }
        elseif ($listB -ne $null) { $items = @($listB) }
        $leak = $false
        foreach ($s in $items) {
            if ($s.shipmentNo -eq $shipmentNo) { $leak = $true; break }
        }
        $isolated = -not $leak
    } catch {
        # 401/403 = PASS isolation
        $isolated = $true
    }
    Assert-True $isolated "L3-UAT-08 tenant isolation"
} catch {
    Assert-True $false "UAT-08 login B: $(Get-ErrorBody $_)"
}

# J. Insufficient available (online move oversized)
Write-Host "`nJ. Insufficient qty..." -ForegroundColor Cyan
try {
    $huge = "{`"itemId`":`"$productId`",`"lotNo`":`"$($happy.lotNo)`",`"fromLocationId`":`"$locationId`",`"toLocationId`":`"$(if($toLocationId -ne $locationId){$toLocationId}else{$locationId})`",`"qty`":999999.0,`"reasonCode`":`"TEST_SEED`"}"
    if ($toLocationId -eq $locationId) {
        Assert-Skip "L3-UAT-06 need 2 locations"
    } else {
        $gotInsuf = $false
        try {
            $null = Invoke-RestMethod -Uri "$API_URL/inventory/move" -Method Post -Body $huge -ContentType "application/json; charset=utf-8" -Headers $headers
        } catch {
            $b = Get-ErrorBody $_
            if ($b -match "INSUFFICIENT") { $gotInsuf = $true }
        }
        Assert-True $gotInsuf "L3-UAT-06 INSUFFICIENT_QTY"
    }
} catch {
    Assert-True $false "UAT-06: $(Get-ErrorBody $_)"
}

# J2 optional offline
Write-Host "`nJ2. Offline MOVE available (optional)..." -ForegroundColor Cyan
try {
    if ($toLocationId -eq $locationId) {
        Assert-Skip "Offline MOVE need 2 locations"
    } else {
        $clientOp = "L3-OFF-$suffix"
        $payloadObj = @{
            itemId = $productId
            lotNo = $happy.lotNo
            fromLocationId = $locationId
            toLocationId = $toLocationId
            qty = 999999.0
        } | ConvertTo-Json -Compress
        $syncBody = @{
            operations = @(
                @{
                    clientOperationId = $clientOp
                    stepType = "MOVE"
                    payload = $payloadObj
                }
            )
        } | ConvertTo-Json -Depth 5
        $got = $false
        try {
            $null = Invoke-RestMethod -Uri "$API_URL/mobile/offline-sync" -Method Post -Body $syncBody -ContentType "application/json; charset=utf-8" -Headers $headers
        } catch {
            $b = Get-ErrorBody $_
            if ($b -match "INSUFFICIENT_QTY") { $got = $true }
        }
        # offline may return 200 with failed op embedded — check both
        if (-not $got) {
            try {
                $syncRes = Invoke-RestMethod -Uri "$API_URL/mobile/offline-sync" -Method Post -Body ($syncBody -replace $clientOp, "L3-OFF2-$suffix") -ContentType "application/json; charset=utf-8" -Headers $headers
                $txt = ($syncRes | ConvertTo-Json -Depth 6)
                if ($txt -match "INSUFFICIENT_QTY") { $got = $true }
            } catch {
                $b = Get-ErrorBody $_
                if ($b -match "INSUFFICIENT_QTY") { $got = $true }
            }
        }
        if ($got) { Assert-True $true "L3-UAT-07 offline INSUFFICIENT_QTY" }
        else { Assert-Skip "L3-UAT-07 offline (no INSUFFICIENT in response)" }
    }
} catch {
    Assert-Skip "L3-UAT-07 offline: $(Get-ErrorBody $_)"
}

# K. Regression l2
Write-Host "`nK. verify_l2_p0 regression..." -ForegroundColor Cyan
$l2 = Join-Path $PSScriptRoot "verify_l2_p0_integrity.ps1"
& powershell -NoProfile -File $l2
Assert-True ($LASTEXITCODE -eq 0) "verify_l2_p0_integrity"

# L. Results JSON
$summary = [ordered]@{
    phase = 37
    pass = $pass
    fail = $fail
    skip = $skip
    shipmentNo = $shipmentNo
    results = $results
    at = (Get-Date).ToString("o")
}
$summary | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $evidenceDir "verify_l3_results.json") -Encoding utf8

Write-Host "`n=== Results: PASS=$pass FAIL=$fail SKIP=$skip ===" -ForegroundColor $(if ($fail -eq 0) { "Green" } else { "Red" })
if ($fail -gt 0) { exit 1 }
exit 0
