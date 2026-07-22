# Phase 35 — verify Admin Nav Ops ↔ Modules lens (static)
# SoT: planning/phases/phase_35_admin_nav_ops_modules_lens.md §21.5

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$frontend = Join-Path $root "frontend"
$navDir = Join-Path $frontend "src\components\nav"
$sidebar = Join-Path $frontend "src\components\app-sidebar.tsx"
$enSidebar = Join-Path $frontend "messages\en\Sidebar.json"
$viSidebar = Join-Path $frontend "messages\vi\Sidebar.json"
$mobileApp = Join-Path $frontend "src\app\mobile"

Write-Host "=== verify_nav_lens.ps1 Phase 35 ==="

function Assert-True([bool]$cond, [string]$msg) {
  if (-not $cond) { throw "FAIL: $msg" }
  Write-Host "PASS: $msg"
}

# 1 — files exist
$required = @(
  (Join-Path $navDir "nav-registry.ts"),
  (Join-Path $navDir "nav-groups-modules.ts"),
  (Join-Path $navDir "nav-groups-ops.ts"),
  (Join-Path $navDir "nav-mode.ts"),
  $sidebar
)
foreach ($f in $required) {
  Assert-True (Test-Path $f) "exists $($f.Replace($root, '.'))"
}

# 2 — sidebar imports registry; no inline navGroupDefs
$sidebarText = Get-Content $sidebar -Raw
Assert-True ($sidebarText -match 'nav-groups-modules') "app-sidebar imports modules groups"
Assert-True ($sidebarText -match 'nav-groups-ops') "app-sidebar imports ops groups"
Assert-True ($sidebarText -match 'nav-mode') "app-sidebar imports nav-mode"
Assert-True ($sidebarText -notmatch 'const navGroupDefs') "no inline navGroupDefs"
Assert-True ($sidebarText -match 'nav-mode-modules') "testid nav-mode-modules"
Assert-True ($sidebarText -match 'nav-mode-ops') "testid nav-mode-ops"

# 3 — registry count 44
$regText = Get-Content (Join-Path $navDir "nav-registry.ts") -Raw
Assert-True ($regText -match 'NAV_LINK_COUNT\s*=\s*44') "NAV_LINK_COUNT = 44"
Assert-True ($regText -match '@nav-registry-count 44') "@nav-registry-count 44"
$idMatches = [regex]::Matches($regText, 'id:\s*"([^"]+)"')
Assert-True ($idMatches.Count -eq 44) "registry id count = $($idMatches.Count) (expect 44)"
$ids = @($idMatches | ForEach-Object { $_.Groups[1].Value })
Assert-True (($ids | Select-Object -Unique).Count -eq 44) "registry ids unique"

$hrefMap = @{}
foreach ($m in [regex]::Matches($regText, 'id:\s*"([^"]+)"[^\}]*href:\s*"([^"]+)"', [System.Text.RegularExpressions.RegexOptions]::Singleline)) {
  $hrefMap[$m.Groups[1].Value] = $m.Groups[2].Value
}
Assert-True ($hrefMap.Count -eq 44) "href map size 44"

function Get-LinkIdsFromGroups([string]$path) {
  $text = Get-Content $path -Raw
  $all = New-Object System.Collections.Generic.List[string]
  foreach ($m in [regex]::Matches($text, 'linkIds:\s*\[([^\]]+)\]')) {
    $inner = $m.Groups[1].Value
    foreach ($idMatch in [regex]::Matches($inner, '"([^"]+)"')) {
      $all.Add($idMatch.Groups[1].Value) | Out-Null
    }
  }
  return ,$all.ToArray()
}

$modIds = Get-LinkIdsFromGroups (Join-Path $navDir "nav-groups-modules.ts")
$opsIds = Get-LinkIdsFromGroups (Join-Path $navDir "nav-groups-ops.ts")
Assert-True ($modIds.Count -eq 44) "modules flatten count = $($modIds.Count)"
Assert-True ($opsIds.Count -eq 44) "ops flatten count = $($opsIds.Count)"
Assert-True (($modIds | Select-Object -Unique).Count -eq 44) "modules no duplicate ids"
Assert-True (($opsIds | Select-Object -Unique).Count -eq 44) "ops no duplicate ids"

