$API_URL = "http://localhost:5024/api"

# 1. Login
Write-Host "1. Logging in as admin..." -ForegroundColor Cyan
$loginBody = @{
    email = "admin@nexustock.com"
    password = "AdminSecret123!"
} | ConvertTo-Json

try {
    $loginRes = Invoke-RestMethod -Uri "$API_URL/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
    $token = $loginRes.token
    $headers = @{ Authorization = "Bearer $token" }
    Write-Host "Login successful."
} catch {
    Write-Error "Login failed: $_"
    exit 1
}

# 2. Fetch master data product and partner
Write-Host "`n2. Fetching product & partner..." -ForegroundColor Cyan
try {
    $products = Invoke-RestMethod -Uri "$API_URL/master-data/products" -Method Get -Headers $headers
    $product = $null
    foreach ($p in $products.items) {
        Write-Host "Debug Product: $($p.code), Active: $($p.isActive), SerialTracked: $($p.isSerialTracked)" -ForegroundColor Gray
        if ($p.isActive -and ($null -eq $p.isSerialTracked -or $p.isSerialTracked -eq $false)) {
            $product = $p
            break
        }
    }
    if ($null -eq $product) {
        $product = $products.items[0]
    }
    $productId = $product.id
    $uomId = $product.baseUomId
    $productCode = $product.code

    $partners = Invoke-RestMethod -Uri "$API_URL/master-data/partners" -Method Get -Headers $headers
    $partnerId = $partners.items[0].id

    # Lấy vị trí kệ và tìm kệ không phải LOC-A-01 để tránh quá tải
    $locations = Invoke-RestMethod -Uri "$API_URL/master-data/storage-locations" -Method Get -Headers $headers
    $selectedLoc = $null
    foreach ($loc in $locations.items) {
        if ($loc.code -ne "LOC-A-01") {
            $selectedLoc = $loc
            break
        }
    }
    if ($null -eq $selectedLoc) {
        $selectedLoc = $locations.items[0]
    }
    $locationId = $selectedLoc.id
    $locationCode = $selectedLoc.code

    # Lấy zoneId để tạo LOC-SORT-01
    $zones = Invoke-RestMethod -Uri "$API_URL/master-data/storage-zones" -Method Get -Headers $headers
    $zoneId = $zones.items[0].id
    
    # Tạo vị trí tạm thời LOC-SORT-01 nếu chưa có
    try {
        $createLocBody = @{
            zoneId = $zoneId
            code = "LOC-SORT-01"
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
        } | ConvertTo-Json
        $null = Invoke-RestMethod -Uri "$API_URL/master-data/storage-locations" -Method Post -Body $createLocBody -ContentType "application/json" -Headers $headers
        Write-Host "[Setup] Created temporary location LOC-SORT-01." -ForegroundColor Green
    } catch {
        Write-Host "[Setup] LOC-SORT-01 already exists or cannot be created (skipped)." -ForegroundColor Yellow
    }

    Write-Host "Using Product: $productCode ($productId), Partner ID: $partnerId, Location: $locationCode ($locationId)"

    # --- TỰ ĐỘNG NHẬP KHO ĐỂ TẠO TỒN KHO MỚI ---
    Write-Host "`n[Setup] Creating Inbound Order to generate inventory..." -ForegroundColor Magenta
    $ioNo = "IO-WAVE-SETUP-" + (Get-Date -Format "HHmmss")
    $ioBody = @{
        orderNo = $ioNo
        partnerId = $partnerId
        items = @(
            @{
                itemId = $productId
                uomId = $uomId
                expectedQty = 15.0
                tolerance = 0.1
            }
        )
    } | ConvertTo-Json
    $ioRes = Invoke-RestMethod -Uri "$API_URL/inbound/orders" -Method Post -Body $ioBody -ContentType "application/json" -Headers $headers
    $ioId = $ioRes.id

    Write-Host "[Setup] Receiving 15 units into Lot..." -ForegroundColor Magenta
    $lotNo = "LOT-WAVE-SETUP-" + (Get-Date -Format "HHmmss")
    $receiveBody = @{
        itemId = $productId
        lotNo = $lotNo
        receivedQty = 15.0
        toLocationId = $locationId
    } | ConvertTo-Json
    
    try {
        $null = Invoke-RestMethod -Uri "$API_URL/inbound/orders/$ioId/receive" -Method Post -Body $receiveBody -ContentType "application/json" -Headers $headers
        
        Write-Host "[Setup] Fetching Lot details..." -ForegroundColor Magenta
        $lotRes = Invoke-RestMethod -Uri "$API_URL/lots/$lotNo" -Method Get -Headers $headers
        $lot = $lotRes[0]
        $lotId = $lot.id

        Write-Host "[Setup] Fetching QC Queue to find request..." -ForegroundColor Magenta
        $queue = Invoke-RestMethod -Uri "$API_URL/qc/queue" -Method Get -Headers $headers
        $queueItem = $null
        foreach ($item in $queue) {
            if ($item.lotNo -eq $lotNo) {
                $queueItem = $item
                break
            }
        }
        if ($null -eq $queueItem) {
            Write-Error "Lot not found in QC Queue!"
            exit 1
        }

        Write-Host "[Setup] Recording QC PASS for Lot..." -ForegroundColor Magenta
        $qcBody = @{
            qcRequestId = $queueItem.id
            isPassed = $true
            metrics = "Auto Setup for Wave Picking"
        } | ConvertTo-Json
        $null = Invoke-RestMethod -Uri "$API_URL/qc/$lotId/result" -Method Post -Body $qcBody -ContentType "application/json" -Headers $headers
        Write-Host "[Setup] Inventory successfully created and released." -ForegroundColor Green
    } catch {
        Write-Host "[Setup] Setup inventory skipped/failed (using existing inventory): $_" -ForegroundColor Yellow
    }
} catch {
    Write-Error "Master data fetch failed: $_"
    exit 1
}

