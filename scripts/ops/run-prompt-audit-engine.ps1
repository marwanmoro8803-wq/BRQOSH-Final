# Run universal Prompt Audit Engine — multi-language harness (not cognitive-qa).
param(
    [switch]$Watch,
    [switch]$SkipBuild,
    [switch]$RunShura,
    [int]$IntervalSeconds = 300
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $repoRoot

$proj = "PromptAuditEngine/PromptAuditEngine.csproj"
Write-Host "Building PromptAuditEngine..." -ForegroundColor Cyan
dotnet build $proj -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$args = @()
if ($Watch) { $args += "--watch"; $args += "--interval"; $args += $IntervalSeconds }
if ($SkipBuild) { $args += "--skip-build" }

Write-Host "Running audit engine..." -ForegroundColor Cyan
dotnet run --project $proj -c Release --no-build -- @args
$auditExit = $LASTEXITCODE

if ($RunShura -and -not $Watch) {
    $ceef = "dist/evidence/prompt-governance/CEEF-PROMPT-002.md"
    Write-Host "Running Shura on $ceef..." -ForegroundColor Cyan
    dotnet run --project ShuraCouncil/ShuraCouncil.csproj -c Release -- --source $ceef
    exit $LASTEXITCODE
}

exit $auditExit
