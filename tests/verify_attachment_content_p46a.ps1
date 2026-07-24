# PowerShell verification script for Phase 46A: Secure Attachment Content & Preview

$ErrorActionPreference = "Stop"

Write-Host "=== Step 1: Static Code Inspection ===" -ForegroundColor Cyan

$panelPath = "d:\1_Project\48_Nexustock\frontend\src\features\files\entity-attachments-panel.tsx"
$panelContent = Get-Content $panelPath -Raw

if ($panelContent -like "*href={item.url}*") {
    Write-Error "FAIL: EntityAttachmentsPanel still contains direct link to item.url!"
} else {
    Write-Host "PASS: EntityAttachmentsPanel does not use direct item.url link." -ForegroundColor Green
}

if ($panelContent -like "*href=*uploads*") {
    Write-Error "FAIL: EntityAttachmentsPanel contains direct /uploads link!"
} else {
    Write-Host "PASS: EntityAttachmentsPanel has no direct /uploads link." -ForegroundColor Green
}

Write-Host "`n=== Step 2: Translation Key Parity ===" -ForegroundColor Cyan

$vi = Get-Content "d:\1_Project\48_Nexustock\frontend\messages\vi\Common.json" -Raw | ConvertFrom-Json
$en = Get-Content "d:\1_Project\48_Nexustock\frontend\messages\en\Common.json" -Raw | ConvertFrom-Json

$viKeys = $vi.Common.files.PSObject.Properties.Name | Sort-Object
$enKeys = $en.Common.files.PSObject.Properties.Name | Sort-Object

$diff = Compare-Object $viKeys $enKeys
if ($diff) {
    Write-Error "FAIL: Translation keys mismatch between vi and en: $($diff | Out-String)"
} else {
    Write-Host "PASS: Translation keys match between vi and en ($($viKeys.Count) keys)." -ForegroundColor Green
}

Write-Host "`n=== Step 3: Backend Module Compilation ===" -ForegroundColor Cyan
Set-Location "d:\1_Project\48_Nexustock\backend\modules\Nexustock.Modules.Files"
$buildOutput = dotnet build --no-incremental 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Error "FAIL: Backend Files module build failed: `n$buildOutput"
} else {
    Write-Host "PASS: Backend Files module compiled successfully." -ForegroundColor Green
}

Write-Host "`n=== All Phase 46A Automated Checks Passed! ===" -ForegroundColor Green
