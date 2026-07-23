# Phase 42 — verify Storage Provider Bulk Migrate (static)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

function Assert-True([bool]$cond, [string]$msg) {
  if (-not $cond) { throw "FAIL: $msg" }
  Write-Host "PASS: $msg"
}

Write-Host "=== verify_storage_migrate.ps1 Phase 42 ==="

$filesRoot = Join-Path $root "backend\modules\Nexustock.Modules.Files"
$jobEntity = Join-Path $filesRoot "Entities\FileStorageMigrateJob.cs"
$worker = Join-Path $filesRoot "Workers\StorageMigrateWorker.cs"
$svc = Join-Path $filesRoot "Services\StorageMigrateService.cs"
$ctrl = Join-Path $filesRoot "Controllers\FileStorageMigrateController.cs"
$provider = Join-Path $filesRoot "Providers\IObjectStorageProvider.cs"
$panel = Join-Path $root "frontend\src\features\files\storage-migrate-panel.tsx"
$adminPage = Join-Path $root "frontend\src\app\admin\settings\storage\page.tsx"
$seeder = Join-Path $root "backend\Nexustock.Api\Infrastructure\DatabaseSeeder.cs"
$di = Join-Path $filesRoot "DependencyInjection.cs"

Assert-True (Test-Path $jobEntity) "migrateJobEntity"
$jobText = Get-Content $jobEntity -Raw
Assert-True ($jobText -match "EligibleIdsJson|CancelRequested") "migrateJobColumns"

$mig = @(Get-ChildItem (Join-Path $filesRoot "Migrations") -Filter "*AddStorageMigrateJobs*")
Assert-True ($mig.Count -ge 1) "migrateJobMigration"

Assert-True ((Get-Content $provider -Raw) -match "OpenReadAsync") "openReadReuse"

Assert-True (Test-Path $ctrl) "migrateApi"
$ctrlText = Get-Content $ctrl -Raw
Assert-True ($ctrlText -match "dry-run" -and $ctrlText -match "purge-source" -and $ctrlText -match "jobs/active") "migrateApiRoutes"

Assert-True (Test-Path $worker) "workerHosted"
Assert-True ((Get-Content $worker -Raw) -match "BackgroundService" -and (Get-Content $worker -Raw) -match "IgnoreQueryFilters") "workerTenantContract"
Assert-True ((Get-Content $di -Raw) -match "StorageMigrateWorker") "workerRegistered"

Assert-True ((Get-Content $seeder -Raw) -match "files\.storage\.migrate\.purge") "purgePermission"

Assert-True (Test-Path $panel) "adminPanel"
Assert-True ((Get-Content $adminPage -Raw) -match "StorageMigratePanel") "adminPanelWired"

Assert-True ((Get-Content $svc -Raw) -match "MIGRATE_TARGET_NOT_ACTIVE" -and (Get-Content $svc -Raw) -match "CapPerJob") "migrateGuards"

Write-Host "--- files regression ---"
& (Join-Path $root "tests\verify_files_spreadsheet.ps1")
if ($LASTEXITCODE -ne 0) { throw "filesRegression FAIL" }
Write-Host "PASS: filesRegression"

Write-Host "=== ALL PASS ==="
exit 0
