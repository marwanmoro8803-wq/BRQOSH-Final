# System 86 — supreme palette + presets evidence
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$cli = Join-Path $repoRoot "CognitiveQa\src\CognitiveQa.Cli\CognitiveQa.Cli.csproj"

dotnet build $cli -c Release --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet run --project $cli -c Release --no-build -- shell validate $repoRoot
$shellExit = $LASTEXITCODE
dotnet run --project $cli -c Release --no-build -- workflow preset list
$listExit = $LASTEXITCODE
dotnet run --project $cli -c Release --no-build -- workflow commands bridge
$cmdExit = $LASTEXITCODE
dotnet run --project $cli -c Release --no-build -- workflow preset daily $repoRoot
$dailyExit = $LASTEXITCODE

if ($shellExit -ne 0 -or $listExit -ne 0 -or $cmdExit -ne 0) { exit 1 }
Write-Host "System 86 evidence PASS (daily=$dailyExit)"
exit 0
