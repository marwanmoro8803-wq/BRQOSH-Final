# Journal stress benchmark - O-04 preparation.
# Default: 2,000 entries on in-memory SQLite (CI-safe).
# -FullMillion: 1,000,000 entries (dedicated VM - hours).
# -Postgres:    the PostgreSQL TEST server (databases brqosh_t_*), never the operator's books.
#
# (H-59 / data-upgrade-performance#3) Both branches used to set the same variables, so -FullMillion
# ran 2,000 entries on in-memory SQLite; and the test capped any requested count at 100,000.
# The count and the provider below are exactly what JournalStressLoadTests reads.

param(
    [switch]$FullMillion,
    [switch]$Postgres
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Location $root

Write-Host "=== BRQOSH Stress Benchmark (O-04 prep) ===" -ForegroundColor Cyan

$env:BRQOSH_TEST = "1"
$env:BRQOSH_SKIP_STRESS = "0"
$filter = "FullyQualifiedName~JournalStress"

if ($FullMillion) {
    Write-Host "Mode: 1,000,000 journal entries (requires dedicated hardware - may take hours)" -ForegroundColor Yellow
    $env:BRQOSH_LOAD_ENTRIES = "1000000"
} else {
    Write-Host "Mode: 2,000 journal entries (CI default)" -ForegroundColor Yellow
    Remove-Item Env:BRQOSH_LOAD_ENTRIES -ErrorAction SilentlyContinue
}

if ($Postgres) {
    Write-Host "Provider: PostgreSQL test server (brqosh_t_*)" -ForegroundColor Yellow
    $env:BRQOSH_LOAD_PROVIDER = "postgres"
    $env:BRQOSH_REQUIRE_PG = "1"
} else {
    Write-Host "Provider: in-memory SQLite (NOT PostgreSQL - not an O-04 measurement)" -ForegroundColor Yellow
    Remove-Item Env:BRQOSH_LOAD_PROVIDER -ErrorAction SilentlyContinue
}

$sw = [System.Diagnostics.Stopwatch]::StartNew()
dotnet test tests/Brqosh.Application.Tests/Brqosh.Application.Tests.csproj `
    --filter $filter --nologo -v normal --logger "console;verbosity=detailed"
$exit = $LASTEXITCODE
$sw.Stop()

Write-Host ""
Write-Host "Elapsed: $($sw.Elapsed.ToString())"
if ($exit -eq 0) {
    Write-Host "Stress benchmark PASS" -ForegroundColor Green
} else {
    Write-Host "Stress benchmark FAIL (exit $exit)" -ForegroundColor Red
}

if (-not ($FullMillion -and $Postgres)) {
    Write-Host ""
    Write-Host "For O-04 closure: re-run with -FullMillion -Postgres on a dedicated VM and archive the log." -ForegroundColor Cyan
}

exit $exit
