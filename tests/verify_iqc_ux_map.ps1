param(
    [string]$BaseUrl = "http://localhost:5024",
    [switch]$SkipLiveApi
)

Write-Host "=== Phase 34 IQC UX Map Verification ===" -ForegroundColor Cyan
Write-Host "Base URL: $BaseUrl" -ForegroundColor Gray

$pass = 0
$fail = 0
$skip = 0
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (-not (Test-Path (Join-Path $root "planning"))) {
    $root = "D:\1_Project\48_Nexustock"
}

function Assert-True($cond, $name) {
    if ($cond) {
        Write-Host "  PASS  $name" -ForegroundColor Green
        $script:pass++
    } else {
        Write-Host "  FAIL  $name" -ForegroundColor Red
        $script:fail++
    }
}

Write-Host "`n[Static] Artifacts & Gate code" -ForegroundColor Yellow
Assert-True (Test-Path "$root\planning\IQC_UX_MAP_GCM_PART.md") "UX map artifact exists"
Assert-True (Test-Path "$root\planning\phases\phase_34_iqc_ux_map_gcm.md") "Phase 34 spec exists"
Assert-True (Test-Path "$root\backend\modules\Nexustock.Modules.Qc.Abstractions\IQcGateService.cs") "IQcGateService Abstractions"
Assert-True (Test-Path "$root\backend\modules\Nexustock.Modules.Qc\Services\QcGateService.cs") "QcGateService impl"
Assert-True (Select-String -Path "$root\backend\modules\Nexustock.Modules.Inventory\Controllers\InventoryController.cs" -Pattern "IQcGateService" -Quiet) "Inventory move wires Gate"
Assert-True (Select-String -Path "$root\backend\modules\Nexustock.Modules.Inventory\Controllers\MobileController.cs" -Pattern "EnsureLotUsableByLotNoAsync" -Quiet) "Mobile offline MOVE wires Gate"
Assert-True (Select-String -Path "$root\backend\modules\Nexustock.Modules.Lpn\Services\LpnService.cs" -Pattern "EnsureLotUsableByLotNoAsync" -Quiet) "LPN wires Gate"
Assert-True (Select-String -Path "$root\backend\modules\Nexustock.Modules.Replenishment\Services\ReplenishmentService.cs" -Pattern "EnsureLotUsableByLotNoAsync" -Quiet) "Replenishment wires Gate"
Assert-True (Select-String -Path "$root\backend\Nexustock.Api\Infrastructure\DatabaseSeeder.cs" -Pattern "FF_MOBILE_QC" -Quiet) "FF_MOBILE_QC seeded"
Assert-True (Test-Path "$root\frontend\src\app\mobile\qc\page.tsx") "Mobile QC page exists"
Assert-True (Select-String -Path "$root\frontend\messages\en\Errors.json" -Pattern "QC_LOT_NOT_RELEASED" -Quiet) "Errors EN QC_LOT_*"
Assert-True (Select-String -Path "$root\frontend\messages\vi\Errors.json" -Pattern "QC_LOT_ON_HOLD" -Quiet) "Errors VI QC_LOT_*"
Assert-True (Select-String -Path "$root\backend\modules\Nexustock.Modules.Qc\Controllers\QcController.cs" -Pattern "agingHours|GetHistory|GetLotTimeline" -Quiet) "Queue filter + history APIs"

$map = Get-Content "$root\planning\IQC_UX_MAP_GCM_PART.md" -Raw
Assert-True ($map -match "frm113_Iqc_Input" -and $map -match "frm108a_Part_Move_FC") "UX map has 8-form coverage markers"
Assert-True ($map -match "Call-site freeze") "UX map has call-site freeze"

if ($SkipLiveApi) {
    Write-Host "`n[Live] Skipped (-SkipLiveApi)" -ForegroundColor DarkYellow
    $skip++
} else {
    Write-Host "`n[Live] API smoke ($BaseUrl)" -ForegroundColor Yellow
    try {
        $health = Invoke-WebRequest -Uri "$BaseUrl/health/live" -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
        Assert-True ($health.StatusCode -eq 200) "Health endpoint reachable"
    } catch {
        Write-Host "  SKIP  Live API not reachable — start API then re-run without -SkipLiveApi" -ForegroundColor DarkYellow
        $skip++
    }
}

Write-Host "`n=== Result: PASS=$pass FAIL=$fail SKIP=$skip ===" -ForegroundColor Cyan
if ($fail -gt 0) { exit 1 } else { exit 0 }
