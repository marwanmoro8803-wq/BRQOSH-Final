# BRQOSH — Run executable Shura Council v2 on a saved CEEF report.
# Usage:
#   .\ShuraCouncil\run-shura-council.ps1
#   .\ShuraCouncil\run-shura-council.ps1 -Source "تقارير/CEEF-145_Live_UI_Audit_Marwan.md"
param(
    [string]$Source = "dist/evidence/prompt-governance/CEEF-PROMPT-001.md"
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $repoRoot

$proj = if (Test-Path "tools/ShuraCouncil/ShuraCouncil.csproj") {
    "tools/ShuraCouncil/ShuraCouncil.csproj"
} else {
    "ShuraCouncil/ShuraCouncil.csproj"
}

Write-Host "Building ShuraCouncil v2..." -ForegroundColor Cyan
dotnet build $proj -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Running council on: $Source" -ForegroundColor Cyan
dotnet run --project $proj -c Release --no-build -- --source $Source
exit $LASTEXITCODE
