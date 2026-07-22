# Phase 36 — L2-P0 integrity verify
# Port: $env:NEXUSTOCK_API_URL hoặc http://localhost:5024/api
# Lưu ý: không dùng biến $pid (reserved trong Windows PowerShell)

$ErrorActionPreference = "Stop"
$API_URL = if ($env:NEXUSTOCK_API_URL) { $env:NEXUSTOCK_API_URL } else { "http://localhost:5024/api" }
$pass = 0
$fail = 0

function Assert-True($cond, $msg) {
    if ($cond) {
        Write-Host "  PASS: $msg" -ForegroundColor Green
        $script:pass++
    } else {
        Write-Host "  FAIL: $msg" -ForegroundColor Red
        $script:fail++
    }
}

Write-Host "=== verify_l2_p0_integrity ===" -ForegroundColor Cyan
Write-Host "API: $API_URL"

Write-Host "`n1. Login..." -ForegroundColor Cyan
try {
    $loginRes = Invoke-RestMethod -Uri "$API_URL/auth/login" -Method Post -Body (@{
        email = "admin@nexustock.com"
        password = "AdminSecret123!"
    } | ConvertTo-Json) -ContentType "application/json"
    $headers = @{ Authorization = "Bearer $($loginRes.token)" }
    Assert-True $true "Login"
} catch {
    Write-Error "Login failed: $_"
    exit 1
}

Write-Host "`n2. Master data..." -ForegroundColor Cyan
$products = Invoke-RestMethod -Uri "$API_URL/master-data/products" -Method Get -Headers $headers
$product = $null
foreach ($p in $products.items) {
    if ($p.isActive -and ($null -eq $p.isSerialTracked -or $p.isSerialTracked -eq $false)) {
        $product = $p
        break
    }
}
if ($null -eq $product) { $product = $products.items[0] }
$productId = $product.id.ToString()
$uoms = Invoke-RestMethod -Uri "$API_URL/master-data/uoms" -Method Get -Headers $headers
if ($product.baseUomId -and $product.baseUomId -ne [guid]::Empty) {
    $uomId = $product.baseUomId.ToString()
} else {
    $uomId = $uoms.items[0].id.ToString()
}
$partners = Invoke-RestMethod -Uri "$API_URL/master-data/partners" -Method Get -Headers $headers
$partnerId = $partners.items[0].id.ToString()
$locations = Invoke-RestMethod -Uri "$API_URL/master-data/storage-locations" -Method Get -Headers $headers
$locationId = $null
foreach ($loc in $locations.items) {
    if ($loc.code -eq "LOC-SORT-01") { $locationId = $loc.id.ToString(); break }
}
if (-not $locationId) {
    foreach ($loc in $locations.items) {
        if ($loc.code -ne "LOC-A-01") { $locationId = $loc.id.ToString(); break }
    }
}
if (-not $locationId) { $locationId = $locations.items[0].id.ToString() }

# Tạo LOC-SORT-01 dung lượng lớn nếu chưa có (tránh LOCATION_OVER_CAPACITY)
try {
    $zones = Invoke-RestMethod -Uri "$API_URL/master-data/storage-zones" -Method Get -Headers $headers
    $zoneId = $zones.items[0].id.ToString()
    $createLocJson = "{`"zoneId`":`"$zoneId`",`"code`":`"LOC-SORT-01`",`"maxCapacity`":999999.0,`"maxVolume`":999999.0,`"xCoord`":0,`"yCoord`":0,`"zCoord`":0,`"length`":1.0,`"width`":1.0,`"height`":1.0,`"isLocked`":false,`"isActive`":true}"
    $created = Invoke-RestMethod -Uri "$API_URL/master-data/storage-locations" -Method Post -Body $createLocJson -ContentType "application/json; charset=utf-8" -Headers $headers
    if ($created.id) { $locationId = $created.id.ToString() }
} catch {
    $locations2 = Invoke-RestMethod -Uri "$API_URL/master-data/storage-locations" -Method Get -Headers $headers
    foreach ($loc in $locations2.items) {
        if ($loc.code -eq "LOC-SORT-01") { $locationId = $loc.id.ToString(); break }
    }
}
Assert-True ($productId -and $partnerId -and $locationId -and $uomId) "Master data ($($product.code))"

