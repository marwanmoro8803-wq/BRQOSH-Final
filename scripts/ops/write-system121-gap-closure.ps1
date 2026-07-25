# System 121 - Omega gap closure gate
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $repoRoot

Write-Host "=== System 121 Omega gap closure gate ===" -ForegroundColor Cyan

$ceef = Join-Path $repoRoot "dist\evidence\prompt-governance\CEEF-OMEGA-GAP-121-001.md"
if (-not (Test-Path $ceef)) { Write-Error "Missing CEEF-OMEGA-GAP-121-001"; exit 1 }

$gates = @(
    "write-system100-security-gate.ps1",
    "run-coverlet-gate.ps1",
    "run-dead-code-scan.ps1"
)
foreach ($g in $gates) {
    $p = Join-Path $PSScriptRoot $g
    if (-not (Test-Path $p)) { Write-Error "Missing gate $g"; exit 1 }
    Write-Host ">> $g" -ForegroundColor DarkCyan
    & $p
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$sln = Join-Path $repoRoot "CognitiveQa\CognitiveQa.sln"
dotnet build $sln -c Release --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$testOut = dotnet test $sln -c Release --nologo 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) { Write-Host $testOut; exit $LASTEXITCODE }
$testCount = 122
if ($testOut -match "Passed:\s+(\d+),\s+Skipped") { $testCount = [int]$Matches[1] }
if ($testCount -lt 128) {
    Write-Error "Expected 128+ tests, got $testCount"
    exit 1
}

$cli = Join-Path $repoRoot "CognitiveQa\src\CognitiveQa.Cli\CognitiveQa.Cli.csproj"
dotnet run --project $cli -c Release --no-build -- twin emit 2>$null
dotnet run --project $cli -c Release --no-build -- omega suggest 2>$null

$evidenceDir = Join-Path $repoRoot "dist\evidence\omega-gap-121"
New-Item -ItemType Directory -Force -Path $evidenceDir | Out-Null
$latest = Join-Path $evidenceDir "latest.json"
@{
    system = 121
    generatedUtc = (Get-Date).ToUniversalTime().ToString("o")
    ceef = "CEEF-OMEGA-GAP-121-001.md"
    dotnetTests = $testCount
    omegaGapClosed = $true
    wavesIndex = "CEEF-OMEGA-WAVES-99-119-INDEX.md"
} | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 $latest

Write-Host "System 121 gap closure PASS - tests=$testCount"
exit 0
