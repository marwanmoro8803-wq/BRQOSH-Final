#Requires -Version 5.1
<#
.SYNOPSIS
    بناء MSI لـ BRQOSH باستخدام WiX Toolset v4+.
    يتطلب: wix (dotnet tool install --global wix)
#>
param([int]$BuildNumber = 0)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$PublishDir = Join-Path $Root "dist\BRQOSH"
$MsiOut = Join-Path $Root "dist\BRQOSH-Setup.msi"

Write-Host "=== BRQOSH MSI Build ===" -ForegroundColor Cyan

# ‏(م-٦ · ن-١٢) ‏**التوقيعُ صار يشمل الحمولةَ — وكان الحزمةَ وحدَها.**
#
# ‏توقيعُ MSI يحمي **قناةَ التسليم**: تحذيرُ SmartScreen، وتحريفُ المُنصِّب أثناء النقل.
# ‏**ولا يترك أثراً بعد التنصيب** — الحزمةُ تفكّ حمولتَها والحمولةُ تبقى بلا توقيع. فتطبيقٌ
# ‏مُنصَّبٌ من حزمةٍ موقَّعةٍ تماماً يقول UAC عنه «ناشرٌ غير معروف»، **ولا تستطيع سياسةُ
# ‏قوائمِ سماحٍ (WDAC/AppLocker) أن تميّزه من ثنائيّةٍ مُستبدَلةٍ في مجلّد التنصيب** —
# ‏وهو المسارُ الذي يُهاجَم فعلاً، لأنّ المُنصِّبَ يُستعمل مرّةً والملفّاتُ تبقى.
#
# ‏**والترتيبُ مُلزِم**: التوقيعُ يُلحق كتلةً بملفّ PE فيتغيّر محتواه وبصمتُه. فتوقيعُ
# ‏الحمولة **قبل أن يحصدها WiX** — وإلّا حملت الحزمةُ نسخاً غيرَ موقَّعةٍ بينما القرصُ
# ‏يحمل الموقَّعة، **وهو أسوأُ الحالَين**: يُقاس التوقيعُ على القرص فيُقرأ نجاحاً.
# ‏(ح-١٢٨) ‏**الطريقةُ من `signing.ps1`** — بصمةُ شهادةٍ في مخزن ويندوز (رمزٌ أو HSM، وهي ما تُصدره الجهاتُ اليوم) أو ملفُّ PFX.
# ‏وكان PFX وحدَه، فالشهادةُ التي ستصل لا يقبلها الخطّاف. **وإعدادٌ متناقضٌ أو بصمةٌ غيرُ صالحةٍ تُسقط البناء** لا تُتخطّى.
. (Join-Path $PSScriptRoot 'signing.ps1')
$script:Signing = Resolve-BrqoshSigning
if ($script:Signing.Mode -eq 'Error') {
    Write-Host "إعدادُ التوقيع: $($script:Signing.Error) — أُسقط البناء." -ForegroundColor Red
    exit 1
}
$script:SignTool = $null

