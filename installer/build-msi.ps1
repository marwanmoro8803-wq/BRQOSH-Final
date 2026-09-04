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
    # (CEEF-234) v7+ يتوقف عند بناءٍ أول برسالة WIX7015 تطلب قبول "Open Source
    # Maintenance Fee" (OSMF) — قرارٌ ماليّ/تعاقديّ لا يُتّخذ من سكربت بناء. فالإصدارُ
    # المثبَّت هنا مثبَّتٌ صراحةً (v5)، وهو ما بُني به هذا المُثبِّت وقيس عليه.
    Write-Host "WiX CLI غير مثبت. ثبّت بـ: dotnet tool install --global wix --version 5.0.2" -ForegroundColor Yellow
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

# (CEEF-236 · ر-ك · م-٣٢) ‏**‏-arch x64 — والحزمةُ كانت تُبنى ٣٢-بت بالافتراض.**
#
# ‏`wix build` يبني x86 ما لم يُقَل غيرُه، **وحزمةٌ ٣٢-بت يُعيد MSI فيها توجيهَ
# ‏`ProgramFiles64Folder` إلى `Program Files (x86)`** — فينتهي تطبيقُ x64 في شجرة ٣٢-بت
# ‏وتُكتب علامةُ السجلّ في `WOW6432Node`. والوسمُ في `brqosh.wxs` كان يقول 64 والناتجُ يخالفه.
#
# ‏**ولم يكشفه بناءٌ ولا اختبارٌ ولا مراجعة** — كشفه تنصيبٌ حيٌّ على آلةٍ نظيفة (CEEF-236 · ر-ك):
# ‏  C:\Program Files\BRQOSH\Brqosh.Desktop.exe        False
# ‏  C:\Program Files (x86)\BRQOSH\Brqosh.Desktop.exe  True
wix build $WxsPath `
    -arch x64 `
    -d PublishDir=$PublishDir `
    -d ProductVersion=$ProductVersion `
    -o $MsiOut

if ($LASTEXITCODE -ne 0 -or -not (Test-Path $MsiOut)) {
    Write-Host "wix build أخفق (exit=$LASTEXITCODE) — لا حزمة." -ForegroundColor Red
    exit 1
}

# ‏(CEEF-236 · م-٣٢) **عضّةٌ لا تعليق:** يُقرأ `Template` من معلومات الملخّص في الحزمة الناتجة
# ‏(PID_TEMPLATE = 7، صيغتُه `arch;lang`)، فإن لم يبدأ بـ`x64` سقط البناءُ **هنا** — لا عند عميلٍ
# ‏يجد التطبيقَ في `Program Files (x86)`. فالسطرُ `-arch x64` أعلاه تعليقٌ قابلٌ للحذف؛ وهذا لا.
$installer = New-Object -ComObject WindowsInstaller.Installer
try {
    # ‏الصبُّ صريحٌ عن قصد: pwsh 7 يمرّر متغيّرَ النطاق العامّ مُغلَّفاً (PSObject) فيردّه COM
    # ‏بـDISP_E_TYPEMISMATCH — وقع فعلاً عند أوّل تشغيلٍ لهذه العضّة، بينما مرّ في دالةٍ ذاتِ مُعاملٍ مُنمَّط.
    $db = $installer.GetType().InvokeMember("OpenDatabase", "InvokeMethod", $null, $installer, [object[]]@([string]$MsiOut, [int]0))
    $si = $db.GetType().InvokeMember("SummaryInformation", "GetProperty", $null, $db, [object[]]@([int]0))
    $template = [string]$si.GetType().InvokeMember("Property", "GetProperty", $null, $si, [object[]]@([int]7))
}
finally {
    foreach ($o in @($si, $db, $installer)) {
        if ($o) { [void][Runtime.InteropServices.Marshal]::ReleaseComObject($o) }
    }
}
if ($template -notmatch '^x64;') {
    Write-Host "Template (arch;lang) = '$template' — الحزمةُ ليست 64-بت؛ أُسقط البناء (م-٣٢)." -ForegroundColor Red
    Remove-Item $MsiOut -Force -ErrorAction SilentlyContinue
    exit 1
}
Write-Host "Template (arch;lang) = $template" -ForegroundColor Green

Write-Host "MSI: $MsiOut" -ForegroundColor Green
