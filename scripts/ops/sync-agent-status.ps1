# Sync STATUS.yaml test counts from dotnet test (single source of truth)
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
)

$ErrorActionPreference = "Stop"
Push-Location $RepoRoot

# (CEEF-229 · م2-٤) لا تخطّي: هذا النصُّ **مصدرُ الحقيقة لعدّاد الاختبارات** في
# STATUS.yaml. وعددٌ يُنشر عن تشغيلٍ متخطٍّ يصف حزمةً غير التي تُشحن.
Remove-Item Env:\BRQOSH_SKIP_STRESS -ErrorAction SilentlyContinue
$env:BRQOSH_TEST = "1"

Write-Host "Running dotnet test to refresh agent STATUS.yaml..."
$raw = dotnet test Brqosh.sln -c Release --nologo -v q 2>&1 | Out-String

Pop-Location

$byProject = [ordered]@{}
$total = 0
$failed = 0

$projMatches = [regex]::Matches(
    $raw,
    'Passed!\s+-\s+Failed:\s+(\d+),\s+Passed:\s+(\d+),\s+Skipped:\s+\d+,\s+Total:\s+\d+.*?-\s+(Brqosh\.\w+\.Tests\.dll)'
)

foreach ($pm in $projMatches) {
    $projFailed = [int]$pm.Groups[1].Value
    $passed = [int]$pm.Groups[2].Value
    $dll = $pm.Groups[3].Value
    $name = $dll -replace "Brqosh\.", "" -replace "\.Tests\.dll", ""
    $byProject[$name] = $passed
    $total += $passed
    $failed += $projFailed
}

if ($total -eq 0) {
    Write-Error "Could not parse test counts from dotnet test output."
}

$statusPath = Get-ChildItem -Path (Join-Path $RepoRoot ".cursor\skills") -Filter "STATUS.yaml" -Recurse |
    Where-Object { $_.DirectoryName -match "brqosh-skills-hub" } |
    Select-Object -First 1 -ExpandProperty FullName

if (-not $statusPath) {
    Write-Error "STATUS.yaml not found under .cursor/skills"
}

$today = (Get-Date).ToString("yyyy-MM-dd")
$domain = if ($byProject.Contains("Domain")) { $byProject["Domain"] } else { 0 }
$desktop = if ($byProject.Contains("Desktop")) { $byProject["Desktop"] } else { 0 }
$api = if ($byProject.Contains("Api")) { $byProject["Api"] } else { 0 }
$application = if ($byProject.Contains("Application")) { $byProject["Application"] } else { 0 }

$content = @"
version: v16
updated: "$today"

skills_architecture: "SKILL.md + reference modules; leak corpus grep-only; archive deleted"

tests:
  total: $total
  failed: $failed
  breakdown:
    Domain: $domain
    Desktop: $desktop
    Api: $api
    Application: $application

phase:
  current: "CEEF-138 Supreme A-to-Z audit complete · O-01..O-05 live external"
  in_repo_closed: "V12 + V13 + CEEF-120..138 in-repo"
  next: "docs/ops/WAKE_UP_CHECKLIST.md · O-01 OTP · API P0 hardening"

ceef:
  latest_in_repo: 138
  open_items: "docs/OPEN_ITEMS.md"

verify:
  build: "dotnet build Brqosh.sln -c Release --nologo"
  test: "dotnet test Brqosh.sln -c Release --nologo"
  gates: "scripts/ops/run-external-gates.ps1"
  i18n: "scripts/ops/i18n-coverage-audit.ps1"
  env:
    BRQOSH_TEST: "1"
"@

Set-Content -Path $statusPath -Value $content -Encoding UTF8
Write-Host "Updated $statusPath - total tests: $total"
if ($failed -gt 0) { exit 1 }
