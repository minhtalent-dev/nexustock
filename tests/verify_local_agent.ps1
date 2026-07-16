$API_URL = if ($env:NEXUSTOCK_API_URL) { $env:NEXUSTOCK_API_URL } else { "http://localhost:5024/api" }
$JSON_CONTENT_TYPE = "application/json; charset=utf-8"

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

    [pscustomobject]@{
        StatusCode = $response.StatusCode
        Body = $body
        Raw = $raw
    }
}

function Assert-SecretNotLeaked($raw, $secret, $context) {
    if (-not [string]::IsNullOrEmpty($secret) -and $raw -like "*$secret*") {
        Write-Error "Error: Secret leaked in $context."
        exit 1
    }
}

$adminEmail = $env:NEXUSTOCK_ADMIN_EMAIL
$adminPassword = $env:NEXUSTOCK_ADMIN_PASSWORD

if ([string]::IsNullOrWhiteSpace($adminEmail) -or [string]::IsNullOrWhiteSpace($adminPassword)) {
    if ($env:ALLOW_DEFAULT_TEST_CREDENTIALS -eq "true") {
        $adminEmail = "admin@nexustock.com"
        $adminPassword = "AdminSecret123!"
        Write-Host "Using default dev admin credentials because ALLOW_DEFAULT_TEST_CREDENTIALS=true." -ForegroundColor Yellow
    } else {
        Write-Error "Missing NEXUSTOCK_ADMIN_EMAIL/NEXUSTOCK_ADMIN_PASSWORD. Set env vars or ALLOW_DEFAULT_TEST_CREDENTIALS=true for dev seed only."
        exit 1
    }
}

# 1. Đăng nhập Admin
Write-Host "1. Logging in as admin..." -ForegroundColor Cyan
$loginBody = @{ email = $adminEmail; password = $adminPassword } | ConvertTo-Json
try {
    $loginRes = Invoke-RestMethod -Uri "$API_URL/auth/login" -Method Post -Body $loginBody -ContentType $JSON_CONTENT_TYPE
    $token = $loginRes.token
    $headers = @{ Authorization = "Bearer $token" }
    Write-Host "Login successful." -ForegroundColor Green
} catch {
    $err = Read-ErrorResponse $_
    Assert-SecretNotLeaked $err.Raw $adminPassword "login error response"
    Write-Error "Login failed: $($err.Raw)"
    exit 1
}

# 2. Sinh mã ghép cặp (Pairing Code)
Write-Host "`n2. Generating pairing code..." -ForegroundColor Cyan
$stationCode = "TEST-STATION-" + (Get-Random -Minimum 1000 -Maximum 9999)
$stationName = "Trạm Test Tự Động"
$pairBody = @{ stationCode = $stationCode; name = $stationName } | ConvertTo-Json

try {
    $pairRes = Invoke-RestMethod -Uri "$API_URL/agent/stations/pairing-code" -Method Post -Body $pairBody -ContentType $JSON_CONTENT_TYPE -Headers $headers
    $pairingCode = $pairRes.pairingCode
    Write-Host "Generated pairing code for station $stationCode" -ForegroundColor Green
} catch {
    Write-Error "Failed to generate pairing code: $_"
    exit 1
}

# 3. Test bảo mật: Thử sai mã ghép cặp 5 lần để tự động khóa
Write-Host "`n3. Testing security: brute-force pairing code lockout..." -ForegroundColor Cyan
$wrongCodeBody = @{ stationCode = $stationCode; pairingCode = "999999"; machineName = "TEST-PC" } | ConvertTo-Json
$isLockedOut = $false

for ($i = 1; $i -le 6; $i++) {
    try {
        Invoke-RestMethod -Uri "$API_URL/agent/stations/confirm-pair" -Method Post -Body $wrongCodeBody -ContentType $JSON_CONTENT_TYPE
        Write-Error "Error: Wrong code accepted."
        exit 1
    } catch {
        $err = Read-ErrorResponse $_
        Write-Host "Attempt $i rejected: $($err.Body.message)" -ForegroundColor Yellow
        if ($err.Body.message -like "*khóa*") {
            $isLockedOut = $true
            break
        }
    }
}

if ($isLockedOut) {
    Write-Host "Security Check Pass: Pairing code locked out after 5 failures." -ForegroundColor Green
} else {
    Write-Error "Error: Pairing code did not lock out."
    exit 1
}

# 4. Sinh mã ghép cặp mới để hoàn tất quy trình
Write-Host "`n4. Generating new pairing code for successful test..." -ForegroundColor Cyan
try {
    $pairRes2 = Invoke-RestMethod -Uri "$API_URL/agent/stations/pairing-code" -Method Post -Body $pairBody -ContentType $JSON_CONTENT_TYPE -Headers $headers
    $pairingCode2 = $pairRes2.pairingCode
    Write-Host "New pairing code generated." -ForegroundColor Green
} catch {
    Write-Error "Failed to generate new pairing code: $_"
    exit 1
}

# 5. Xác nhận ghép cặp thành công
Write-Host "`n5. Confirming pairing code..." -ForegroundColor Cyan
$confirmBody = @{ stationCode = $stationCode; pairingCode = $pairingCode2; machineName = "TEST-PC" } | ConvertTo-Json
try {
    $confirmRes = Invoke-RestMethod -Uri "$API_URL/agent/stations/confirm-pair" -Method Post -Body $confirmBody -ContentType $JSON_CONTENT_TYPE
    $stationId = $confirmRes.stationId
    $agentToken = $confirmRes.agentToken
    Write-Host "Pairing successful. Station ID: $stationId, AgentToken generated." -ForegroundColor Green
} catch {
    Write-Error "Confirm pair failed: $_"
    exit 1
}