# 3. Create 2 Outbound Shipments
Write-Host "`n3. Creating 2 Outbound Shipments..." -ForegroundColor Cyan
$shipment1No = "SHIP-WAVE-TEST-01-" + (Get-Date -Format "HHmmss")
$shipment2No = "SHIP-WAVE-TEST-02-" + (Get-Date -Format "HHmmss")

$createBody1 = @{
    shipmentNo = $shipment1No
    partnerId = $partnerId
    items = @(
        @{
            itemId = $productId
            uomId = $uomId
            requestedQty = 5.0
        }
    )
} | ConvertTo-Json

$createBody2 = @{
    shipmentNo = $shipment2No
    partnerId = $partnerId
    items = @(
        @{
            itemId = $productId
            uomId = $uomId
            requestedQty = 5.0
        }
    )
} | ConvertTo-Json

try {
    $ship1 = Invoke-RestMethod -Uri "$API_URL/outbound/shipments" -Method Post -Body $createBody1 -ContentType "application/json" -Headers $headers
    $ship2 = Invoke-RestMethod -Uri "$API_URL/outbound/shipments" -Method Post -Body $createBody2 -ContentType "application/json" -Headers $headers
    $ship1Id = $ship1.id
    $ship2Id = $ship2.id
    Write-Host "Created Shipments: $shipment1No ($ship1Id) & $shipment2No ($ship2Id)"
} catch {
    Write-Error "Failed to create shipments: $_"
    exit 1
}

# 4. Create Picking Wave
Write-Host "`n4. Creating Picking Wave..." -ForegroundColor Cyan
$waveBody = @{
    shipmentIds = @($ship1Id, $ship2Id)
} | ConvertTo-Json

try {
    $wave = Invoke-RestMethod -Uri "$API_URL/waves" -Method Post -Body $waveBody -ContentType "application/json" -Headers $headers
    $waveId = $wave.id
    Write-Host "Picking Wave created successfully. Wave ID: $waveId"
} catch {
    Write-Error "Failed to create Picking Wave: $_"
    exit 1
}

# 5. Release Wave
Write-Host "`n5. Releasing Wave (Running Allocation)..." -ForegroundColor Cyan
try {
    $releaseRes = Invoke-RestMethod -Uri "$API_URL/waves/$waveId/release" -Method Post -Headers $headers
    Write-Host "Wave released: $($releaseRes.message)"
} catch {
    Write-Error "Failed to release Wave: $_"
    exit 1
}

