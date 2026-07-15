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

# 2. Get master data product
Write-Host "`n2. Fetching product..." -ForegroundColor Cyan
try {
    $products = Invoke-RestMethod -Uri "$API_URL/master-data/products" -Method Get -Headers $headers
    $productId = $products.items[0].id
    Write-Host "Using Product ID: $productId"
} catch {
    Write-Error "Failed to fetch master data: $_"
    exit 1
}

# 3. Create RMA Request
Write-Host "`n3. Creating RMA request..." -ForegroundColor Cyan
$createBody = @{
    customerId = "00000000-0000-0000-0000-000000000001"
    referenceNo = "OUT-SHIP-2026-TEST"
    items = @(
        @{
            itemId = $productId
            qtyExpected = 2.0
            serialNo = $null
            reasonCode = "DAMAGED"
        }
    )
} | ConvertTo-Json
try {
    $rma = Invoke-RestMethod -Uri "$API_URL/rma" -Method Post -Body $createBody -ContentType "application/json" -Headers $headers
    $rmaId = $rma.id
    Write-Host "RMA created: $($rma.rmaNo) with status: $($rma.status)"
} catch {
    Write-Error "Failed to create RMA: $_"
    exit 1
}

# 4. Receive RMA Items
Write-Host "`n4. Receiving returned goods..." -ForegroundColor Cyan
$receiveBody = @{
    items = @(
        @{
            itemId = $productId
            qtyReceived = 2.0
            serialNo = $null
        }
    )
} | ConvertTo-Json
try {
    $received = Invoke-RestMethod -Uri "$API_URL/rma/$rmaId/receive" -Method Post -Body $receiveBody -ContentType "application/json" -Headers $headers
    Write-Host "RMA Status updated: $($received.status)"
} catch {
    Write-Error "Failed to receive RMA: $_"
    exit 1
}

# 5. Process QC (Restock)
Write-Host "`n5. Submitting QC classification (RESTOCK)..." -ForegroundColor Cyan
try {
    $rmaDetails = Invoke-RestMethod -Uri "$API_URL/rma/$rmaId" -Method Get -Headers $headers
    $rmaItemId = $rmaDetails.items[0].id

    $qcBody = @{
        results = @(
            @{
                rmaItemId = $rmaItemId
                qcStatus = "PASS"
                disposition = "RESTOCK"
                qty = 2.0
                notes = "QC Passed. Repackaged."
            }
        )
    } | ConvertTo-Json

    $qcRes = Invoke-RestMethod -Uri "$API_URL/rma/$rmaId/qc" -Method Post -Body $qcBody -ContentType "application/json" -Headers $headers
    Write-Host "QC completed. RMA Status: $($qcRes.status)"
} catch {
    Write-Error "Failed to process QC: $_"
    exit 1
}

Write-Host "`n=========================================="
Write-Host "    RMA INTEGRATION TESTS PASSED 100%!" -ForegroundColor Green
Write-Host "=========================================="
