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
    $isSerial = $product.isSerialTracked

    $partners = Invoke-RestMethod -Uri "$API_URL/master-data/partners" -Method Get -Headers $headers
    $partnerId = $partners.items[0].id

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

    Write-Host "Using Product: $productCode ($productId), Location: $locationCode ($locationId), SerialTracked: $isSerial"
} catch {
    Write-Error "Master data fetch failed: $_"
    exit 1
}

# 3. Create 2 Outbound Shipments
Write-Host "`n3. Creating 2 Outbound Shipments..." -ForegroundColor Cyan
$shipment1No = "SHIP-WAVE-UI-01-" + (Get-Date -Format "HHmmss")
$shipment2No = "SHIP-WAVE-UI-02-" + (Get-Date -Format "HHmmss")

$createBody1 = @{
    shipmentNo = $shipment1No
    partnerId = $partnerId
    items = @(
        @{
            itemId = $productId
            uomId = $uomId
            requestedQty = 3.0
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
            requestedQty = 2.0
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

$serials = @()
if ($isSerial -eq $true -or $isSerial -eq "True") {
    # Generate serials for the picking quantity
    for ($i = 1; $i -le $qtyToPick; $i++) {
        $serials += "SR-WAVE-UI-" + (Get-Date -Format "HHmmss") + "-$i"
    }
}

$completePickBody = @{
    taskId = $pickTaskId
    pickedQty = $qtyToPick
    serialNos = $serials
} | ConvertTo-Json

try {
    $completeRes = Invoke-RestMethod -Uri "$API_URL/waves/pick-tasks/complete" -Method Post -Body $completePickBody -ContentType "application/json" -Headers $headers
    Write-Host "Pick Task completed: $($completeRes.message)"
    
    Write-Host "`n=================================================" -ForegroundColor Green
    Write-Host "WAVE_ID: $waveId" -ForegroundColor Green
    Write-Host "PRODUCT_CODE: $productCode" -ForegroundColor Green
    Write-Host "SETUP COMPLETED. READY FOR BROWSER TEST." -ForegroundColor Green
    Write-Host "=================================================" -ForegroundColor Green
} catch {
    $streamReader = [System.IO.StreamReader]($_.Exception.Response.GetResponseStream())
    $errBody = $streamReader.ReadToEnd()
    Write-Error "Failed to complete pick task: $errBody"
    exit 1
}
