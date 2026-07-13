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

# 2. Seed test data using direct API adjustment
Write-Host "`n2. Seeding inventory balance for testing..."
$adjKey = [guid]::NewGuid().ToString()
$adjBody = @{
    itemId = "f8e8f296-f0ab-4fac-adae-7ecdfe5b268e"
    lotNo = "LOT-PUT-E2E-001"
    locationId = "00000000-0000-0000-0000-000000000045" # LOC-STG-01
    qty = 100
    reasonCode = "TEST_SEED"
    idempotencyKey = $adjKey
} | ConvertTo-Json

try {
    $adjRes = Invoke-RestMethod -Uri "$API_URL/inventory/adjust" -Method Post -Body $adjBody -ContentType "application/json" -Headers $headers
    Write-Host "Inventory adjusted: $($adjRes.message)"
} catch {
    Write-Error "Failed to adjust inventory: $_"
    exit 1
}

# 3. Request Putaway Proposals
Write-Host "`n3. Requesting Putaway Proposals for Lot ID a1b2c3d4-1234-4567-89ab-cdef01234567..."
try {
    $propRes = Invoke-RestMethod -Uri "$API_URL/putaway/proposals?lotId=a1b2c3d4-1234-4567-89ab-cdef01234567&qty=100" -Method Get -Headers $headers
    Write-Host "Proposals received successfully."
    Write-Host "Lot Number: $($propRes.lotNo)"
    Write-Host "Total candidates: $($propRes.proposals.Count)"
    
    if ($propRes.proposals.Count -eq 0) {
        Write-Error "No putaway candidates returned!"
        exit 1
    }
    
    # Show first candidate
    $best = $propRes.proposals[0]
    Write-Host "Best candidate: $($best.locationCode) | Zone: $($best.zoneCode) | Score: $($best.score) | Reason: $($best.reason)"
    
    # Verify zone layout is returned
    Write-Host "Zone locations grid count: $($propRes.zoneLocations.Count)"
    if ($propRes.zoneLocations.Count -eq 0) {
        Write-Error "Zone layout coordinate array is empty!"
        exit 1
    }
} catch {
    Write-Error "Failed to request putaway proposals: $_"
    exit 1
}

# 4. Confirm Putaway
Write-Host "`n4. Confirming Putaway to candidate: $($best.locationCode)..."
$confirmBody = @{
    proposalId = $best.proposalId
    lotId = "a1b2c3d4-1234-4567-89ab-cdef01234567"
    fromLocationId = "00000000-0000-0000-0000-000000000045" # LOC-STG-01
    selectedLocationId = $best.locationId
    qty = 100
} | ConvertTo-Json

try {
    $confirmRes = Invoke-RestMethod -Uri "$API_URL/putaway/confirm" -Method Post -Body $confirmBody -ContentType "application/json" -Headers $headers
    Write-Host "Confirm result: $($confirmRes.message)"
    
    if ($confirmRes.success -ne $true) {
        Write-Error "Confirmation success was not true!"
        exit 1
    }
} catch {
    Write-Error "Failed to confirm putaway: $_"
    exit 1
}

# 5. Check Idempotency Guard (Double confirmation should return success instantly)
Write-Host "`n5. Re-sending identical confirm request for double-submit check..."
try {
    $doubleRes = Invoke-RestMethod -Uri "$API_URL/putaway/confirm" -Method Post -Body $confirmBody -ContentType "application/json" -Headers $headers
    Write-Host "Double-submit result: $($doubleRes.message)"
    if ($doubleRes.message -notmatch "idempotent") {
        Write-Error "Idempotency check failed! Response should indicate idempotent success."
        exit 1
    }
    Write-Host "Idempotency Guard: PASSED"
} catch {
    Write-Error "Double-submit test failed: $_"
    exit 1
}

# 6. Reject Proposal testing
# Pick the next proposal if available, and reject it
if ($propRes.proposals.Count -gt 1) {
    $second = $propRes.proposals[1]
    Write-Host "`n6. Testing reject proposal on candidate: $($second.locationCode)..."
    $rejectBody = @{
        proposalId = $second.proposalId
        reasonCode = "LOC_FULL"
        note = "E2E testing reject proposal"
    } | ConvertTo-Json
    
    try {
        $rejectRes = Invoke-RestMethod -Uri "$API_URL/putaway/reject" -Method Post -Body $rejectBody -ContentType "application/json" -Headers $headers
        Write-Host "Reject result: $($rejectRes.message)"
        if ($rejectRes.success -ne $true) {
            Write-Error "Rejection was not successful!"
            exit 1
        }
        Write-Host "Rejection test: PASSED"
    } catch {
        Write-Error "Failed to reject proposal: $_"
        exit 1
    }
} else {
    Write-Host "`n6. Skipping reject proposal test (only 1 candidate proposal exists)."
}

Write-Host "`n>>> ALL PUTAWAY SLOTTING PHASE 12 E2E TESTS PASSED SUCCESSFULLY 100%! <<<"
