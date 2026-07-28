# Phase 46B static verification gate
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

function Require-Content([string]$Path, [string[]]$Patterns) {
    $content = Get-Content (Join-Path $root $Path) -Raw
    foreach ($pattern in $Patterns) {
        if ($content -notmatch $pattern) { throw "Missing '$pattern' in $Path" }
    }
}

function Reject-Content([string]$Path, [string[]]$Patterns) {
    $content = Get-Content (Join-Path $root $Path) -Raw
    foreach ($pattern in $Patterns) {
        if ($content -match $pattern) { throw "Forbidden '$pattern' in $Path" }
    }
}

Require-Content "backend/modules/Nexustock.Modules.Files/Services/FileStorageService.cs" @(
    'header\[3\] != 0x46', 'header\[8\] != 0x57', 'header\[11\] != 0x50')
Require-Content "backend/modules/Nexustock.Modules.Files/Services/ThumbnailBackfillService.cs" @(
    '\.AsNoTracking\(\)', '\.ExecuteUpdateAsync\(', 'a\.TenantId == att\.TenantId',
    'a\.DeletedAt == null', 'a\.ThumbnailKey == null', 'a\.Provider == att\.Provider',
    'a\.StorageKey == att\.StorageKey', 'OperationCanceledException', 'ThrowIfCancellationRequested')

$filesModule = Join-Path $root "backend/modules/Nexustock.Modules.Files"
$logCalls = Get-ChildItem $filesModule -Recurse -Filter *.cs | Select-String -Pattern 'Log(Trace|Debug|Information|Warning|Error|Critical)'
$forbiddenTemplates = $logCalls | Where-Object { $_.Line -match '\{(Key|StorageKey|ThumbnailKey|Path|Url)\}' }
if ($forbiddenTemplates) { throw "Raw storage locator log template detected: $($forbiddenTemplates.Path -join ', ')" }

$registration = Get-Content (Join-Path $root "backend/Nexustock.Api/Infrastructure/ModuleServiceRegistration.cs") -Raw
$handlers = [regex]::Matches($registration, 'AddScoped<Nexustock\.Modules\.Files\.Services\.IEntityExistenceHandler,').Count
if ($handlers -ne 9) { throw "Expected 9 extended handler registrations, found $handlers" }

$handlerTypes = Get-ChildItem (Join-Path $root "backend/Nexustock.Api/ExistenceHandlers") -Filter *AttachmentExistenceHandler.cs |
    ForEach-Object { Get-Content $_.FullName -Raw }
foreach ($entityType in @('LOT','EXCEPTION','LPN','WAVE','PUTAWAY_PROPOSAL','CROSS_DOCK_CANDIDATE')) {
    if (($handlerTypes -join "`n") -notmatch ('"' + $entityType + '"')) { throw "Missing handler for $entityType" }
}

$uiMatches = Get-ChildItem (Join-Path $root "frontend/src") -Recurse -Filter *.tsx |
    Select-String -Pattern '<EntityAttachmentsPanel'
foreach ($entityType in @('LOT','EXCEPTION','LPN','WAVE','PUTAWAY_PROPOSAL','CROSS_DOCK_CANDIDATE')) {
    if (($uiMatches.Line -join "`n") -notmatch ('entityType="' + $entityType + '"')) { throw "Missing UI context for $entityType" }
}

Require-Content "frontend/src/features/files/attachment-thumbnail.tsx" @('AbortController', 'URL\.revokeObjectURL')
Require-Content "frontend/src/features/files/attachment-preview-dialog.tsx" @('AbortController', 'URL\.revokeObjectURL')
Require-Content "backend/modules/Nexustock.Modules.Files/Controllers/FilesController.cs" @('ETag', 'Cache-Control', 'X-Content-Type-Options')
Require-Content "tests/Nexustock.Files.IntegrationTests/ThumbnailServiceTests.cs" @('image/webp', 'CallerCancellationPropagates')

$locales = @('en','vi')
$keySets = foreach ($locale in $locales) {
    $json = Get-Content (Join-Path $root "frontend/messages/$locale/Common.json") -Raw | ConvertFrom-Json
    ,@($json.Common.files.PSObject.Properties.Name | Sort-Object)
}
if (Compare-Object $keySets[0] $keySets[1] -SyncWindow 0) { throw "Files i18n mismatch en/vi" }

Write-Host "PASS: Phase 46B static coverage gate" -ForegroundColor Green
