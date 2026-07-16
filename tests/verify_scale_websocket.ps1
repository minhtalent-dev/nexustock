$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$agentProject = Join-Path $projectRoot "local-agent/Nexustock.LocalAgent/Nexustock.LocalAgent.csproj"
$handlerPath = Join-Path $projectRoot "local-agent/Nexustock.LocalAgent/WebSocketHandler.cs"
$securityPath = Join-Path $projectRoot "local-agent/Nexustock.LocalAgent/WebSocketSecurity.cs"
$frontendHookPath = Join-Path $projectRoot "frontend/src/features/outbound/hooks/use-local-scale.ts"

Write-Host "1. Building Local Agent..." -ForegroundColor Cyan
dotnet build $agentProject --no-restore | Out-Host
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n2. Verifying scale WebSocket message contracts..." -ForegroundColor Cyan
$handler = Get-Content $handlerPath -Raw
$requiredHandlerTokens = @(
    'case "scale.status.request"',
    'case "scale.weight.subscribe"',
    'case "scale.zero.request"',
    'case "scale.tare.request"',
    '"scale.status.response"',
    '"scale.weightChanged"',
    '"scale.zero.response"',
    '"scale.tare.response"',
    'EnsureSignedScaleCommandAsync',
    'ToScalePayload',
    'deviceId = reading.DeviceId',
    'weightKg = reading.WeightKg',
    'stable = reading.Stable',
    'connectionState = reading.ConnectionState'
)

foreach ($token in $requiredHandlerTokens) {
    if ($handler -notlike "*$token*") {
        Write-Error "Missing WebSocket scale contract token: $token"
        exit 1
    }
}

Write-Host "Scale WebSocket handlers found." -ForegroundColor Green

Write-Host "`n3. Verifying signed command boundary..." -ForegroundColor Cyan
$security = Get-Content $securityPath -Raw
if ($handler -notlike '*HandleScaleSubscribeAsync(webSocket, msg);*') {
    Write-Error "scale.weight.subscribe must stay unsigned and direct."
    exit 1
}
if ($handler -notlike '*HandleScaleZeroAsync(webSocket, msg, config);*' -or $handler -notlike '*HandleScaleTareAsync(webSocket, msg, config);*') {
    Write-Error "Zero/Tare handlers must receive config for HMAC verification."
    exit 1
}
if ($security -notlike '*VerifySignedPayload*' -or $security -notlike '*auth.signature_missing*' -or $security -notlike '*auth.invalid_signature*') {
    Write-Error "Signed payload verification is incomplete."
    exit 1
}

Write-Host "Signed command boundary found." -ForegroundColor Green

Write-Host "`n4. Verifying frontend loopback subscription..." -ForegroundColor Cyan
$hook = Get-Content $frontendHookPath -Raw
$requiredHookTokens = @(
    '127.0.0.1:${agent.port}/ws',
    'scale.weight.subscribe',
    'scale.weightChanged',
    'weightKg',
    'stable'
)
foreach ($token in $requiredHookTokens) {
    if ($hook -notlike "*$token*") {
        Write-Error "Missing frontend scale subscription token: $token"
        exit 1
    }
}

Write-Host "Frontend loopback subscription found." -ForegroundColor Green
Write-Host "`nALL SCALE WEBSOCKET TESTS PASSED!" -ForegroundColor Green
