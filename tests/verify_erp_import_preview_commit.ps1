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

# 2. Retrieve actual Product code
$pds = Invoke-RestMethod -Uri "$API_URL/master-data/products" -Method Get -Headers $headers
$pdCode = $pds.items[0].code

# 3. Create invalid CSV file
Write-Host "`n2. Preparing invalid CSV and testing import preview (Expected: failed_validation, errorRows > 0)..." -ForegroundColor Cyan
$invalidCsv = "mappingType,externalCode,internalCode,status`n" +
              "item,SAP-MAT-INVALID,NON_EXISTENT_WMS_CODE_999,active`n" +
              "invalidType,SAP-MAT-01,$pdCode,active"

$tempInvalidFile = [System.IO.Path]::GetTempFileName() + ".csv"
[System.IO.File]::WriteAllText($tempInvalidFile, $invalidCsv)

try {
    # Perform multipart upload using curl/Invoke-RestMethod helper
    # We will use Invoke-WebRequest for simplicity with PowerShell 7+
    $boundary = [System.Guid]::NewGuid().ToString()
    $LF = "`r`n"
    $bodyLines = (
        "--$boundary",
        "Content-Disposition: form-data; name=`"file`"; filename=`"invalid.csv`"",
        "Content-Type: text/csv",
        "",
        $invalidCsv,
        "--$boundary--"
    ) -join $LF

    $previewRes = Invoke-RestMethod -Uri "$API_URL/integration/import/preview?externalSystem=SAP-ERP" `
                                    -Method Post `
                                    -Body $bodyLines `
                                    -ContentType "multipart/form-data; boundary=$boundary" `
                                    -Headers $headers

    $jobId = $previewRes.jobId
    Write-Host "Preview returned Job ID: $jobId, Total: $($previewRes.totalRows), Errors: $($previewRes.errorRows)" -ForegroundColor Green

    if ($previewRes.status -eq "failed_validation" -and $previewRes.errorRows -eq 2) {
        Write-Host "Invalid preview validation passed." -ForegroundColor Green
    } else {
        Write-Error "Invalid preview validation failed. Status: $($previewRes.status), Errors: $($previewRes.errorRows)"
        exit 1
    }
} finally {
    if (Test-Path $tempInvalidFile) { Remove-Item $tempInvalidFile }
}

# 4. Try to commit the invalid Job ID (Expected: 400 Bad Request)
Write-Host "`n3. Trying to commit invalid Job (Expected: 400 Bad Request)..." -ForegroundColor Cyan
try {
    Send-Request -Uri "$API_URL/integration/import/commit/$jobId" -Method Post -Headers $headers
    Write-Error "Error: Invalid job was committed successfully."
    exit 1
} catch {
    $err = Read-ErrorResponse $_
    if ($err.StatusCode -eq 400) {
        Write-Host "Commit rejection check passed: returned 400 Bad Request." -ForegroundColor Green
    } else {
        Write-Error "Commit rejection check failed: returned status $($err.StatusCode)"
        exit 1
    }
}

# 5. Create valid CSV file
Write-Host "`n4. Preparing valid CSV and testing import preview (Expected: previewed, errorRows = 0)..." -ForegroundColor Cyan
$importExtCode = "SAP-MAT-IMP-" + (Get-Random -Minimum 100000 -Maximum 999999)
$validCsv = "mappingType,externalCode,internalCode,status`n" +
            "item,$importExtCode,$pdCode,active"

$tempValidFile = [System.IO.Path]::GetTempFileName() + ".csv"
[System.IO.File]::WriteAllText($tempValidFile, $validCsv)

try {
    $bodyLines2 = (
        "--$boundary",
        "Content-Disposition: form-data; name=`"file`"; filename=`"valid.csv`"",
        "Content-Type: text/csv",
        "",
        $validCsv,
        "--$boundary--"
    ) -join $LF

    $previewRes2 = Invoke-RestMethod -Uri "$API_URL/integration/import/preview?externalSystem=SAP-ERP" `
                                     -Method Post `
                                     -Body $bodyLines2 `
                                     -ContentType "multipart/form-data; boundary=$boundary" `
                                     -Headers $headers

    $jobId2 = $previewRes2.jobId
    Write-Host "Preview returned Job ID: $jobId2, Total: $($previewRes2.totalRows), Errors: $($previewRes2.errorRows)" -ForegroundColor Green

    if ($previewRes2.status -eq "previewed" -and $previewRes2.errorRows -eq 0) {
        Write-Host "Valid preview validation passed." -ForegroundColor Green
    } else {
        Write-Error "Valid preview validation failed. Status: $($previewRes2.status), Errors: $($previewRes2.errorRows)"
        exit 1
    }
} finally {
    if (Test-Path $tempValidFile) { Remove-Item $tempValidFile }
}

# 6. Commit valid Job ID (Expected: 200 OK)
Write-Host "`n5. Committing valid Job ID $jobId2 (Expected: 200 OK)..." -ForegroundColor Cyan
try {
    $commitRes = Invoke-RestMethod -Uri "$API_URL/integration/import/commit/$jobId2" -Method Post -Headers $headers
    if ($commitRes.status -eq "committed") {
        Write-Host "Commit succeeded." -ForegroundColor Green
    } else {
        Write-Error "Commit failed: $($commitRes.message)"
        exit 1
    }
} catch {
    $err = Read-ErrorResponse $_
    Write-Error "Commit failed: $($err.Raw)"
    exit 1
}

# 7. Verify mapping created in DB
Write-Host "`n6. Querying mapping API to verify import exists..." -ForegroundColor Cyan
try {
    $searchRes = Invoke-RestMethod -Uri "$API_URL/integration/mappings?mappingType=item&externalSystem=SAP-ERP&externalCode=$importExtCode" -Method Get -Headers $headers
    if ($searchRes.items.Count -gt 0 -and $searchRes.items[0].internalCode -eq $pdCode) {
        Write-Host "Verified import exists in DB: $importExtCode -> $pdCode" -ForegroundColor Green
    } else {
        Write-Error "Could not find imported mapping in DB."
        exit 1
    }
} catch {
    Write-Error "Failed to query mappings: $_"
    exit 1
}

Write-Host "`nERP IMPORT PREVIEW & COMMIT VERIFICATION PASSED!" -ForegroundColor Green
exit 0
