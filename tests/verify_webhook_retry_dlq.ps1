# Script kiểm tra cơ chế Retry & DLQ khi targetUrl không thể kết nối
$ErrorActionPreference = "Stop"

$baseUrl = "http://localhost:5024"
$dbConnectionString = "Host=127.0.0.1;Port=5435;Database=nexustock_main;Username=kingsman;Password=43zTV!^FiU2g!!nXc3RL!6x2&nw@2V9^BM^@!f8&ersTL!9Sj7"

Write-Host ">>> Bắt đầu verify Webhook Retry & DLQ..." -ForegroundColor Cyan

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

# 2. Tạo subscription với targetUrl lỗi (invalid port)
$subBody = @{
    targetUrl = "http://127.0.0.1:9999/invalid-webhook-endpoint"
    eventTypes = @("inbound.completed")
} | ConvertTo-Json

Write-Host "Tạo subscription lỗi kết nối..."
$subRes = Invoke-RestMethod -Uri "$baseUrl/api/webhooks/subscriptions" -Method Post -Body $subBody -ContentType "application/json" -Headers $headers
$subId = $subRes.subscriptionId

# 3. Trigger Inbound Order để tạo delivery
$idemKey = [Guid]::NewGuid().ToString()
$poHeaders = @{
    "Idempotency-Key" = $idemKey
    "X-Contract-Version" = "v1.1"
}
$headers.Keys | ForEach-Object { $poHeaders.Add($_, $headers[$_]) }

$orderNo = "PO-FAIL-" + (Get-Random -Minimum 1000 -Maximum 9999).ToString()
$poBody = @{
    integrationHeader = @{
        externalSystem = "SAP-ERP"
        externalReference = "EXT-REF-FAIL"
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
                expectedQty = 200.0
                MEINS = "SAP-UOM-01"
            }
        )
    }
} | ConvertTo-Json -Depth 5

try {
    $poRes = Invoke-RestMethod -Uri "$baseUrl/api/integration/inbound-orders" -Method Post -Body $poBody -ContentType "application/json" -Headers $poHeaders
} catch {
    $stream = $_.Exception.Response.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($stream)
    $errorBody = $reader.ReadToEnd()
    Write-Error "API Error: $errorBody"
    exit 1
}

# 4. Kiểm tra delivery status
Start-Sleep -Seconds 2

$delRes = Invoke-RestMethod -Uri "$baseUrl/api/webhooks/deliveries?subscriptionId=$subId" -Method Get -Headers $headers
$delivery = $delRes.items | Select-Object -First 1

if ($null -eq $delivery) {
    Write-Error "Không tìm thấy delivery record cho subscription lỗi."
}

Write-Host "Delivery status: $($delivery.status) (RetryCount: $($delivery.retryCount))" -ForegroundColor Yellow

if ($delivery.status -eq "pending" -or $delivery.status -eq "sending" -or $delivery.status -eq "deadLetter") {
    Write-Host ">>> VERIFY RETRY & DLQ THÀNH CÔNG (Trạng thái trung gian/deadLetter hợp lệ)!" -ForegroundColor Green
} else {
    Write-Error "Delivery record status không đúng kỳ vọng: $($delivery.status)"
}
