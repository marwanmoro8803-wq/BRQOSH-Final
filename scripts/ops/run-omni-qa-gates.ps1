# Omni-QA unified gates — PAE + governance validate + Shura (optional)
param(
    [switch]$SkipBuild,
    [switch]$SkipShura,
    [string]$ShuraSource = "dist/evidence/prompt-governance/CEEF-PROMPT-004.md"
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $repoRoot

Write-Host "=== Omni-QA Gates ===" -ForegroundColor Cyan

& (Join-Path $PSScriptRoot "validate-prompt-governance.ps1")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$paeScript = Join-Path $PSScriptRoot "run-prompt-audit-engine.ps1"
if ($SkipBuild) {
    & $paeScript -SkipBuild
} else {
    & $paeScript
}
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (-not $SkipShura) {
    & (Join-Path $PSScriptRoot "run-shura-council.ps1") -Source $ShuraSource
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host "`n=== Omni-QA Gates: PASS ===" -ForegroundColor Cyan
exit 0
