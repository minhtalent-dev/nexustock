# Script xác thực tự động Phase 46D - Package & Operational Line Imports
$ErrorActionPreference = "Stop"

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host "PHASE 46D VERIFIER - Package & Operational Line Imports" -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan

Write-Host "`n[1/4] Building solution backend..." -ForegroundColor Yellow
dotnet build Nexustock.sln --no-restore --configuration Release -m:1
if ($LASTEXITCODE -ne 0) {
    Write-Error "Backend build failed!"
    exit 1
}
Write-Host "  -> Backend build PASS" -ForegroundColor Green

Write-Host "`n[2/4] Running MasterData & Operational Line Import Integration Tests..." -ForegroundColor Yellow
dotnet test .\tests\Nexustock.MasterData.IntegrationTests\Nexustock.MasterData.IntegrationTests.csproj --no-build --no-restore --configuration Release -m:1
if ($LASTEXITCODE -ne 0) {
    Write-Error "Integration tests failed!"
    exit 1
}
Write-Host "  -> All integration tests PASS" -ForegroundColor Green

Write-Host "`n[3/4] Running Frontend TypeScript Typecheck..." -ForegroundColor Yellow
Push-Location .\frontend
try {
    npx tsc --noEmit
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Frontend TypeScript check failed!"
        exit 1
    }
    Write-Host "  -> Frontend TypeScript check PASS" -ForegroundColor Green

    Write-Host "`n[4/4] Running Frontend ESLint..." -ForegroundColor Yellow
    npm run lint -- --max-warnings 0
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Frontend ESLint failed!"
        exit 1
    }
    Write-Host "  -> Frontend ESLint PASS" -ForegroundColor Green
} finally {
    Pop-Location
}

Write-Host "`n=========================================================" -ForegroundColor Cyan
Write-Host "Phase 46D Verification 100% SUCCESSFUL!" -ForegroundColor Green
Write-Host "=========================================================" -ForegroundColor Cyan
