param (
    [string]$BaseUrl = "http://localhost:5024"
)

Write-Host "Running production health checks verification..."

# 1. Verify Live Endpoint
$liveUrl = "$BaseUrl/health/live"
Write-Host "Calling GET $liveUrl"
try {
    $res = Invoke-WebRequest -Uri $liveUrl -UseBasicParsing -TimeoutSec 10
    $body = $res.Content.Trim()
    
    if ($res.StatusCode -ne 200) {
        Write-Error "Live check failed with status code: $($res.StatusCode)"
        exit 1
    }
    
    # Assert plain text Healthy or JSON payload status
    if ($body -ne "Healthy" -and $body -ne "healthy") {
        try {
            $json = ConvertFrom-Json $body
            if ($json.status -ne "Healthy" -and $json.status -ne "healthy") {
                Write-Error "Live check status is not Healthy: $body"
                exit 1
            }
        } catch {
            Write-Error "Live check status is not Healthy: $body"
            exit 1
        }
    }
    
    Write-Host "SUCCESS: Live check healthy."
} catch {
    Write-Error "Exception calling Live endpoint: $_"
    exit 1
}

# 2. Verify Ready Endpoint
$readyUrl = "$BaseUrl/health/ready"
Write-Host "Calling GET $readyUrl"
try {
    $res = Invoke-WebRequest -Uri $readyUrl -UseBasicParsing -TimeoutSec 10
    $body = $res.Content
    
    if ($res.StatusCode -ne 200) {
        Write-Error "Ready check failed with status code: $($res.StatusCode)"
        exit 1
    }
    
    # Verify secrecy (no connectionString, password, token, host details)
    $sensitivePatterns = @("password", "connectionString", "Host=", "Username=", "port=", "database=", "redis_connection")
    foreach ($pattern in $sensitivePatterns) {
        if ($body -match $pattern) {
            Write-Error "SECURITY WARNING: Ready response contains sensitive configuration data: $pattern"
            exit 1
        }
    }
    
    Write-Host "SUCCESS: Ready check healthy and secure."
} catch {
    Write-Error "Exception calling Ready endpoint: $_"
    exit 1
}

Write-Host "All health check validations passed successfully."
exit 0
