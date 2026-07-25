# BRQOSH i18n Coverage Audit — G-27 gate (foundation + runtime sweep)
param(
    [string]$Root = (Join-Path $PSScriptRoot "..\..\src\Brqosh.Desktop"),
    [string]$ReportPath = (Join-Path $PSScriptRoot "..\..\dist\evidence\i18n-audit\coverage-report.txt")
)

$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path (Split-Path $ReportPath) | Out-Null

$arabic = '[\u0600-\u06FF]'
$xamlFiles = Get-ChildItem -Path $Root -Recurse -Filter *.xaml
$csFiles = Get-ChildItem -Path $Root -Recurse -Filter *.cs

$xamlArabicLines = 0
$csLocaleT = 0
$csArabicLines = 0

foreach ($f in $xamlFiles) {
    $lines = Select-String -Path $f.FullName -Pattern $arabic -AllMatches
    if ($lines) { $xamlArabicLines += $lines.Count }
}

foreach ($f in $csFiles) {
    $t = Select-String -Path $f.FullName -Pattern 'LocaleManager\.T\(' -AllMatches
    if ($t) { $csLocaleT += $t.Count }
    $ar = Select-String -Path $f.FullName -Pattern $arabic -AllMatches
    if ($ar) { $csArabicLines += $ar.Count }
}

$navEn = Select-String -Path (Join-Path $Root "Navigation\NavigationCatalog.cs") -Pattern 'PageTitlesEn' -Quiet
$registry = Join-Path $Root "Ui\UiBilingualRegistry.g.cs"
$localizer = Join-Path $Root "Ui\XamlUiLocalizer.cs"
$missingFile = Join-Path $PSScriptRoot "..\..\dist\evidence\i18n-audit\registry-missing.txt"
$missingCount = 0
if (Test-Path $missingFile) {
    $missingCount = @(Get-Content $missingFile -Encoding UTF8 | Where-Object { $_.Trim().Length -gt 0 }).Count
}

$report = @"
BRQOSH i18n Coverage Audit — $(Get-Date -Format 'yyyy-MM-dd HH:mm')
Root: $Root

XAML lines with Arabic (static):  $xamlArabicLines
CS lines with Arabic:             $csArabicLines
CS LocaleManager.T calls:         $csLocaleT
Navigation PageTitlesEn:          $(if ($navEn) { 'PRESENT' } else { 'MISSING' })
XamlUiLocalizer:                  $(if (Test-Path $localizer) { 'PRESENT' } else { 'MISSING' })
UiBilingualRegistry.g.cs:         $(if (Test-Path $registry) { 'PRESENT' } else { 'MISSING' })
Registry untranslated strings:    $missingCount

Gate G-27: nav bilingual + runtime registry 100% coverage.
"@

$report | Set-Content -Path $ReportPath -Encoding UTF8
Write-Host $report
Write-Host "`nReport: $ReportPath"

if (-not $navEn) { exit 1 }
if (-not (Test-Path $registry)) { exit 1 }
if (-not (Test-Path $localizer)) { exit 1 }
if ($missingCount -gt 0) { exit 1 }
exit 0
