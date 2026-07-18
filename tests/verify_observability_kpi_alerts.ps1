# Script kiểm tra tích hợp KPI và Alerts
$ErrorActionPreference = "Stop"

$baseUrl = "http://localhost:5024"

Write-Host ">>> Bắt đầu verify KPI & Alerts..." -ForegroundColor Cyan

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

# 2. Kiểm tra KPI Summary API
Write-Host "Đang gọi API summary KPI..."
$summary = Invoke-RestMethod -Uri "$baseUrl/api/observability/summary" -Method Get -Headers $headers

if ($null -eq $summary -or $null -eq $summary.cards) {
    Write-Error "Không lấy được danh sách KPI cards."
    exit 1
}

Write-Host "Danh sách KPI cards nhận được:"
foreach ($card in $summary.cards) {
    Write-Host "- Key: $($card.metricKey), Value: $($card.value), Unit: $($card.unit), Trend: $($card.trend)"
}

# Đảm bảo có ít nhất 1 card KPI liên quan đến webhook
$webhookCard = $summary.cards | Where-Object { $_.metricKey -eq "webhook.deliverySuccessRate" }
if ($null -eq $webhookCard) {
    Write-Error "Thiếu card KPI 'webhook.deliverySuccessRate' trong summary response."
    exit 1
}

# 3. Tạo mock alert
$alertBody = @{
    alertType = "test.evaluator"
    severity = "warning"
    title = "Test Mock Alert"
    message = "Test mock alert message for verify script"
} | ConvertTo-Json

Write-Host "Đang tạo mock alert..."
$alertRes = Invoke-RestMethod -Uri "$baseUrl/api/observability/alerts/test-alert" -Method Post -Body $alertBody -ContentType "application/json" -Headers $headers
$alertId = $alertRes.alertId

if ($null -eq $alertId) {
    Write-Error "Tạo mock alert thất bại."
    exit 1
}
Write-Host "Mock Alert ID: $alertId" -ForegroundColor Green

# 4. Xác nhận alert (Ack)
$ackBody = @{
    note = "Investigating"
} | ConvertTo-Json

Write-Host "Đang xác nhận alert (Ack)..."
$ackRes = Invoke-RestMethod -Uri "$baseUrl/api/observability/alerts/$alertId/ack" -Method Post -Body $ackBody -ContentType "application/json" -Headers $headers

if ($ackRes.status -ne "acknowledged") {
    Write-Error "Cập nhật status sang acknowledged thất bại."
    exit 1
}
Write-Host "Alert Ack thành công!" -ForegroundColor Green

# 5. Giải quyết alert (Resolve)
$resolveBody = @{
    note = "Resolved network connection"
} | ConvertTo-Json

Write-Host "Đang giải quyết alert (Resolve)..."
$resRes = Invoke-RestMethod -Uri "$baseUrl/api/observability/alerts/$alertId/resolve" -Method Post -Body $resolveBody -ContentType "application/json" -Headers $headers

if ($resRes.status -ne "resolved") {
    Write-Error "Cập nhật status sang resolved thất bại."
    exit 1
}
Write-Host "Alert Resolve thành công!" -ForegroundColor Green

Write-Host ">>> VERIFY KPI & ALERTS THÀNH CÔNG!" -ForegroundColor Green
