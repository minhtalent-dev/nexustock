# Strict Verifier for Phase 46E — RF Camera + Full P43–P45 Acceptance
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = Resolve-Path (Join-Path $scriptDir "..") | Select-Object -ExpandProperty Path
Set-Location $root

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "  PHASE 46E STRICT VERIFIER & ACCEPTANCE AGGREGATOR" -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

$evidenceDir = Join-Path $root "planning/evidence/phase_46_dbm"
if (-not (Test-Path $evidenceDir)) {
    New-Item -ItemType Directory -Path $evidenceDir -Force | Out-Null
}

$global:results = [System.Collections.Generic.List[PSCustomObject]]::new()
$global:overallSuccess = $true

function Record-Gate($gateId, $gateName, $scriptBlock) {
    $startTime = Get-Date
    Write-Host "`n---> Running Gate: $gateId — $gateName" -ForegroundColor Yellow
    $logPath = Join-Path $evidenceDir "gate_${gateId}.log"
    
    try {
        $LASTEXITCODE = 0
        $output = & $scriptBlock 2>&1 | Out-String
        $nativeExitCode = $LASTEXITCODE
        Write-Host $output
        [System.IO.File]::WriteAllText($logPath, $output, [System.Text.Encoding]::UTF8)
        if ($nativeExitCode -ne 0) {
            throw "Native command failed with exit code $nativeExitCode`n$output"
        }

        $duration = [math]::Round(((Get-Date) - $startTime).TotalMilliseconds)
        Write-Host ">>> Gate $gateId PASSED (${duration}ms)" -ForegroundColor Green
        $global:results.Add([PSCustomObject]@{
            id = $gateId
            name = $gateName
            status = "PASSED"
            exitCode = 0
            durationMs = $duration
            logPath = "planning/evidence/phase_46_dbm/gate_${gateId}.log"
        })
    }
    catch {
        $duration = [math]::Round(((Get-Date) - $startTime).TotalMilliseconds)
        $errText = $_.ToString()
        Write-Host ">>> Gate $gateId FAILED (${duration}ms): $errText" -ForegroundColor Red
        [System.IO.File]::WriteAllText($logPath, $errText, [System.Text.Encoding]::UTF8)
        $global:overallSuccess = $false
        $global:results.Add([PSCustomObject]@{
            id = $gateId
            name = $gateName
            status = "FAILED"
            exitCode = 1
            durationMs = $duration
            logPath = "planning/evidence/phase_46_dbm/gate_${gateId}.log"
            error = $errText
        })
    }
}