function Invoke-BrqoshSign {
    param(
        [Parameter(Mandatory)][string[]]$Paths,
        [Parameter(Mandatory)][string]$What
    )

    if ($script:Signing.Mode -eq 'None') { return }

    $targets = @($Paths | Where-Object { Test-Path $_ })
    if ($targets.Count -eq 0) {
        # ‏**شهادةٌ حاضرةٌ وهدفٌ غائبٌ عطلٌ لا تخطٍّ**: صمتٌ هنا يُخرج حزمةً يظنّها بانيها موقّعة.
        Write-Host "لا هدفَ للتوقيع في «$What» — أُسقط البناء." -ForegroundColor Red
        exit 1
    }

    if (-not $script:SignTool) {
        $script:SignTool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue |
                           Sort-Object FullName -Descending | Select-Object -First 1 | ForEach-Object FullName
    }

    if ($script:SignTool) {
        $signArgs = @($script:Signing.Args)
        & $script:SignTool sign @signArgs @targets
        if ($LASTEXITCODE -ne 0) {
            Write-Host "توقيعُ «$What» أخفق (exit=$LASTEXITCODE)." -ForegroundColor Red
            exit 1
        }
    }
    elseif ($script:Signing.Mode -eq 'CertificateStore') {
        # ‏بلا signtool (لا حزمةَ SDK على الجهاز): التوقيعُ بـSet-AuthenticodeSignature المضمَّن في ويندوز — يوقّع PE وMSI بالشهادة نفسِها من المخزن.
        $thumbprint = ($env:BRQOSH_SIGNING_THUMBPRINT -replace '\s', '').ToUpperInvariant()
        $cert = Get-ChildItem Cert:\CurrentUser\My, Cert:\LocalMachine\My -ErrorAction SilentlyContinue | Where-Object Thumbprint -eq $thumbprint | Select-Object -First 1
        if (-not $cert) { Write-Host "الشهادةُ $thumbprint غيرُ موجودةٍ في المخزن — أُسقط البناء." -ForegroundColor Red; exit 1 }
        $stamp = if ($env:BRQOSH_SIGNING_TIMESTAMP) { $env:BRQOSH_SIGNING_TIMESTAMP } else { 'http://timestamp.digicert.com' }
        foreach ($t in $targets) {
            $r = Set-AuthenticodeSignature -FilePath $t -Certificate $cert -HashAlgorithm SHA256 -TimestampServer $stamp
            if (-not $r.SignerCertificate) { Write-Host "توقيعُ $(Split-Path $t -Leaf) أخفق: $($r.StatusMessage)" -ForegroundColor Red; exit 1 }
        }
    }
    else {
        Write-Host "التوقيعُ مضبوطٌ ($($script:Signing.Mode)) ولا signtool — أُسقط البناء." -ForegroundColor Red
        exit 1
    }

    # ‏**والتحقّقُ بعد التنفيذ هو العضّة**: وسمُ `signtool` في السطر تعليقٌ، وهذا قياس.
    # ‏ويُفحص **كلُّ** هدفٍ لا أوّلُه — فتوقيعٌ جزئيٌّ يُقرأ نجاحاً، وهو أسوأُ من لا توقيع.
    foreach ($target in $targets) {
        $sig = Get-AuthenticodeSignature $target
        # ‏شهادةٌ ذاتيّةُ التوقيع (BRQOSH_SIGNING_SELFSIGNED=1) تُثبت سلامةَ الملفّ لا هويّةَ الناشر: يُقبل «غيرُ موثوق الجذر» لها وحدَها، وبشرط أن يطابق الموقِّعُ بصمتَنا.
        $selfSignedOk = $env:BRQOSH_SIGNING_SELFSIGNED -eq '1' -and $sig.SignerCertificate -and
                        $sig.SignerCertificate.Thumbprint -eq (($env:BRQOSH_SIGNING_THUMBPRINT -replace '\s', '').ToUpperInvariant()) -and
                        $sig.StatusMessage -match 'not trusted|untrusted|root'
        if ($sig.Status -ne 'Valid' -and -not $selfSignedOk) {
            Write-Host "توقيعُ $(Split-Path $target -Leaf): $($sig.Status) — أُسقط البناء." -ForegroundColor Red
            exit 1
        }
    }

    $subject = (Get-AuthenticodeSignature $targets[0]).SignerCertificate.Subject
    Write-Host "موقَّع — $What ($($targets.Count) ملفّاً): $subject" -ForegroundColor Green
}

