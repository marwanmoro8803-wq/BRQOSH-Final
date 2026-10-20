#Requires -Version 7
<#
.SYNOPSIS
    (ح-١٤٢ · installer-deployment-ops#2 · commercial-readiness#9) إصدارٌ واحدٌ من أوّله إلى قناته.

.DESCRIPTION
    ‏ما كان: لا خطَّ إصدار — CI لا يبني MSI، والحزمةُ الوحيدةُ بناءٌ محلّيٌّ بالرقم 1.0.0 بلا سجلّ تغييراتٍ
    ‏ولا قناة. وهذا السكربتُ هو الخطُّ نفسُه، يُشغّله `.github/workflows/release.yml` ويُشغَّل محلّيّاً بالترتيب ذاته:

      ١. الإصدارُ = BrqoshVersionPrefix + رقمُ البناء (والصفرُ مرفوض)، وقسمُه في CHANGELOG.md شرط.
      ٢. installer/build-msi.ps1 -BuildNumber (يرفض رقماً مشحوناً، يتحقّق من 64-بت وICE، ويوقّع إن ضُبط التوقيع).
      ٣. FileVersion الثنائيّة = الإصدار، وإلا فالحزمةُ ليست ما يُسمّى.
      ٤. tools/UpdatePublisher publish إلى مجلّد القناة بالملاحظات من السجلّ، ثمّ verify بمرساة البناء.
      ٥. -Ship وحدَه يُسجّل الرقمَ في installer/shipped-versions.txt — يُسجَّل عند الشحن لا عند البناء.

.EXAMPLE
    pwsh scripts/release.ps1 -BuildNumber 12 -UpdateKey "$env:APPDATA\BRQOSH\vendor-signing\update-private.pem" -Channel D:\brqosh-channel
#>
param(
    [Parameter(Mandatory)][int]$BuildNumber,
    [Parameter(Mandatory)][string]$UpdateKey,
    [Parameter(Mandatory)][string]$Channel,
    [switch]$Ship
)

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'release-lib.ps1')

$prefix = Get-BrqoshVersionPrefix -Props (Join-Path $Root 'Directory.Build.props')
$version = Get-BrqoshReleaseVersion -Prefix $prefix -BuildNumber $BuildNumber
$notes = Get-BrqoshReleaseNotes -Changelog (Join-Path $Root 'CHANGELOG.md') -Version $version
Assert-BrqoshEulaFinal -Eula (Join-Path $Root 'assets\legal\eula.txt')   # (installer-deployment-ops#13)
if (-not (Test-Path $UpdateKey)) { throw "مفتاحُ التحديث غير موجود: $UpdateKey" }

Write-Host "=== إصدار $version ===" -ForegroundColor Cyan

& (Join-Path $Root 'installer\build-msi.ps1') -BuildNumber $BuildNumber
if ($LASTEXITCODE -ne 0) { throw "build-msi.ps1 أخفق (exit=$LASTEXITCODE)." }

$msi = Join-Path $Root 'dist\BRQOSH-Setup.msi'
$exe = Join-Path $Root 'dist\BRQOSH\Brqosh.Desktop.exe'
$built = ((Get-Item $exe).VersionInfo.FileVersion -split '\.')[0..2] -join '.'
if ($built -ne $version) {
    throw "الثنائيّةُ المبنيّة $built وليست $version — لا يُنشر ما لا يحمل اسمَه."
}

$releaseDir = Join-Path $Root 'dist\release'
New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null
$named = Join-Path $releaseDir "BRQOSH-Setup-$version.msi"
Copy-Item $msi $named -Force

$publisher = Join-Path $Root 'tools\UpdatePublisher\UpdatePublisher.csproj'
& dotnet run --project $publisher -c Release -- publish $named $version $UpdateKey $Channel --notes $notes
if ($LASTEXITCODE -ne 0) { throw "النشرُ إلى القناة أخفق (exit=$LASTEXITCODE)." }

& dotnet run --project $publisher -c Release --no-build -- verify (Join-Path $Channel 'manifest.json') $version
if ($LASTEXITCODE -ne 0) { throw "القناةُ المنشورة لا تجتاز تحقّقَ العميل (exit=$LASTEXITCODE)." }

if ($Ship) {
    Add-Content -Path (Join-Path $Root 'installer\shipped-versions.txt') -Value $version -Encoding utf8
    Write-Host "سُجِّل $version مشحوناً في installer\shipped-versions.txt — أودِعه." -ForegroundColor Yellow
}

Write-Host "الحزمة: $named" -ForegroundColor Green
Write-Host "القناة: $Channel" -ForegroundColor Green
