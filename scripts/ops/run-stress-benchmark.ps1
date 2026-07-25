# Journal stress benchmark - O-04 preparation.
# Default: 2k-5k (CI-safe). Use -FullMillion on dedicated VM (hours).

param(
    [switch]$FullMillion
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Location $root

Write-Host "=== BRQOSH Stress Benchmark (O-04 prep) ===" -ForegroundColor Cyan

$env:BRQOSH_TEST = "1"
if ($FullMillion) {
    Write-Host "Mode: 1M journal lines (requires dedicated hardware - may take hours)" -ForegroundColor Yellow
    $env:BRQOSH_SKIP_STRESS = "0"
    $filter = "FullyQualifiedName~JournalStress"
} else {
    Write-Host "Mode: documented 2k-5k stress (CI default)" -ForegroundColor Yellow
    $env:BRQOSH_SKIP_STRESS = "0"
    $filter = "FullyQualifiedName~JournalStress"
}

$sw = [System.Diagnostics.Stopwatch]::StartNew()
dotnet test tests/Brqosh.Application.Tests/Brqosh.Application.Tests.csproj `
    --filter $filter --nologo -v normal
$exit = $LASTEXITCODE
$sw.Stop()

Write-Host ""
Write-Host "Elapsed: $($sw.Elapsed.ToString())"
if ($exit -eq 0) {
    Write-Host "Stress benchmark PASS" -ForegroundColor Green
} else {
    Write-Host "Stress benchmark FAIL (exit $exit)" -ForegroundColor Red
}

if (-not $FullMillion) {
    Write-Host ""
    Write-Host "For O-04 1M closure: re-run with -FullMillion on dedicated VM and archive logs." -ForegroundColor Cyan
}

exit $exit
