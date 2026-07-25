# System 93 - Reality law evidence (no simulation paths)
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $repoRoot

Write-Host "=== System 93 Reality law evidence ===" -ForegroundColor Cyan

$gov = Join-Path $repoRoot "dist\evidence\prompt-governance\CEEF-SYSTEM-93-REALITY-LAW-001.md"
if (-not (Test-Path $gov)) {
    Write-Error "Missing CEEF-93"
    exit 1
}

$sln = Join-Path $repoRoot "CognitiveQa\CognitiveQa.sln"
$testOut = dotnet test $sln -c Release --nologo --filter "FullyQualifiedName~Source_only|FullyQualifiedName~FlaUi|FullyQualifiedName~HumanNavigator" 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) { Write-Host $testOut; exit $LASTEXITCODE }

$pySmoke = $false
$venvPy = Join-Path $repoRoot "cognitive-qa\.venv\Scripts\python.exe"
if (Test-Path $venvPy) {
    & $venvPy -m pytest (Join-Path $repoRoot "cognitive-qa\tests\test_smoke.py") -q
    if ($LASTEXITCODE -eq 0) { $pySmoke = $true }
}

$evidenceDir = Join-Path $repoRoot "dist\evidence\reality-law"
New-Item -ItemType Directory -Force -Path $evidenceDir | Out-Null
$latest = Join-Path $evidenceDir "latest.json"

$payload = [ordered]@{
    system = 93
    generatedUtc = (Get-Date).ToUniversalTime().ToString("o")
    dotnetRealityFilterPass = $true
    pythonSmoke = $pySmoke
    ceef = "CEEF-SYSTEM-93-REALITY-LAW-001.md"
}
$payload | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 $latest

Write-Host "System 93 evidence PASS - pythonSmoke=$pySmoke latest=$latest"
exit 0
