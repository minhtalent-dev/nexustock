# Kiểm tra WebSocket Local Agent thông qua .NET ClientWebSocket

function Protect-AgentToken($plainText, $scopeName) {
    Add-Type -AssemblyName System.Security
    $scope = [System.Security.Cryptography.DataProtectionScope]::$scopeName
    $entropy = [System.Text.Encoding]::UTF8.GetBytes("NexustockAgentEntropy2026")
    $plainBytes = [System.Text.Encoding]::UTF8.GetBytes($plainText)
    $encryptedBytes = [System.Security.Cryptography.ProtectedData]::Protect($plainBytes, $entropy, $scope)
    return [Convert]::ToBase64String($encryptedBytes)
}

function Assert-AgentAcl($path) {
    if (-not $IsWindows -and $PSVersionTable.PSEdition -eq "Core") {
        Write-Host "Skipping ACL check on non-Windows runtime." -ForegroundColor Yellow
        return
    }

    $acl = Get-Acl $path
    $blockedIdentities = @("Everyone", "BUILTIN\Users", "Users")
    $blockedRights = [System.Security.AccessControl.FileSystemRights]::Write -bor
        [System.Security.AccessControl.FileSystemRights]::Modify -bor
        [System.Security.AccessControl.FileSystemRights]::FullControl -bor
        [System.Security.AccessControl.FileSystemRights]::WriteData -bor
        [System.Security.AccessControl.FileSystemRights]::CreateFiles

    foreach ($rule in $acl.Access) {
        $identity = $rule.IdentityReference.Value
        $isBlockedIdentity = $false
        foreach ($blocked in $blockedIdentities) {
            if ($identity -eq $blocked -or $identity.EndsWith("\$blocked")) {
                $isBlockedIdentity = $true
                break
            }
        }

        if ($isBlockedIdentity -and $rule.AccessControlType -eq "Allow" -and (($rule.FileSystemRights -band $blockedRights) -ne 0)) {
            Write-Error "Error: Broad write ACL found on ${path}: $identity $($rule.FileSystemRights)."
            exit 1
        }
    }
}

# Chuẩn bị paired config cô lập để command security test chạy đúng paired-mode.
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptRoot
$agentProject = Join-Path $projectRoot "local-agent/Nexustock.LocalAgent/Nexustock.LocalAgent.csproj"
$ownsConfigDir = [string]::IsNullOrWhiteSpace($env:NEXUSTOCK_AGENT_CONFIG_DIR)
$configDir = if ($ownsConfigDir) {
    Join-Path ([System.IO.Path]::GetTempPath()) ("nexustock-agent-ws-test-" + [Guid]::NewGuid().ToString("N"))
} else {
    $env:NEXUSTOCK_AGENT_CONFIG_DIR
}
$env:NEXUSTOCK_AGENT_CONFIG_DIR = $configDir
$configPath = Join-Path $configDir "agent.json"
$agentToken = "ws-test-token-" + [Guid]::NewGuid().ToString("N")
$dpapiScope = "LocalMachine"
$encryptedToken = Protect-AgentToken $agentToken $dpapiScope

if (-not (Test-Path $configDir)) {
    New-Item -ItemType Directory -Path $configDir -Force | Out-Null
}

$agentStartPort = if ($ownsConfigDir) { 9200 + (Get-Random -Minimum 0 -Maximum 200) } else { 9000 }
$pairedConfig = [ordered]@{
    stationId = [Guid]::NewGuid()
    stationCode = "WS-TEST-STATION"
    backendBaseUrl = if ($env:NEXUSTOCK_BACKEND_BASE_URL) { $env:NEXUSTOCK_BACKEND_BASE_URL } else { "http://localhost:5000" }
    webSocketPort = $agentStartPort
    dpapiScope = $dpapiScope
    encryptedAgentToken = $encryptedToken
    certificateThumbprint = $null
    allowedOrigins = @("http://localhost:3000", "http://localhost:3003")
    allowInsecureWebSocket = $true
}

$pairedConfig | ConvertTo-Json -Depth 5 | Set-Content -Path $configPath -Encoding UTF8

$agentProcess = $null
$agentOutLog = Join-Path $configDir "agent-startup.out.log"
$agentErrLog = Join-Path $configDir "agent-startup.err.log"
if ($ownsConfigDir) {
    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:DOTNET_ENVIRONMENT = "Development"
    $env:NEXUSTOCK_AGENT_DISABLE_WORKER = "true"
    $agentProcess = Start-Process -FilePath "dotnet" -ArgumentList @("run", "--project", $agentProject, "--no-build") -WorkingDirectory $projectRoot -RedirectStandardOutput $agentOutLog -RedirectStandardError $agentErrLog -PassThru -WindowStyle Hidden
}

