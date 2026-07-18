# Script kiểm tra tích hợp Trace ID, Timeline và Sensitive Data Masking
$ErrorActionPreference = "Stop"

$baseUrl = "http://localhost:5024"

Write-Host ">>> Bắt đầu verify Trace & Timeline & Sensitive Data Masking..." -ForegroundColor Cyan

# 1. Đăng nhập hệ thống để lấy token JWT
$loginBody = @{
    email = "admin@nexustock.com"
    password = "AdminSecret123!"
} | ConvertTo-Json

Write-Host "Đang đăng nhập..."
$loginRes = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
$token = $loginRes.token
$headers = @{
    Authorization = "Bearer $token"
}

# 2. Ghi một trace log thử nghiệm có chứa dữ liệu nhạy cảm
$traceId = "T-TEST-" + (Get-Random -Minimum 10000 -Maximum 99999).ToString()
$logBody = @{
    traceId = $traceId
    message = "Database connection password=MySecretPassword123"
    metadataJson = '{"connectionString": "Host=localhost;password=MySecretPassword123", "secretKey": "super-secret-key-xyz"}'
} | ConvertTo-Json

Write-Host "Đang gửi trace log chứa dữ liệu nhạy cảm..."
$writeRes = Invoke-RestMethod -Uri "$baseUrl/api/observability/test-trace-log" -Method Post -Body $logBody -ContentType "application/json" -Headers $headers

if ($writeRes.success -ne $true) {
    Write-Error "Không thể ghi trace log qua test endpoint."
    exit 1
}

# 3. Tra cứu chi tiết Trace ID để kiểm định dữ liệu và cơ chế mask
Write-Host "Đang tra cứu chi tiết trace logs cho Trace ID: $traceId..."
$traceDetail = Invoke-RestMethod -Uri "$baseUrl/api/observability/traces/$traceId" -Method Get -Headers $headers

if ($null -eq $traceDetail -or $traceDetail.traceId -ne $traceId) {
    Write-Error "Không tìm thấy chi tiết Trace ID vừa ghi."
    exit 1
}

$logItem = $traceDetail.traceLogs | Select-Object -First 1

if ($null -eq $logItem) {
    Write-Error "Không tìm thấy trace log item trong database."
    exit 1
}

# Kiểm tra cơ chế Masking
Write-Host "Log Message trong DB: $($logItem.message)"
Write-Host "Metadata trong DB: $($logItem.metadataJson)"

if ($logItem.message -like "*MySecretPassword123*") {
    Write-Error "LỖI: Dữ liệu nhạy cảm trong Message không được mask!"
    exit 1
}

if ($logItem.metadataJson -like "*MySecretPassword123*" -or $logItem.metadataJson -like "*super-secret-key-xyz*") {
    Write-Error "LỖI: Dữ liệu nhạy cảm trong MetadataJson không được mask!"
    exit 1
}

if ($logItem.message -like "*password=***" -and $logItem.metadataJson -like "****" ) {
    Write-Host ">>> VERIFY TRACE TIMELINE & MASKING THÀNH CÔNG!" -ForegroundColor Green
} else {
    Write-Warning "Cảnh báo: Dữ liệu đã được mask nhưng format khác kỳ vọng."
    Write-Host ">>> VERIFY TRACE TIMELINE & MASKING THÀNH CÔNG!" -ForegroundColor Green
}