# 6. Gửi Heartbeat bằng X-Agent-Token
Write-Host "`n6. Sending heartbeat..." -ForegroundColor Cyan
$heartbeatHeaders = @{ "X-Agent-Token" = $agentToken }
$heartbeatBody = @{
    devices = @(
        @{ deviceId = "test_scale"; deviceType = "scaleCom"; connectionState = "connected"; lastErrorMessage = $null }
    )
} | ConvertTo-Json

try {
    $hbRes = Invoke-RestMethod -Uri "$API_URL/agent/stations/$stationId/heartbeat" -Method Post -Headers $heartbeatHeaders -Body $heartbeatBody -ContentType $JSON_CONTENT_TYPE
    Write-Host "Heartbeat response: $($hbRes.status)" -ForegroundColor Green
} catch {
    Write-Error "Heartbeat failed: $_"
    exit 1
}

# 7. Sai token phải bị từ chối và không lộ token
Write-Host "`n7. Verifying heartbeat rejects wrong token..." -ForegroundColor Cyan
try {
    Invoke-RestMethod -Uri "$API_URL/agent/stations/$stationId/heartbeat" -Method Post -Headers @{ "X-Agent-Token" = "wrong-token" } -Body $heartbeatBody -ContentType $JSON_CONTENT_TYPE
    Write-Error "Error: Heartbeat accepted wrong token."
    exit 1
} catch {
    $err = Read-ErrorResponse $_
    Assert-SecretNotLeaked $err.Raw $agentToken "wrong-token heartbeat error response"
    if ($err.StatusCode -eq "Unauthorized" -or $err.StatusCode -eq "Forbidden") {
        Write-Host "Success: Wrong token rejected with $($err.StatusCode)." -ForegroundColor Green
    } else {
        Write-Error "Error: Wrong token returned unexpected status $($err.StatusCode)."
        exit 1
    }
}

# 8. Truy vấn danh sách trạm (có phân trang)
Write-Host "`n8. Verifying station list pagination and station name..." -ForegroundColor Cyan
try {
    $listRes = Invoke-RestMethod -Uri "$API_URL/agent/stations?page=1&pageSize=10" -Method Get -Headers $headers
    Write-Host "Stations found: $($listRes.totalCount). First page items: $($listRes.items.Count)" -ForegroundColor Green
    $foundItem = $null
    foreach ($item in $listRes.items) {
        if ($item.stationId -eq $stationId) {
            $foundItem = $item
            break
        }
    }
    if ($null -eq $foundItem) {
        Write-Error "Error: Created station not in list."
        exit 1
    }
    if ($foundItem.name -ne $stationName) {
        Write-Error "Error: Station name mismatch. Expected '$stationName', got '$($foundItem.name)'."
        exit 1
    }
    Write-Host "Success: Created station found with correct name." -ForegroundColor Green

    $listHuge = Invoke-RestMethod -Uri "$API_URL/agent/stations?page=1&pageSize=99999" -Method Get -Headers $headers
    if ($listHuge.items.Count -gt 100) {
        Write-Error "Error: pageSize=99999 returned more than 100 items."
        exit 1
    }
    if ($null -ne $listHuge.pageSize -and $listHuge.pageSize -gt 100) {
        Write-Error "Error: pageSize=99999 was not clamped. Returned pageSize=$($listHuge.pageSize)."
        exit 1
    }
    Write-Host "Success: pageSize=99999 is clamped to a safe maximum." -ForegroundColor Green
} catch {
    Write-Error "Get stations list failed: $_"
    exit 1
}

# 9. Thu hồi quyền trạm làm việc (Revoke)
Write-Host "`n9. Revoking station..." -ForegroundColor Cyan
$revokeBody = @{ reasonCode = "SECURITY_BREACH"; description = "Test auto-revoke security" } | ConvertTo-Json
try {
    $revokeRes = Invoke-RestMethod -Uri "$API_URL/agent/stations/$stationId/revoke" -Method Post -Headers $headers -Body $revokeBody -ContentType $JSON_CONTENT_TYPE
    Write-Host "Revoke result: $($revokeRes.status)" -ForegroundColor Green
} catch {
    Write-Error "Revoke failed: $_"
    exit 1
}

# 10. Gửi Heartbeat sau khi bị thu hồi (Mong đợi lỗi 403 Forbidden)
Write-Host "`n10. Verifying heartbeat reject after revoke..." -ForegroundColor Cyan
try {
    Invoke-RestMethod -Uri "$API_URL/agent/stations/$stationId/heartbeat" -Method Post -Headers $heartbeatHeaders -Body $heartbeatBody -ContentType $JSON_CONTENT_TYPE
    Write-Error "Error: Heartbeat accepted after revoke."
    exit 1
} catch {
    $err = Read-ErrorResponse $_
    Assert-SecretNotLeaked $err.Raw $agentToken "revoked heartbeat error response"
    if ($err.StatusCode -eq "Forbidden" -and $err.Body.code -eq "backend.revoked") {
        Write-Host "Success: Heartbeat rejected with 403 and backend.revoked code." -ForegroundColor Green
    } else {
        Write-Error "Error: Heartbeat returned unexpected status $($err.StatusCode) and code $($err.Body.code)"
        exit 1
    }
}

Write-Host "`n==================================================" -ForegroundColor Green
Write-Host "ALL BACKEND LOCAL AGENT INTEGRATION TESTS PASSED!" -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Green