# 1. Publish portable
# ‏(CEEF-236 · م-٣٣) خروجُ النشر يُفحص: `&` على سكربتٍ لا يرفع استثناءً حين يخرج
# ‏ذاك السكربت بغير الصفر، و`$ErrorActionPreference = "Stop"` **لا يوقف برنامجاً
# ‏أصليّاً فاشلاً**. فبلا هذا يمضي البناءُ فوق شجرةِ نشرٍ قديمة، فتخرج حزمةٌ
# ‏خضراءُ الظاهر تحمل ثنائيّاتِ الأمس ورقمَ إصدارِ اليوم.
& (Join-Path $Root "installer\publish.ps1") -BuildNumber $BuildNumber
if ($LASTEXITCODE -ne 0) {
    Write-Host "publish.ps1 أخفق (exit=$LASTEXITCODE) — لا MSI يُبنى." -ForegroundColor Red
    exit $LASTEXITCODE
}

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
# ‏(م-١ · ن-٠١) ‏**الإصدارُ يُقرأ من الثنائيّة المنشورة، لا من ملفّ الخصائص بتعبيرٍ نمطيّ.**
#
# ‏كان يُستخرج بـ`Select-String` من `<BrqoshVersion>`. وذلك يكسر لسببين:
# ‏  ١) صار الرقمُ مركّباً (`$(BrqoshVersionPrefix).$(BrqoshBuildNumber)`)، فالتعبيرُ
# ‏     النمطيُّ يلتقط **نصَّ التعبير** لا قيمتَه.
# ‏  ٢) وأعمقُ: الملفُّ يقول ما **يُفترض** أن يكون، والثنائيّةُ تقول ما **كان**. فلو
# ‏     نُشرت الشجرةُ بخصائصَ مختلفة، شحنت الحزمةُ رقماً لا يطابق ما بداخلها — وهو
# ‏     بالضبط العطلُ الذي جاءت هذه المرحلةُ لإغلاقه.
#
# ‏فالمصدرُ الآن `Brqosh.Desktop.exe` المنشورُ نفسُه: رقمُه هو رقمُ الحزمة بالتعريف.
$MainExe = Join-Path $PublishDir "Brqosh.Desktop.exe"
if (-not (Test-Path $MainExe)) {
    Write-Host "لا ثنائيّةً منشورةً في $PublishDir — لا رقمَ إصدارٍ يُقرأ." -ForegroundColor Red
    exit 1
}

$FileVersion = (Get-Item $MainExe).VersionInfo.FileVersion
if ($FileVersion -notmatch '^\d+\.\d+\.\d+') {
    Write-Host "FileVersion غيرُ مفهوم: '$FileVersion'." -ForegroundColor Red
    exit 1
}

# ‏ثلاثةُ حقولٍ لا أربعة: Windows Installer يُهمل الرابع في المقارنة، فحملُه يُوهم
# ‏باختلافٍ لا وجودَ له عنده.
$ProductVersion = ($FileVersion -split '\.')[0..2] -join '.'
$InformationalVersion = (Get-Item $MainExe).VersionInfo.ProductVersion
Write-Host "الإصدار: $ProductVersion   (البصمة: $InformationalVersion)" -ForegroundColor Cyan

# ‏(م-١ · ن-٠١) ‏**بوّابةُ بناءٍ ترفض إصداراً سبق شحنُه.**
#
# ‏`AllowSameVersionUpgrades` يُنقذ الترقيةَ حين يتساوى الرقمان، لكنّه لا يجعل التساويَ
# ‏صحيحاً: إصداران بالرقم نفسِه لا يُميَّزان في «إضافة وإزالة البرامج» ولا في سجلّ عطل
# ‏ولا في بلاغِ عميل. فالتساوي يُمنع هنا، ويُنقذ هناك.
$Ledger = Join-Path $PSScriptRoot "shipped-versions.txt"
# ‏(ح-٧٣) ‏البوّابةُ في ملفٍّ يُشغّله `verify-gates.ps1` أيضاً — وعلى دفترٍ مؤقّتٍ هناك لا على هذا.
& (Join-Path $PSScriptRoot "..\scripts\assert-version-not-shipped.ps1") -Version $ProductVersion -Ledger $Ledger

