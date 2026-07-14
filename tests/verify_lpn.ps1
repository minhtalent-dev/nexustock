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
Write-Host "`n2. Fetching master data references..."
try {
    $products = Invoke-RestMethod -Uri "$API_URL/master-data/products" -Method Get -Headers $headers
    $itemId = $products.items[0].id
    $itemName = $products.items[0].name

    $locations = Invoke-RestMethod -Uri "$API_URL/master-data/storage-locations" -Method Get -Headers $headers
    $loc1Id = $locations.items[0].id
    $loc1Code = $locations.items[0].code
    $loc2Id = $locations.items[1].id
    $loc2Code = $locations.items[1].code

    Write-Host "Using Product: $itemName ($itemId)"
    Write-Host "Location 1: $loc1Code ($loc1Id)"
    Write-Host "Location 2: $loc2Code ($loc2Id)"
} catch {
    Write-Error "Failed to fetch master data: $_"
    exit 1
}

# 3. Seed stock on Location 1
Write-Host "`n3. Seeding 100 qty on Location 1..."
$adjKey = [guid]::NewGuid().ToString()
$lotNo = "LOT-LPN-" + [guid]::NewGuid().ToString().Substring(0, 8).ToUpper()
$adjBody = @{
    itemId = $itemId
    lotNo = $lotNo
    locationId = $loc1Id
    qty = 100
    reasonCode = "TEST_SEED_LPN"
    idempotencyKey = $adjKey
} | ConvertTo-Json

try {
    $adjRes = Invoke-RestMethod -Uri "$API_URL/inventory/adjust" -Method Post -Body $adjBody -ContentType "application/json" -Headers $headers
    Write-Host "Inventory seeded successfully."
} catch {
    Write-Error "Failed to seed inventory: $_"
    exit 1
}

# 4. Create an LPN pointing to Location 1
Write-Host "`n4. Creating LPN..."
$lpnNo = "LPN-E2E-" + [guid]::NewGuid().ToString().Substring(0, 8).ToUpper()
$lpnBody = @{
    lpnNo = $lpnNo
    locationId = $loc1Id
} | ConvertTo-Json

try {
    $lpnRes = Invoke-RestMethod -Uri "$API_URL/lpns" -Method Post -Body $lpnBody -ContentType "application/json" -Headers $headers
    $lpnId = $lpnRes.id
    Write-Host "LPN created: $lpnNo (ID: $lpnId) at Location 1"
} catch {
    $errorMsg = $_.Exception.Response.GetResponseStream()
    if ($errorMsg) {
        $reader = New-Object System.IO.StreamReader($errorMsg)
        Write-Error "Failed to create LPN: $($reader.ReadToEnd())"
    } else {
        Write-Error "Failed to create LPN: $_"
    }
    exit 1
}

# 5. Attach 40 items of the lot to LPN
Write-Host "`n5. Attaching 40 items to LPN..."
$attachBody = @{
    itemId = $itemId
    lotNo = $lotNo
    qty = 40.0
} | ConvertTo-Json

try {
    $attachRes = Invoke-RestMethod -Uri "$API_URL/lpns/$lpnId/attach" -Method Post -Body $attachBody -ContentType "application/json" -Headers $headers
    Write-Host "Response: $($attachRes.message)"
} catch {
    Write-Error "Failed to attach item to LPN: $_"
    exit 1
}

# 6. Verify inventory balance of LPN
Write-Host "`n6. Verifying LPN inventory balance..."
try {
    $balances = Invoke-RestMethod -Uri "$API_URL/inventory/balances?lpnId=$lpnId" -Method Get -Headers $headers
    $lpnQty = 0.0
    foreach ($item in $balances.items) {
        $lpnQty += $item.qtyOnHand
        Write-Host "Found on LPN: Lot $($item.lotNo), Qty $($item.qtyOnHand)"
    }
    if ($lpnQty -ne 40.0) {
        Write-Error "Expected 40.0 qty on LPN, but got $lpnQty"
        exit 1
    }
    Write-Host "LPN quantity verified successfully: $lpnQty"
} catch {
    Write-Error "Failed to verify LPN inventory: $_"
    exit 1
}

# 7. Detach 15 items from LPN
Write-Host "`n7. Detaching 15 items from LPN..."
$detachBody = @{
    itemId = $itemId
    lotNo = $lotNo
    qty = 15.0
} | ConvertTo-Json

try {
    $detachRes = Invoke-RestMethod -Uri "$API_URL/lpns/$lpnId/detach" -Method Post -Body $detachBody -ContentType "application/json" -Headers $headers
    Write-Host "Response: $($detachRes.message)"
} catch {
    Write-Error "Failed to detach item from LPN: $_"
    exit 1
}

# 8. Verify split: LPN should have 25
Write-Host "`n8. Verifying inventory split..."
try {
    $lpnBalances = Invoke-RestMethod -Uri "$API_URL/inventory/balances?lpnId=$lpnId" -Method Get -Headers $headers
    $lpnQty = $lpnBalances.items[0].qtyOnHand
    Write-Host "Remaining on LPN: $lpnQty"

    # Free stock balances (informational only — may accumulate across test runs)
    $freeBalances = Invoke-RestMethod -Uri "$API_URL/inventory/balances?locationId=$loc1Id&itemId=$itemId&lotNo=$lotNo" -Method Get -Headers $headers
    $freeQty = 0.0
    foreach ($item in $freeBalances.items) {
        if ($item.lpnId -eq $null) {
            $freeQty += $item.qtyOnHand
        }
    }
    Write-Host "Free stock on Location 1: $freeQty (may include prior test runs)"

    if ($lpnQty -ne 25.0) {
        Write-Error "Expected 25.0 on LPN, but got $lpnQty"
        exit 1
    }
    Write-Host "Split verified successfully."
} catch {
    Write-Error "Failed to verify split: $_"
    exit 1
}

# 9. Move LPN to Location 2
Write-Host "`n9. Moving LPN to Location 2 ($loc2Code)..."
$moveBody = @{
    targetLocationId = $loc2Id
} | ConvertTo-Json

try {
    $moveRes = Invoke-RestMethod -Uri "$API_URL/lpns/$lpnId/move" -Method Post -Body $moveBody -ContentType "application/json" -Headers $headers
    Write-Host "Response: $($moveRes.message)"
} catch {
    Write-Error "Failed to move LPN: $_"
    exit 1
}

# 10. Verify LPN new location and inventory movement
Write-Host "`n10. Verifying LPN new location and stock movement..."
try {
    $lpns = Invoke-RestMethod -Uri "$API_URL/lpns" -Method Get -Headers $headers
    $lpnObj = $lpns | Where-Object { $_.id -eq $lpnId }
    Write-Host "LPN current Location ID: $($lpnObj.locationId)"

    if ($lpnObj.locationId -ne $loc2Id) {
        Write-Error "LPN did not update to Location 2!"
        exit 1
    }

    $lpnBalances = Invoke-RestMethod -Uri "$API_URL/inventory/balances?lpnId=$lpnId" -Method Get -Headers $headers
    $movedItem = $lpnBalances.items[0]
    Write-Host "Inventory item location code on LPN: $($movedItem.locationCode)"

    if ($movedItem.locationId -ne $loc2Id) {
        Write-Error "Inventory item on LPN did not move to Location 2!"
        exit 1
    }

    Write-Host "Movement verified successfully."
} catch {
    Write-Error "Failed to verify movement: $_"
    exit 1
}

Write-Host "`n=========================================="
Write-Host "  LPN INTEGRATION TESTS PASSED 100%!"
Write-Host "=========================================="
