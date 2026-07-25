# تشغيل التدقيق الشامل — بيانات QA-AUDIT + واجهة FlaUI
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not (Test-Path "$root\Brqosh.sln")) { $root = Split-Path -Parent $PSScriptRoot }

Write-Host "=== BRQOSH Full Audit ===" -ForegroundColor Cyan

Get-Process -Name "Brqosh.Desktop" -ErrorAction SilentlyContinue | Stop-Process -Force

Push-Location "$root\tools\UiAudit"
try {
    dotnet build -v q
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet run 2>&1 | Tee-Object -FilePath "last-run.txt"
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
