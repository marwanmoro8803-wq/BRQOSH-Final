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

# (CEEF-231 - t5-5) رقم الإصدار من مصدر واحد.
#
# كان مثبتا هنا (1.0.0.0) وفي التجميعات لا شيء - فالمثبت يقول شيئا والتطبيق آخر.
# وأخطر من ذلك: MajorUpgrade يعتمد على رقم الـMSI، فإصدار يشحن بلا تحرير هذا السطر
# بيد لا يرقي تثبيتا قائما - ينصب جنبه أو يرفض.
$PropsPath = Join-Path $Root "Directory.Build.props"
if (-not (Test-Path $PropsPath)) {
    Write-Host "Directory.Build.props مفقود - ولا رقم إصدار يشتق منه." -ForegroundColor Red
    exit 1
}

$VersionMatch = Select-String -Path $PropsPath -Pattern "<BrqoshVersion>([^<]+)</BrqoshVersion>"
if (-not $VersionMatch) {
    Write-Host "BrqoshVersion غير موجود في Directory.Build.props." -ForegroundColor Red
    exit 1
}

$ProductVersion = "$($VersionMatch.Matches[0].Groups[1].Value).0"
Write-Host "الإصدار: $ProductVersion" -ForegroundColor Cyan

wix build $WxsPath `
    -d PublishDir=$PublishDir `
    -d ProductVersion=$ProductVersion `
    -o $MsiOut

Write-Host "MSI: $MsiOut" -ForegroundColor Green
