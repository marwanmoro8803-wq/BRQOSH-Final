# System 91 - Toolchain install evidence wrapper
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $repoRoot

Write-Host "=== System 91 Toolchain evidence ===" -ForegroundColor Cyan

& (Join-Path $PSScriptRoot "install-verify-toolchain.ps1") -VerifyOnly -SkipHeavy
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$latest = Join-Path $repoRoot "dist\evidence\toolchain-install\latest.json"
if (-not (Test-Path $latest)) {
    Write-Error "Missing toolchain evidence: $latest"
    exit 1
}

$gov = Join-Path $repoRoot "dist\evidence\prompt-governance\CEEF-SYSTEM-91-TOOLCHAIN-INSTALL-001.md"
if (-not (Test-Path $gov)) {
    Write-Error "Missing CEEF-91"
    exit 1
}

Write-Host "System 91 evidence PASS - $latest"
exit 0
