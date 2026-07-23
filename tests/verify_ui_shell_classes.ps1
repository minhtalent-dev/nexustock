# Phase 38 — verify UI shell classes (hardcode gate)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$appDir = Join-Path $root "frontend/src/app"
$allowPath = Join-Path $root "planning/evidence/phase_38/allowlist.md"
$allow = @()
if (Test-Path $allowPath) {
  Get-Content $allowPath | ForEach-Object {
    if ($_ -match '^\|\s*([^|]+?)\s*\|' -and $_ -notmatch 'Path|---|slots|Reason') {
      $p = $Matches[1].Trim().Trim('`')
      if ($p) { $allow += ($p -replace '\\', '/') }
    }
  }
}

$fail = New-Object System.Collections.Generic.List[object]
$zinc900 = 0
$files = Get-ChildItem -Path $appDir -Recurse -Include *.tsx,*.ts,*.css -File

foreach ($f in $files) {
  $rel = $f.FullName.Substring($root.Length + 1) -replace '\\', '/'
  $skip = $false
  foreach ($a in $allow) {
    if ($a -and $rel -like "*$a*") { $skip = $true; break }
  }
  if ($skip) { continue }
  $text = [System.IO.File]::ReadAllText($f.FullName)
  if ($text -match 'bg-\[#0a0a0a\]') {
    $fail.Add([pscustomobject]@{ file = $rel; pattern = 'bg-[#0a0a0a]' }) | Out-Null
  }
  if ($text -match 'bg-zinc-950') {
    $fail.Add([pscustomobject]@{ file = $rel; pattern = 'bg-zinc-950' }) | Out-Null
  }
  if ($text -match 'bg-zinc-900') { $zinc900++ }
}

Write-Host "=== verify_ui_shell_classes ==="
Write-Host "Allowlist entries: $($allow.Count)"
Write-Host "bg-zinc-900 files (report): $zinc900"

if ($fail.Count -gt 0) {
  Write-Host "FAIL $($fail.Count)"
  $fail | Format-Table -AutoSize
  exit 1
}

Write-Host "PASS: no bg-[#0a0a0a] / bg-zinc-950 in app/**"
exit 0
