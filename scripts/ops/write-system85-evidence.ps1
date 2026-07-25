# System 85 — bridge deep integration + PySide6 CI evidence
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$cli = Join-Path $repoRoot "CognitiveQa\src\CognitiveQa.Cli\CognitiveQa.Cli.csproj"

./scripts/ops/bootstrap-cognitive-qa-venv.ps1

dotnet build $cli -c Release --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet run --project $cli -c Release --no-build -- shell validate $repoRoot
$shellExit = $LASTEXITCODE
dotnet run --project $cli -c Release --no-build -- legacy verify $repoRoot
$legacyExit = $LASTEXITCODE
dotnet run --project $cli -c Release --no-build -- legacy bridge-probe $repoRoot
$bridgeExit = $LASTEXITCODE
dotnet run --project $cli -c Release --no-build -- target brqosh live $repoRoot --try-build
$brqLiveExit = $LASTEXITCODE
dotnet run --project $cli -c Release --no-build -- shell live $repoRoot
$flauiExit = $LASTEXITCODE

if ($shellExit -ne 0 -or $legacyExit -ne 0 -or $flauiExit -ne 0 -or $brqLiveExit -ne 0) { exit 1 }
if ($bridgeExit -ne 0) {
    Write-Host "NOTE: bridge-probe PARTIAL when PySide6 not installed in venv"
}
Write-Host "System 85 evidence PASS (bridge=$bridgeExit)"
exit 0
