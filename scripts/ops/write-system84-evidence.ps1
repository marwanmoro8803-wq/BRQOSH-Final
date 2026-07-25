# System 84 — BRQOSH live + workflow evidence
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$cli = Join-Path $repoRoot "CognitiveQa\src\CognitiveQa.Cli\CognitiveQa.Cli.csproj"

dotnet build $cli -c Release --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet run --project $cli -c Release --no-build -- shell validate $repoRoot
$shellExit = $LASTEXITCODE
dotnet run --project $cli -c Release --no-build -- target brqosh live $repoRoot --try-build
$brqLiveExit = $LASTEXITCODE
dotnet run --project $cli -c Release --no-build -- legacy verify $repoRoot
$legacyExit = $LASTEXITCODE
dotnet run --project $cli -c Release --no-build -- shell live $repoRoot
$flauiExit = $LASTEXITCODE

if ($shellExit -ne 0 -or $flauiExit -ne 0 -or $legacyExit -ne 0 -or $brqLiveExit -ne 0) { exit 1 }
Write-Host "NOTE: BRQOSH live UIA needs Brqosh.Desktop.exe built"
exit 0
