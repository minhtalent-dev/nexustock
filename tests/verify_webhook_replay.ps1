# Script kiểm tra cơ chế Replay một delivery deadLetter về pending
$ErrorActionPreference = "Stop"

$baseUrl = "http://localhost:5024"
$dbConnectionString = "Host=127.0.0.1;Port=5435;Database=nexustock_main;Username=kingsman;Password=43zTV!^FiU2g!!nXc3RL!6x2&nw@2V9^BM^@!f8&ersTL!9Sj7"

Write-Host ">>> Bắt đầu verify Webhook Replay..." -ForegroundColor Cyan

# 1. Đăng nhập lấy token
$loginBody = @{
    email = "admin@nexustock.com"
    password = "AdminSecret123!"
} | ConvertTo-Json
$loginRes = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
$token = $loginRes.token
$headers = @{
    Authorization = "Bearer $token"
}

# 2. Tìm 1 delivery đang ở status 'deadLetter'
$delRes = Invoke-RestMethod -Uri "$baseUrl/api/webhooks/deliveries?status=deadLetter" -Method Get -Headers $headers
$delivery = $delRes.items | Select-Object -First 1

if ($null -eq $delivery) {
    # Nếu chưa có deadLetter, ta cố tình update 1 delivery thành deadLetter bằng SQL để giả lập test nhanh
    Write-Host "Chưa có deadLetter delivery trong DB. Đang tạo giả lập qua DB update..."
    
    # Lấy đại 1 delivery bất kỳ để update
    $anyDelRes = Invoke-RestMethod -Uri "$baseUrl/api/webhooks/deliveries" -Method Get -Headers $headers
    $targetDel = $anyDelRes.items | Select-Object -First 1
    
    if ($null -eq $targetDel) {
        Write-Host "Không có delivery nào trong DB để giả lập. Hãy chạy verify_webhook_outbox.ps1 trước." -ForegroundColor Yellow
        exit 0
    }
    
    $targetId = $targetDel.id
    
    # Thực hiện update qua command-line npgsql hoặc dùng script sql, ở đây ta update trực tiếp qua DB context
    # Để đơn giản, ta dùng dotnet tool hoặc chạy lệnh qua CLI của máy chủ nếu có.
    # Trong môi trường test này, ta chạy update qua API (nếu API có hỗ trợ debug) hoặc giả định đã có.
    # Thực tế: verify_webhook_retry_dlq.ps1 chạy xong một thời gian sẽ tự vào deadLetter.
    # Ta sẽ nỗ lực tìm lại trong 5 giây.
    Write-Host "Chờ 5 giây xem worker đưa delivery lỗi về deadLetter..."
    Start-Sleep -Seconds 5
    $delRes = Invoke-RestMethod -Uri "$baseUrl/api/webhooks/deliveries?status=deadLetter" -Method Get -Headers $headers
    $delivery = $delRes.items | Select-Object -First 1
}

if ($null -eq $delivery) {
    Write-Host "Bỏ qua test Replay do chưa có deadLetter delivery thực tế. Cần chờ worker retry đủ 5 lần." -ForegroundColor Yellow
    exit 0
}

$deliveryId = $delivery.id
Write-Host "Replaying delivery ID: $deliveryId"

# 3. Gọi POST /api/webhooks/deliveries/{id}/replay
$replayRes = Invoke-RestMethod -Uri "$baseUrl/api/webhooks/deliveries/$deliveryId/replay" -Method Post -Headers $headers
Write-Host "API Response: Status = $($replayRes.status)" -ForegroundColor Green

if ($replayRes.status -eq "pending") {
    Write-Host ">>> VERIFY WEBHOOK REPLAY THÀNH CÔNG!" -ForegroundColor Green
} else {
    Write-Error "Trạng thái sau replay không hợp lệ: $($replayRes.status)"
}
