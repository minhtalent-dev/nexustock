$API_URL = "http://localhost:5024/api"

# 1. Login to get token
Write-Host "1. Logging in as admin..."
$loginBody = @{
    email = "admin@nexustock.com"
    password = "AdminSecret123!"
} | ConvertTo-Json

try {
    $loginRes = Invoke-RestMethod -Uri "$API_URL/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
    $token = $loginRes.token
    Write-Host "Login successful. Token acquired."
} catch {
    Write-Error "Login failed: $_"
    exit 1
}

$headers = @{
    Authorization = "Bearer $token"
}

# 2. Fetch Master Data
Write-Host "`n2. Fetching master data references..."
try {
    $products = Invoke-RestMethod -Uri "$API_URL/master-data/products" -Method Get -Headers $headers
    $product = $products.items[0]
    $itemId = $product.id
    $itemCode = $product.code
    $itemName = $product.name

    $locations = Invoke-RestMethod -Uri "$API_URL/master-data/storage-locations" -Method Get -Headers $headers
    $locId = $locations.items[0].id
    $locCode = $locations.items[0].code

    Write-Host "Using Product: $itemCode - $itemName ($itemId)"
    Write-Host "Using Location: $locCode ($locId)"
} catch {
    Write-Error "Failed to fetch master data: $_"
    exit 1
}

# 3. Configure product to track serial
Write-Host "`n3. Setting product to track serial (isSerialTracked = true)..."
$updateBody = @{
    code = $product.code
    name = $product.name
    description = $product.description
    barcode = $product.barcode
    baseUomId = $product.baseUomId
    isActive = $product.isActive
    isSerialTracked = $true
    rowVersion = $product.rowVersion
    config = @{
        iqcCheckType = $product.config.iqcCheckType
        vendorInnerLotCtl = $product.config.vendorInnerLotCtl
        isWafer = $product.config.isWafer
        lotValidationRegex = $product.config.lotValidationRegex
        minStock = $product.config.minStock
        maxStock = $product.config.maxStock
        weightClass = $product.config.weightClass
        rotationSpeed = $product.config.rotationSpeed
        trackSerial = $product.config.trackSerial
        length = $product.config.length
        width = $product.config.width
        height = $product.config.height
        weight = $product.config.weight
    }
    packages = @()
} | ConvertTo-Json

try {
    $updateRes = Invoke-RestMethod -Uri "$API_URL/master-data/products/$itemId" -Method Put -Body $updateBody -ContentType "application/json" -Headers $headers
    Write-Host "Product updated successfully: isSerialTracked = $($updateRes.isSerialTracked)"
} catch {
    $errorMsg = $_.Exception.Response.GetResponseStream()
    if ($errorMsg) {
        $reader = New-Object System.IO.StreamReader($errorMsg)
        Write-Error "Failed to update product: $($reader.ReadToEnd())"
    } else {
        Write-Error "Failed to update product: $_"
    }
    exit 1
}

# 4. Receive new serial
Write-Host "`n4. Registering / receiving a new serial number..."
$serialNo = "SR-" + [guid]::NewGuid().ToString().Substring(0, 8).ToUpper()
$receiveBody = @{
    itemId = $itemId
    locationId = $locId
    serialNo = $serialNo
} | ConvertTo-Json

try {
    $recRes = Invoke-RestMethod -Uri "$API_URL/serials/receive" -Method Post -Body $receiveBody -ContentType "application/json" -Headers $headers
    Write-Host "Serial received successfully: $($recRes.serialNo) with status: $($recRes.status)"
} catch {
    $errorMsg = $_.Exception.Response.GetResponseStream()
    if ($errorMsg) {
        $reader = New-Object System.IO.StreamReader($errorMsg)
        Write-Error "Failed to receive serial: $($reader.ReadToEnd())"
    } else {
        Write-Error "Failed to receive serial: $_"
    }
    exit 1
}

# 5. Receive duplicate serial (should fail)
Write-Host "`n5. Verifying duplicate serial rejection..."
try {
    $recRes = Invoke-RestMethod -Uri "$API_URL/serials/receive" -Method Post -Body $receiveBody -ContentType "application/json" -Headers $headers
    Write-Error "Test failed: Duplicate serial was accepted!"
    exit 1
} catch {
    Write-Host "Duplicate serial rejected as expected."
}

# 6. Validate serial
Write-Host "`n6. Validating serial..."
$validateBody = @{
    itemId = $itemId
    serialNo = $serialNo
    currentLocationId = $locId
} | ConvertTo-Json

try {
    $valRes = Invoke-RestMethod -Uri "$API_URL/serials/validate" -Method Post -Body $validateBody -ContentType "application/json" -Headers $headers
    Write-Host "Validation result: valid = $($valRes.valid)"
    if ($valRes.valid -ne $true) {
        Write-Error "Serial validation failed!"
        exit 1
    }
} catch {
    Write-Error "Failed to validate serial: $_"
    exit 1
}

# 7. Get Serial timeline
Write-Host "`n7. Fetching serial timeline..."
try {
    $timeline = Invoke-RestMethod -Uri "$API_URL/serials/$serialNo" -Method Get -Headers $headers
    Write-Host "Timeline events count: $($timeline.Count)"
    foreach ($evt in $timeline) {
        Write-Host "Event: $($evt.eventType) at $($evt.createdAt) by $($evt.createdBy)"
    }
    if ($timeline.Count -eq 0) {
        Write-Error "Timeline should not be empty!"
        exit 1
    }
} catch {
    Write-Error "Failed to get timeline: $_"
    exit 1
}

Write-Host "`n=========================================="
Write-Host "  SERIAL INTEGRATION TESTS PASSED 100%!"
Write-Host "=========================================="
