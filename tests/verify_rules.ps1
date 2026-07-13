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

# 2. Create RuleSet via API
Write-Host "`n2. Creating a new Rule Set (BLOCK Chemical in Normal Zone)..."
$ruleBody = @{
    code = "RULE-PUT-CHEM"
    name = "Luat cat hang hoa chat"
    type = "PUTAWAY"
    priority = 10
    conditions = @(
        @{ field = "productGroup"; operator = "EQUALS"; value = "CHEMICAL" },
        @{ field = "locationZone"; operator = "NOT_EQUALS"; value = "ZONE-HAZARDOUS" }
    )
    action = @{
        actionType = "BLOCK"
        actionParameters = '{"message": "Hoa chat chi duoc cat vao vung ZONE-HAZARDOUS"}'
    }
} | ConvertTo-Json -Depth 4

try {
    $ruleRes = Invoke-RestMethod -Uri "$API_URL/rules" -Method Post -Body $ruleBody -ContentType "application/json" -Headers $headers
    Write-Host "Rule created successfully. Code=$($ruleRes.code), Id=$($ruleRes.id)"
} catch {
    # If already exists from previous runs, we can log it
    Write-Host "Note: Creation may fail if the rule code already exists. Let's check."
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $errBody = $reader.ReadToEnd()
        Write-Host "Server response: $errBody"
        if ($errBody -match "đã tồn tại") {
            Write-Host "Rule already exists. Proceeding to evaluation testing."
        } else {
            Write-Error "Failed to create rule: $_"
            exit 1
        }
    } else {
        Write-Error "Failed to create rule: $_"
        exit 1
    }
}

# 3. Test Evaluation - Case A: CHEMICAL in ZONE-NORMAL (Should BLOCK)
Write-Host "`n3. Testing Evaluation - Case A (CHEMICAL in ZONE-NORMAL)..."
$evalBodyA = @{
    ruleType = "PUTAWAY"
    context = @{
        productGroup = "CHEMICAL"
        locationZone = "ZONE-NORMAL"
    }
} | ConvertTo-Json -Depth 3

try {
    $resA = Invoke-RestMethod -Uri "$API_URL/rules/evaluate" -Method Post -Body $evalBodyA -ContentType "application/json" -Headers $headers
    Write-Host "Result Matched: $($resA.matched)"
    Write-Host "Result Action: $($resA.actionType)"
    Write-Host "Result Parameters: $($resA.actionParameters)"
    Write-Host "Details: $($resA.details)"
    
    if ($resA.matched -ne $true -or $resA.actionType -ne "BLOCK") {
        Write-Error "Evaluation Case A failed! Expected MATCH=true and Action=BLOCK."
        exit 1
    }
    Write-Host "Case A PASSED."
} catch {
    Write-Error "Evaluation Case A failed: $_"
    exit 1
}

# 4. Test Evaluation - Case B: CHEMICAL in ZONE-HAZARDOUS (Should ALLOW)
Write-Host "`n4. Testing Evaluation - Case B (CHEMICAL in ZONE-HAZARDOUS)..."
$evalBodyB = @{
    ruleType = "PUTAWAY"
    context = @{
        productGroup = "CHEMICAL"
        locationZone = "ZONE-HAZARDOUS"
    }
} | ConvertTo-Json -Depth 3

try {
    $resB = Invoke-RestMethod -Uri "$API_URL/rules/evaluate" -Method Post -Body $evalBodyB -ContentType "application/json" -Headers $headers
    Write-Host "Result Matched: $($resB.matched)"
    Write-Host "Result Action: $($resB.actionType)"
    Write-Host "Details: $($resB.details)"
    
    if ($resB.matched -eq $true -or $resB.actionType -ne "ALLOW") {
        Write-Error "Evaluation Case B failed! Expected MATCH=false and Action=ALLOW."
        exit 1
    }
    Write-Host "Case B PASSED."
} catch {
    Write-Error "Evaluation Case B failed: $_"
    exit 1
}

# 5. Test Evaluation - Case C: FOOD in ZONE-NORMAL (Should ALLOW)
Write-Host "`n5. Testing Evaluation - Case C (FOOD in ZONE-NORMAL)..."
$evalBodyC = @{
    ruleType = "PUTAWAY"
    context = @{
        productGroup = "FOOD"
        locationZone = "ZONE-NORMAL"
    }
} | ConvertTo-Json -Depth 3

try {
    $resC = Invoke-RestMethod -Uri "$API_URL/rules/evaluate" -Method Post -Body $evalBodyC -ContentType "application/json" -Headers $headers
    Write-Host "Result Matched: $($resC.matched)"
    Write-Host "Result Action: $($resC.actionType)"
    Write-Host "Details: $($resC.details)"
    
    if ($resC.matched -eq $true -or $resC.actionType -ne "ALLOW") {
        Write-Error "Evaluation Case C failed! Expected MATCH=false and Action=ALLOW."
        exit 1
    }
    Write-Host "Case C PASSED."
} catch {
    Write-Error "Evaluation Case C failed: $_"
    exit 1
}

# 6. Verify Execution Logs
Write-Host "`n6. Verifying execution logs..."
try {
    $logsRes = Invoke-RestMethod -Uri "$API_URL/rules/logs?ruleType=PUTAWAY&page=1&pageSize=10" -Method Get -Headers $headers
    Write-Host "Logs count in database: $($logsRes.totalCount)"
    if ($logsRes.items.Count -lt 3) {
        Write-Error "Logs count should be at least 3 after running the tests."
        exit 1
    }
    
    Write-Host "`nSample execution log:"
    $sampleLog = $logsRes.items[0]
    Write-Host " - Time: $($sampleLog.createdAt)"
    Write-Host " - Input: $($sampleLog.inputContextJson)"
    Write-Host " - Action: $($sampleLog.resultAction)"
    Write-Host " - Details: $($sampleLog.details)"
    
    Write-Host "`n>>> ALL RULE ENGINE FOUNDATION MVP TESTS PASSED SUCCESSFULLY 100%! <<<"
} catch {
    Write-Error "Failed to fetch logs: $_"
    exit 1
}
