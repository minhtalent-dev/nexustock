# PowerShell verification script for Phase 46A: Secure Attachment Content & Preview
$ErrorActionPreference = "Stop"

$originalDir = Get-Location

function Invoke-Gate {
    param (
        [string]$Message,
        [scriptblock]$Script
    )
    Write-Host "`n=== Gate: $Message ===" -ForegroundColor Cyan
    try {
        & $Script
        if ($LASTEXITCODE -ne 0 -and $null -ne $LASTEXITCODE) {
            throw "Command failed with exit code $LASTEXITCODE"
        }
        Write-Host "PASS: $Message" -ForegroundColor Green
    }
    catch {
        Write-Host "FAIL: $Message" -ForegroundColor Red
        Write-Host $_ -ForegroundColor Red
        Set-Location $originalDir
        Exit 1
    }
}

try {
    # Step 1: Static Code Inspection
    Invoke-Gate "Static Code Inspection" {
        $panelPath = "frontend\src\features\files\entity-attachments-panel.tsx"
        $panelContent = Get-Content $panelPath -Raw

        if ($panelContent -like "*href={item.url}*") {
            throw "EntityAttachmentsPanel still contains direct link to item.url!"
        }
        if ($panelContent -like "*href=*uploads*") {
            throw "EntityAttachmentsPanel contains direct /uploads link!"
        }
        if ($panelContent -notlike "*useConfirmDialog*") {
            throw "EntityAttachmentsPanel does not use useConfirmDialog!"
        }
        if ($panelContent -notlike "*files.delete*" -or $panelContent -notlike "*files.upload*" -or $panelContent -notlike "*files.read*") {
            throw "EntityAttachmentsPanel is missing files.read, files.delete or files.upload permission checks!"
        }
        $rfPath = "frontend\src\features\files\rf-camera-upload.tsx"
        $rfContent = if (Test-Path $rfPath) { Get-Content $rfPath -Raw } else { "" }
        if ($panelContent -notlike '*fileInputRef.current.value = ""*' -and $rfContent -notlike '*cameraInputRef.current.value = ""*') {
            throw "File input reset is missing!"
        }

        $previewPath = "frontend\src\features\files\attachment-preview-dialog.tsx"
        $previewContent = Get-Content $previewPath -Raw
        if ($previewContent -notlike "*AbortController*") {
            throw "AttachmentPreviewDialog does not use AbortController!"
        }

        $qcDialogPath = "frontend\src\features\qc\components\qc-result-dialog.tsx"
        $qcDialogContent = Get-Content $qcDialogPath -Raw
        if ($qcDialogContent -notlike "*createdResultId*") {
            throw "QcResultDialog does not use createdResultId state!"
        }
        if ($qcDialogContent -notlike "*confirmCloseWithErrors*") {
            throw "QcResultDialog does not check confirmCloseWithErrors!"
        }

        $crudPath = "frontend\src\features\master-data\master-data-crud.tsx"
        $crudContent = Get-Content $crudPath -Raw
        if ($crudContent -notlike "*createdItem*") {
            throw "MasterDataCrudPage does not use createdItem state!"
        }
        if ($crudContent -notlike "*confirmClosePendingTitle*" -or $crudContent -notlike "*if (createdItem)*") {
            throw "MasterDataCrudPage does not guard closing a pending create session!"
        }

        $productsPath = "frontend\src\app\master-data\products\page.tsx"
        $productsContent = Get-Content $productsPath -Raw
        if ($productsContent -notlike "*pendingOwnerEntityId*" -or $productsContent -notlike "*ownerId === pendingOwnerEntityId*") {
            throw "ProductsPage does not enforce pending attachment ownership!"
        }

        $contentTestsPath = "tests\Nexustock.MasterData.IntegrationTests\FilesAttachmentContentTests.cs"
        $contentTests = Get-Content $contentTestsPath -Raw
        if ($contentTests -notlike '*Assert.Single(response.Headers.GetValues("X-Content-Type-Options"))*' -or $contentTests -notlike '*response.Headers.GetValues("Cache-Control")*') {
            throw "Attachment content tests do not assert security header values!"
        }
        if ($contentTests -notlike '*disposition=attachment*' -or $contentTests -notlike '*ReadAsByteArrayAsync*') {
            throw "Attachment content tests do not cover authenticated download bytes!"
        }

        $qcReadServicePath = "backend\modules\Nexustock.Modules.Qc\Services\QcAttachmentReadService.cs"
        $qcReadServiceContent = Get-Content $qcReadServicePath -Raw
        if ($qcReadServiceContent -notlike "*entitiesWithAttachmentRows*") {
            throw "QcAttachmentReadService does not use entitiesWithAttachmentRows logic!"
        }
    }

    # Step 2: Translation Key Parity
    Invoke-Gate "Translation Key Parity (Common.json)" {
        $vi = Get-Content "frontend\messages\vi\Common.json" -Raw | ConvertFrom-Json
        $en = Get-Content "frontend\messages\en\Common.json" -Raw | ConvertFrom-Json

        $viKeys = $vi.Common.files.PSObject.Properties.Name | Sort-Object
        $enKeys = $en.Common.files.PSObject.Properties.Name | Sort-Object

        $diff = Compare-Object $viKeys $enKeys
        if ($diff) {
            throw "Translation keys mismatch in Common.json: $($diff | Out-String)"
        }
    }

    Invoke-Gate "Translation Key Parity (Features.json)" {
        $vi = Get-Content "frontend\messages\vi\Features.json" -Raw | ConvertFrom-Json
        $en = Get-Content "frontend\messages\en\Features.json" -Raw | ConvertFrom-Json

        $viKeys = $vi.Features.qc.PSObject.Properties.Name | Sort-Object
        $enKeys = $en.Features.qc.PSObject.Properties.Name | Sort-Object

        $diff = Compare-Object $viKeys $enKeys
        if ($diff) {
            throw "Translation keys mismatch in Features.json: $($diff | Out-String)"
        }
    }

    # Step 3: Run Frontend Self-Test
    Invoke-Gate "Frontend Partial-Bind Self-Test" {
        Set-Location "frontend"
        node --no-warnings --experimental-strip-types src/features/files/bind-pending-attachments.self-test.ts
        Set-Location ".."
    }

    # Step 4: Frontend Lint Gate
    Invoke-Gate "Frontend Lint Gate" {
        Set-Location "frontend"
        npm run lint
        Set-Location ".."
    }

    # Step 5: Frontend TypeScript compilation
    Invoke-Gate "Frontend TypeScript Compilation" {
        Set-Location "frontend"
        npm exec -- tsc --noEmit
        Set-Location ".."
    }

    # Step 6: Backend Integration Tests compilation
    Invoke-Gate "Backend Integration Tests Compilation" {
        dotnet build tests/Nexustock.MasterData.IntegrationTests -c Release
    }

    # Step 7: Run Backend Integration Tests
    Invoke-Gate "Backend Integration Tests Execution" {
        dotnet test tests/Nexustock.MasterData.IntegrationTests -c Release --no-build --filter "Category=Phase46A" --logger "trx;LogFileName=phase46a-tests.trx"
    }

    # Step 8: Save Evidence
    Invoke-Gate "Export Test Evidence" {
        $evidenceDir = "planning\evidence\phase_46a_rp45"
        if (-not (Test-Path $evidenceDir)) {
            New-Item -ItemType Directory -Path $evidenceDir -Force | Out-Null
        }
        Copy-Item "tests\Nexustock.MasterData.IntegrationTests\TestResults\phase46a-tests.trx" -Destination "$evidenceDir\phase46a-tests.trx" -Force
    }

    Write-Host "`n=== [SUCCESS] All Phase 46A Automated Checks Passed! ===" -ForegroundColor Green
    Exit 0
}
finally {
    Set-Location $originalDir
}
