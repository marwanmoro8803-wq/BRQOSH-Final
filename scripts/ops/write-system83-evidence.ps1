# System 83 — Desktop FlaUI self-probe + workflow progress evidence
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$cli = Join-Path $repoRoot "CognitiveQa\src\CognitiveQa.Cli\CognitiveQa.Cli.csproj"

dotnet build $cli -c Release --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet run --project $cli -c Release --no-build -- shell validate $repoRoot
$shellExit = $LASTEXITCODE
dotnet run --project $cli -c Release --no-build -- legacy verify $repoRoot
$legacyExit = $LASTEXITCODE
dotnet run --project $cli -c Release --no-build -- aicouncil verify $repoRoot
$aiExit = $LASTEXITCODE
dotnet run --project $cli -c Release --no-build -- target brqosh probe $repoRoot
$brqExit = $LASTEXITCODE
dotnet run --project $cli -c Release --no-build -- shell live $repoRoot
$flauiExit = $LASTEXITCODE

if ($shellExit -ne 0 -or $flauiExit -ne 0 -or $aiExit -ne 0 -or $legacyExit -ne 0 -or $brqExit -ne 0) { exit 1 }
Write-Host "NOTE: live-self FlaUI requires CognitiveQa.Desktop WORKFLOW tab (C18)"
exit 0
