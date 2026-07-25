# Merge AiCouncil agent-logs.md with Shura council reports
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$cognitiveQa = Join-Path $repoRoot "CognitiveQa"

if (-not (Test-Path $cognitiveQa)) {
    Write-Host "SKIP: CognitiveQa not present - council chronicle merge skipped" -ForegroundColor Yellow
    exit 0
}

Set-Location $cognitiveQa

dotnet run --project src/CognitiveQa.Cli/CognitiveQa.Cli.csproj -c Release --no-build -- chronicle merge $repoRoot 2>$null
if ($LASTEXITCODE -ne 0) {
    dotnet build src/CognitiveQa.Cli/CognitiveQa.Cli.csproj -c Release --nologo -v q
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet run --project src/CognitiveQa.Cli/CognitiveQa.Cli.csproj -c Release --no-build -- chronicle merge $repoRoot
}
exit $LASTEXITCODE
