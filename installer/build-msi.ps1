#Requires -Version 5.1
<#
.SYNOPSIS
    بناء MSI لـ BRQOSH باستخدام WiX Toolset v4+.
    يتطلب: wix (dotnet tool install --global wix)
#>
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$PublishDir = Join-Path $Root "dist\BRQOSH"
$MsiOut = Join-Path $Root "dist\BRQOSH-Setup.msi"

Write-Host "=== BRQOSH MSI Build ===" -ForegroundColor Cyan

# 1. Publish portable
& (Join-Path $Root "installer\publish.ps1")

if (-not (Get-Command wix -ErrorAction SilentlyContinue)) {
    Write-Host "WiX CLI غير مثبت. ثبّت بـ: dotnet tool install --global wix" -ForegroundColor Yellow
    Write-Host "ثم أعد تشغيل: installer\build-msi.ps1" -ForegroundColor Yellow
    exit 1
}

$WxsPath = Join-Path $PSScriptRoot "brqosh.wxs"
$WixObj = Join-Path $Root "dist\brqosh.wixobj"

wix build $WxsPath `
    -d PublishDir=$PublishDir `
    -o $MsiOut

Write-Host "MSI: $MsiOut" -ForegroundColor Green
