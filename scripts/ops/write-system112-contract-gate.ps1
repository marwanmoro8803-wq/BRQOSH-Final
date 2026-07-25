# System 112 - API contract gate
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$sln = Join-Path $repoRoot "CognitiveQa\CognitiveQa.sln"

dotnet test $sln -c Release --no-build --filter "FullyQualifiedName~ContractSchema" --nologo 2>$null
if ($LASTEXITCODE -ne 0) {
    dotnet build $sln -c Release --nologo
    dotnet test $sln -c Release --filter "FullyQualifiedName~ContractSchema" --nologo
}
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$out = Join-Path $repoRoot "dist\evidence\contract-gate"
New-Item -ItemType Directory -Force -Path $out | Out-Null
@{ system = 112; pass = $true; generatedUtc = (Get-Date).ToUniversalTime().ToString("o") } | ConvertTo-Json | Set-Content (Join-Path $out "latest.json")
Write-Host "System 112 contract gate PASS"
exit 0
