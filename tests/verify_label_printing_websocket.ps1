$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$agentProject = Join-Path $projectRoot "local-agent/Nexustock.LocalAgent/Nexustock.LocalAgent.csproj"
$configDir = Join-Path $env:TEMP ("nexustock-agent-label-test-" + [Guid]::NewGuid().ToString("N"))
$mockOutput = Join-Path $configDir "mock-labels"
$rawCommand = "^XA^FO50,50^ADN,36,20^FDE2E LABEL TEST^FS^XZ"
$port = 9000
$agentProcess = $null

function Stop-AgentProcess {
    if ($agentProcess -and -not $agentProcess.HasExited) {
        Stop-Process -Id $agentProcess.Id -Force -ErrorAction SilentlyContinue
    }
}

function Receive-WebSocketText($socket) {
    $buffer = New-Object byte[] 8192
    $segment = [ArraySegment[byte]]::new($buffer)
    $builder = [System.Text.StringBuilder]::new()

    do {
        $result = $socket.ReceiveAsync($segment, [Threading.CancellationToken]::None).GetAwaiter().GetResult()
        if ($result.MessageType -eq [System.Net.WebSockets.WebSocketMessageType]::Close) {
            throw "WebSocket closed before response."
        }
        [void]$builder.Append([System.Text.Encoding]::UTF8.GetString($buffer, 0, $result.Count))
    } while (-not $result.EndOfMessage)

    return $builder.ToString()
}

try {
    New-Item -ItemType Directory -Path $configDir, $mockOutput -Force | Out-Null

    $config = @{
        stationId = "00000000-0000-0000-0000-000000009001"
        stationCode = "TEST-LABEL"
        backendBaseUrl = "http://localhost:5000"
        webSocketPort = $port
        dpapiScope = "CurrentUser"
        encryptedAgentToken = "test-token-not-used-when-bypass-enabled"
        allowedOrigins = @("http://localhost:3003")
        allowInsecureWebSocket = $true
        allowTestSignatureBypass = $true
        scale = @{ mode = "mock" }
        printers = @(@{
            enabled = $true
            mode = "mock"
            printerCode = "PRINTER-01"
            printerName = "PRINTER-01"
            language = "zpl"
            mockOutputPath = $mockOutput
        })
    }
    $config | ConvertTo-Json -Depth 8 | Set-Content -Path (Join-Path $configDir "agent.json") -Encoding UTF8

    $env:NEXUSTOCK_AGENT_CONFIG_DIR = $configDir
    $env:NEXUSTOCK_AGENT_TEST_MODE = "true"
    $env:NEXUSTOCK_AGENT_DISABLE_WORKER = "true"

    Write-Host "1. Building Local Agent..." -ForegroundColor Cyan
    dotnet build $agentProject --no-restore | Out-Host
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host "`n2. Starting Local Agent test mode..." -ForegroundColor Cyan
    $agentProcess = Start-Process -FilePath "dotnet" -ArgumentList @("run", "--project", $agentProject, "--no-build") -PassThru -WindowStyle Hidden
    Start-Sleep -Seconds 3
    if ($agentProcess.HasExited) {
        throw "Local Agent exited during startup. ExitCode=$($agentProcess.ExitCode)"
    }

    Write-Host "`n3. Sending signed printer.print.request..." -ForegroundColor Cyan
    $socket = [System.Net.WebSockets.ClientWebSocket]::new()
    $socket.Options.SetRequestHeader("Origin", "http://localhost:3003")
    $socket.ConnectAsync([Uri]"ws://127.0.0.1:$port/ws", [Threading.CancellationToken]::None).GetAwaiter().GetResult()

    $message = @{
        messageId = [Guid]::NewGuid().ToString()
        type = "printer.print.request"
        timestamp = [DateTimeOffset]::UtcNow.ToString("o")
        payload = @{
            printerCode = "PRINTER-01"
            rawCommand = $rawCommand
        }
        signature = "NEXUSTOCK_TEST_SIGNATURE"
    }
    $json = $message | ConvertTo-Json -Depth 8 -Compress
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
    $socket.SendAsync([ArraySegment[byte]]::new($bytes), [System.Net.WebSockets.WebSocketMessageType]::Text, $true, [Threading.CancellationToken]::None).GetAwaiter().GetResult()

    $responseJson = Receive-WebSocketText $socket
    $response = $responseJson | ConvertFrom-Json
    if ($response.type -eq "agent.error") {
        throw "Agent error: $($response.payload.code) $($response.payload.message)"
    }
    if ($response.type -ne "printer.print.response") {
        throw "Unexpected response type: $($response.type)"
    }

    $socket.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "done", [Threading.CancellationToken]::None).GetAwaiter().GetResult()

    Write-Host "`n4. Verifying mock output..." -ForegroundColor Cyan
    $deadline = (Get-Date).AddSeconds(5)
    do {
        $files = Get-ChildItem -Path $mockOutput -Filter "print_PRINTER-01_*.zpl" -ErrorAction SilentlyContinue
        if ($files) { break }
        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)

    if (-not $files) { throw "Mock printer output was not created." }
    $printed = Get-Content -Path $files[0].FullName -Raw
    if ($printed -ne $rawCommand) { throw "Mock printer output content mismatch." }

    Write-Host "`nLABEL PRINTING WEBSOCKET E2E PASSED!" -ForegroundColor Green
    exit 0
}
finally {
    Stop-AgentProcess
    Remove-Item Env:\NEXUSTOCK_AGENT_CONFIG_DIR -ErrorAction SilentlyContinue
    Remove-Item Env:\NEXUSTOCK_AGENT_TEST_MODE -ErrorAction SilentlyContinue
    Remove-Item Env:\NEXUSTOCK_AGENT_DISABLE_WORKER -ErrorAction SilentlyContinue
    Remove-Item -Path $configDir -Recurse -Force -ErrorAction SilentlyContinue
}
