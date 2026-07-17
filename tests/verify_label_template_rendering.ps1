# Script verify Label Template Renderer pure logic

$testDir = "D:\1_Project\48_Nexustock\backend\modules\Nexustock.Modules.LabelPrinting\Tests"
Write-Host "Running Label Template Renderer tests..." -ForegroundColor Cyan

Push-Location $testDir
try {
    dotnet run
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Renderer tests failed!"
        exit 1
    }
} finally {
    Pop-Location
}

Write-Host "Label Template Renderer tests PASSED!" -ForegroundColor Green
exit 0