Write-Host "`n3. Seed inbound + QC Release..." -ForegroundColor Cyan
$suffix = Get-Date -Format "HHmmss"
$lotNo = "LOT-L2P0-$suffix"
try {
    $ioJson = "{`"orderNo`":`"PO-L2P0-$suffix`",`"partnerId`":`"$partnerId`",`"items`":[{`"itemId`":`"$productId`",`"uomId`":`"$uomId`",`"expectedQty`":20.0,`"tolerance`":0.1}]}"
    $ioRes = Invoke-RestMethod -Uri "$API_URL/inbound/orders" -Method Post -Body $ioJson -ContentType "application/json; charset=utf-8" -Headers $headers
    $orderId = $ioRes.id.ToString()

    $recvJson = "{`"itemId`":`"$productId`",`"lotNo`":`"$lotNo`",`"receivedQty`":20.0,`"toLocationId`":`"$locationId`"}"
    $null = Invoke-RestMethod -Uri "$API_URL/inbound/orders/$orderId/receive" -Method Post -Body $recvJson -ContentType "application/json; charset=utf-8" -Headers $headers

    $lotRes = Invoke-RestMethod -Uri "$API_URL/lots/$lotNo" -Method Get -Headers $headers
    $lotId = $lotRes[0].id.ToString()

    $queue = Invoke-RestMethod -Uri "$API_URL/qc/queue" -Method Get -Headers $headers
    $queueItem = $null
    foreach ($item in $queue) {
        if ($item.lotNo -eq $lotNo) { $queueItem = $item; break }
    }
    if ($null -eq $queueItem) { throw "Lot not in QC queue" }

    $qcReqId = $queueItem.id.ToString()
    $qcJson = "{`"qcRequestId`":`"$qcReqId`",`"isPassed`":true,`"metrics`":`"L2-P0 verify`"}"
    $null = Invoke-RestMethod -Uri "$API_URL/qc/$lotId/result" -Method Post -Body $qcJson -ContentType "application/json; charset=utf-8" -Headers $headers
    Assert-True $true "Inbound + QC Release ($lotNo)"
} catch {
    $detail = $_.ErrorDetails.Message
    if (-not $detail -and $_.Exception.Response) {
        $sr = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $detail = $sr.ReadToEnd()
    }
    if (-not $detail) { $detail = "$_" }
    Write-Host "  FAIL: Seed QC: $detail" -ForegroundColor Red
    $fail++
    Write-Host "`nResults: PASS=$pass FAIL=$fail" -ForegroundColor Yellow
    exit 1
}

Write-Host "`n4. Create shipment + generate-picks..." -ForegroundColor Cyan
$shipmentNo = "SH-L2P0-$suffix"
$shipJson = "{`"shipmentNo`":`"$shipmentNo`",`"partnerId`":`"$partnerId`",`"items`":[{`"itemId`":`"$productId`",`"uomId`":`"$uomId`",`"requestedQty`":5.0}]}"
$ship = Invoke-RestMethod -Uri "$API_URL/outbound/shipments" -Method Post -Body $shipJson -ContentType "application/json; charset=utf-8" -Headers $headers
$shipmentId = $ship.id.ToString()
Assert-True ($shipmentId -ne "") "Shipment created"

try {
    $gen = Invoke-RestMethod -Uri "$API_URL/outbound/shipments/$shipmentId/generate-picks?strategy=FEFO" -Method Post -Headers $headers
    Assert-True ($null -ne $gen.message) "Generate picks response"
    Assert-True ($gen.pickTaskCount -gt 0) "pickTaskCount > 0 (got $($gen.pickTaskCount))"
} catch {
    $detail = $_.ErrorDetails.Message
    if (-not $detail -and $_.Exception.Response) {
        $sr = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $detail = $sr.ReadToEnd()
    }
    Write-Host "  FAIL: generate-picks: $detail" -ForegroundColor Red
    $fail++
}

Write-Host "`n5. Second generate-picks (PICKS_ALREADY_EXIST)..." -ForegroundColor Cyan
try {
    $null = Invoke-RestMethod -Uri "$API_URL/outbound/shipments/$shipmentId/generate-picks" -Method Post -Headers $headers
    Assert-True $false "Expected 400 PICKS_ALREADY_EXIST"
} catch {
    $resp = $_.ErrorDetails.Message
    if (-not $resp -and $_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $resp = $reader.ReadToEnd()
    }
    Assert-True ($resp -match "PICKS_ALREADY_EXIST") "PICKS_ALREADY_EXIST"
}

Write-Host "`n6. Disk static checks..." -ForegroundColor Cyan
$root = Split-Path $PSScriptRoot -Parent
$gpOld = Select-String -Path (Join-Path $root "backend/modules/Nexustock.Modules.Inventory/Controllers/OutboundController.cs") -Pattern 'HttpPost\("shipments/\{id:guid\}/generate-picks"\)' -ErrorAction SilentlyContinue
Assert-True ($null -eq $gpOld) "Old GeneratePicks removed"
Assert-True (Test-Path (Join-Path $root "backend/modules/Nexustock.Modules.Allocation/Controllers/OutboundGeneratePicksController.cs")) "New controller exists"
Assert-True (Select-String -Path (Join-Path $root "backend/modules/Nexustock.Modules.Allocation/Services/AllocationService.cs") -Pattern "CreatePickTasks" -Quiet) "CreatePickTasks in Allocate"
Assert-True (Select-String -Path (Join-Path $root "backend/modules/Nexustock.Modules.Inventory/Controllers/MobileController.cs") -Pattern "QtyOnHand - inventory.QtyReserved" -Quiet) "DF-01"
Assert-True (Select-String -Path (Join-Path $root "backend/modules/Nexustock.Modules.Inventory/Controllers/OutboundController.cs") -Pattern "RESERVED_UNDERFLOW" -Quiet) "RESERVED_UNDERFLOW"
Assert-True (Test-Path (Join-Path $root "backend/modules/Nexustock.Modules.Inventory/Interceptors/InventoryIntegrityInterceptor.cs")) "Interceptor"
Assert-True (Test-Path (Join-Path $root "backend/modules/Nexustock.Modules.Inventory/Migrations/20260722073000_AddQtyOnHandNonNegativeCheck.cs")) "Migration"

Write-Host "`n=== Results: PASS=$pass FAIL=$fail ===" -ForegroundColor $(if ($fail -eq 0) { "Green" } else { "Red" })
if ($fail -gt 0) { exit 1 }
exit 0