$PORT = $agentStartPort
$foundPort = $false
# Dò tìm cổng Agent đang chạy thực tế, chờ tối đa 15 giây cho process test khởi động.
for ($attempt = 1; $attempt -le 15 -and -not $foundPort; $attempt++) {
    if ($null -ne $agentProcess -and $agentProcess.HasExited) {
        break
    }

    for ($p = $agentStartPort; $p -le ($agentStartPort + 5); $p++) {
        $tcp = New-Object System.Net.Sockets.TcpClient
        try {
            $tcp.Connect("127.0.0.1", $p)
            $PORT = $p
            $foundPort = $true
            $tcp.Close()
            Write-Host "Found Agent running on port $PORT" -ForegroundColor Green
            break
        } catch {
            # port offline
        } finally {
            $tcp.Dispose()
        }
    }

    if (-not $foundPort) {
        Start-Sleep -Seconds 1
    }
}

if (-not $foundPort) {
    if ($null -ne $agentProcess -and -not $agentProcess.HasExited) {
        Stop-Process -Id $agentProcess.Id -Force
        Start-Sleep -Seconds 1
    }

    $startupLog = @(
        if (Test-Path $agentOutLog) { try { Get-Content $agentOutLog -Raw } catch { "stdout log locked or denied: $($_.Exception.Message)" } }
        if (Test-Path $agentErrLog) { try { Get-Content $agentErrLog -Raw } catch { "stderr log locked or denied: $($_.Exception.Message)" } }
    ) -join "`n"
    if ([string]::IsNullOrWhiteSpace($startupLog)) { $startupLog = "No startup log." }
    Write-Error "Local Agent is not running on ports $agentStartPort-$($agentStartPort + 5). Log: $startupLog"
    exit 1
}

$WS_URI = "ws://127.0.0.1:$PORT/ws"

# Hàm gửi/nhận message qua ClientWebSocket
function Send-ReceiveWS ($headers, $payloadJson) {
    $ws = New-Object System.Net.WebSockets.ClientWebSocket
    foreach ($h in $headers.Keys) {
        $ws.Options.SetRequestHeader($h, $headers[$h])
    }

    $cts = New-Object System.Threading.CancellationTokenSource
    $cts.CancelAfter(3000)

    try {
        $uri = New-Object System.Uri($WS_URI)
        $ws.ConnectAsync($uri, $cts.Token).GetAwaiter().GetResult()

        # Gửi dữ liệu
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($payloadJson)
        $segment = New-Object System.ArraySegment[Byte] -ArgumentList @(,$bytes)
        $ws.SendAsync($segment, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, $cts.Token).GetAwaiter().GetResult()

        # Nhận phản hồi
        $buffer = New-Object Byte[] 4096
        $recvSegment = New-Object System.ArraySegment[Byte] -ArgumentList @(,$buffer)
        $recvResult = $ws.ReceiveAsync($recvSegment, $cts.Token).GetAwaiter().GetResult()

        $responseJson = [System.Text.Encoding]::UTF8.GetString($buffer, 0, $recvResult.Count)

        $ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "Done", $cts.Token).GetAwaiter().GetResult()
        return $responseJson | ConvertFrom-Json
    } catch {
        throw $_
    } finally {
        $ws.Dispose()
    }
}

# 1. Test chặn Origin lạ (Mong đợi bị từ chối bắt tay)
Write-Host "1. Testing Origin allowlist enforcement..." -ForegroundColor Cyan
$badHeaders = @{ "Origin" = "http://evil.com" }
try {
    Send-ReceiveWS -headers $badHeaders -payloadJson "{}"
    Write-Error "Error: Connection accepted for malicious origin http://evil.com."
    exit 1
} catch {
    # Mong đợi lỗi Handshake fail 403
    Write-Host "Success: Connection rejected for malicious origin http://evil.com." -ForegroundColor Green
}

