#Requires -Version 5.1
<#
.PARAMETER BuildNumber
  ‏(م-١ · ن-٠١) ‏الحقلُ الثالث من رقم الإصدار. الافتراضُ `0` = بناءٌ محلّيّ لا يدّعي
  ‏أنّه إصدار. وفي CI يُمرَّر عدّادُ التشغيلة فيصير كلُّ بناءٍ إصداراً مميَّزاً.

  ‏**ولا يكفي ضبطُ `-p:BrqoshBuildNumber` قبل استدعاء هذا السكربت**: هو يُشغّل
  ‏`dotnet publish` بنفسه، فالخاصّيّةُ يجب أن تصله كمُعامل لا كأثرٍ جانبيّ — وقد
  ‏وقع ذلك فعلاً: نُشر بـ7 ثمّ أعاد `build-msi.ps1` النشرَ بـ0 فدهس الرقم.
#>
# -NoRestore uses previously restored packages; it does not ignore missing dependencies.
param([int]$BuildNumber = 0, [switch]$NoRestore, [string]$OutDir)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot

# **وجهةُ النشر قابلةٌ للتخصيص** (٢٠٢٦-١٠-٢٧): شجرةُ النشر الافتراضيّة قد تكون مفتوحةً في
# عمليّاتِ تطبيقٍ عاملة، فيُتعذّر مسحُها فيسقط كلُّ بناءِ مُنصِّب. والتخصيصُ يفتح بناءً
# متوازياً أو تحقّقاً بلا إغلاقِ تشغيلِ المستخدم — **والافتراضُ لم يتغيّر بحرف.**
if ([string]::IsNullOrWhiteSpace($OutDir)) {
    $OutDir = Join-Path $Root "dist\BRQOSH"
    $ZipPath = Join-Path $Root "dist\BRQOSH-portable.zip"
} else {
    $OutDir = [IO.Path]::GetFullPath($OutDir)
    $ZipPath = Join-Path $Root ("dist\BRQOSH-portable-{0}.zip" -f $BuildNumber)
}

Write-Host "=== BRQOSH Publish ===" -ForegroundColor Cyan

# (CEEF-236 · ر-ك · م-٣٣) شجرةُ النشر تُمسح قبل النشر، وخروجُ dotnet يُفحص.
#
# ‏`dotnet publish` كان يُستدعى بلا فحصِ `$LASTEXITCODE`، و`$ErrorActionPreference = "Stop"`
# ‏**لا يوقف برنامجاً أصليّاً يخرج بغير الصفر** — يوقف الأخطاءَ غيرَ المُنهية لـ PowerShell وحدَها.
# ‏فبناءٌ فاشل كان يمضي: تُكتب علامةُ البيئة وملفُّ التشغيل فوق شجرةِ نشرٍ **قديمة**، ثمّ
# ‏يحصدها WiX — فتخرج حزمةٌ خضراءُ الظاهر تحمل ثنائيّاتِ الأمس ورقمَ إصدارِ اليوم.
# ‏والمسحُ المسبق يمنع الحصادَ من بقايا نشرٍ سابق حتى لو تعطّل النشرُ في منتصفه.
if (Test-Path $OutDir) { Remove-Item $OutDir -Recurse -Force }

Write-Host "رقمُ البناء: $BuildNumber" -ForegroundColor DarkGray
$restoreArgs = @()
if ($NoRestore) { $restoreArgs += '--no-restore' }

# **هدفُ win-x64 يُستعاد إن غاب** (٢٠٢٦-١٠-٢٧): `dotnet build`/`dotnet test` بلا RID يعيد
# كتابةَ `project.assets.json` **بلا** هدف `net10.0-windows/win-x64`، فيسقط النشرُ بعدها
# بـ`NETSDK1047` — وقد وقع مرّتين في جلسةٍ واحدة، وكلُّ مرّةٍ تُقرأ كأنها عطلُ حزمة.
# والاستعادةُ هنا **محلّيّة** (الحزمُ مستعادةٌ سابقاً) ولا تُلغي معنى `-NoRestore`: الذي
# يمنع تنزيلَ حزمٍ لا يمنع تسجيلَ هدفٍ مفقود. وهذا يطابق ما يقوله السكربت نفسُه بعدها.
$desktopProject = Join-Path $Root "src\Brqosh.Desktop\Brqosh.Desktop.csproj"
$assetsFile = Join-Path $Root "src\Brqosh.Desktop\obj\project.assets.json"
if ((Test-Path $assetsFile) -and
    -not (Select-String -LiteralPath $assetsFile -Pattern '"net10.0-windows/win-x64"' -SimpleMatch -Quiet)) {
    Write-Host "استعادةُ هدف win-x64 الغائب من assets …" -ForegroundColor DarkGray
    dotnet restore $desktopProject -r win-x64 -m:1 -nr:false
    if ($LASTEXITCODE -ne 0) {
        Write-Host "restore -r win-x64 أخفق (exit=$LASTEXITCODE) — لا نشر." -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

dotnet publish $desktopProject `
    -c Release -r win-x64 --self-contained true `
    -o $OutDir -p:PublishSingleFile=false -p:BrqoshBuildNumber=$BuildNumber -p:BrqoshShipped=true -m:1 -nr:false @restoreArgs
# ‏(ح-٧٥) ‏`BrqoshShipped` يجعل الثنائيّةَ لا تعرف غيرَ الإنتاج — لا متغيّرُ بيئةٍ ولا ملفُّ علامةٍ ولا مسؤولٌ محلّيٌّ
# ‏يخفضها إلى تطويرٍ يفتح `BRQOSH_UITEST` والفترةَ التجريبيّة. **وهذا السكربتُ وحدَه يمرّرها.**
if ($LASTEXITCODE -ne 0) {
    Write-Host "dotnet publish فشل (exit=$LASTEXITCODE) — لا حزمةَ تُبنى من شجرةٍ ناقصة." -ForegroundColor Red
    exit $LASTEXITCODE
}

$publishedExe = Join-Path $OutDir "Brqosh.Desktop.exe"
if (-not (Test-Path $publishedExe)) {
    Write-Host "النشرُ انتهى بلا Brqosh.Desktop.exe — شجرةٌ ناقصة." -ForegroundColor Red
    exit 1
}

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
