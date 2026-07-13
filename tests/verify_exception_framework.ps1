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

# 2. Get existing inventory balance to use for test
Write-Host "`n2. Fetching existing inventory balance..."
try {
    $balancesRes = Invoke-RestMethod -Uri "$API_URL/inventory/balances?page=1&pageSize=100" -Method Get -Headers $headers
    if ($balancesRes.items.Count -eq 0) {
        Write-Error "No inventory balances found. Seed inventory data first."
        exit 1
    }
    
    $inv = $null
    foreach ($item in $balancesRes.items) {
        $avail = $item.qtyOnHand - $item.qtyReserved
        if ($avail -ge 5) {
            $inv = $item
            break
        }
    }
    
    if ($inv -eq $null) {
        Write-Error "No inventory balances with available quantity >= 5 found."
        exit 1
    }
    
    $itemId = $inv.itemId
    $locationId = $inv.locationId
    $lotNo = $inv.lotNo
    $initialQty = $inv.qtyOnHand
    Write-Host "Found inventory with sufficient stock: ItemId=$itemId, LocationId=$locationId, LotNo=$lotNo, Initial Qty=$initialQty"
} catch {
    Write-Error "Failed to fetch inventory balances: $_"
    exit 1
}

# 3. Create an Operational Exception (SHORTAGE -5)
Write-Host "`n3. Creating Operational Exception for Shortage..."
$exceptionBody = @{
    type = "SHORTAGE"
    severity = "HIGH"
    referenceType = "INVENTORY_RECORD"
    referenceId = $itemId
    locationId = $locationId
    lotNo = $lotNo
    qty = -5
    reasonCode = "SHORTAGE"
    note = "Thieu hang phat hien luc pick"
} | ConvertTo-Json

try {
    $exc = Invoke-RestMethod -Uri "$API_URL/exceptions" -Method Post -Body $exceptionBody -ContentType "application/json" -Headers $headers
    $exceptionId = $exc.id
    $exceptionCode = $exc.code
    Write-Host "Exception created successfully. Code=$exceptionCode, Id=$exceptionId, Status=$($exc.status)"
    if ($exc.status -ne "Open") { Write-Error "Status should be Open" }
} catch {
    Write-Error "Failed to create exception: $_"
    exit 1
}

# 4. Get Open Exceptions
Write-Host "`n4. Fetching open exceptions..."
try {
    $openRes = Invoke-RestMethod -Uri "$API_URL/exceptions/open" -Method Get -Headers $headers
    $found = $openRes.items | Where-Object { $_.id -eq $exceptionId }
    if ($found) {
        Write-Host "Successfully verified exception is in the open list."
    } else {
        Write-Error "Created exception not found in open list."
        exit 1
    }
} catch {
    Write-Error "Failed to fetch open exceptions: $_"
    exit 1
}

# 5. Assign Exception to Operator
Write-Host "`n5. Assigning exception to operator..."
$assignBody = @{
    owner = "operator_01"
    slaHours = 1
} | ConvertTo-Json

try {
    $assignRes = Invoke-RestMethod -Uri "$API_URL/exceptions/$exceptionId/assign" -Method Post -Body $assignBody -ContentType "application/json" -Headers $headers
    Write-Host "Response: $($assignRes.message)"
    
    # Verify status changed to In_Progress
    $excDetail = Invoke-RestMethod -Uri "$API_URL/exceptions/$exceptionId" -Method Get -Headers $headers
    Write-Host "Updated Status: $($excDetail.status)"
    if ($excDetail.status -ne "In_Progress") {
        Write-Error "Exception status did not change to In_Progress"
        exit 1
    }
} catch {
    Write-Error "Failed to assign exception: $_"
    exit 1
}

# 6. Check Exception Events Timeline
Write-Host "`n6. Checking exception events timeline..."
try {
    $events = Invoke-RestMethod -Uri "$API_URL/exceptions/$exceptionId/events" -Method Get -Headers $headers
    Write-Host "Events count: $($events.Count)"
    foreach ($e in $events) {
        Write-Host " - Transition: $($e.transition) by $($e.actor) at $($e.createdAt). Note: $($e.note)"
    }
    if ($events.Count -lt 2) {
        Write-Error "Events timeline should have at least 2 events (CREATE and ASSIGN)"
        exit 1
    }
} catch {
    Write-Error "Failed to fetch events: $_"
    exit 1
}

