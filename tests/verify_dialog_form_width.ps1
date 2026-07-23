# Phase 40 — fail dangerous dialog form density patterns
# Contract: phase_40 §22.5 + §24.2 (+ hotfix bareMaxW 2026-07-23)
$ErrorActionPreference = "Stop"
$root = Get-Location
$src = Join-Path $root "frontend/src"
$allowlistPath = "planning/evidence/phase_40/allowlist.md"
$allow = @()
if (Test-Path $allowlistPath) {
  $allow = @(
    Select-String -Path $allowlistPath -Pattern '`([^`]+)`' -AllMatches |
      ForEach-Object { $_.Matches } |
      ForEach-Object { $_.Groups[1].Value } |
      Where-Object { $_ -like "*.tsx" -or $_ -like "*.ts" }
  )
}

$excludeNames = @("dialog.tsx", "alert-dialog.tsx")
$files = @(Get-ChildItem -Path $src -Recurse -Include *.tsx, *.ts -File |
  Where-Object { $excludeNames -notcontains $_.Name })

$fail = 0
$report = @()

function RelPath([string]$full) {
  return $full.Replace((Get-Location).Path + [IO.Path]::DirectorySeparatorChar, "").Replace("\", "/")
}

foreach ($f in $files) {
  $rel = RelPath $f.FullName
  if ($allow -contains $rel -or ($allow | Where-Object { $rel.EndsWith($_) })) { continue }

  $text = [IO.File]::ReadAllText($f.FullName)
  $hasDialog = $text.Contains("DialogContent")
  # bare max-w-sm only (không khớp sm:max-w-sm)
  $hasMaxSm = [regex]::IsMatch($text, '(?<!:)max-w-sm\b')
  $selectCount = ([regex]::Matches($text, "<select\b")).Count
  $inputCount = ([regex]::Matches($text, "<Input\b")).Count

  # g12: DialogContent + grid-cols-12 without responsive breakpoint on grid
  if ($hasDialog -and $text.Contains("grid-cols-12")) {
    $hasBp = [regex]::IsMatch($text, "(sm|md|lg|xl):grid-cols")
    if (-not $hasBp) {
      $fail++
      $report += "FAIL g12: $rel"
    }
  }

  # w24: className="w-24" or 'w-24' but not min-w-24 / TableHead context lines
  $w24Matches = [regex]::Matches($text, 'className="[^"]*\bw-24\b[^"]*"')
  foreach ($m in $w24Matches) {
    $val = $m.Value
    if ($val -match "min-w-24") { continue }
    $idx = $m.Index
    $start = [Math]::Max(0, $idx - 80)
    $ctx = $text.Substring($start, [Math]::Min(200, $text.Length - $start))
    if ($ctx -match "TableHead") { continue }
    $fail++
    $report += "FAIL w24: $rel :: $val"
  }

  # maxSmDense: bare max-w-sm + (≥2 select or Input)
  if ($hasMaxSm -and ($selectCount + $inputCount) -ge 2) {
    $fail++
    $report += "FAIL maxSmDense: $rel (select=$selectCount input=$inputCount)"
  }

  # bareMaxW: DialogContent max-w-(md|lg|…) không có sm:/md:/lg: → default sm:max-w-sm đè
  if ($hasDialog) {
    $dialogBlocks = [regex]::Matches($text, 'DialogContent\s+className="([^"]*)"')
    foreach ($db in $dialogBlocks) {
      $cls = $db.Groups[1].Value
      $hasBare = [regex]::IsMatch($cls, '(?<![a-z0-9]:)max-w-(md|lg|xl|2xl|3xl|4xl|5xl|6xl|7xl)\b')
      $hasResponsive = [regex]::IsMatch($cls, '(sm|md|lg|xl):max-w-')
      if ($hasBare -and -not $hasResponsive) {
        $fail++
        $report += "FAIL bareMaxW: $rel :: $cls"
      }
    }
  }
}

$report | ForEach-Object { Write-Host $_ }
if ($fail -gt 0) {
  Write-Host "VERIFY_DIALOG_WIDTH FAIL=$fail"
  exit 1
}
Write-Host "VERIFY_DIALOG_WIDTH PASS"
exit 0
