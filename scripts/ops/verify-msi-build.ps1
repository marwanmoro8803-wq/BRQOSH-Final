# O-03 - verify MSI build pipeline in-repo (not clean-VM field proof).

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Location $root

Write-Host "=== BRQOSH MSI Build Verification (O-03 prep) ===" -ForegroundColor Cyan

Write-Host "[1/3] Publish portable..." -ForegroundColor Yellow
& "$root\installer\publish.ps1"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$publishDir = Join-Path $root "dist\BRQOSH"
if (-not (Test-Path (Join-Path $publishDir "Brqosh.Desktop.exe"))) {
    Write-Error "Publish output missing Brqosh.Desktop.exe"
}

Write-Host "[2/3] WiX MSI (if wix CLI installed)..." -ForegroundColor Yellow
if (Get-Command wix -ErrorAction SilentlyContinue) {
    & "$root\installer\build-msi.ps1"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    $msi = Join-Path $root "dist\BRQOSH-Setup.msi"
    if (-not (Test-Path $msi)) { Write-Error "MSI not produced: $msi" }
    Write-Host "MSI produced: $msi" -ForegroundColor Green
} else {
    Write-Host "WiX CLI not installed - publish-only verification PASS." -ForegroundColor Yellow
    Write-Host "Install: dotnet tool install --global wix" -ForegroundColor Yellow
}

Write-Host "[3/3] Document field steps..." -ForegroundColor Yellow
Write-Host "See docs/ops/O-03_MSI_VM_RUNBOOK.md for clean VM + screenshot evidence."

Write-Host ""
Write-Host "In-repo MSI pipeline verification complete." -ForegroundColor Green
Write-Host "O-03 remains EXTERNAL until clean VM install + field signing."
