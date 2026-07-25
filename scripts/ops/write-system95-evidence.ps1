# System 95 - Second completeness pass (evidence chain 91-94 + master verify)
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $repoRoot

Write-Host "=== System 95 Second completeness evidence ===" -ForegroundColor Cyan

$chain = @(
    "write-system91-evidence.ps1",
    "write-system92-evidence.ps1",
    "write-system93-evidence.ps1",
    "write-system94-evidence.ps1"
)
foreach ($script in $chain) {
    $path = Join-Path $PSScriptRoot $script
    if (-not (Test-Path $path)) {
        Write-Error "Missing evidence script: $script"
        exit 1
    }
    & $path
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$govCeef = Join-Path $repoRoot "dist\evidence\prompt-governance\CEEF-SYSTEM-95-COMPLETENESS-PASS2-001.md"
if (-not (Test-Path $govCeef)) {
    Write-Error "Missing CEEF-95"
    exit 1
}

$sln = Join-Path $repoRoot "CognitiveQa\CognitiveQa.sln"
$testOut = dotnet test $sln -c Release --nologo 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) { Write-Host $testOut; exit $LASTEXITCODE }

$testCount = 94
if ($testOut -match "Passed:\s+(\d+),\s+Skipped") {
    $testCount = [int]$Matches[1]
}

$evidenceDir = Join-Path $repoRoot "dist\evidence\completeness-pass2"
New-Item -ItemType Directory -Force -Path $evidenceDir | Out-Null
$latest = Join-Path $evidenceDir "latest.json"

$payload = [ordered]@{
    system = 95
    generatedUtc = (Get-Date).ToUniversalTime().ToString("o")
    evidenceChain = $chain
    dotnetTests = $testCount
    ceef = "CEEF-SYSTEM-95-COMPLETENESS-PASS2-001.md"
}
$payload | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 $latest

Write-Host "System 95 evidence PASS - tests=$testCount latest=$latest"
exit 0
