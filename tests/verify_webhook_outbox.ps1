# Script kiểm tra tích hợp gửi Webhook Outbox
$ErrorActionPreference = "Stop"

$baseUrl = "http://localhost:5024"
$dbConnectionString = "Host=127.0.0.1;Port=5435;Database=nexustock_main;Username=kingsman;Password=43zTV!^FiU2g!!nXc3RL!6x2&nw@2V9^BM^@!f8&ersTL!9Sj7"

Write-Host ">>> Bắt đầu verify Webhook Outbox..." -ForegroundColor Cyan

# 1. Seed Subscription mẫu thông qua API
# Lấy token JWT trước
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

# Tạo subscription cho event type "inbound.completed"
$subBody = @{
    targetUrl = "https://httpbin.org/post"
    eventTypes = @("inbound.completed")
} | ConvertTo-Json

Write-Host "Tạo subscription mới..."
$subRes = Invoke-RestMethod -Uri "$baseUrl/api/webhooks/subscriptions" -Method Post -Body $subBody -ContentType "application/json" -Headers $headers
$subId = $subRes.subscriptionId
Write-Host "Đã tạo subscription ID: $subId" -ForegroundColor Green

# 2. Gửi Inbound Order để trigger webhook
$idemKey = [Guid]::NewGuid().ToString()
$poHeaders = @{
    "Idempotency-Key" = $idemKey
    "X-Contract-Version" = "v1.1"
}
$headers.Keys | ForEach-Object { $poHeaders.Add($_, $headers[$_]) }

$orderNo = "PO-TEST-" + (Get-Random -Minimum 1000 -Maximum 9999).ToString()
$poBody = @{
    integrationHeader = @{
        externalSystem = "SAP-ERP"
        externalReference = "EXT-REF-123"
        contractVersion = "v1.1"
        idempotencyKey = $idemKey
        timestamp = (Get-Date).ToString("o")
    }
    inboundOrder = @{
        tenantId = "00000000-0000-0000-0000-000000000001"
        WERKS = "SAP-WH-01"
        EBELN = $orderNo
        LIFNR = "SAP-SUP-01"
        orderDate = (Get-Date).ToString("yyyy-MM-dd")
        expectedArrivalDate = (Get-Date).AddDays(1).ToString("yyyy-MM-dd")
        items = @(
            @{
                EBELP = 10
                MATNR = "SAP-MAT-01"
                expectedQty = 150.0
                MEINS = "SAP-UOM-01"
            }
        )
    }
} | ConvertTo-Json -Depth 5

try {
    $poRes = Invoke-RestMethod -Uri "$baseUrl/api/integration/inbound-orders" -Method Post -Body $poBody -ContentType "application/json" -Headers $poHeaders
    Write-Host "Inbound Order Created: ID = $($poRes.payload.orderId)" -ForegroundColor Green
} catch {
    $stream = $_.Exception.Response.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($stream)
    $errorBody = $reader.ReadToEnd()
    Write-Error "API Error: $errorBody"
    exit 1
}

# 3. Quét kiểm tra Webhook Deliveries qua API
Write-Host "Đang quét danh sách Webhook Deliveries..."
Start-Sleep -Seconds 2 # Chờ worker poll

$delRes = Invoke-RestMethod -Uri "$baseUrl/api/webhooks/deliveries?eventType=inbound.completed" -Method Get -Headers $headers
$delivery = $delRes.items | Where-Object { $_.subscriptionId -eq $subId } | Select-Object -First 1

if ($null -eq $delivery) {
    Write-Error "Không tìm thấy delivery record cho subscription vừa tạo."
}

Write-Host "Delivery record found!" -ForegroundColor Green
Write-Host "Status: $($delivery.status)" -ForegroundColor Yellow
Write-Host "Trace ID: $($delivery.traceId)"

if ($delivery.status -eq "delivered" -or $delivery.status -eq "pending" -or $delivery.status -eq "sending") {
    Write-Host ">>> VERIFY WEBHOOK OUTBOX THÀNH CÔNG!" -ForegroundColor Green
} else {
    Write-Error "Delivery record status không hợp lệ: $($delivery.status)"
}
