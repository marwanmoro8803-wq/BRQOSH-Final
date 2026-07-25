# Systems 99-100 - Digital twin + security gate
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $repoRoot

& (Join-Path $PSScriptRoot "write-system98-truth-alignment.ps1")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$cli = Join-Path $repoRoot "CognitiveQa\src\CognitiveQa.Cli\CognitiveQa.Cli.csproj"
dotnet run --project $cli -c Release --no-build -- twin emit 2>$null
if ($LASTEXITCODE -ne 0) {
    dotnet build (Join-Path $repoRoot "CognitiveQa\CognitiveQa.sln") -c Release --nologo
    dotnet run --project $cli -c Release -- twin emit
}
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$twin = Join-Path $repoRoot "dist\evidence\digital-twin\latest.json"
if (-not (Test-Path $twin)) { Write-Error "Twin missing"; exit 1 }

& (Join-Path $PSScriptRoot "write-system100-security-gate.ps1")
exit $LASTEXITCODE
