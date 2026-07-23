# Phase 41 — verify Files + Spreadsheet + Storage Hub (static)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

function Assert-True([bool]$cond, [string]$msg) {
  if (-not $cond) { throw "FAIL: $msg" }
  Write-Host "PASS: $msg"
}

Write-Host "=== verify_files_spreadsheet.ps1 Phase 41 ==="

$filesRoot = Join-Path $root "backend\modules\Nexustock.Modules.Files"
$provider = Join-Path $filesRoot "Providers\IObjectStorageProvider.cs"
$settingsEntity = Join-Path $filesRoot "Entities\FileAttachment.cs"
$settingsEntity2 = Get-ChildItem -Path $filesRoot -Recurse -Filter "*FileStorageSettings*" | Select-Object -First 1
$exports = Join-Path $root "backend\modules\Nexustock.Modules.MasterData\Controllers\ExportsController.cs"
$mdCsproj = Join-Path $root "backend\modules\Nexustock.Modules.MasterData\Nexustock.Modules.MasterData.csproj"
$adminPage = Join-Path $root "frontend\src\app\admin\settings\storage\page.tsx"
$products = Join-Path $root "frontend\src\app\master-data\products\page.tsx"
$importPage = Join-Path $root "frontend\src\app\master-data\import\page.tsx"
$qc = Join-Path $root "frontend\src\features\qc\components\qc-result-dialog.tsx"
$panel = Join-Path $root "frontend\src\features\files\entity-attachments-panel.tsx"

Assert-True (Test-Path $provider) "filesModule IObjectStorageProvider"
$provText = Get-Content $provider -Raw
Assert-True ($provText -match "OpenReadAsync") "OpenReadAsync on provider"

Assert-True (Test-Path $settingsEntity) "file_attachments entity"
Assert-True ($null -ne $settingsEntity2) "settingsTable FileStorageSettings"

$localText = Get-Content (Join-Path $filesRoot "Providers\IObjectStorageProvider.cs") -Raw
$localText2 = Get-Content (Join-Path $filesRoot "Services\FileStorageService.cs") -Raw
Assert-True (($localText -match 'LOCAL') -or ($localText2 -match 'LOCAL')) "defaultLocal LOCAL available"

$mdText = Get-Content $mdCsproj -Raw
Assert-True ($mdText -match "ClosedXML") "closedXml package"

Assert-True (Test-Path $exports) "exportsApi ExportsController"

Assert-True (Test-Path $adminPage) "adminStoragePage"

$prodText = Get-Content $products -Raw
Assert-True ($prodText -match "EntityAttachmentsPanel|entity-attachments-panel") "productPanel"

$impText = Get-Content $importPage -Raw
Assert-True ($impText -match '\.xlsx') "xlsxImport"

$qcText = Get-Content $qc -Raw
Assert-True ($qcText -match 'storage/upload|files/upload') "qcCompat"

Assert-True (Test-Path $panel) "EntityAttachmentsPanel exists"

Write-Host "=== ALL PASS ==="
exit 0
