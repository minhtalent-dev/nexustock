# Script kiểm tra bảo mật phân quyền API Observability
$ErrorActionPreference = "Continue" # Cho phép catch lỗi HTTP

$baseUrl = "http://localhost:5024"

Write-Host ">>> Bắt đầu verify Security & Permissions..." -ForegroundColor Cyan

# 1. Kiểm tra truy cập KHÔNG có token JWT
Write-Host "1. Thử gọi API không có token..."
try {
    $res = Invoke-RestMethod -Uri "$baseUrl/api/observability/summary" -Method Get -Headers @{}
    Write-Error "LỖI: Truy cập thành công mà không cần token!"
    exit 1
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    if ($statusCode -eq 401 -or $statusCode -eq 403) {
        Write-Host "Đúng chuẩn: Trả về lỗi $statusCode khi không có token." -ForegroundColor Green
    } else {
        Write-Error "LỖI: Trả về HTTP Code không mong muốn: $statusCode"
        exit 1
    }
}

# 2. Kiểm tra truy cập với token SAI/HẾT HẠN
Write-Host "2. Thử gọi API với token sai..."
try {
    $headers = @{
        Authorization = "Bearer sai_token_jwt_random_123"
    }
    $res = Invoke-RestMethod -Uri "$baseUrl/api/observability/summary" -Method Get -Headers $headers
    Write-Error "LỖI: Truy cập thành công với token sai!"
    exit 1
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    if ($statusCode -eq 401 -or $statusCode -eq 403) {
        Write-Host "Đúng chuẩn: Trả về lỗi $statusCode khi token không hợp lệ." -ForegroundColor Green
    } else {
        Write-Error "LỖI: Trả về HTTP Code không mong muốn: $statusCode"
        exit 1
    }
}

# 3. Kiểm tra truy cập với token HỢP LỆ
Write-Host "3. Đăng nhập để lấy token hợp lệ..."
$loginBody = @{
    email = "admin@nexustock.com"
    password = "AdminSecret123!"
} | ConvertTo-Json

try {
    $loginRes = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
    $token = $loginRes.token
    $headers = @{
        Authorization = "Bearer $token"
    }

    $res = Invoke-RestMethod -Uri "$baseUrl/api/observability/summary" -Method Get -Headers $headers
    Write-Host "Đúng chuẩn: Truy cập thành công với token admin hợp lệ." -ForegroundColor Green
} catch {
    Write-Error "LỖI: Token admin hợp lệ bị chặn! Chi tiết: $_"
    exit 1
}

Write-Host ">>> VERIFY SECURITY & PERMISSIONS THÀNH CÔNG!" -ForegroundColor Green
exit 0
