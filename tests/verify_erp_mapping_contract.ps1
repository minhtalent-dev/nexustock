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
$whs = Invoke-RestMethod -Uri "$API_URL/master-data/warehouses" -Method Get -Headers $headers
$pts = Invoke-RestMethod -Uri "$API_URL/master-data/partners" -Method Get -Headers $headers
$pds = Invoke-RestMethod -Uri "$API_URL/master-data/products" -Method Get -Headers $headers
$uoms = Invoke-RestMethod -Uri "$API_URL/master-data/uoms" -Method Get -Headers $headers

$whCode = $whs.items[0].code
$ptCode = $pts.items[0].code
$pdCode = $pds.items[0].code
$uomCode = $uoms.items[0].code

# 3. Test unresolved item code (MATNR not mapped)
Write-Host "`n2. Sending PO with unresolved MATNR (Expected: 422 Unprocessable)..." -ForegroundColor Cyan
$idempotencyKey = "idem_po_err_" + (Get-Random -Minimum 100000 -Maximum 999999)
$poSuffix = (Get-Random -Minimum 100000 -Maximum 999999)
$badItemPoBody = @{
    integrationHeader = @{
        externalSystem = "SAP-ERP"
        externalReference = "PO-2026-ERR-ITEM-$poSuffix"
        contractVersion = "v1.1"
        idempotencyKey = $idempotencyKey
        timestamp = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ")
    }
    inboundOrder = @{
        tenantId = "00000000-0000-0000-0000-000000000001"
        WERKS = "SAP-WH-01"
        EBELN = "SAP-PO-NO-ERR-ITEM-$poSuffix"
        LIFNR = "SAP-SUP-01"
        orderDate = "2026-07-18"
        expectedArrivalDate = "2026-07-20"
        items = @(
            @{ EBELP = 10; MATNR = "UNRESOLVED-MAT-CODE"; expectedQty = 10.0; MEINS = "SAP-UOM-01" }
        )
    }
} | ConvertTo-Json -Depth 5

$reqHeaders = @{
    Authorization = "Bearer $token"
    "Idempotency-Key" = $idempotencyKey
}

try {
    Send-Request -Uri "$API_URL/integration/inbound-orders" -Method Post -Body $badItemPoBody -ContentType $JSON_CONTENT_TYPE -Headers $reqHeaders
    Write-Error "Error: PO with unresolved MATNR was accepted."
    exit 1
} catch {
    $err = Read-ErrorResponse $_
    if ($err.StatusCode -eq 422 -and $err.Body.errorCode -eq "mapping.unresolvedItemCode") {
        Write-Host "Unresolved MATNR check passed: returned 422 mapping.unresolvedItemCode." -ForegroundColor Green
    } else {
        Write-Error "Unresolved MATNR check failed: returned status $($err.StatusCode) ($($err.Raw))"
        exit 1
    }
}

# 4. Test unresolved partner code (LIFNR not mapped)
Write-Host "`n3. Sending PO with unresolved LIFNR (Expected: 422 Unprocessable)..." -ForegroundColor Cyan
$idempotencyKey = "idem_po_err_" + (Get-Random -Minimum 100000 -Maximum 999999)
$poSuffix2 = (Get-Random -Minimum 100000 -Maximum 999999)
$badPartnerPoBody = @{
    integrationHeader = @{
        externalSystem = "SAP-ERP"
        externalReference = "PO-2026-ERR-PT-$poSuffix2"
        contractVersion = "v1.1"
        idempotencyKey = $idempotencyKey
        timestamp = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ")
    }
    inboundOrder = @{
        tenantId = "00000000-0000-0000-0000-000000000001"
        WERKS = "SAP-WH-01"
        EBELN = "SAP-PO-NO-ERR-PT-$poSuffix2"
        LIFNR = "UNRESOLVED-LIFNR"
        orderDate = "2026-07-18"
        expectedArrivalDate = "2026-07-20"
        items = @(
            @{ EBELP = 10; MATNR = "SAP-MAT-01"; expectedQty = 10.0; MEINS = "SAP-UOM-01" }
        )
    }
} | ConvertTo-Json -Depth 5

$reqHeaders.Set_Item("Idempotency-Key", $idempotencyKey)

try {
    Send-Request -Uri "$API_URL/integration/inbound-orders" -Method Post -Body $badPartnerPoBody -ContentType $JSON_CONTENT_TYPE -Headers $reqHeaders
    Write-Error "Error: PO with unresolved LIFNR was accepted."
    exit 1
} catch {
    $err = Read-ErrorResponse $_
    if ($err.StatusCode -eq 422 -and $err.Body.errorCode -eq "mapping.unresolvedPartner") {
        Write-Host "Unresolved LIFNR check passed: returned 422 mapping.unresolvedPartner." -ForegroundColor Green
    } else {
        Write-Error "Unresolved LIFNR check failed: returned status $($err.StatusCode) ($($err.Raw))"
        exit 1
    }
}

Write-Host "`nERP MAPPING CONTRACT VERIFICATION PASSED!" -ForegroundColor Green
exit 0
