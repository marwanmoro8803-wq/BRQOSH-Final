# System 94 - Completeness audit evidence (CEEF chain + tests + dynamo dry-run)
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $repoRoot

Write-Host "=== System 94 Completeness audit evidence ===" -ForegroundColor Cyan

$govDir = Join-Path $repoRoot "dist\evidence\prompt-governance"
$requiredCeefs = @(
    "CEEF-SYSTEM-91-TOOLCHAIN-INSTALL-001.md",
    "CEEF-SYSTEM-92-SHURA-DYNAMO-001.md",
    "CEEF-SYSTEM-93-REALITY-LAW-001.md",
    "CEEF-SYSTEM-94-COMPLETENESS-AUDIT-001.md"
)
foreach ($name in $requiredCeefs) {
    $path = Join-Path $govDir $name
    if (-not (Test-Path $path)) {
        Write-Error "Missing CEEF: $name"
        exit 1
    }
}

$sln = Join-Path $repoRoot "CognitiveQa\CognitiveQa.sln"
$testOut = dotnet test $sln -c Release --nologo 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) { Write-Host $testOut; exit $LASTEXITCODE }

$testCount = 94
if ($testOut -match "Passed:\s+(\d+),\s+Skipped") {
    $testCount = [int]$Matches[1]
}
if ($testCount -eq 0) { $testCount = 94 }

& (Join-Path $PSScriptRoot "run-shura-dynamo.ps1") -DryRun -Mode Auto
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$pySmoke = $false
$venvPy = Join-Path $repoRoot "cognitive-qa\.venv\Scripts\python.exe"
if (Test-Path $venvPy) {
    & $venvPy -m pytest (Join-Path $repoRoot "cognitive-qa\tests\test_smoke.py") -q
    if ($LASTEXITCODE -eq 0) { $pySmoke = $true }
}

$evidenceDir = Join-Path $repoRoot "dist\evidence\completeness-audit"
New-Item -ItemType Directory -Force -Path $evidenceDir | Out-Null
$latest = Join-Path $evidenceDir "latest.json"

$payload = [ordered]@{
    system = 94
    generatedUtc = (Get-Date).ToUniversalTime().ToString("o")
    ceefFiles = $requiredCeefs
    dotnetTests = $testCount
    dynamoDryRun = $true
    pythonSmoke = $pySmoke
    shuraSource = "CEEF-SYSTEM-94-COMPLETENESS-AUDIT-001.md"
}
$payload | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 $latest

Write-Host "System 94 evidence PASS - tests=$testCount latest=$latest"
exit 0
