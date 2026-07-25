#Requires -Version 5.1
<#
.SYNOPSIS
  UI UX Pro Max — فحص ما قبل التسليم لـ BRQOSH Desktop (WPF).

.DESCRIPTION
  يطبّق قائمة التحقق من skill UI UX Pro Max v2.0 على ملفات المستودع.
  لا يستبدل اختبار بصري يدوي — يكتشف انتهاكات آلية (emoji، موارد مفقودة).
#>
param(
    [switch]$Strict
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
if (-not (Test-Path (Join-Path $root "Brqosh.sln"))) {
    throw "Cannot locate repo root from $PSScriptRoot"
}

$desktop = Join-Path $root "src\Brqosh.Desktop"
$theme = Join-Path $desktop "Themes\BrqoshTheme.xaml"
$failures = @()
$warnings = @()

function Add-Fail($msg) { $script:failures += $msg }
function Add-Warn($msg) { $script:warnings += $msg }

Write-Host "=== UI UX Pro Max Audit ===" -ForegroundColor Cyan
Write-Host "Root: $root"

# 1) Design system artifacts
$master = Join-Path $root "design-system\MASTER.md"
if (-not (Test-Path $master)) { Add-Fail "Missing design-system/MASTER.md" }
else { Write-Host "[OK] design-system/MASTER.md" -ForegroundColor Green }

foreach ($page in @("dashboard.md", "shell.md", "login.md")) {
    $p = Join-Path $root "design-system\pages\$page"
    if (-not (Test-Path $p)) { Add-Warn "Missing design-system/pages/$page" }
    else { Write-Host "[OK] design-system/pages/$page" -ForegroundColor Green }
}

# 2) Theme tokens (KPI + focus)
$themeText = Get-Content $theme -Raw -Encoding UTF8
$requiredKeys = @(
    "KpiMetricCard", "KpiMonogramBadge", "AppFocusVisual",
    "NavMonogramBadge", "LoadingStatePanel", "EmptyStatePanel"
)
foreach ($key in $requiredKeys) {
    if ($themeText -notmatch "x:Key=`"$key`"") {
        Add-Fail "BrqoshTheme.xaml missing style: $key"
    } else {
        Write-Host "[OK] Theme style: $key" -ForegroundColor Green
    }
}

# 3) No emoji in NavigationCatalog (heuristic: no surrogate pairs / common emoji blocks)
$navFile = Join-Path $desktop "Navigation\NavigationCatalog.cs"
$navBytes = [System.IO.File]::ReadAllBytes($navFile)
$hasEmoji = $false
for ($i = 0; $i -lt $navBytes.Length - 3; $i++) {
    if ($navBytes[$i] -eq 0xF0 -and $navBytes[$i+1] -ge 0x9F) { $hasEmoji = $true; break }
}
if ($hasEmoji) {
    Add-Fail "NavigationCatalog.cs may contain emoji (UTF-8 4-byte sequences)"
} else {
    Write-Host "[OK] NavigationCatalog: no emoji" -ForegroundColor Green
}

# 4) NavIconFactory present
$navIcon = Join-Path $desktop "Navigation\NavIconFactory.cs"
if (-not (Test-Path $navIcon)) { Add-Fail "Missing NavIconFactory.cs" }
else { Write-Host "[OK] NavIconFactory.cs" -ForegroundColor Green }

# 5) KPI builder present
$kpi = Join-Path $desktop "Ui\KpiMetricCardBuilder.cs"
if (-not (Test-Path $kpi)) { Add-Fail "Missing KpiMetricCardBuilder.cs" }
else { Write-Host "[OK] KpiMetricCardBuilder.cs" -ForegroundColor Green }

# 5b) DataGrid RTL column layout helper (fixes star-column gaps)
$gridRtl = Join-Path $desktop "Ui\DataGridRtlHelper.cs"
if (-not (Test-Path $gridRtl)) { Add-Fail "Missing DataGridRtlHelper.cs" }
elseif ($themeText -notmatch "DataGridRtlHelper\.EnableLayout") {
    Add-Fail "BrqoshTheme.xaml must enable ui:DataGridRtlHelper.EnableLayout on DataGrid style"
} else {
    Write-Host "[OK] DataGridRtlHelper + theme EnableLayout" -ForegroundColor Green
}

# 6) Dashboard uses WrapPanel + no emoji in code-behind
$dashCs = Join-Path $desktop "Views\Pages\DashboardPage.xaml.cs"
$dashBytes = [System.IO.File]::ReadAllBytes($dashCs)
$dashEmoji = $false
for ($i = 0; $i -lt $dashBytes.Length - 3; $i++) {
    if ($dashBytes[$i] -eq 0xF0 -and $dashBytes[$i+1] -ge 0x9F) { $dashEmoji = $true; break }
}
if ($dashEmoji) {
    Add-Fail "DashboardPage.xaml.cs may contain emoji"
} else {
    Write-Host "[OK] DashboardPage: no emoji KPI icons" -ForegroundColor Green
}

$dashXaml = Join-Path $desktop "Views\Pages\DashboardPage.xaml"
if ((Get-Content $dashXaml -Raw) -notmatch "WrapPanel") {
    Add-Warn "DashboardPage.xaml should use WrapPanel for bento KPI grid"
}

# 7) MainWindow theme toggle — no moon/sun emoji
$mainCs = Join-Path $desktop "MainWindow.xaml.cs"
$mainBytes = [System.IO.File]::ReadAllBytes($mainCs)
$mainEmoji = $false
for ($i = 0; $i -lt $mainBytes.Length - 3; $i++) {
    if ($mainBytes[$i] -eq 0xF0 -and $mainBytes[$i+1] -ge 0x9F) { $mainEmoji = $true; break }
}
if ($mainEmoji) {
    Add-Fail "MainWindow.xaml.cs may contain emoji"
} else {
    Write-Host "[OK] MainWindow: no emoji in code-behind" -ForegroundColor Green
}

Write-Host "[OK] Shell scan complete" -ForegroundColor Green

Write-Host ""
if ($warnings.Count -gt 0) {
    Write-Host "Warnings ($($warnings.Count)):" -ForegroundColor Yellow
    $warnings | ForEach-Object { Write-Host "  - $_" -ForegroundColor Yellow }
}

if ($failures.Count -gt 0) {
    Write-Host "FAILURES ($($failures.Count)):" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    if ($Strict) { exit 1 }
    exit 1
}

Write-Host "PASS - UI UX Pro Max automated checks" -ForegroundColor Green
exit 0
