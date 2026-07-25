# Ops wrapper — delegates to ShuraCouncil/run-shura-council.ps1
param(
    [string]$Source = "dist/evidence/prompt-governance/CEEF-PROMPT-001.md"
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$inner = Join-Path $repoRoot "ShuraCouncil\run-shura-council.ps1"

if (-not (Test-Path $inner)) {
    Write-Error "Shura launcher not found: $inner"
    exit 1
}

& $inner -Source $Source
$shuraExit = $LASTEXITCODE

if ($shuraExit -eq 0 -or $shuraExit -eq 1) {
    $merge = Join-Path $repoRoot "scripts\ops\merge-council-chronicle.ps1"
    if (Test-Path $merge) {
        Write-Host "Merging agent-logs + Shura into council chronicle..." -ForegroundColor Cyan
        & $merge
    }
}

exit $shuraExit
