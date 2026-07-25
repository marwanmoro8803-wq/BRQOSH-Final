# Ops — launch AiCouncil from monorepo root
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$launcher = Join-Path $repoRoot "AiCouncil\Start-AiCouncil.bat"

if (-not (Test-Path $launcher)) {
    Write-Error "AiCouncil launcher not found: $launcher (see MEM-127)"
    exit 1
}

Write-Host "Launching AiCouncil from $repoRoot"
& $launcher
exit $LASTEXITCODE
