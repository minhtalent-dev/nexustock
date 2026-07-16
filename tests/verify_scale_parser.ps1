$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$agentProject = Join-Path $projectRoot "local-agent/Nexustock.LocalAgent/Nexustock.LocalAgent.csproj"
$parserPath = Join-Path $projectRoot "local-agent/Nexustock.LocalAgent/Devices/Scale/ScaleFrameParser.cs"
$filterPath = Join-Path $projectRoot "local-agent/Nexustock.LocalAgent/Devices/Scale/StableWeightFilter.cs"

Write-Host "1. Building Local Agent..." -ForegroundColor Cyan
dotnet build $agentProject --no-restore | Out-Host
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n2. Verifying scale parser safeguards..." -ForegroundColor Cyan
$parser = Get-Content $parserPath -Raw
$requiredParserTokens = @(
    "scale.frame_empty",
    "scale.frame_no_weight",
    "scale.frame_ambiguous",
    "scale.frame_invalid_weight",
    "generic-rs232",
    "NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint",
    "CultureInfo.InvariantCulture"
)

foreach ($token in $requiredParserTokens) {
    if ($parser -notlike "*$token*") {
        Write-Error "Missing parser safeguard: $token"
        exit 1
    }
}

Write-Host "Parser safeguards found." -ForegroundColor Green

Write-Host "`n3. Verifying stable weight filter safeguards..." -ForegroundColor Cyan
$filter = Get-Content $filterPath -Raw
$requiredFilterTokens = @(
    "StableWindowMs",
    "StableToleranceKg",
    "MinimumWeightKg",
    "weightKg <= _minimumWeightKg",
    "_samples.Count < 2",
    "max - min <= _toleranceKg",
    "Reset()"
)

foreach ($token in $requiredFilterTokens) {
    if ($filter -notlike "*$token*") {
        Write-Error "Missing stability safeguard: $token"
        exit 1
    }
}

Write-Host "Stable filter safeguards found." -ForegroundColor Green
Write-Host "`nALL SCALE PARSER TESTS PASSED!" -ForegroundColor Green
