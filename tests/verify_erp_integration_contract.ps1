$API_URL = if ($env:NEXUSTOCK_API_URL) { $env:NEXUSTOCK_API_URL } else { "http://localhost:5024/api" }
$JSON_CONTENT_TYPE = "application/json; charset=utf-8"

$ErrorActionPreference = "Stop"

function Read-ErrorResponse($errorRecord) {
    $response = $errorRecord.Exception.Response
    if ($null -eq $response) {
        return [pscustomobject]@{ StatusCode = $null; Body = $null; Raw = $errorRecord.ToString() }
    }
    $stream = $response.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($stream)
    $raw = $reader.ReadToEnd()
    $body = $null
    if (-not [string]::IsNullOrWhiteSpace($raw)) {
        try { $body = $raw | ConvertFrom-Json } catch { $body = $raw }
    }
    [pscustomobject]@{ StatusCode = $response.StatusCode; Body = $body; Raw = $raw }
}

function Send-Request($Uri, $Method, $Body, $ContentType, $Headers) {
    $params = @{
        Uri = $Uri
        Method = $Method
        Headers = $Headers
        UseBasicParsing = $true
    }
    if ($null -ne $Body) { $params.Add("Body", $Body) }
    if ($null -ne $ContentType) { $params.Add("ContentType", $ContentType) }
    return Invoke-WebRequest @params
}

# 1. Login Admin
Write-Host "1. Logging in as admin..." -ForegroundColor Cyan
$adminEmail = "admin@nexustock.com"
$adminPassword = "AdminSecret123!"
$loginBody = @{ email = $adminEmail; password = $adminPassword } | ConvertTo-Json

try {
    $loginRes = Invoke-RestMethod -Uri "$API_URL/auth/login" -Method Post -Body $loginBody -ContentType $JSON_CONTENT_TYPE
    $token = $loginRes.token
    $headers = @{ Authorization = "Bearer $token" }
    Write-Host "Login successful." -ForegroundColor Green
} catch {
    $err = Read-ErrorResponse $_
    Write-Error "Login failed: $($err.Raw)"
    exit 1
}

# 2. Retrieve actual MasterData codes
Write-Host "`n2. Retrieving MasterData entities to establish aliases..." -ForegroundColor Cyan
try {
    $whs = Invoke-RestMethod -Uri "$API_URL/master-data/warehouses" -Method Get -Headers $headers
    $pts = Invoke-RestMethod -Uri "$API_URL/master-data/partners" -Method Get -Headers $headers
    $pds = Invoke-RestMethod -Uri "$API_URL/master-data/products" -Method Get -Headers $headers
    $uoms = Invoke-RestMethod -Uri "$API_URL/master-data/uoms" -Method Get -Headers $headers

    $whCode = $whs.items[0].code
    $ptCode = $pts.items[0].code
    $pdCode = $pds.items[0].code
    $uomCode = $uoms.items[0].code

    Write-Host "Resolved MasterData codes: Wh=$whCode, Partner=$ptCode, Product=$pdCode, UOM=$uomCode" -ForegroundColor Green
} catch {
    Write-Error "Failed to retrieve master data: $_"
    exit 1
}

# 3. Create mapping aliases
Write-Host "`n3. Seeding mapping aliases via integration API..." -ForegroundColor Cyan
$mappingsToSeed = @(
    @{ mappingType = "warehouse"; externalCode = "SAP-WH-01"; internalCode = $whCode },
    @{ mappingType = "partner"; externalCode = "SAP-SUP-01"; internalCode = $ptCode },
    @{ mappingType = "item"; externalCode = "SAP-MAT-01"; internalCode = $pdCode },
    @{ mappingType = "uom"; externalCode = "SAP-UOM-01"; internalCode = $uomCode }
)

foreach ($map in $mappingsToSeed) {
    $body = @{
        externalSystem = "SAP-ERP"
        mappingType = $map.mappingType
        externalCode = $map.externalCode
        internalCode = $map.internalCode
    } | ConvertTo-Json

    try {
        # Delete old mapping if exists
        $existing = Invoke-RestMethod -Uri "$API_URL/integration/mappings?mappingType=$($map.mappingType)&externalSystem=SAP-ERP&externalCode=$($map.externalCode)" -Method Get -Headers $headers
        if ($existing.items.Count -gt 0) {
            $mapId = $existing.items[0].id
            Invoke-RestMethod -Uri "$API_URL/integration/mappings/$mapId" -Method Delete -Headers $headers
        }

        $res = Invoke-RestMethod -Uri "$API_URL/integration/mappings" -Method Post -Body $body -ContentType $JSON_CONTENT_TYPE -Headers $headers
        Write-Host "Created mapping alias for $($map.mappingType): $($map.externalCode) -> $($map.internalCode)" -ForegroundColor Green
    } catch {
        Write-Error "Failed to seed mapping alias: $_"
        exit 1
    }
}