# 6. Fetch Wave Details and get Pick Task ID
Write-Host "`n6. Fetching Wave Details..." -ForegroundColor Cyan
try {
    $waveDetail = Invoke-RestMethod -Uri "$API_URL/waves/$waveId" -Method Get -Headers $headers
    Write-Host "Wave Status: $($waveDetail.status)"
    Write-Host "Wave Details JSON: $($waveDetail | ConvertTo-Json -Depth 4)"
    
    if ($waveDetail.pickTasks.Count -eq 0) {
        Write-Error "No pick tasks generated! Checking allocations on items..."
        foreach ($it in $waveDetail.items) {
            Write-Host "Item: $($it.itemCode), Expected: $($it.qtyExpected), Allocated: $($it.qtyAllocated)"
        }
        exit 1
    }

    $pickTask = $waveDetail.pickTasks[0]
    $pickTaskId = $pickTask.id
    $qtyToPick = $pickTask.qtyToPick
    Write-Host "Found Pick Task ID: $pickTaskId for Qty: $qtyToPick at location: $($pickTask.locationCode)"
} catch {
    Write-Error "Failed to fetch wave details: $_"
    exit 1
}

# 7. Complete Pick Task
Write-Host "`n7. Completing Pick Task..." -ForegroundColor Cyan
$completePickBody = @{
    taskId = $pickTaskId
    pickedQty = $qtyToPick
    serialNos = @("SR-WAVE-01", "SR-WAVE-02", "SR-WAVE-03", "SR-WAVE-04", "SR-WAVE-05", "SR-WAVE-06", "SR-WAVE-07", "SR-WAVE-08", "SR-WAVE-09", "SR-WAVE-10")
} | ConvertTo-Json

try {
    $completeRes = Invoke-RestMethod -Uri "$API_URL/waves/pick-tasks/complete" -Method Post -Body $completePickBody -ContentType "application/json" -Headers $headers
    Write-Host "Pick Task completed: $($completeRes.message)"
} catch {
    $streamReader = [System.IO.StreamReader]($_.Exception.Response.GetResponseStream())
    $errBody = $streamReader.ReadToEnd()
    Write-Error "Failed to complete pick task: $errBody"
    exit 1
}

# 8. Sort Items on Put-Wall
Write-Host "`n8. Sorting Items on Put-Wall..." -ForegroundColor Cyan
$sortBody = @{
    barcodeOrSerial = $productCode
} | ConvertTo-Json

try {
    # Quét 10 lần vì tổng số lượng lấy là 10 cái (5 cái đơn 1, 5 cái đơn 2)
    for ($i = 1; $i -le 10; $i++) {
        $sortRes = Invoke-RestMethod -Uri "$API_URL/waves/$waveId/sort" -Method Post -Body $sortBody -ContentType "application/json" -Headers $headers
        Write-Host "Scan $($i): Product sorted to Slot $($sortRes.recommendedSlotNumber). Progress: $($sortRes.qtySorted)/$($sortRes.qtyExpected)"
    }
} catch {
    Write-Error "Failed to sort items: $_"
    exit 1
}

# 9. Complete Sortation / Wave
Write-Host "`n9. Completing Sortation..." -ForegroundColor Cyan
try {
    $completeWaveRes = Invoke-RestMethod -Uri "$API_URL/waves/$waveId/complete" -Method Post -Headers $headers
    Write-Host "Wave completed: $($completeWaveRes.message)"
} catch {
    Write-Error "Failed to complete wave sortation: $_"
    exit 1
}

# 10. Re-verify Wave status
Write-Host "`n10. Verifying final Wave status..." -ForegroundColor Cyan
try {
    $waveFinal = Invoke-RestMethod -Uri "$API_URL/waves/$waveId" -Method Get -Headers $headers
    Write-Host "Final Wave Status: $($waveFinal.status)"
    if ($waveFinal.status -eq "COMPLETED") {
        Write-Host "Status is correct."
    } else {
        Write-Error "Incorrect status: $($waveFinal.status)"
        exit 1
    }
} catch {
    Write-Error "Failed to verify final status: $_"
    exit 1
}

Write-Host "`n================================================="
Write-Host "    WAVE PICKING INTEGRATION TESTS PASSED 100%!" -ForegroundColor Green
Write-Host "================================================="
