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
    
    # Kệ lấy hàng (Pick Face)
    $pickLocId = $locations.items[0].id
    $pickLocCode = $locations.items[0].code
    
    # Kệ lưu trữ (Bulk)
    $bulkLocId = $locations.items[1].id
    $bulkLocCode = $locations.items[1].code

    Write-Host "Using Product: $itemName ($itemId)"
    Write-Host "Using Pick Face Shelf: $pickLocCode ($pickLocId)"
    Write-Host "Using Bulk Reserve Shelf: $bulkLocCode ($bulkLocId)"
    
    $initialAvail = Invoke-RestMethod -Uri "$API_URL/allocation/availability?itemId=$itemId" -Method Get -Headers $headers
    $initialReserved = $initialAvail.qtyReserved
    Write-Host "Initial Qty Reserved in DB: $initialReserved"
} catch {
    Write-Error "Failed to fetch master data: $_"
    exit 1
}

# 2b. Clear any existing inventory on Pick Face shelf to ensure it is below Min (20)
Write-Host "`n2b. Clearing existing stock of product $itemId on Pick Face $pickLocCode..."
try {
    $balances = Invoke-RestMethod -Uri "$API_URL/inventory/balances?locationId=$pickLocId&itemId=$itemId" -Method Get -Headers $headers
    foreach ($item in $balances.items) {
        $availQty = $item.qtyAvailable
        if ($availQty -gt 0) {
            $clearBody = @{
                itemId = $itemId
                lotNo = $item.lotNo
                locationId = $pickLocId
                qty = -$availQty
                reasonCode = "CYCLE_COUNT"
                idempotencyKey = [guid]::NewGuid().ToString()
            } | ConvertTo-Json
            $clearRes = Invoke-RestMethod -Uri "$API_URL/inventory/adjust" -Method Post -Body $clearBody -ContentType "application/json" -Headers $headers
            Write-Host "Cleared $availQty qty of lot $($item.lotNo) from Pick Face."
        }
    }
} catch {
    Write-Host "No existing stock to clear or error: $_. Proceeding..."
}

# 3. Seed stock on the Bulk shelf
Write-Host "`n3. Seeding 100 qty on Bulk Shelf $bulkLocCode..."
$adjKey = [guid]::NewGuid().ToString()
$adjBody = @{
    itemId = $itemId
    lotNo = "LOT-REP-E2E-001"
    locationId = $bulkLocId
    qty = 100
    reasonCode = "TEST_SEED_REPLENISHMENT"
    idempotencyKey = $adjKey
} | ConvertTo-Json

try {
    $adjRes = Invoke-RestMethod -Uri "$API_URL/inventory/adjust" -Method Post -Body $adjBody -ContentType "application/json" -Headers $headers
    Write-Host "Bulk inventory adjusted successfully."
} catch {
    Write-Error "Failed to adjust bulk inventory: $_"
    exit 1
}

# 4. Clear any existing replenishment rules for this item/location
Write-Host "`n4. Creating Replenishment Rule (Min: 20, Max: 80)..."
$ruleBody = @{
    itemId = $itemId
    locationId = $pickLocId
    minQty = 20
    maxQty = 80
} | ConvertTo-Json

try {
    # Thử tạo mới quy tắc bổ sung hàng
    $ruleRes = Invoke-RestMethod -Uri "$API_URL/replenishment/rules" -Method Post -Body $ruleBody -ContentType "application/json" -Headers $headers
    Write-Host "Replenishment rule created successfully."
} catch {
    # Nếu đã tồn tại rule thì bỏ qua và tiếp tục
    Write-Host "Replenishment rule already exists or error: $_. Proceeding..."
}

# 5. Run Replenishment Engine Scan
Write-Host "`n5. Running Replenishment Engine Scan..."
try {
    $genRes = Invoke-RestMethod -Uri "$API_URL/replenishment/generate?strategy=FEFO" -Method Post -Headers $headers
    Write-Host "Generated tasks count: $($genRes.Count)"
    
    if ($genRes.Count -le 0) {
        Write-Error "Replenishment engine failed to generate tasks when Pick Face was below Min!"
        exit 1
    }
    
    $taskId = $genRes[0].id
    $requestedQty = $genRes[0].requestedQty
    Write-Host "Task generated: ID = $taskId, Requested Qty = $requestedQty"
    
    if ($requestedQty -ne 80) {
        Write-Error "Expected requested qty to be 80, but got $requestedQty"
        exit 1
    }
} catch {
    $errorMsg = $_.Exception.Response.GetResponseStream()
    if ($errorMsg) {
        $reader = New-Object System.IO.StreamReader($errorMsg)
        Write-Error "Failed to run replenishment engine: $($reader.ReadToEnd())"
    } else {
        Write-Error "Failed to run replenishment engine: $_"
    }
    exit 1
}

# 6. Verify reservation at Bulk shelf
Write-Host "`n6. Verifying reservation at Bulk shelf..."
try {
    $availRes = Invoke-RestMethod -Uri "$API_URL/allocation/availability?itemId=$itemId" -Method Get -Headers $headers
    Write-Host "Bulk Qty On Hand: $($availRes.qtyOnHand)"
    Write-Host "Bulk Qty Reserved: $($availRes.qtyReserved)"
    
    # Vì ta seed 100 và reserve 80 cho task bổ sung hàng
    $expectedReserved = $initialReserved + 80
    if ($availRes.qtyReserved -lt $expectedReserved) {
        Write-Error "Bulk reserved quantity is incorrect! Expected >= $expectedReserved, got $($availRes.qtyReserved)"
        exit 1
    }
    Write-Host "Bulk reservation: PASSED"
} catch {
    Write-Error "Failed to check availability: $_"
    exit 1
}

# 7. Complete the Replenishment task
Write-Host "`n7. Completing the replenishment task with actual quantity 80..."
$completeBody = @{
    actualQty = 80
    operatorName = "E2E Tester"
} | ConvertTo-Json

try {
    $compRes = Invoke-RestMethod -Uri "$API_URL/replenishment/tasks/$taskId/complete" -Method Post -Body $completeBody -ContentType "application/json" -Headers $headers
    Write-Host "Task Status after completion: $($compRes.status)"
    
    if ($compRes.status -ne "COMPLETED") {
        Write-Error "Expected task status to be COMPLETED, got $($compRes.status)"
        exit 1
    }
} catch {
    Write-Error "Failed to complete task: $_"
    exit 1
}

# 8. Verify post-replenishment stock balances
Write-Host "`n8. Verifying stock balances after replenishment..."
try {
    $availRes2 = Invoke-RestMethod -Uri "$API_URL/allocation/availability?itemId=$itemId" -Method Get -Headers $headers
    Write-Host "Total Qty On Hand: $($availRes2.qtyOnHand)"
    Write-Host "Total Qty Reserved (should be $initialReserved): $($availRes2.qtyReserved)"
    
    if ($availRes2.qtyReserved -ne $initialReserved) {
        Write-Error "Qty Reserved was not released to $initialReserved!"
        exit 1
    }
    Write-Host "Stock balances: PASSED"
} catch {
    Write-Error "Failed to verify final availability: $_"
    exit 1
}

Write-Host "`n>>> ALL REPLENISHMENT PHASE 14 E2E TESTS PASSED SUCCESSFULLY 100%! <<<"
