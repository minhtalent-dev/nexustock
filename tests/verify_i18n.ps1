# Phase 31 / 31a / 32 / 33 — verify i18n catalogs & inventory
param(
  [ValidateSet("31", "31a", "32", "33")]
  [string]$Phase = "31a"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$frontend = Join-Path $root "frontend"
$appRoot = Join-Path $frontend "src\app"
$messages = Join-Path $frontend "messages"
$evidence = Join-Path $root "planning\evidence\phase_31a"

$CATALOG_MODULES = @(
  "Common", "Language", "Sidebar", "Breadcrumb", "Errors",
  "Home", "Login", "HealthUi", "Admin", "Features", "MasterData"
)
if ($Phase -eq "33") {
  $CATALOG_MODULES += "Mobile"
}

Write-Host "=== verify_i18n.ps1 Phase $Phase ==="

function Get-MergedFlat([string]$locale) {
  $helper = Join-Path $PSScriptRoot "helpers\merge_i18n_catalogs.js"
  $json = node $helper $messages $locale
  if ($LASTEXITCODE -ne 0) { throw "Merge load failed for $locale : $json" }
  return $json | ConvertFrom-Json
}

# --- Module tree ---
foreach ($loc in @("vi", "en")) {
  $dir = Join-Path $messages $loc
  if (-not (Test-Path $dir)) { throw "Missing messages/$loc" }
  foreach ($m in $CATALOG_MODULES) {
    $f = Join-Path $dir "$m.json"
    if (-not (Test-Path $f)) { throw "Missing $loc/$m.json" }
    if ($m -notmatch '^[A-Z][A-Za-z0-9]*$') { throw "Non-PascalCase module: $m" }
  }
}
Write-Host "PASS: module files PascalCase $($CATALOG_MODULES.Count)x2"

if (Test-Path (Join-Path $messages "vi.json")) { throw "Monolith messages/vi.json still exists" }
if (Test-Path (Join-Path $messages "en.json")) { throw "Monolith messages/en.json still exists" }
Write-Host "PASS: monolith removed"

$vi = Get-MergedFlat "vi"
$en = Get-MergedFlat "en"
if (-not $vi.ok -or -not $en.ok) { throw "Merge failed" }
if ($vi.kebabCount -gt 0) { throw "Kebab keys VI remain" }
if ($en.kebabCount -gt 0) { throw "Kebab keys EN remain" }
if ($vi.missing.Count -gt 0 -or $en.missing.Count -gt 0) { throw "Missing Errors.codes" }
if (-not $vi.generic -or -not $en.generic) { throw "Missing Errors.messages.generic" }

$onlyVi = @($vi.keys | Where-Object { $en.keys -notcontains $_ })
$onlyEn = @($en.keys | Where-Object { $vi.keys -notcontains $_ })
if ($onlyVi.Count -or $onlyEn.Count) {
  throw "Parity fail onlyVi=$($onlyVi.Count) onlyEn=$($onlyEn.Count)"
}
Write-Host "PASS: catalogs parity count=$($vi.count)"

# --- Foundation ---
$adminPages = @(Get-ChildItem (Join-Path $appRoot "admin") -Recurse -Filter page.tsx)
$shell = @("page.tsx", "login\page.tsx", "health-ui\page.tsx") | Where-Object { Test-Path (Join-Path $appRoot $_) }
$foundation = $adminPages.Count + $shell.Count
if ($Phase -eq "33") {
  $allPages = @(Get-ChildItem $appRoot -Recurse -Filter page.tsx)
  if ($allPages.Count -ne 59) { throw "Expected 59 pages, found $($allPages.Count)" }
  Write-Host "PASS: inventory 59/59"
} else {
  if ($foundation -ne 44) { throw "Expected 44 P31 pages, found $foundation" }
  Write-Host "PASS: inventory 44/44"
}

$i18nPkg = Join-Path $frontend "node_modules\next-intl\package.json"
if (-not (Test-Path $i18nPkg)) { throw "next-intl not installed" }
Write-Host "PASS: next-intl installed"

$middleware = Join-Path $frontend "src\middleware.ts"
$switcher = Join-Path $frontend "src\components\language-switcher.tsx"
if (-not (Test-Path $middleware)) { throw "Missing middleware.ts" }
if (-not (Test-Path $switcher)) { throw "Missing language-switcher.tsx" }
Write-Host "PASS: foundation files"

$request = Get-Content (Join-Path $frontend "src\i18n\request.ts") -Raw
if ($request -notmatch 'loadMessages') { throw "request.ts must use loadMessages" }
if ($request -match 'messages/\$\{locale\}\.json' -or $request -match 'messages/vi\.json') {
  throw "request.ts still imports monolith"
}
Write-Host "PASS: request.ts loadMessages"

if ($Phase -eq "31a" -or $Phase -eq "32" -or $Phase -eq "33") {
  foreach ($f in @("load-messages.ts", "merge-messages.ts", "catalog-modules.ts")) {
    if (-not (Test-Path (Join-Path $frontend "src\i18n\$f"))) { throw "Missing i18n/$f" }
  }
  Write-Host "PASS: i18n helpers"

  $bc = Get-Content (Join-Path $frontend "src\components\breadcrumb-nav.tsx") -Raw
  if ($bc -notmatch 'segmentToKey' -and $bc -notmatch 'replace\(/-\(\[a-z\]\)/g') {
    throw "breadcrumb-nav missing segmentToKey"
  }
  Write-Host "PASS: breadcrumb segmentToKey"

  if ($Phase -eq "31a") {
    foreach ($e in @("keys_before.txt", "keys_after.txt", "hygiene_rename.json")) {
      if (-not (Test-Path (Join-Path $evidence $e))) { throw "Missing evidence $e" }
    }
    Write-Host "PASS: evidence phase_31a"
  }

  $load = Get-Content (Join-Path $frontend "src\i18n\load-messages.ts") -Raw
  if ($load -match 'import\(`\$\{') { throw "load-messages must not use dynamic import template" }
  if ($load -notmatch 'MasterData') { throw "load-messages.ts missing MasterData static import" }
  if ($Phase -eq "33" -and $load -notmatch 'Mobile') { throw "load-messages.ts missing Mobile static import" }
  Write-Host "PASS: static import map (+MasterData$(if ($Phase -eq '33') { '+Mobile' } else { '' }))"
}

if ($Phase -eq "32" -or $Phase -eq "33") {
  $mdAreas = @("common", "products", "uoms", "warehouses", "zones", "locations", "partners", "reasons", "import")
  foreach ($loc in @("vi", "en")) {
    $mdPath = Join-Path $messages "$loc\MasterData.json"
    $md = Get-Content $mdPath -Raw | ConvertFrom-Json
    $roots = @($md.PSObject.Properties.Name)
    if ($roots.Count -ne 1 -or $roots[0] -ne "MasterData") { throw "Bad root in $loc/MasterData.json" }
    foreach ($area in $mdAreas) {
      if (-not $md.MasterData.PSObject.Properties.Name -contains $area) {
        throw "Missing MasterData.$area in $loc"
      }
    }
  }
  Write-Host "PASS: MasterData areas ($($mdAreas.Count))"

  $mdPages = @(Get-ChildItem (Join-Path $appRoot "master-data") -Recurse -Filter page.tsx)
  if ($mdPages.Count -ne 8) { throw "Expected 8 master-data pages, found $($mdPages.Count)" }
  Write-Host "PASS: inventory master-data 8/8"

  $mdKebab = @($vi.keys | Where-Object { $_ -like "MasterData.*" -and ($_ -split '\.' | Where-Object { $_ -match '-' }).Count -gt 0 })
  if ($mdKebab.Count -gt 0) { throw "Kebab under MasterData.*: $($mdKebab -join ', ')" }
  Write-Host "PASS: no kebab under MasterData.*"

  $catalogModulesSrc = Get-Content (Join-Path $frontend "src\i18n\catalog-modules.ts") -Raw
  if ($catalogModulesSrc -notmatch "'MasterData'") { throw "catalog-modules.ts missing MasterData" }
  Write-Host "PASS: catalog-modules MasterData"
}

if ($Phase -eq "33") {
  if ($CATALOG_MODULES.Count -ne 12) { throw "Expected 12 catalog modules, found $($CATALOG_MODULES.Count)" }
  Write-Host "PASS: 12 catalog modules"

  $mobileAreas = @("common", "shell", "home", "picking", "movement", "replenishment", "lpn", "serial", "tasks")
  foreach ($loc in @("vi", "en")) {
    $mobPath = Join-Path $messages "$loc\Mobile.json"
    $mob = Get-Content $mobPath -Raw | ConvertFrom-Json
    $roots = @($mob.PSObject.Properties.Name)
    if ($roots.Count -ne 1 -or $roots[0] -ne "Mobile") { throw "Bad root in $loc/Mobile.json" }
    foreach ($area in $mobileAreas) {
      if (-not ($mob.Mobile.PSObject.Properties.Name -contains $area)) {
        throw "Missing Mobile.$area in $loc"
      }
    }
  }
  Write-Host "PASS: Mobile areas ($($mobileAreas.Count))"

  $mobilePages = @(Get-ChildItem (Join-Path $appRoot "mobile") -Recurse -Filter page.tsx)
  if ($mobilePages.Count -ne 7) { throw "Expected 7 mobile pages, found $($mobilePages.Count)" }
  Write-Host "PASS: inventory mobile 7/7"

  $mobKebab = @($vi.keys | Where-Object { $_ -like "Mobile.*" -and ($_ -split '\.' | Where-Object { $_ -match '-' }).Count -gt 0 })
  if ($mobKebab.Count -gt 0) { throw "Kebab under Mobile.*: $($mobKebab -join ', ')" }
  Write-Host "PASS: no kebab under Mobile.*"

  $catalogModulesSrc = Get-Content (Join-Path $frontend "src\i18n\catalog-modules.ts") -Raw
  if ($catalogModulesSrc -notmatch "'Mobile'") { throw "catalog-modules.ts missing Mobile" }
  $mergeHelper = Get-Content (Join-Path $PSScriptRoot "helpers\merge_i18n_catalogs.js") -Raw
  if ($mergeHelper -notmatch "'Mobile'") { throw "merge_i18n_catalogs.js missing Mobile" }
  Write-Host "PASS: catalog-modules + merge helper Mobile"

  $shellSrc = Get-Content (Join-Path $frontend "src\components\mobile\mobile-shell.tsx") -Raw
  if ($shellSrc -notmatch 'LanguageSwitcher') { throw "mobile-shell missing LanguageSwitcher" }
  if ($shellSrc -notmatch "Mobile\.shell") { throw "mobile-shell missing Mobile.shell translations" }
  Write-Host "PASS: MobileShell + LanguageSwitcher"

  $scanSrc = Get-Content (Join-Path $frontend "src\components\mobile\scan-input.tsx") -Raw
  if ($scanSrc -notmatch "Mobile\.common") { throw "scan-input missing Mobile.common" }
  Write-Host "PASS: scan-input i18n"

  $titles = @{
    "picking" = "Lấy hàng xuất kho (Picking)"
    "movement" = "Dịch chuyển kho (Movement)"
    "replenishment" = "Bổ sung Pick Face"
    "lpn" = "Di chuyển Pallet LPN"
    "serial" = "Nhận mã Serial"
    "tasks" = "Gợi ý việc tiếp theo"
  }
  $viMob = Get-Content (Join-Path $messages "vi\Mobile.json") -Raw | ConvertFrom-Json
  foreach ($area in $titles.Keys) {
    $actual = $viMob.Mobile.$area.page.title
    if ($actual -ne $titles[$area]) {
      throw "page.title SoT mismatch $area : expected '$($titles[$area])' got '$actual'"
    }
  }
  Write-Host "PASS: page.title disk SoT"

  $mobileGetHttp = Select-String -Path (Join-Path $appRoot "mobile\**\*.tsx") -Pattern "getHttpErrorMessage" -SimpleMatch -ErrorAction SilentlyContinue
  $mobileCompGetHttp = Select-String -Path (Join-Path $frontend "src\components\mobile\*.tsx") -Pattern "getHttpErrorMessage" -SimpleMatch -ErrorAction SilentlyContinue
  if ($mobileGetHttp -or $mobileCompGetHttp) {
    throw "mobile still uses getHttpErrorMessage"
  }
  Write-Host "PASS: mobile free of getHttpErrorMessage"

  foreach ($rel in @(
    "mobile\page.tsx",
    "mobile\picking\page.tsx",
    "mobile\movement\page.tsx",
    "mobile\replenishment\page.tsx",
    "mobile\lpn\page.tsx",
    "mobile\serial\page.tsx",
    "mobile\tasks\next\page.tsx"
  )) {
    $src = Get-Content (Join-Path $appRoot $rel) -Raw
    if ($src -notmatch "useTranslations") { throw "$rel missing useTranslations" }
    if ($src -notmatch "resolveApiError" -and $rel -ne "mobile\page.tsx") {
      throw "$rel missing resolveApiError"
    }
  }
  $tasksSrc = Get-Content (Join-Path $appRoot "mobile\tasks\next\page.tsx") -Raw
  if ($tasksSrc -notmatch "MobileShell") { throw "tasks/next missing MobileShell" }
  Write-Host "PASS: seven mobile pages wired"
}

# deepMerge snippet
$mergeOk = node -e "function deepMerge(t,s){for(const [k,v] of Object.entries(s)){if(v&&typeof v==='object'&&!Array.isArray(v)&&t[k]&&typeof t[k]==='object'&&!Array.isArray(t[k]))deepMerge(t[k],v);else t[k]=v;}return t;} const r=deepMerge({a:{b:1}},{a:{c:2}}); if(r.a.b!==1||r.a.c!==2) process.exit(1); console.log('ok');"
if ($LASTEXITCODE -ne 0) { throw "deepMerge snippet failed" }
Write-Host "PASS: deepMerge snippet"

Write-Host "=== ALL PASS Phase $Phase ==="
exit 0
