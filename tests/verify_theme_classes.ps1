# Phase 39 — fail hardcode dark-only classes in migrate scopes
$ErrorActionPreference = "Stop"
$roots = @(
  "frontend/src/app",
  "frontend/src/components/nav",
  "frontend/src/components/mobile",
  "frontend/src/features",
  "frontend/src/components/app-sidebar.tsx"
)
$allowlistPath = "planning/evidence/phase_39/allowlist.md"
$allow = @()
if (Test-Path $allowlistPath) {
  $allow = @(Select-String -Path $allowlistPath -Pattern '`([^`]+)`' -AllMatches | ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups[1].Value } | Where-Object { $_ -like "*.tsx" -or $_ -like "*.ts" })
}

function Get-Files($root) {
  if (Test-Path $root -PathType Leaf) { return @((Resolve-Path $root).Path) }
  return @(Get-ChildItem -Path $root -Recurse -Include *.tsx,*.ts -File | ForEach-Object { $_.FullName })
}

$fail = 0
$report = @()
$patterns = @(
  @{ Id = "a0"; Needle = "bg-[#0a0a0a]" },
  @{ Id = "a111"; Needle = "bg-[#111]" },
  @{ Id = "z950"; Needle = "bg-zinc-950" },
  @{ Id = "tw"; Needle = "text-white" },
  @{ Id = "layoutDark"; Needle = "antialiased dark" },
  @{ Id = "toasterDark"; Needle = 'theme="dark"' }
)

$files = $roots | ForEach-Object { Get-Files $_ } | Select-Object -Unique
foreach ($p in $patterns) {
  foreach ($f in $files) {
    $rel = $f.Replace((Get-Location).Path + [IO.Path]::DirectorySeparatorChar, "").Replace("\", "/")
    if ($allow -contains $rel -or ($allow | Where-Object { $rel.EndsWith($_) })) { continue }
    $text = [IO.File]::ReadAllText($f)
    if ($text.Contains($p.Needle)) {
      if ($p.Id -eq "layoutDark" -and $rel -notlike "*/layout.tsx") { continue }
      if ($p.Id -eq "toasterDark" -and $rel -notlike "*/layout.tsx" -and $rel -notlike "*/theme-aware-toaster.tsx") { continue }
      if ($p.Id -eq "toasterDark" -and $rel -like "*/theme-aware-toaster.tsx") { continue }
      $fail++
      $report += "FAIL $($p.Id): $rel"
    }
  }
}

$report | ForEach-Object { Write-Host $_ }
if ($fail -gt 0) {
  Write-Host "VERIFY_THEME FAIL=$fail"
  exit 1
}
Write-Host "VERIFY_THEME PASS"
exit 0