# ‏(data-upgrade-performance#6 · installer-deployment-ops#10 · قرارُ المالك 2026-10-18) ‏**أدواتُ النسخ تُشحن مع البرنامج.**
#
# ‏كان النسخُ المجدولُ يعمل فقط حيث ثُبِّت PostgreSQL يدويّاً، والمُنصِّبُ لا يشحن الأدواتِ ولا يفحصها. فتُنسخ هنا إلى
# ‏`pgtools` تحت الحمولة — و`Files Include="**"` يحصدها — من تثبيت PostgreSQL على جهاز البناء (ترخيصُه يسمح بإعادة
# ‏التوزيع)، **ومعها إشعاراتُ ترخيص المكتبات كما شحنها مصدرُها حرفاً**. والقائمةُ إغلاقُ جداول الاستيراد لـpg_dump
# ‏وpg_restore (مقروءةٌ من ملفّاتها)، و`VCRUNTIME140.dll` محلّيٌّ بجوارها لأنّ ويندوز لا يشحنه. **ولا تُوقَّع بمفتاحنا**:
# ‏الحمولةُ الموقَّعةُ `Brqosh.*` في الجذر وحدَه، وثنائيّاتُ PostgreSQL تحمل توقيعَ ناشرها.
$PgBin = if ($env:BRQOSH_PG_BIN) { $env:BRQOSH_PG_BIN } else { "C:\Program Files\PostgreSQL\17\bin" }
$PgToolFiles = @(
    'pg_dump.exe', 'pg_restore.exe', 'libpq.dll', 'libintl-9.dll', 'libiconv-2.dll', 'libwinpthread-1.dll',
    'liblz4.dll', 'libzstd.dll', 'libssl-3-x64.dll', 'libcrypto-3-x64.dll'
)
$PgNotices = @('server_license.txt', 'commandlinetools_3rd_party_licenses.txt')
$PgToolsDir = Join-Path $PublishDir "pgtools"
$missingPg = @($PgToolFiles | Where-Object { -not (Test-Path (Join-Path $PgBin $_)) }) +
             @($PgNotices | Where-Object { -not (Test-Path (Join-Path (Split-Path $PgBin -Parent) $_)) })
if ($missingPg.Count -gt 0) {
    Write-Host "أدوات PostgreSQL ناقصة في $PgBin — لا حزمةَ بلا أدوات النسخ: $($missingPg -join ', ')" -ForegroundColor Red
    exit 1
}
$pgVersion = & (Join-Path $PgBin 'pg_dump.exe') --version
if ($pgVersion -notmatch '\) (\d+)\.' -or [int]$Matches[1] -lt 17) {
    Write-Host "pg_dump في $PgBin إصدارُه '$pgVersion' — المشحونُ 17 فما فوق (أقدمُ من الخادم يرفضه PostgreSQL)." -ForegroundColor Red
    exit 1
}
New-Item -ItemType Directory -Force $PgToolsDir | Out-Null
foreach ($f in $PgToolFiles) { Copy-Item (Join-Path $PgBin $f) $PgToolsDir -Force }
foreach ($f in $PgNotices) { Copy-Item (Join-Path (Split-Path $PgBin -Parent) $f) $PgToolsDir -Force }
Copy-Item (Join-Path $env:SystemRoot 'System32\vcruntime140.dll') $PgToolsDir -Force
Write-Host "أدوات النسخ: $pgVersion ⟵ $PgToolsDir" -ForegroundColor Cyan