# 1. Static RF contract assertions
Record-Gate "G01_STATIC_RF" "Static RF Contract & Opt-in Assertions" {
    $panelFile = Join-Path $root "frontend/src/features/files/entity-attachments-panel.tsx"
    $rfCompFile = Join-Path $root "frontend/src/features/files/rf-camera-upload.tsx"
    $valFile = Join-Path $root "frontend/src/features/files/rf-upload-validation.ts"

    if (-not (Test-Path $panelFile)) { throw "Missing $panelFile" }
    if (-not (Test-Path $rfCompFile)) { throw "Missing $rfCompFile" }
    if (-not (Test-Path $valFile)) { throw "Missing $valFile" }

    $panelContent = Get-Content $panelFile -Raw
    $rfContent = Get-Content $rfCompFile -Raw

    if ($panelContent -notmatch "enableRfCapture\s*=\s*false") { throw "EntityAttachmentsPanel must default enableRfCapture to false" }
    if ($rfContent -notmatch "capture=`"environment`"") { throw "RfCameraUpload missing capture='environment'" }
    if ($rfContent -notmatch "accept=`"image/\*`"") { throw "RfCameraUpload missing accept='image/*'" }
    if ($rfContent -notmatch "URL\.revokeObjectURL") { throw "RfCameraUpload missing URL.revokeObjectURL cleanup" }

    # Check 4 opt-in pages
    $optInPages = @(
        "frontend\src\app\admin\inbound\[id]\receive\page.tsx",
        "frontend\src\app\admin\outbound\page.tsx",
        "frontend\src\app\admin\exceptions\page.tsx",
        "frontend\src\app\admin\lpn\page.tsx"
    )

    foreach ($page in $optInPages) {
        $pPath = Join-Path $root $page
        if (-not (Test-Path -LiteralPath $pPath)) { throw "Missing opt-in page: $page" }
        $pContent = Get-Content -LiteralPath $pPath -Raw
        if ($pContent -notmatch "enableRfCapture=\{\s*true\s*\}") {
            throw "Page $page does not explicitly opt-in enableRfCapture={true}"
        }
    }
    Write-Host "Static RF contract and 4 opt-in pages verified!" -ForegroundColor Green
}

# 2. EN/VI i18n parity
Record-Gate "G02_I18N_PARITY" "EN/VI Common.files Parity" {
    $enFile = Join-Path $root "frontend/messages/en/Common.json"
    $viFile = Join-Path $root "frontend/messages/vi/Common.json"

    $en = Get-Content $enFile -Raw | ConvertFrom-Json
    $vi = Get-Content $viFile -Raw | ConvertFrom-Json

    $enKeys = $en.Common.files.psobject.properties.Name
    $viKeys = $vi.Common.files.psobject.properties.Name

    $missingInVi = $enKeys | Where-Object { $_ -notin $viKeys }
    $missingInEn = $viKeys | Where-Object { $_ -notin $enKeys }

    if ($missingInVi.Count -gt 0) { throw "Keys missing in VI: $($missingInVi -join ', ')" }
    if ($missingInEn.Count -gt 0) { throw "Keys missing in EN: $($missingInEn -join ', ')" }

    $rfKeys = @("takePhotoBtn", "chooseFileBtn", "offlineWarning", "fileTooLarge", "invalidCameraImage", "unsupportedType", "localPreviewAlt", "sourceCamera", "sourceFile")
    foreach ($rk in $rfKeys) {
        if ($rk -notin $enKeys) { throw "Missing RF i18n key in EN: $rk" }
        if ($rk -notin $viKeys) { throw "Missing RF i18n key in VI: $rk" }
    }
    Write-Host "EN/VI i18n parity 100% verified!" -ForegroundColor Green
}

# 3. RF Validation Self-Test
Record-Gate "G03_SELF_TEST" "RF Upload Validation Self-Test (Node)" {
    Set-Location (Join-Path $root "frontend")
    try {
        node --no-warnings --experimental-strip-types src/features/files/rf-upload-validation.self-test.ts
    } finally {
        Set-Location $root
    }
}

# 4. Frontend Typecheck + ESLint
Record-Gate "G04_FE_TYPECHECK_LINT" "Frontend TypeScript Typecheck & ESLint" {
    Set-Location (Join-Path $root "frontend")
    try {
        npx tsc --noEmit
        npm run lint -- --max-warnings 0
    } finally {
        Set-Location $root
    }
}

# 5. Backend Solution Build
Record-Gate "G05_BE_BUILD" "Backend Solution Build (Release)" {
    dotnet build .\Nexustock.sln --configuration Release --no-restore --warnaserror -m:1
}

# 6. Backend Integration Tests Phase46E
Record-Gate "G06_BE_TESTS" "Phase46E Integration Tests" {
    dotnet test tests/Nexustock.MasterData.IntegrationTests/Nexustock.MasterData.IntegrationTests.csproj --filter "Category=Phase46E" -c Release --no-build --no-restore -m:1
}

# 7. Regression Verification (P43, P46A, P46B, P46C, P46D)
Record-Gate "G07_REGRESSION_P43" "P43 Regression Verifier" {
    $script = Join-Path $root "tests/verify_ops_attach_p43.ps1"
    powershell -NoProfile -ExecutionPolicy Bypass -Command "& '$script'"
    if ($LASTEXITCODE -ne 0) { throw "P43 regression failed with exit code $LASTEXITCODE" }
}

Record-Gate "G08_REGRESSION_P46A" "P46A Regression Verifier" {
    $script = Join-Path $root "tests/verify_attachment_content_p46a.ps1"
    powershell -NoProfile -ExecutionPolicy Bypass -Command "& '$script'"
    if ($LASTEXITCODE -ne 0) { throw "P46A regression failed with exit code $LASTEXITCODE" }
}

Record-Gate "G09_REGRESSION_P46B" "P46B Regression Verifier" {
    $script = Join-Path $root "tests/verify_attachment_coverage_p46b.ps1"
    powershell -NoProfile -ExecutionPolicy Bypass -Command "& '$script'"
    if ($LASTEXITCODE -ne 0) { throw "P46B regression failed with exit code $LASTEXITCODE" }
}

Record-Gate "G10_REGRESSION_P46C" "P46C Regression Verifier" {
    $script = Join-Path $root "tests/verify_spreadsheet_exports_p46c.ps1"
    powershell -NoProfile -ExecutionPolicy Bypass -Command "& '$script'"
    if ($LASTEXITCODE -ne 0) { throw "P46C regression failed with exit code $LASTEXITCODE" }
}

Record-Gate "G11_REGRESSION_P46D" "P46D Regression Verifier" {
    $script = Join-Path $root "tests/verify_package_line_imports_p46d.ps1"
    powershell -NoProfile -ExecutionPolicy Bypass -Command "& '$script'"
    if ($LASTEXITCODE -ne 0) { throw "P46D regression failed with exit code $LASTEXITCODE" }
}

# Export machine-readable results
$jsonOutput = Join-Path $root "planning/evidence/phase_46_dbm/automated_results.json"
[array]$global:results | ConvertTo-Json -Depth 5 | Set-Content -Path $jsonOutput -Encoding UTF8

Write-Host "`n=================================================================" -ForegroundColor Cyan
if ($global:overallSuccess) {
    Write-Host "  ALL GATES PASSED! Machine-readable results saved to:" -ForegroundColor Green
    Write-Host "  $jsonOutput" -ForegroundColor Green
    Write-Host "=================================================================" -ForegroundColor Cyan
    exit 0
} else {
    Write-Host "  VERIFICATION FAILED! Check gate logs in $evidenceDir" -ForegroundColor Red
    Write-Host "=================================================================" -ForegroundColor Cyan
    exit 1
}
