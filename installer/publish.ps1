#Requires -Version 5.1
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$OutDir = Join-Path $Root "dist\BRQOSH"
$ZipPath = Join-Path $Root "dist\BRQOSH-portable.zip"

Write-Host "=== BRQOSH Publish ===" -ForegroundColor Cyan

dotnet publish (Join-Path $Root "src\Brqosh.Desktop\Brqosh.Desktop.csproj") `
    -c Release -r win-x64 --self-contained false `
    -o $OutDir -p:PublishSingleFile=false

# (CEEF-236 - t-8) The repo-root .bat is NOT shipped.
#
# This block copied the first *.bat from the repo root into the publish folder, which
# <Files Include="$(var.PublishDir)\**" /> then harvested into the MSI. That file is
# تشغيل-برقوش.bat - a DEVELOPER launcher running `dotnet run -c Debug` against
# "%~dp0src\Brqosh.Desktop\Brqosh.Desktop.csproj". On a client machine there is no src
# tree and no SDK, so the shortcut fails with a confusing error - and it advertises a
# Debug path inside Program Files. Measured: dist/BRQOSH/تشغيل-برقوش.bat was present.
#
# The production launcher below is written explicitly and is the only .bat that ships.
# (CEEF-229 - m0-12) Authoritative environment marker.
# Harvested by <Files Include="$(var.PublishDir)\**" /> so it lands in Program Files,
# which a standard user cannot write to. AppEnvironmentConfig reads it and refuses to be
# downgraded by a user-scope BRQOSH_ENV (setx needs no admin rights).
Set-Content -Path (Join-Path $OutDir "brqosh.environment") -Value "production" -Encoding ASCII -NoNewline

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
Write-Host "Authoritative marker: brqosh.environment = production (cannot be downgraded by user env)" -ForegroundColor Yellow
Write-Host "See docs/postgres-production.md and installer/README.md" -ForegroundColor Yellow