# (CEEF-236 · ر-ك · م-٣٢) ‏**‏-arch x64 — والحزمةُ كانت تُبنى ٣٢-بت بالافتراض.**
#
# ‏`wix build` يبني x86 ما لم يُقَل غيرُه، **وحزمةٌ ٣٢-بت يُعيد MSI فيها توجيهَ
# ‏`ProgramFiles64Folder` إلى `Program Files (x86)`** — فينتهي تطبيقُ x64 في شجرة ٣٢-بت
# ‏وتُكتب علامةُ السجلّ في `WOW6432Node`. والوسمُ في `brqosh.wxs` كان يقول 64 والناتجُ يخالفه.
#
# ‏**ولم يكشفه بناءٌ ولا اختبارٌ ولا مراجعة** — كشفه تنصيبٌ حيٌّ على آلةٍ نظيفة (CEEF-236 · ر-ك):
# ‏  C:\Program Files\BRQOSH\Brqosh.Desktop.exe        False
# ‏  C:\Program Files (x86)\BRQOSH\Brqosh.Desktop.exe  True
# ‏**الحمولةُ تُوقَّع هنا** — قبل السطر التالي بالضبط، فـ`wix build` يقرأ `$PublishDir`
# ‏من القرص. وتُختار الثنائيّاتُ التي **نملكها** بنمطٍ لا بقائمةٍ مكتوبةٍ باليد: مشروعٌ
# ‏جديدٌ يدخل الحزمةَ يدخل التوقيعَ معها، **ولا يُنسى سطرٌ في سكربتٍ لا يقرؤه أحد**.
$PayloadBinaries = @(Get-ChildItem $PublishDir -File |
    Where-Object { $_.Name -like 'Brqosh.*' -and $_.Extension -in '.exe', '.dll' } |
    ForEach-Object FullName)
Invoke-BrqoshSign -Paths $PayloadBinaries -What "الحمولة"

# مراجعةُ الإصدار 1.0.1: التعبيرُ لا يُقيَّم داخل وسيطِ أمرٍ أصليّ — -d BrandIcon=(Join-Path ...) كان يصل إلى wix فارغاً فلا MSI. فالمسارُ يُحسب قبل النداء.
$BrandIconPath = Join-Path $Root 'assets\brand\brqosh.ico'
if (-not (Test-Path $BrandIconPath)) { throw "أيقونةُ البرنامج غير موجودة: $BrandIconPath" }
$UninstallIconPath = Join-Path $Root 'assets\brand\brqosh-uninstall.ico'
if (-not (Test-Path $UninstallIconPath)) { throw "أيقونةُ الإزالة غير موجودة: $UninstallIconPath — شغّل tools\brand\build_brand.py" }

# ‏(واجهةُ التنصيب) ‏أصولُ الواجهة تُولَّد من مصدرٍ واحد (eula.txt وشعارُ الهويّة) قبل البناء.
$UiDir = Join-Path $Root 'dist\installer-ui'
& (Join-Path $PSScriptRoot 'make-ui-assets.ps1') -OutDir $UiDir -ProductVersion $ProductVersion
if ($LASTEXITCODE -ne 0 -and $null -ne $LASTEXITCODE) { throw "make-ui-assets.ps1 أخفق (exit=$LASTEXITCODE)." }
$UiWxs = Join-Path $PSScriptRoot 'ui.wxs'