$modHrefs = @($modIds | ForEach-Object { $hrefMap[$_] } | Sort-Object -Unique)
$opsHrefs = @($opsIds | ForEach-Object { $hrefMap[$_] } | Sort-Object -Unique)
Assert-True ($modHrefs.Count -eq 44) "modules href unique 44"
Assert-True ($opsHrefs.Count -eq 44) "ops href unique 44"
$diff = Compare-Object $modHrefs $opsHrefs
Assert-True ($null -eq $diff -or $diff.Count -eq 0) "Modules href set ≡ Ops href set"

# 4–6 polish A
$modText = Get-Content (Join-Path $navDir "nav-groups-modules.ts") -Raw
Assert-True ($modText -notmatch 'titleKey:\s*"utilities"') "no utilities group"
Assert-True ($modText -notmatch 'titleKey:\s*"partners"[^\{]*rma') "RMA not asserted via partners block loosely"
# partners linkIds must not include rma
$partnersBlock = [regex]::Match($modText, 'titleKey:\s*"partners",\s*linkIds:\s*\[([^\]]+)\]').Groups[1].Value
Assert-True ($partnersBlock -notmatch '"rma"') "rma not in partners"
$outboundBlock = [regex]::Match($modText, 'titleKey:\s*"outbound",\s*linkIds:\s*\[([^\]]+)\]').Groups[1].Value
Assert-True ($outboundBlock -match '"rma"') "rma in outbound"
$laborBlock = [regex]::Match($modText, 'titleKey:\s*"labor",\s*linkIds:\s*\[([^\]]+)\]').Groups[1].Value
Assert-True ($laborBlock -match '"labor"' -and $laborBlock -match '"laborSessions"' -and $laborBlock -match '"taskInterleaving"') "labor group complete"
$materialsBlock = [regex]::Match($modText, 'titleKey:\s*"materials",\s*linkIds:\s*\[([^\]]+)\]').Groups[1].Value
Assert-True ($materialsBlock -match '"import"') "import in materials"

# 7 — i18n keys
$i18nKeys = @(
  "navMode.modules", "navMode.ops", "navMode.ariaLabel",
  "groups.labor", "groups.opsInbound", "groups.opsOutbound", "groups.opsInventory", "groups.opsOther"
)
foreach ($locPath in @($enSidebar, $viSidebar)) {
  $json = Get-Content $locPath -Raw | ConvertFrom-Json
  $s = $json.Sidebar
  Assert-True ($null -ne $s.navMode.modules -and $s.navMode.modules.Length -gt 0) "$locPath navMode.modules"
  Assert-True ($null -ne $s.navMode.ops -and $s.navMode.ops.Length -gt 0) "$locPath navMode.ops"
  Assert-True ($null -ne $s.navMode.ariaLabel -and $s.navMode.ariaLabel.Length -gt 0) "$locPath navMode.ariaLabel"
  Assert-True ($null -ne $s.groups.labor -and $s.groups.labor.Length -gt 0) "$locPath groups.labor"
  Assert-True ($null -ne $s.groups.opsInbound) "$locPath groups.opsInbound"
  Assert-True ($null -ne $s.groups.opsOutbound) "$locPath groups.opsOutbound"
  Assert-True ($null -ne $s.groups.opsInventory) "$locPath groups.opsInventory"
  Assert-True ($null -ne $s.groups.opsOther) "$locPath groups.opsOther"
  Assert-True ($null -ne $s.groups.utilities) "$locPath groups.utilities kept"
}

# 8 — mobile untouched
$mobileHits = @()
if (Test-Path $mobileApp) {
  $mobileHits = @(Get-ChildItem $mobileApp -Recurse -Include *.tsx,*.ts -ErrorAction SilentlyContinue |
    Select-String -Pattern 'nav-mode|nav-groups-modules|nav-registry' -SimpleMatch:$false)
}
Assert-True ($mobileHits.Count -eq 0) "mobile does not import nav lens"

Write-Host ""
Write-Host "=== verify_nav_lens.ps1 ALL PASS ==="
exit 0
