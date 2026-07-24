# Phase 43 — verify Ops Attachments + Master Spreadsheet IE
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

function Assert-True([bool]$cond, [string]$msg) {
  if (-not $cond) { throw "FAIL: $msg" }
  Write-Host "PASS: $msg"
}

Write-Host "=== verify_ops_attach_p43.ps1 Phase 43 ==="

# Helper to read text file cleanly
function Read-AllText([string]$path) {
  return [System.IO.File]::ReadAllText($path)
}

# 1. Existence Handlers in Backend
$inboundHandler = Join-Path $root "backend\Nexustock.Api\ExistenceHandlers\InboundOrderExistenceHandler.cs"
$rmaHandler = Join-Path $root "backend\Nexustock.Api\ExistenceHandlers\RmaRequestExistenceHandler.cs"
$qcHandler = Join-Path $root "backend\Nexustock.Api\ExistenceHandlers\QcResultExistenceHandler.cs"
$diReg = Join-Path $root "backend\Nexustock.Api\Infrastructure\ModuleServiceRegistration.cs"

Assert-True (Test-Path $inboundHandler) "InboundOrderExistenceHandler exists"
Assert-True (Test-Path $rmaHandler) "RmaRequestExistenceHandler exists"
Assert-True (Test-Path $qcHandler) "QcResultExistenceHandler exists"

$diText = Read-AllText $diReg
Assert-True ($diText -match "InboundOrderExistenceHandler") "InboundOrderExistenceHandler registered in DI"
Assert-True ($diText -match "RmaRequestExistenceHandler") "RmaRequestExistenceHandler registered in DI"
Assert-True ($diText -match "QcResultExistenceHandler") "QcResultExistenceHandler registered in DI"

# 2. Allowed Entities validation in AttachmentService
$attachService = Join-Path $root "backend\modules\Nexustock.Modules.Files\Services\AttachmentService.cs"
Assert-True (Test-Path $attachService) "AttachmentService exists"
$serviceText = Read-AllText $attachService
Assert-True ($serviceText -match "INBOUND_ORDER") "AttachmentService supports INBOUND_ORDER"
Assert-True ($serviceText -match "RMA_REQUEST") "AttachmentService supports RMA_REQUEST"
Assert-True ($serviceText -match "QC_RESULT") "AttachmentService supports QC_RESULT"

# 3. Master Spreadsheet Import/Export Backend
$importService = Join-Path $root "backend\modules\Nexustock.Modules.MasterData\Services\ImportService.cs"
$exportsController = Join-Path $root "backend\modules\Nexustock.Modules.MasterData\Controllers\ExportsController.cs"

$importText = Read-AllText $importService
Assert-True ($importText -match '"UOMS"') "ImportService supports UOMS template"
Assert-True ($importText -match '"WAREHOUSES"') "ImportService supports WAREHOUSES template"
Assert-True ($importText -match '"ZONES"') "ImportService supports ZONES template"
Assert-True ($importText -match '"REASONS"') "ImportService supports REASONS template"

$exportsText = Read-AllText $exportsController
Assert-True ($exportsText -match '"UOMS"') "ExportsController supports UOMS export"
Assert-True ($exportsText -match '"WAREHOUSES"') "ExportsController supports WAREHOUSES export"
Assert-True ($exportsText -match '"ZONES"') "ExportsController supports ZONES export"
Assert-True ($exportsText -match '"REASONS"') "ExportsController supports REASONS export"

# 4. Ops Exports Controller Backend
$opsExportsController = Join-Path $root "backend\Nexustock.Api\Controllers\OpsExportsController.cs"
Assert-True (Test-Path $opsExportsController) "OpsExportsController exists"
$opsText = Read-AllText $opsExportsController
Assert-True ($opsText -match "ops.export") "OpsExportsController checks ops.export permission"
Assert-True ($opsText -match "INBOUND_ORDERS") "OpsExportsController supports INBOUND_ORDERS"
Assert-True ($opsText -match "SHIPMENTS") "OpsExportsController supports SHIPMENTS"
Assert-True ($opsText -match "STOCKTAKES") "OpsExportsController supports STOCKTAKES"
Assert-True ($opsText -match "RMA") "OpsExportsController supports RMA"

# 5. UI Attachment Panels Integration
$inboundReceivePage = Join-Path $root "frontend\src\app\admin\inbound\[id]\receive\page.tsx"
$outboundPage = Join-Path $root "frontend\src\app\admin\outbound\page.tsx"
$stocktakeDetailPage = Join-Path $root "frontend\src\app\admin\inventory\stocktakes\[id]\page.tsx"
$rmaPage = Join-Path $root "frontend\src\app\admin\rma\page.tsx"
$qcDialog = Join-Path $root "frontend\src\features\qc\components\qc-result-dialog.tsx"

Assert-True ((Read-AllText $inboundReceivePage) -match "EntityAttachmentsPanel") "Inbound Receive page has EntityAttachmentsPanel"
Assert-True ((Read-AllText $outboundPage) -match "EntityAttachmentsPanel") "Outbound page has EntityAttachmentsPanel"
Assert-True ((Read-AllText $stocktakeDetailPage) -match "EntityAttachmentsPanel") "Stocktake Detail page has EntityAttachmentsPanel"
Assert-True ((Read-AllText $rmaPage) -match "EntityAttachmentsPanel") "RMA page has EntityAttachmentsPanel"
Assert-True ((Read-AllText $qcDialog) -match "EntityAttachmentsPanel") "QC dialog has EntityAttachmentsPanel"

# 6. UI Exports Buttons
$inboundListPage = Join-Path $root "frontend\src\app\admin\inbound\page.tsx"
$stocktakesListPage = Join-Path $root "frontend\src\app\admin\inventory\stocktakes\page.tsx"

Assert-True ((Read-AllText $inboundListPage) -match "OpsExportButtons") "Inbound list page has OpsExportButtons"
Assert-True ((Read-AllText $outboundPage) -match "OpsExportButtons") "Outbound list page has OpsExportButtons"
Assert-True ((Read-AllText $stocktakesListPage) -match "OpsExportButtons") "Stocktakes list page has OpsExportButtons"
Assert-True ((Read-AllText $rmaPage) -match "OpsExportButtons") "RMA page has OpsExportButtons"

# 7. UI i18n translations & import list options
$viCommon = Join-Path $root "frontend\messages\vi\Common.json"
$enCommon = Join-Path $root "frontend\messages\en\Common.json"
$importPage = Join-Path $root "frontend\src\app\master-data\import\page.tsx"

Assert-True ((Read-AllText $viCommon) -match "Tài liệu đính kèm") "vi/Common.json has files translations"
Assert-True ((Read-AllText $enCommon) -match "Attachments") "en/Common.json has files translations"

$impText = Read-AllText $importPage
Assert-True ($impText -match 'value="UOMS"') "Import page has UOMS option"
Assert-True ($impText -match 'value="WAREHOUSES"') "Import page has WAREHOUSES option"
Assert-True ($impText -match 'value="ZONES"') "Import page has ZONES option"
Assert-True ($impText -match 'value="REASONS"') "Import page has REASONS option"

Write-Host "=== ALL PASS ==="
exit 0