# 2. Test kết nối với Origin hợp lệ
Write-Host "`n2. Connecting with allowed origin http://localhost:3000..." -ForegroundColor Cyan
$goodHeaders = @{ "Origin" = "http://localhost:3000" }
try {
    $statusReq = @{
        messageId = "test-status-id"
        type = "agent.status.request"
        timestamp = [System.DateTime]::UtcNow.ToString("o")
        payload = @{}
    } | ConvertTo-Json

    $statusRes = Send-ReceiveWS -headers $goodHeaders -payloadJson $statusReq
    if ($statusRes.payload.status -ne "paired") {
        Write-Error "Error: Agent status is '$($statusRes.payload.status)', expected paired. Ensure Local Agent uses config path $configDir and port $PORT."
        exit 1
    }
    Assert-AgentAcl $configDir
    Assert-AgentAcl $configPath
    Write-Host "ACL check passed for Local Agent config." -ForegroundColor Green
    Write-Host "Success: Connected. Agent status response: $($statusRes.payload.status)" -ForegroundColor Green
} catch {
    Write-Error "Failed to connect with allowed origin: $_"
    exit 1
}

# 3. Test Time Skew (Lệch giờ quá 30 giây)
Write-Host "`n3. Testing Time Skew defense..." -ForegroundColor Cyan
try {
    $skewedTime = [System.DateTime]::UtcNow.AddMinutes(5).ToString("o")
    $skewReq = @{
        messageId = "test-skew-id"
        type = "agent.status.request"
        timestamp = $skewedTime
        payload = @{}
    } | ConvertTo-Json

    $skewRes = Send-ReceiveWS -headers $goodHeaders -payloadJson $skewReq
    if ($skewRes.type -eq "agent.error" -and $skewRes.payload.code -eq "auth.time_skew") {
        Write-Host "Success: Skewed message rejected with auth.time_skew code." -ForegroundColor Green
    } else {
        Write-Error "Error: Skewed message not rejected properly. Response type: $($skewRes.type), Code: $($skewRes.payload.code)"
        exit 1
    }
} catch {
    Write-Error "Time Skew test failed: $_"
    exit 1
}

# 4. Test paired-mode command thiếu signature
Write-Host "`n4. Testing paired-mode missing signature enforcement..." -ForegroundColor Cyan
try {
    $cmdReq = @{
        messageId = "test-cmd-missing-signature-id"
        type = "agent.command.ping"
        timestamp = [System.DateTime]::UtcNow.ToString("o")
        payload = @{ scaleId = "SCALE-01" }
    } | ConvertTo-Json

    $cmdRes = Send-ReceiveWS -headers $goodHeaders -payloadJson $cmdReq
    if ($cmdRes.type -eq "agent.error" -and $cmdRes.payload.code -eq "auth.signature_missing") {
        Write-Host "Success: Paired command missing signature rejected." -ForegroundColor Green
    } else {
        Write-Error "Error: Missing signature returned type $($cmdRes.type), code $($cmdRes.payload.code)."
        exit 1
    }
} catch {
    Write-Error "Missing signature test failed: $_"
    exit 1
}

# 5. Test paired-mode command sai signature
Write-Host "`n5. Testing paired-mode invalid signature enforcement..." -ForegroundColor Cyan
try {
    $invalidReq = @{
        messageId = "test-cmd-invalid-signature-id"
        type = "agent.command.ping"
        timestamp = [System.DateTime]::UtcNow.ToString("o")
        payload = @{ scaleId = "SCALE-01" }
        signature = "invalid"
    } | ConvertTo-Json

    $invalidRes = Send-ReceiveWS -headers $goodHeaders -payloadJson $invalidReq
    if ($invalidRes.type -eq "agent.error" -and $invalidRes.payload.code -eq "auth.invalid_signature") {
        Write-Host "Success: Paired command invalid signature rejected." -ForegroundColor Green
    } else {
        Write-Error "Error: Invalid signature returned type $($invalidRes.type), code $($invalidRes.payload.code)."
        exit 1
    }
} catch {
    Write-Error "Invalid signature test failed: $_"
    exit 1
}

Write-Host "`n==================================================" -ForegroundColor Green
Write-Host "ALL AGENT WEBSOCKET SECURITY TESTS PASSED!" -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Green

if ($null -ne $agentProcess -and -not $agentProcess.HasExited) {
    Stop-Process -Id $agentProcess.Id -Force
    Wait-Process -Id $agentProcess.Id -Timeout 5 -ErrorAction SilentlyContinue
}
if ($ownsConfigDir -and (Test-Path $configDir)) {
    for ($attempt = 1; $attempt -le 5; $attempt++) {
        try {
            Remove-Item $configDir -Recurse -Force -ErrorAction Stop
            break
        } catch {
            if ($attempt -eq 5) {
                Write-Warning "Could not remove temporary config directory ${configDir}: $($_.Exception.Message)"
                break
            }
            Start-Sleep -Milliseconds 300
        }
    }
}