# 4. Call Integration Inbound API (1st call: Success)
Write-Host "`n4. Sending first mock SAP PO payload (Expected: 201 Created)..." -ForegroundColor Cyan
$idempotencyKey = "idem_po_" + (Get-Random -Minimum 100000 -Maximum 999999)
$poSuffix = (Get-Random -Minimum 100000 -Maximum 999999)
$poNo = "SAP-PO-NO-T-$poSuffix"
$poRef = "PO-2026-T-$poSuffix"

$poBody = @{
    integrationHeader = @{
        externalSystem = "SAP-ERP"
        externalReference = $poRef
        contractVersion = "v1.1"
        idempotencyKey = $idempotencyKey
        timestamp = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ")
    }
    inboundOrder = @{
        tenantId = "00000000-0000-0000-0000-000000000001"
        WERKS = "SAP-WH-01"
        EBELN = $poNo
        LIFNR = "SAP-SUP-01"
        orderDate = "2026-07-18"
        expectedArrivalDate = "2026-07-20"
        items = @(
            @{ EBELP = 10; MATNR = "SAP-MAT-01"; expectedQty = 150.0; MEINS = "SAP-UOM-01" }
        )
    }
} | ConvertTo-Json -Depth 5

$reqHeaders = @{
    Authorization = "Bearer $token"
    "Idempotency-Key" = $idempotencyKey
    "X-Contract-Version" = "v1.1"
}

try {
    $res = Send-Request -Uri "$API_URL/integration/inbound-orders" -Method Post -Body $poBody -ContentType $JSON_CONTENT_TYPE -Headers $reqHeaders
    if ($res.StatusCode -eq 201) {
        Write-Host "First call returned 201 Created." -ForegroundColor Green
    } else {
        Write-Error "First call returned status code $($res.StatusCode)"
        exit 1
    }
} catch {
    $err = Read-ErrorResponse $_
    Write-Error "First call failed: $($err.Raw)"
    exit 1
}

# 5. Call same key/same payload (Expected: 200 Replay)
Write-Host "`n5. Replaying same payload with same Idempotency-Key (Expected: 200 OK)..." -ForegroundColor Cyan
try {
    $res2 = Send-Request -Uri "$API_URL/integration/inbound-orders" -Method Post -Body $poBody -ContentType $JSON_CONTENT_TYPE -Headers $reqHeaders
    if ($res2.StatusCode -eq 200) {
        Write-Host "Replay call returned 200 OK." -ForegroundColor Green
    } else {
        Write-Error "Replay call returned status code $($res2.StatusCode)"
        exit 1
    }
} catch {
    $err = Read-ErrorResponse $_
    Write-Error "Replay call failed: $($err.Raw)"
    exit 1
}

# 6. Call same key/different payload (Expected: 409 Conflict)
Write-Host "`n6. Sending different payload with same Idempotency-Key (Expected: 409 Conflict)..." -ForegroundColor Cyan
$differentPoBody = @{
    integrationHeader = @{
        externalSystem = "SAP-ERP"
        externalReference = $poRef
        contractVersion = "v1.1"
        idempotencyKey = $idempotencyKey
        timestamp = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ")
    }
    inboundOrder = @{
        tenantId = "00000000-0000-0000-0000-000000000001"
        WERKS = "SAP-WH-01"
        EBELN = $poNo
        LIFNR = "SAP-SUP-01"
        orderDate = "2026-07-18"
        expectedArrivalDate = "2026-07-20"
        items = @(
            @{ EBELP = 10; MATNR = "SAP-MAT-01"; expectedQty = 999.0; MEINS = "SAP-UOM-01" }  # Different Qty!
        )
    }
} | ConvertTo-Json -Depth 5

try {
    Send-Request -Uri "$API_URL/integration/inbound-orders" -Method Post -Body $differentPoBody -ContentType $JSON_CONTENT_TYPE -Headers $reqHeaders
    Write-Error "Error: Same key with different payload was accepted."
    exit 1
} catch {
    $err = Read-ErrorResponse $_
    if ($err.StatusCode -eq 409) {
        Write-Host "Conflict check passed: returned 409 Conflict." -ForegroundColor Green
    } else {
        Write-Error "Conflict check failed: returned status $($err.StatusCode)"
        exit 1
    }
}

# 7. Call missing key (Expected: 400 Bad Request)
Write-Host "`n7. Sending request without Idempotency-Key header (Expected: 400 Bad Request)..." -ForegroundColor Cyan
$noIdemHeaders = @{
    Authorization = "Bearer $token"
    "X-Contract-Version" = "v1.1"
}
try {
    Send-Request -Uri "$API_URL/integration/inbound-orders" -Method Post -Body $poBody -ContentType $JSON_CONTENT_TYPE -Headers $noIdemHeaders
    Write-Error "Error: Request without Idempotency-Key was accepted."
    exit 1
} catch {
    $err = Read-ErrorResponse $_
    if ($err.StatusCode -eq 400) {
        Write-Host "Missing key check passed: returned 400 Bad Request." -ForegroundColor Green
    } else {
        Write-Error "Missing key check failed: returned status $($err.StatusCode)"
        exit 1
    }
}

Write-Host "`nERP INTEGRATION CONTRACT VERIFICATION PASSED!" -ForegroundColor Green
exit 0
