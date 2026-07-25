# System 79 — shell validate + live FlaUI + AiCouncil evidence for Shura F2
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$cli = Join-Path $repoRoot "CognitiveQa\src\CognitiveQa.Cli\CognitiveQa.Cli.csproj"

dotnet build $cli -c Release --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet run --project $cli -c Release --no-build -- shell validate $repoRoot
$shellExit = $LASTEXITCODE
dotnet run --project $cli -c Release --no-build -- shell live $repoRoot
$flauiExit = $LASTEXITCODE
dotnet run --project $cli -c Release --no-build -- aicouncil verify $repoRoot
$aiExit = $LASTEXITCODE

if ($shellExit -ne 0 -or $flauiExit -ne 0 -or $aiExit -ne 0) { exit 1 }
exit 0