# ‏`-culture ar-SA`: نصوصُ حوارات WixUI بالعربيّة؛ وحوارُ الإزالةُ ثنائيُّ اللغة.
function Invoke-BrqoshWixBuild([string]$Culture, [string]$Out) {
    wix build $WxsPath $UiWxs `
        -arch x64 `
        -ext WixToolset.UI.wixext -ext WixToolset.Util.wixext `
        -culture $Culture `
        -d "PublishDir=$PublishDir" `
        -d "ProductVersion=$ProductVersion" `
        -d "BrandIcon=$BrandIconPath" `
        -d "UninstallIcon=$UninstallIconPath" `
        -d "LicenseRtf=$(Join-Path $UiDir 'license.rtf')" `
        -d "BannerBmp=$(Join-Path $UiDir 'banner.bmp')" `
        -d "DialogBmp=$(Join-Path $UiDir 'dialog.bmp')" `
        -o $Out
}

Invoke-BrqoshWixBuild 'ar-SA' $MsiOut

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

# ‏(م-١ · ن-٣٩) ‏**تحقّقُ ICE — كان يُتخطّى، والحزمةُ تسقط فيه بأربعة أخطاء.**
#
# ‏`wix build` لا يُشغّل ICE افتراضياً، فشُحنت حزمةٌ تحمل `ICE30` أربع مرّات:
# ‏`Brqosh.Desktop.exe` مُثبَّتٌ بمكوّنين — المُعلَنُ صراحةً والمُحصودُ آلياً — و«‏This
# ‏breaks component reference counting». وعدُّ المراجع هو ما يقرّر ما يبقى وما يُحذف
# ‏عند الترقية والإزالة، فالخطأُ في صميم قناة التسليم لا في زينتها.
Write-Host "تحقّقُ ICE..." -ForegroundColor Yellow
$iceLog = Join-Path $Root "dist\ice-validation.txt"
& wix msi validate $MsiOut > $iceLog 2>&1
$iceExit = $LASTEXITCODE
$iceErrors = @(Select-String -Path $iceLog -Pattern 'error WIX\d+' -ErrorAction SilentlyContinue)
if ($iceExit -ne 0 -or $iceErrors.Count -gt 0) {
    Write-Host "تحقّقُ ICE سقط ($($iceErrors.Count) خطأ · exit=$iceExit) — انظر $iceLog" -ForegroundColor Red
    $iceErrors | Select-Object -First 5 | ForEach-Object { Write-Host "   $($_.Line.Trim())" -ForegroundColor Red }
    Remove-Item $MsiOut -Force -ErrorAction SilentlyContinue
    exit 1
}
$iceWarn = @(Select-String -Path $iceLog -Pattern 'warning WIX\d+' -ErrorAction SilentlyContinue)
Write-Host "تحقّقُ ICE: صفرُ أخطاء · $($iceWarn.Count) تحذيراً" -ForegroundColor Green

# ‏(م-١ · ن-١٢) ‏**خُطّافُ التوقيع — والشهادةُ تبعيّةٌ خارجيّة.**
#
# ‏الحزمةُ اليومَ `NotSigned`: كلُّ تنصيبٍ يُظهر تحذيرَ SmartScreen، ولا سبيلَ للعميل
# ‏إلى التمييز بين حزمتنا وحزمةٍ مُحرَّفة. والتوقيعُ يلزمه شهادةُ توقيعِ كودٍ لا يملكها
# ‏سكربتُ بناء — فالخُطّافُ هنا والقرارُ هناك.
#
# ‏وضبطُ `BRQOSH_SIGNING_THUMBPRINT` (رمزٌ أو HSM) أو `BRQOSH_SIGNING_PFX` يُشغّله (ح-١٢٨). وغيابُهما **يُعلَن** ولا يُبتلع:
# ‏حزمةٌ غيرُ موقّعةٍ تخرج ومعها سطرٌ يقول إنّها كذلك، فلا تُشحن على أنّها موقّعة.
# ‏(اللغة) ‏نسخةٌ إنجليزيّةُ الواجهة من الحزمة نفسِها: ‏خيارُ اللغة = أيُّ ملفٍّ تفتح؛ لا مُشغِّلَ إضافيّ (مُشغِّلٌ غيرُ موقَّع كشفه Defender وحذفه).
# ‏ترميزُ الحزمة مثبَّتٌ على 1256 في brqosh.wxs فلا يسقط البناءُ على نصوص الحوارات العربيّة.
$MsiEn = Join-Path $Root "dist\BRQOSH-Setup-en.msi"
Invoke-BrqoshWixBuild 'en-US' $MsiEn
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $MsiEn)) { Write-Host "بناءُ الحزمة الإنجليزيّة أخفق." -ForegroundColor Red; exit 1 }
Invoke-BrqoshSign -Paths @($MsiOut) -What "الحزمة"
Invoke-BrqoshSign -Paths @($MsiEn) -What "الحزمة الإنجليزيّة"
if ($script:Signing.Mode -eq 'None') {
    Write-Host "غيرُ موقَّعة — لا BRQOSH_SIGNING_THUMBPRINT ولا BRQOSH_SIGNING_PFX. (ن-١٢ · تبعيّةٌ خارجيّة: شهادةُ توقيع كود)" -ForegroundColor Yellow
}

Write-Host "MSI: $MsiOut" -ForegroundColor Green

Write-Host "‏لتسجيل هذا الإصدار مشحوناً: أضف '$ProductVersion' إلى installer\shipped-versions.txt" -ForegroundColor DarkGray
