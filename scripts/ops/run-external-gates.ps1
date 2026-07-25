# Runs external gate preparation + in-repo verification for O-01…O-05.
# Does NOT claim external closure — prints checklist and runs what can run locally.

$ErrorActionPreference = "Continue"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Location $root

Write-Host "=== BRQOSH External Gates Runner (O-01 to O-05) ===" -ForegroundColor Cyan
Write-Host ""

& "$root\scripts\ops\external-gates-checklist.ps1"
Write-Host ""

Write-Host "[In-repo] Security static scan..." -ForegroundColor Yellow
& "$root\scripts\security-static-scan.ps1"
Write-Host ""

Write-Host "[In-repo] Tests (BRQOSH_SKIP_STRESS=1)..." -ForegroundColor Yellow
$env:BRQOSH_SKIP_STRESS = "1"
$env:BRQOSH_TEST = "1"
dotnet test Brqosh.sln -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "[In-repo] FullScenarioAudit..." -ForegroundColor Yellow
dotnet run --project tools\FullScenarioAudit\FullScenarioAudit.csproj --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "[In-repo] Aggregate coverage gate (Application.Tests)..." -ForegroundColor Yellow
& "$root\scripts\check-coverage.ps1" -MinimumLineRate 0.75
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host ""

Write-Host "[In-repo] PostgreSQL schema gate (skips without PG config)..." -ForegroundColor Yellow
& "$root\scripts\ops\schema-gate-pg.ps1"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host ""

Write-Host "[In-repo] i18n navigation gate (G-27)..." -ForegroundColor Yellow
& "$root\scripts\ops\i18n-coverage-audit.ps1"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host ""

Write-Host "[In-repo] Shura Council consensus gate..." -ForegroundColor Yellow
$shuraSource = "تقارير/CEEF-145_Live_UI_Audit_Marwan.md"
& "$root\scripts\ops\run-shura-council.ps1" -Source $shuraSource
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host ""

Write-Host "[In-repo] MSI publish verify (no WiX required)..." -ForegroundColor Yellow
& "$root\installer\publish.ps1"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host ""

Write-Host ""
Write-Host "=== External gates runner complete ===" -ForegroundColor Green
Write-Host "O-01 to O-05 remain EXTERNAL until live evidence (OTP, SOCPA, VM, 1M load, ZAP)."
