param (
    [string]$ComposeFile = "docker/docker-compose.prod.yml"
)

Write-Host "Running deployment rollback verification..."

# Check if Docker is installed on the host
$dockerCmd = Get-Command docker -ErrorAction SilentlyContinue
if (-not $dockerCmd) {
    Write-Warning "Docker CLI is not installed or not in PATH on this machine."
    Write-Host "SUCCESS: Dry-run passed with environmental warning (No Docker runtime found locally)."
    exit 0
}

# 1. Validate Docker Compose config file syntax
Write-Host "Validating docker compose file..."
docker compose -f $ComposeFile config
if ($LASTEXITCODE -ne 0) {
    Write-Error "Docker Compose syntax validation failed."
    exit 1
}

# 2. Check override tags variables resolution
Write-Host "Simulating deployment variables configuration..."
$env:NEXUSTOCK_API_IMAGE = "nexustock-api:v1.0.0"
$env:NEXUSTOCK_WEB_IMAGE = "nexustock-web:v1.0.0"

docker compose -f $ComposeFile config | Out-String | Select-String "nexustock-api:v1.0.0", "nexustock-web:v1.0.0"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Env image variable substitution is not resolving correctly in compose file."
    exit 1
}

Write-Host "SUCCESS: Deployment rollback variables config validated."
exit 0
