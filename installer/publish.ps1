#Requires -Version 5.1
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$OutDir = Join-Path $Root "dist\BRQOSH"
$ZipPath = Join-Path $Root "dist\BRQOSH-portable.zip"

Write-Host "=== BRQOSH Publish ===" -ForegroundColor Cyan

dotnet publish (Join-Path $Root "src\Brqosh.Desktop\Brqosh.Desktop.csproj") `
    -c Release -r win-x64 --self-contained false `
    -o $OutDir -p:PublishSingleFile=false

$rootBat = Get-ChildItem -LiteralPath $Root -Filter "*.bat" -File |
    Where-Object { $_.Name -ne "BRQOSH-Production.bat" } |
    Select-Object -First 1
if ($rootBat) {
    Copy-Item -LiteralPath $rootBat.FullName -Destination $OutDir -ErrorAction SilentlyContinue
}
$launchBat = Join-Path $OutDir "BRQOSH-Production.bat"
@"
@echo off
set BRQOSH_ENV=production
start "" "%~dp0Brqosh.Desktop.exe"
"@ | Set-Content -Path $launchBat -Encoding ASCII

if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
Compress-Archive -Path $OutDir -DestinationPath $ZipPath -Force

Write-Host "Done: $OutDir" -ForegroundColor Green
Write-Host "ZIP:  $ZipPath" -ForegroundColor Green
Write-Host "Production launcher: $launchBat (sets BRQOSH_ENV=production)" -ForegroundColor Yellow
Write-Host "See docs/postgres-production.md and installer/README.md" -ForegroundColor Yellow
