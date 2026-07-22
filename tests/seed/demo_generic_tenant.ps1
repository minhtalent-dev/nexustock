# Phase 37 — Seed DEMO-GENERIC (logical pack trên tenant mặc định)
# REUSE products; đảm bảo LOC-SORT-01; ghi seed_summary.json
# CẤM: biến $pid

$ErrorActionPreference = "Stop"
$API_URL = if ($env:NEXUSTOCK_API_URL) { $env:NEXUSTOCK_API_URL } else { "http://localhost:5024/api" }
# tests/seed → repo root
$root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$outDir = Join-Path $root "planning\evidence\phase_37"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

Write-Host "=== demo_generic_tenant seed ===" -ForegroundColor Cyan
Write-Host "API: $API_URL"

$loginRes = Invoke-RestMethod -Uri "$API_URL/auth/login" -Method Post -Body (@{
    email = "admin@nexustock.com"
    password = "AdminSecret123!"
} | ConvertTo-Json) -ContentType "application/json"
$headers = @{ Authorization = "Bearer $($loginRes.token)" }

$products = Invoke-RestMethod -Uri "$API_URL/master-data/products" -Method Get -Headers $headers
$productIds = @()
foreach ($p in $products.items) {
    if ($p.isActive -and ($null -eq $p.isSerialTracked -or $p.isSerialTracked -eq $false)) {
        $productIds += $p.id.ToString()
        if ($productIds.Count -ge 5) { break }
    }
}
# Fallback: mọi product active (môi trường có thể chỉ còn SKU serial)
if ($productIds.Count -lt 1) {
    foreach ($p in $products.items) {
        if ($p.isActive) {
            $productIds += $p.id.ToString()
            if ($productIds.Count -ge 5) { break }
        }
    }
}
if ($productIds.Count -lt 1 -and $products.items.Count -gt 0) {
    $productIds += $products.items[0].id.ToString()
}
if ($productIds.Count -lt 1) { throw "No products in master-data" }

$uoms = Invoke-RestMethod -Uri "$API_URL/master-data/uoms" -Method Get -Headers $headers
$uomId = $uoms.items[0].id.ToString()
$partners = Invoke-RestMethod -Uri "$API_URL/master-data/partners" -Method Get -Headers $headers
$partnerId = $partners.items[0].id.ToString()

$locationId = $null
$locations = Invoke-RestMethod -Uri "$API_URL/master-data/storage-locations" -Method Get -Headers $headers
foreach ($loc in $locations.items) {
    if ($loc.code -eq "LOC-SORT-01") { $locationId = $loc.id.ToString(); break }
}
try {
    $zones = Invoke-RestMethod -Uri "$API_URL/master-data/storage-zones" -Method Get -Headers $headers
    $zoneId = $zones.items[0].id.ToString()
    $createLocJson = "{`"zoneId`":`"$zoneId`",`"code`":`"LOC-SORT-01`",`"maxCapacity`":999999.0,`"maxVolume`":999999.0,`"xCoord`":0,`"yCoord`":0,`"zCoord`":0,`"length`":1.0,`"width`":1.0,`"height`":1.0,`"isLocked`":false,`"isActive`":true}"
    $created = Invoke-RestMethod -Uri "$API_URL/master-data/storage-locations" -Method Post -Body $createLocJson -ContentType "application/json; charset=utf-8" -Headers $headers
    if ($created.id) { $locationId = $created.id.ToString() }
} catch {
    $locations2 = Invoke-RestMethod -Uri "$API_URL/master-data/storage-locations" -Method Get -Headers $headers
    foreach ($loc in $locations2.items) {
        if ($loc.code -eq "LOC-SORT-01") { $locationId = $loc.id.ToString(); break }
    }
}
if (-not $locationId) { $locationId = $locations.items[0].id.ToString() }

# Second location for move target — capacity cao (tránh LOCATION_OVER_CAPACITY)
$toLocationId = $null
try {
    $zones = Invoke-RestMethod -Uri "$API_URL/master-data/storage-zones" -Method Get -Headers $headers
    $zoneId = $zones.items[0].id.ToString()
    $createDest = "{`"zoneId`":`"$zoneId`",`"code`":`"LOC-L3-DEST`",`"maxCapacity`":999999.0,`"maxVolume`":999999.0,`"xCoord`":1,`"yCoord`":0,`"zCoord`":0,`"length`":1.0,`"width`":1.0,`"height`":1.0,`"isLocked`":false,`"isActive`":true}"
    $createdDest = Invoke-RestMethod -Uri "$API_URL/master-data/storage-locations" -Method Post -Body $createDest -ContentType "application/json; charset=utf-8" -Headers $headers
    if ($createdDest.id) { $toLocationId = $createdDest.id.ToString() }
} catch {
    $locations3 = Invoke-RestMethod -Uri "$API_URL/master-data/storage-locations" -Method Get -Headers $headers
    foreach ($loc in $locations3.items) {
        if ($loc.code -eq "LOC-L3-DEST") { $toLocationId = $loc.id.ToString(); break }
    }
}
if (-not $toLocationId) {
    $locations3 = Invoke-RestMethod -Uri "$API_URL/master-data/storage-locations" -Method Get -Headers $headers
    foreach ($loc in $locations3.items) {
        if ($loc.id.ToString() -ne $locationId -and $loc.isActive -ne $false) {
            $toLocationId = $loc.id.ToString()
            break
        }
    }
}
if (-not $toLocationId) { $toLocationId = $locationId }

$summary = [ordered]@{
    tenantId = "00000000-0000-0000-0000-000000000001"
    pack = "DEMO-GENERIC"
    productIds = $productIds
    primaryProductId = $productIds[0]
    uomId = $uomId
    partnerId = $partnerId
    locationId = $locationId
    toLocationId = $toLocationId
    at = (Get-Date).ToString("o")
}
$summaryPath = Join-Path $outDir "seed_summary.json"
$summary | ConvertTo-Json -Depth 5 | Set-Content $summaryPath -Encoding utf8
Write-Host "PASS seed → $summaryPath (products=$($productIds.Count))" -ForegroundColor Green
exit 0
