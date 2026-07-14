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

# 2. Fetch Master Data dynamically
Write-Host "`nFetching master data references..."
try {
    $partners = Invoke-RestMethod -Uri "$API_URL/master-data/partners" -Method Get -Headers $headers
    $partnerId = $partners.items[0].id
    $partnerName = $partners.items[0].name

    $products = Invoke-RestMethod -Uri "$API_URL/master-data/products" -Method Get -Headers $headers
    $itemId = $products.items[0].id
    $itemName = $products.items[0].name

    $uoms = Invoke-RestMethod -Uri "$API_URL/master-data/uoms" -Method Get -Headers $headers
    $uomId = $uoms.items[0].id

    $locations = Invoke-RestMethod -Uri "$API_URL/master-data/storage-locations" -Method Get -Headers $headers
    $locId = $locations.items[0].id
    $locCode = $locations.items[0].code

    Write-Host "Using Partner: $partnerName ($partnerId)"
    Write-Host "Using Product: $itemName ($itemId)"
    Write-Host "Using Location: $locCode ($locId)"
} catch {
    Write-Error "Failed to fetch master data: $_"
    exit 1
}

# 3. Seed inventory balance for FEFO testing
Write-Host "`n2. Seeding test Lots and Inventory balance..."
# Seed LOT-FEFO-001 (100 qty)
$adjKey1 = [guid]::NewGuid().ToString()
$adjBody1 = @{
    itemId = $itemId
    lotNo = "LOT-FEFO-001"
    locationId = $locId
    qty = 100
    reasonCode = "TEST_SEED"
    idempotencyKey = $adjKey1
} | ConvertTo-Json

# Seed LOT-FEFO-002 (100 qty)
$adjKey2 = [guid]::NewGuid().ToString()
$adjBody2 = @{
    itemId = $itemId
    lotNo = "LOT-FEFO-002"
    locationId = $locId
    qty = 100
    reasonCode = "TEST_SEED"
    idempotencyKey = $adjKey2
} | ConvertTo-Json

try {
    $adjRes1 = Invoke-RestMethod -Uri "$API_URL/inventory/adjust" -Method Post -Body $adjBody1 -ContentType "application/json" -Headers $headers
    $adjRes2 = Invoke-RestMethod -Uri "$API_URL/inventory/adjust" -Method Post -Body $adjBody2 -ContentType "application/json" -Headers $headers
    Write-Host "Inventory adjusted successfully."
} catch {
    Write-Error "Failed to seed inventory: $_"
    exit 1
}

# 4. Create a test Shipment
Write-Host "`n3. Creating a test Shipment..."
$shipmentNo = "SH-" + [guid]::NewGuid().ToString().Substring(0, 8).ToUpper()
$shipmentBody = @{
    shipmentNo = $shipmentNo
    partnerId = $partnerId
    items = @(
        @{
            itemId = $itemId
            uomId = $uomId
            requestedQty = 150
        }
      )
} | ConvertTo-Json

try {
    $shipRes = Invoke-RestMethod -Uri "$API_URL/outbound/shipments" -Method Post -Body $shipmentBody -ContentType "application/json" -Headers $headers
    $shipmentId = $shipRes.id
    Write-Host "Shipment created successfully. ID: $shipmentId"
} catch {
    Write-Error "Failed to create shipment: $_"
    exit 1
}

# 5. Run Allocation (FEFO)
Write-Host "`n4. Running Allocation (FEFO) for Shipment..."
$allocBody = @{
    shipmentId = $shipmentId
    strategy = "FEFO"
    allowPartial = $true
    reservationTtlMinutes = 1440
} | ConvertTo-Json

try {
    $allocRes = Invoke-RestMethod -Uri "$API_URL/allocation/reserve" -Method Post -Body $allocBody -ContentType "application/json" -Headers $headers
    Write-Host "Allocation response: $($allocRes.message)"
    Write-Host "Shipment Status: $($allocRes.status)"
    Write-Host "Allocated lines count: $($allocRes.allocatedLines.Count)"
    
    if ($allocRes.success -ne $true) {
        Write-Error "Allocation was not successful!"
        exit 1
    }
} catch {
    Write-Error "Failed to run allocation: $_"
    exit 1
}

# 6. Check Availability
Write-Host "`n5. Checking availability for item..."
try {
    $availRes = Invoke-RestMethod -Uri "$API_URL/allocation/availability?itemId=$itemId" -Method Get -Headers $headers
    Write-Host "Item: $itemId"
    Write-Host "Qty On Hand: $($availRes.qtyOnHand)"
    Write-Host "Qty Reserved: $($availRes.qtyReserved)"
    Write-Host "Qty Available: $($availRes.qtyAvailable)"
    
    if ($availRes.qtyReserved -lt 150) {
        Write-Error "Reserved quantity is less than allocated!"
        exit 1
    }
} catch {
    Write-Error "Failed to check availability: $_"
    exit 1
}

# 7. Release Allocation
Write-Host "`n6. Releasing Allocation for Shipment..."
$releaseBody = @{
    shipmentId = $shipmentId
} | ConvertTo-Json

try {
    $releaseRes = Invoke-RestMethod -Uri "$API_URL/allocation/release" -Method Post -Body $releaseBody -ContentType "application/json" -Headers $headers
    Write-Host "Release response: $($releaseRes.message)"
    
    # Check availability again to confirm release
    $availRes2 = Invoke-RestMethod -Uri "$API_URL/allocation/availability?itemId=$itemId" -Method Get -Headers $headers
    Write-Host "Qty Reserved after release: $($availRes2.qtyReserved)"
    if ($availRes2.qtyReserved -ne ($availRes.qtyReserved - 150)) {
        Write-Error "Reserved quantity was not decremented correctly!"
        exit 1
    }
    Write-Host "Release check: PASSED"
} catch {
    Write-Error "Failed to release allocation: $_"
    exit 1
}

Write-Host "`n>>> ALL ALLOCATION & RESERVATION PHASE 13 E2E TESTS PASSED SUCCESSFULLY 100%! <<<"
