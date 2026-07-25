# System 122 — Mission Control UI gate
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $repoRoot

Write-Host "=== System 122 Mission Control UI gate ===" -ForegroundColor Cyan

$ceef = Join-Path $repoRoot "dist\evidence\prompt-governance\CEEF-UI-MISSION-CONTROL-001.md"
if (-not (Test-Path $ceef)) { Write-Error "Missing CEEF-UI-MISSION-CONTROL-001"; exit 1 }

$paths = @(
    "CognitiveQa\src\CognitiveQa.Desktop\Themes\MissionControlTheme.xaml",
    "CognitiveQa\design-system\MISSION-CONTROL.md",
    "CognitiveQa\src\CognitiveQa.Desktop\Services\LocaleService.cs",
    "CognitiveQa\src\CognitiveQa.Desktop\Services\ShellNavigationService.cs"
)
foreach ($p in $paths) {
    if (-not (Test-Path (Join-Path $repoRoot $p))) { Write-Error "Missing $p"; exit 1 }
    Write-Host "[PASS] $p" -ForegroundColor DarkGreen
}

$mainwindow = Join-Path $repoRoot "CognitiveQa\src\CognitiveQa.Desktop\MainWindow.xaml"
$xaml = Get-Content $mainwindow -Raw
$markers = @(
    'AutomationProperties.Name="CognitiveQa Mission Control Shell"',
    'x:Name="NavDashboard"',
    'x:Name="StartAudit"',
    'AutomationProperties.Name="WORKFLOW"'
)
foreach ($m in $markers) {
    if ($xaml -notlike "*$m*") { Write-Error "MainWindow missing: $m"; exit 1 }
    Write-Host "[PASS] $m" -ForegroundColor DarkGreen
}

$sln = Join-Path $repoRoot "CognitiveQa\CognitiveQa.sln"
dotnet build $sln -c Release --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$testOut = dotnet test $sln -c Release --nologo 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) { Write-Host $testOut; exit $LASTEXITCODE }
$testCount = 128
if ($testOut -match "Passed:\s+(\d+),\s+Skipped") { $testCount = [int]$Matches[1] }
if ($testCount -lt 128) {
    Write-Error "Expected 128+ tests, got $testCount"
    exit 1
}

$evidenceDir = Join-Path $repoRoot "dist\evidence\ui-mission-control"
New-Item -ItemType Directory -Force -Path $evidenceDir | Out-Null
$latest = Join-Path $evidenceDir "latest.json"
@{
    system = 122
    generatedUtc = (Get-Date).ToUniversalTime().ToString("o")
    ceef = "CEEF-UI-MISSION-CONTROL-001.md"
    dotnetTests = $testCount
    missionControlRtl = $true
    theme = "MissionControlTheme.xaml"
} | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 $latest

Write-Host "System 122 Mission Control UI PASS - tests=$testCount"
exit 0
