$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$servicePath = Join-Path $projectRoot "backend/modules/Nexustock.Modules.LabelPrinting/Services/LabelPrintingService.cs"
$dtoPath = Join-Path $projectRoot "backend/modules/Nexustock.Modules.LabelPrinting/DTOs/LabelPrintingDtos.cs"
$masterContextPath = Join-Path $projectRoot "backend/modules/Nexustock.Modules.MasterData/Contexts/MasterDataDbContext.cs"
$migrationPath = Join-Path $projectRoot "backend/modules/Nexustock.Modules.MasterData/Migrations/20260717030434_SeedLabelReprintReasonCodes.cs"

Write-Host "1. Verifying reprint service audit contract..." -ForegroundColor Cyan
$service = Get-Content $servicePath -Raw
$requiredServiceTokens = @(
    'private const int MaxReprintCount = 3;',
    'source.ReprintCount += 1;',
    'ReasonType == "LABEL_REPRINT"',
    'SourceJobId = source.Id',
    'ReasonCode = normalizedReason',
    'REPRINT_LIMIT_EXCEEDED',
    'INVALID_REASON_CODE',
    'IdempotencyKey == request.IdempotencyKey.Trim()'
)
foreach ($token in $requiredServiceTokens) {
    if ($service -notlike "*$token*") {
        Write-Error "Missing reprint audit token: $token"
        exit 1
    }
}

Write-Host "Reprint service audit contract found." -ForegroundColor Green

Write-Host "`n2. Verifying DTO camelCase-compatible contract..." -ForegroundColor Cyan
$dto = Get-Content $dtoPath -Raw
$requiredDtoTokens = @(
    'ReprintJobRequest',
    'ReasonCode',
    'IdempotencyKey',
    'SourceJobId',
    'ReprintCount',
    'ErrorMessage'
)
foreach ($token in $requiredDtoTokens) {
    if ($dto -notlike "*$token*") {
        Write-Error "Missing DTO token: $token"
        exit 1
    }
}

Write-Host "DTO contract found." -ForegroundColor Green

Write-Host "`n3. Verifying reason code seed source..." -ForegroundColor Cyan
$masterContext = Get-Content $masterContextPath -Raw
$migration = Get-Content $migrationPath -Raw
$requiredReasonCodes = @(
    'LABEL_DAMAGED',
    'PRINTER_JAM',
    'WRONG_LABEL_APPLIED',
    'SUPERVISOR_APPROVED'
)
foreach ($code in $requiredReasonCodes) {
    if ($masterContext -notlike "*$code*" -or $migration -notlike "*$code*") {
        Write-Error "Missing LABEL_REPRINT reason seed: $code"
        exit 1
    }
}

Write-Host "Reason code seeds found." -ForegroundColor Green
Write-Host "`nLABEL REPRINT AUDIT VERIFY PASSED!" -ForegroundColor Green
exit 0
