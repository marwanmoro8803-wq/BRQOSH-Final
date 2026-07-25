# Omega birth gate — Systems 98-120
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $repoRoot

Write-Host "=== Omega Birth Gate (98-120) ===" -ForegroundColor Cyan

$gates = @(
    "write-system98-truth-alignment.ps1",
    "write-system97-launch-gate.ps1",
    "write-system99-digital-twin.ps1",
    "write-system101-103-cellular.ps1",
    "write-system104-106-soul.ps1",
    "write-system107-109-compass.ps1",
    "write-system110-112-evolve.ps1",
    "write-system113-115-immortal.ps1"
)
foreach ($g in $gates) {
    $p = Join-Path $PSScriptRoot $g
    if (-not (Test-Path $p)) { Write-Error "Missing gate $g"; exit 1 }
    Write-Host ">> $g" -ForegroundColor DarkCyan
    & $p
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$sln = Join-Path $repoRoot "CognitiveQa\CognitiveQa.sln"
$testOut = dotnet test $sln -c Release --nologo 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) { Write-Host $testOut; exit $LASTEXITCODE }
$testCount = 98
if ($testOut -match "Passed:\s+(\d+),\s+Skipped") { $testCount = [int]$Matches[1] }
if ($testCount -lt 120) {
    Write-Error "Expected 120+ tests, got $testCount"
    exit 1
}

$cli = Join-Path $repoRoot "CognitiveQa\src\CognitiveQa.Cli\CognitiveQa.Cli.csproj"
dotnet run --project $cli -c Release --no-build -- twin emit 2>$null
dotnet run --project $cli -c Release --no-build -- omega suggest 2>$null
dotnet run --project $cli -c Release --no-build -- omega living-docs 2>$null

$delivery = Join-Path $repoRoot "dist\omega-delivery"
New-Item -ItemType Directory -Force -Path $delivery | Out-Null
$desktop = Join-Path $repoRoot "CognitiveQa\publish\desktop"
if (Test-Path $desktop) {
    $destDesk = Join-Path $delivery "desktop"
    New-Item -ItemType Directory -Force -Path $destDesk | Out-Null
    Copy-Item "$desktop\*" $destDesk -Recurse -Force -ErrorAction SilentlyContinue
}
Copy-Item (Join-Path $repoRoot "docs\OMEGA-DIGITAL-COVENANT-ar.md") $delivery -Force -ErrorAction SilentlyContinue
$dockerDest = Join-Path $delivery "docker"
New-Item -ItemType Directory -Force -Path $dockerDest | Out-Null
Copy-Item (Join-Path $repoRoot "deploy\docker-compose.omega.yml") $dockerDest -Force -ErrorAction SilentlyContinue

@"
# Omega Launch Checklist (Arabic)

1. dotnet test PASS (120+)
2. write-system120-omega-birth-gate.ps1 exit 0
3. راجع docs/OPEN_ITEMS.md
4. شغّل START-CognitiveQa.bat
5. Shura على CEEF-OMEGA-BIRTH-120-001
"@ | Set-Content -Encoding UTF8 (Join-Path $delivery "OMEGA-LAUNCH-CHECKLIST-ar.md")

$evidenceCopy = Join-Path $delivery "evidence"
New-Item -ItemType Directory -Force -Path $evidenceCopy | Out-Null
$srcEvidence = Join-Path $repoRoot "dist\evidence"
if (Test-Path $srcEvidence) {
    Copy-Item "$srcEvidence\*" $evidenceCopy -Recurse -Force -ErrorAction SilentlyContinue
}

$latest = Join-Path $repoRoot "dist\evidence\omega-birth\latest.json"
New-Item -ItemType Directory -Force -Path (Split-Path $latest) | Out-Null
@{
    system = 120
    generatedUtc = (Get-Date).ToUniversalTime().ToString("o")
    ceef = "CEEF-OMEGA-BIRTH-120-001.md"
    dotnetTests = $testCount
    deliveryPath = $delivery
    omegaPilotComplete = $true
} | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 $latest

Write-Host "Omega birth gate PASS - tests=$testCount delivery=$delivery"
exit 0
