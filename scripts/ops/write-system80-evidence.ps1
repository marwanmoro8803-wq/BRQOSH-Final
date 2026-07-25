# System 80 — pytest ensure + BRQOSH target probe + combined evidence
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$cli = Join-Path $repoRoot "CognitiveQa\src\CognitiveQa.Cli\CognitiveQa.Cli.csproj"

dotnet build $cli -c Release --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "→ pip install pytest (AiCouncil)…"
Push-Location (Join-Path $repoRoot "AiCouncil")
python -m pip install -q pytest 2>&1 | Out-Null
Pop-Location

dotnet run --project $cli -c Release --no-build -- shell validate $repoRoot
$shellExit = $LASTEXITCODE
dotnet run --project $cli -c Release --no-build -- aicouncil verify $repoRoot
$aiExit = $LASTEXITCODE
dotnet run --project $cli -c Release --no-build -- target brqosh probe $repoRoot
$brqExit = $LASTEXITCODE
dotnet run --project $cli -c Release --no-build -- target brqosh smoke $repoRoot
$brqSmokeExit = $LASTEXITCODE
dotnet run --project $cli -c Release --no-build -- shell live $repoRoot
$flauiExit = $LASTEXITCODE

if ($shellExit -ne 0 -or $flauiExit -ne 0 -or $aiExit -ne 0 -or $brqExit -ne 0 -or $brqSmokeExit -ne 0) { exit 1 }
exit 0