# 7. Resolve Exception with CORRECTIVE_TRANSACTION
Write-Host "`n7. Resolving exception with CORRECTIVE_TRANSACTION..."
$resolveBody = @{
    action = "CORRECTIVE_TRANSACTION"
    reasonCode = "SHORTAGE"
    note = "Xac nhan thieu va dieu chinh giam ton kho he thong"
} | ConvertTo-Json

try {
    $resolveRes = Invoke-RestMethod -Uri "$API_URL/exceptions/$exceptionId/resolve" -Method Post -Body $resolveBody -ContentType "application/json" -Headers $headers
    Write-Host "Response: $($resolveRes.message)"
    
    # Verify status changed to Resolved
    $excDetail = Invoke-RestMethod -Uri "$API_URL/exceptions/$exceptionId" -Method Get -Headers $headers
    Write-Host "Final Status: $($excDetail.status)"
    if ($excDetail.status -ne "Resolved") {
        Write-Error "Exception status did not change to Resolved"
        exit 1
    }
} catch {
    Write-Error "Failed to resolve exception: $_"
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Error Response Body: $responseBody"
    }
    exit 1
}

# 8. Verify Inventory Qty has been synchronized real-time
Write-Host "`n8. Verifying real-time inventory quantity sync..."
try {
    $balancesRes2 = Invoke-RestMethod -Uri "$API_URL/inventory/balances?page=1&pageSize=100" -Method Get -Headers $headers
    $updatedInv = $balancesRes2.items | Where-Object { $_.itemId -eq $itemId -and $_.locationId -eq $locationId -and $_.lotNo -eq $lotNo }
    
    $expectedQty = $initialQty - 5
    $actualQty = $updatedInv.qtyOnHand
    Write-Host "Initial Qty: $initialQty"
    Write-Host "Expected Qty after adjustment: $expectedQty"
    Write-Host "Actual Qty after adjustment: $actualQty"
    
    if ($actualQty -eq $expectedQty) {
        Write-Host "Inventory sync verification passed."
    } else {
        Write-Error "Inventory sync failed! Expected $expectedQty but got $actualQty"
        exit 1
    }
} catch {
    Write-Error "Failed to verify inventory sync: $_"
    exit 1
}

# 9. Test Auto-Capture Exception via Middleware
Write-Host "`n9. Testing Auto-Capture Exception via Middleware..."
$scanBody = @{
    context = "LOCATION"
    barcode = "LOC-NON-EXISTENT-XYZ"
} | ConvertTo-Json

try {
    $scanRes = Invoke-RestMethod -Uri "$API_URL/mobile/scan/validate" -Method Post -Body $scanBody -ContentType "application/json" -Headers $headers
    Write-Error "Scan should have failed with 400 Bad Request but returned success."
    exit 1
} catch {
    $errRes = $_.Exception.Response
    if ($errRes) {
        $reader = New-Object System.IO.StreamReader($errRes.GetResponseStream())
        $bodyText = $reader.ReadToEnd()
        $body = $bodyText | ConvertFrom-Json
        
        Write-Host "Auto-captured exception response details:"
        Write-Host " - ErrorCode: $($body.errorCode)"
        Write-Host " - Generated Exception Code: $($body.code)"
        Write-Host " - Message: $($body.message)"
        
        if ($body.errorCode -eq "INVALID_LOCATION_NOT_FOUND" -and $body.code -like "EX-*") {
            Write-Host "`n>>> MIDDLEWARE AUTO-CAPTURE VERIFICATION PASSED! <<<"
            Write-Host "`n>>> ALL EXCEPTION FRAMEWORK MVP TESTS PASSED SUCCESSFULLY 100%! <<<"
        } else {
            Write-Error "Response body verification failed. Body: $bodyText"
            exit 1
        }
    } else {
        Write-Error "Expected 400 BadRequest but got: $_"
        exit 1
    }
}
