# G-27 — regenerate UiBilingualRegistry.g.cs (100% XAML string coverage)
$ErrorActionPreference = "Stop"
$root = Join-Path $PSScriptRoot "..\.."
dotnet run --project (Join-Path $root "tools\I18nRegistryGen\I18nRegistryGen.csproj") -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& (Join-Path $PSScriptRoot "i18n-coverage-audit.ps1")
