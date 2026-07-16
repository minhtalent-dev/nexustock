$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$apiProject = Join-Path $projectRoot "backend/Nexustock.Api/Nexustock.Api.csproj"
$controllerPath = Join-Path $projectRoot "backend/modules/Nexustock.Modules.Inventory/Controllers/OutboundController.cs"
$dtoPath = Join-Path $projectRoot "backend/modules/Nexustock.Modules.Inventory/Dtos/OutboundDtos.cs"
$servicePath = Join-Path $projectRoot "backend/modules/Nexustock.Modules.Inventory/Services/WeightValidationService.cs"
$dialogPath = Join-Path $projectRoot "frontend/src/features/outbound/components/pack-dialog.tsx"

Write-Host "1. Building backend API..." -ForegroundColor Cyan
dotnet build $apiProject --no-restore | Out-Host
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n2. Verifying manual override API contract..." -ForegroundColor Cyan
$controller = Get-Content $controllerPath -Raw
$dto = Get-Content $dtoPath -Raw
$service = Get-Content $servicePath -Raw
$dialog = Get-Content $dialogPath -Raw

$requiredControllerTokens = @(
    '[HttpPost("packing/weight/manual")]',
    'CreateManualWeightOverride',
    'ManualWeightOverrideRequestDto',
    'Outbound.Packing.Execute',
    'ManualWeightOverrides.Add',
    'ManualWeightOverrideResponseDto',
    'DUPLICATE_PACKAGE_NO'
)
foreach ($token in $requiredControllerTokens) {
    if ($controller -notlike "*$token*") {
        Write-Error "Missing manual override API token: $token"
        exit 1
    }
}

$requiredDtoTokens = @(
    'ManualWeightOverrideRequestDto',
    'ManualWeightOverrideResponseDto',
    'ManualOverrideId',
    'ManualWeight',
    'Reason',
    'manual_override'
)
foreach ($token in $requiredDtoTokens) {
    if ($dto -notlike "*$token*") {
        Write-Error "Missing manual override DTO token: $token"
        exit 1
    }
}

$requiredServiceTokens = @(
    'WeightSources.ManualOverride',
    'ManualOverrideId.HasValue',
    'ManualWeightOverrides.FirstOrDefaultAsync',
    'UsedAt == null',
    'manualOverride.UsedAt = DateTime.UtcNow'
)
foreach ($token in $requiredServiceTokens) {
    if ($service -notlike "*$token*") {
        Write-Error "Missing manual override validation token: $token"
        exit 1
    }
}

Write-Host "Manual override backend contract found." -ForegroundColor Green

Write-Host "`n3. Verifying UI fallback contract..." -ForegroundColor Cyan
$requiredUiTokens = @(
    'Manual override',
    'manualWeight',
    'manualReason',
    '/outbound/packing/weight/manual',
    'manual_override',
    'manualOverrideId'
)
foreach ($token in $requiredUiTokens) {
    if ($dialog -notlike "*$token*") {
        Write-Error "Missing manual override UI token: $token"
        exit 1
    }
}

Write-Host "Manual override UI fallback found." -ForegroundColor Green
Write-Host "`nALL SCALE MANUAL OVERRIDE TESTS PASSED!" -ForegroundColor Green
