# Ops — Aggregate archived audits into INTELLIGENCE_INDEX.yaml (M14)
param(
    [string]$RepoRoot = ""
)

if (-not $RepoRoot) {
    $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
}

$cqaNet = Join-Path $RepoRoot "CognitiveQa"
Push-Location $cqaNet
try {
    dotnet build src/CognitiveQa.Cli/CognitiveQa.Cli.csproj -c Release --nologo -v q
    dotnet run --project src/CognitiveQa.Cli/CognitiveQa.Cli.csproj -c Release --no-build -- intelligence-index $RepoRoot
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
