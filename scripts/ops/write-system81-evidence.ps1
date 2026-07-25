# System 81 — venv bootstrap + BRQOSH try-build + combined evidence last
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$cli = Join-Path $repoRoot "CognitiveQa\src\CognitiveQa.Cli\CognitiveQa.Cli.csproj"

dotnet build $cli -c Release --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& (Join-Path $PSScriptRoot "bootstrap-aicouncil-venv.ps1")

dotnet run --project $cli -c Release --no-build -- shell validate $repoRoot
$shellExit = $LASTEXITCODE
dotnet run --project $cli -c Release --no-build -- aicouncil verify $repoRoot
$aiExit = $LASTEXITCODE
dotnet run --project $cli -c Release --no-build -- target brqosh probe $repoRoot
$brqExit = $LASTEXITCODE
dotnet run --project $cli -c Release --no-build -- target brqosh smoke $repoRoot --try-build
$brqSmokeExit = $LASTEXITCODE
dotnet run --project $cli -c Release --no-build -- shell live $repoRoot
$flauiExit = $LASTEXITCODE

if ($shellExit -ne 0 -or $flauiExit -ne 0 -or $aiExit -ne 0 -or $brqExit -ne 0 -or $brqSmokeExit -ne 0) { exit 1 }
exit 0
