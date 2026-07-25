# CEEF-125 - PostgreSQL schema gate
# Runs PG-path tests when %APPDATA%\BRQOSH\database.json points to PostgreSQL.
# Default: warn-and-pass when PG is unavailable (SQLite CI).
# Strict:  set BRQOSH_REQUIRE_PG=1 (mandatory for PRs touching Persistence/) -> fail without PG.
# NOTE: ASCII-only strings (PS 5.1 parses this file without BOM).

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Location $root

$configPath = Join-Path $env:APPDATA "BRQOSH\database.json"
$requirePg = $env:BRQOSH_REQUIRE_PG -eq "1"

Write-Host "=== BRQOSH Schema Gate - PostgreSQL ===" -ForegroundColor Cyan

$hasPg = $false
if (Test-Path $configPath) {
    $raw = Get-Content $configPath -Raw
    if ($raw -match 'postgresql') { $hasPg = $true }
}

if (-not $hasPg) {
    if ($requirePg) {
        Write-Error "BRQOSH_REQUIRE_PG=1 but no PostgreSQL config at $configPath - schema PR cannot close on SQLite alone (brqosh-schema-governance)."
    }
    Write-Host "No PostgreSQL config - gate SKIPPED (SQLite CI). Set BRQOSH_REQUIRE_PG=1 for Persistence PRs." -ForegroundColor Yellow
    exit 0
}

Write-Host "[PG] PostgreSqlStartupTests + SchemaSqlGuardTests..." -ForegroundColor Yellow
$env:BRQOSH_TEST = "1"
dotnet test tests\Brqosh.Application.Tests -c Release --nologo -v q `
    --filter "FullyQualifiedName~PostgreSqlStartup|FullyQualifiedName~SchemaSqlGuard"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "[PG] DbStartupProbe (Desktop smoke path)..." -ForegroundColor Yellow
dotnet run --project tools\DbStartupProbe --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "=== PG schema gate PASS ===" -ForegroundColor Green
