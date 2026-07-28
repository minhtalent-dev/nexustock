# Script xác thực tự động Phase 46C
$ErrorActionPreference = "Stop"

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "PHASE 46C VERIFIER - Master & Ops Exports" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

Write-Host "`n[1/3] Building solution..." -ForegroundColor Yellow
dotnet build Nexustock.sln --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed!"
    exit 1
}
Write-Host "  -> Solution build PASS" -ForegroundColor Green

Write-Host "`n[2/3] Running integration tests..." -ForegroundColor Yellow
dotnet test .\tests\Nexustock.MasterData.IntegrationTests\Nexustock.MasterData.IntegrationTests.csproj --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Error "Integration tests failed!"
    exit 1
}
Write-Host "  -> All integration tests PASS" -ForegroundColor Green

Write-Host "`n[3/3] Running Frontend typecheck & lint..." -ForegroundColor Yellow
Push-Location .\frontend
try {
    npx tsc --noEmit
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Frontend TypeScript check failed!"
        exit 1
    }
    Write-Host "  -> Frontend TypeScript check PASS" -ForegroundColor Green

    npm run lint
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Frontend lint failed!"
        exit 1
    }
    Write-Host "  -> Frontend ESLint PASS" -ForegroundColor Green
} finally {
    Pop-Location
}

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "Phase 46C Verification 100% SUCCESSFUL!" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Cyan
