$API_URL = "http://localhost:5024/api"

# 1. Login
Write-Host "1. Logging in as admin..." -ForegroundColor Cyan
$loginBody = @{ email = "admin@nexustock.com"; password = "AdminSecret123!" } | ConvertTo-Json
try {
    $loginRes = Invoke-RestMethod -Uri "$API_URL/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
    $token = $loginRes.token
    $headers = @{ Authorization = "Bearer $token" }
    Write-Host "Login successful." -ForegroundColor Green
} catch {
    Write-Error "Login failed: $_"
    exit 1
}

# 2. Fetch master data product and partner
Write-Host "`n2. Fetching product, partner and zone..." -ForegroundColor Cyan
try {
    $products = Invoke-RestMethod -Uri "$API_URL/master-data/products" -Method Get -Headers $headers
    $product = $products.items[0]
    $productId = $product.id
    $uomId = $product.baseUomId
    $productCode = $product.code

    $partners = Invoke-RestMethod -Uri "$API_URL/master-data/partners" -Method Get -Headers $headers
    $partnerId = $partners.items[0].id

    $zones = Invoke-RestMethod -Uri "$API_URL/master-data/storage-zones" -Method Get -Headers $headers
    $zoneId = $zones.items[0].id

    # Tạo vị trí tạm thời có dung lượng vô hạn LOC-GEN-TEST-01
    $locationId = $null
    try {
        $createLocBody = @{
            zoneId = $zoneId
            code = "LOC-GEN-TEST-01"
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
        $locRes = Invoke-RestMethod -Uri "$API_URL/master-data/storage-locations" -Method Post -Body $createLocBody -ContentType "application/json" -Headers $headers
        $locationId = $locRes.id
        Write-Host "Created temporary location LOC-GEN-TEST-01." -ForegroundColor Green
    } catch {
        # Nếu đã tồn tại, lấy thông tin vị trí đó
        $locations = Invoke-RestMethod -Uri "$API_URL/master-data/storage-locations" -Method Get -Headers $headers
        foreach ($loc in $locations.items) {
            if ($loc.code -eq "LOC-GEN-TEST-01") {
                $locationId = $loc.id
                break
            }
        }
        if ($null -eq $locationId) {
            $locationId = $locations.items[0].id
        }
        Write-Host "LOC-GEN-TEST-01 already exists or fallback to existing." -ForegroundColor Yellow
    }

    Write-Host "Using Product: $productCode, Location ID: $locationId" -ForegroundColor Gray
} catch {
    Write-Error "Failed to fetch master data: $_"
    exit 1
}

# 3. Tạo Inbound Order để chuẩn bị hàng nhận
Write-Host "`n3. Creating Inbound Order to generate Lots..." -ForegroundColor Cyan
$ioNo = "IO-GEN-" + (Get-Date -Format "HHmmss")
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
Write-Host "Inbound Order created: $ioNo" -ForegroundColor Green

# 4. Nhận hàng lần 1 tạo Lot cha (nhận 10.0 units)
$parentLotNo = "LOT-PRNT-" + (Get-Date -Format "HHmmss")
Write-Host "`n4. Receiving items to create Parent Lot: $parentLotNo..." -ForegroundColor Cyan
$receiveParentBody = @{
    itemId = $productId
    lotNo = $parentLotNo
    receivedQty = 10.0
    toLocationId = $locationId
} | ConvertTo-Json
$null = Invoke-RestMethod -Uri "$API_URL/inbound/orders/$ioId/receive" -Method Post -Body $receiveParentBody -ContentType "application/json" -Headers $headers
Write-Host "Parent Lot received successfully." -ForegroundColor Green

# 5. Nhận hàng lần 2 tạo Lot con (nhận 2.0 units)
$childLotNo = "LOT-CHLD-" + (Get-Date -Format "HHmmss")
Write-Host "`n5. Receiving items to create Child Lot: $childLotNo..." -ForegroundColor Cyan
$receiveChildBody = @{
    itemId = $productId
    lotNo = $childLotNo
    receivedQty = 2.0
    toLocationId = $locationId
} | ConvertTo-Json
$null = Invoke-RestMethod -Uri "$API_URL/inbound/orders/$ioId/receive" -Method Post -Body $receiveChildBody -ContentType "application/json" -Headers $headers
Write-Host "Child Lot received successfully." -ForegroundColor Green

# 6. Tạo liên kết phả hệ Lot (Split)
Write-Host "`n6. Creating Lot Relation (Split from Parent to Child)..." -ForegroundColor Cyan
$createRelBody = @{
    parentLotNo = $parentLotNo
    childLotNo = $childLotNo
    relationType = "SPLIT"
    qtyTransferred = 4.0
    serialNos = @()
} | ConvertTo-Json

try {
    $relRes = Invoke-RestMethod -Uri "$API_URL/genealogy/relations" -Method Post -Body $createRelBody -ContentType "application/json" -Headers $headers
    Write-Host "Relation created successfully: $($relRes.message)" -ForegroundColor Green
} catch {
    Write-Error "Failed to create relation: $_"
    exit 1
}

# 7. Lấy cây phả hệ Lot cha
Write-Host "`n7. Retrieving Genealogy Tree for Lot: $parentLotNo..." -ForegroundColor Cyan
try {
    $tree = Invoke-RestMethod -Uri "$API_URL/genealogy/lots/$parentLotNo/tree" -Method Get -Headers $headers
    Write-Host "Tree response: LotNo=$($tree.lotNo), Status=$($tree.status), QtyOnHand=$($tree.qtyOnHand)" -ForegroundColor Green
    Write-Host "Children count: $($tree.children.Count)" -ForegroundColor Green
    if ($tree.children.Count -eq 0) {
        Write-Error "Genealogy Tree does not contain child node!"
        exit 1
    }
    Write-Host "Child LotNo: $($tree.children[0].lotNo), Child Qty: $($tree.children[0].qtyOnHand)" -ForegroundColor Green
} catch {
    Write-Error "Failed to retrieve tree: $_"
    exit 1
}

# 8. Test chặn chu kỳ (Prevent Cycle)
Write-Host "`n8. Verifying Prevent Cycle Guardrail..." -ForegroundColor Cyan
$cycleBody = @{
    parentLotNo = $childLotNo
    childLotNo = $parentLotNo
    relationType = "SPLIT"
    qtyTransferred = 1.0
} | ConvertTo-Json

$cyclePrevented = $false
try {
    $null = Invoke-RestMethod -Uri "$API_URL/genealogy/relations" -Method Post -Body $cycleBody -ContentType "application/json" -Headers $headers
} catch {
    $cyclePrevented = $true
    Write-Host "Cycle detected and blocked successfully (Expected Error: $_)" -ForegroundColor Green
}

if (-not $cyclePrevented) {
    Write-Error "Security Flaw: System failed to prevent genealogy cycle!"
    exit 1
}

# 9. Phong tỏa nhánh Lot (Cascade Hold)
Write-Host "`n9. Verifying Cascade Hold Branch..." -ForegroundColor Cyan
$holdBody = @{
    targetLotNo = $parentLotNo
    reasonCode = "QUALITY_ISSUE"
    description = "Test cascade hold branch quality propagation"
} | ConvertTo-Json

try {
    $holdRes = Invoke-RestMethod -Uri "$API_URL/genealogy/hold-branch" -Method Post -Body $holdBody -ContentType "application/json" -Headers $headers
    Write-Host "Hold branch triggered: $($holdRes.message)" -ForegroundColor Green

    # Kiểm tra trạng thái của Lot cha
    $parentLotCheck = Invoke-RestMethod -Uri "$API_URL/lots/$parentLotNo" -Method Get -Headers $headers
    Write-Host "Parent Lot status after hold: $($parentLotCheck[0].qcStatus)" -ForegroundColor Green
    if ($parentLotCheck[0].qcStatus -ne "HOLD") {
        Write-Error "Parent Lot was not held!"
        exit 1
    }

    # Kiểm tra trạng thái của Lot con
    $childLotCheck = Invoke-RestMethod -Uri "$API_URL/lots/$childLotNo" -Method Get -Headers $headers
    Write-Host "Child Lot status after hold: $($childLotCheck[0].qcStatus)" -ForegroundColor Green
    if ($childLotCheck[0].qcStatus -ne "HOLD") {
        Write-Error "Child Lot was not held!"
        exit 1
    }
} catch {
    Write-Error "Failed to test Cascade Hold: $_"
    exit 1
}

Write-Host "`n=================================================" -ForegroundColor Green
Write-Host "     ALL GENEALGOY INTEGRATION TESTS PASSED!" -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor Green
