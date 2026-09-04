# مسح شامل لكل صفحات BRQOSH — لقطة أعلى/أسفل + جرد نصوص، بالعربية وبالإنجليزية (CEEF-221)
#
# لماذا آليّاً لا يدوياً؟ 100+ صفحة × لغتين × (أعلى+أسفل) = مئات اللقطات. الفحص البصريّ
# وحده لا يتوسّع؛ فالسكربت يجمع **الدليل النصّيّ** (جرد UIA لكل عنصر) الذي يُفحص برمجياً
# لكشف: نصّ عربيّ باقٍ في الوضع الإنجليزيّ، صفحة فارغة، زرّ بلا اسم، نافذة خطأ تظهر عند
# الفتح. ثم تُراجَع اللقطات بصرياً لما يُعلَّم فقط.
#
# قراءة فقط: لا يضغط زرّ حفظ/حذف/ترحيل — فقط أزرار التنقّل (AutomationId = Nav_*).
#
#   pwsh -File scripts/ops/sweep-ui-pages.ps1 -OutDir dist/ui-sweep

[CmdletBinding()]
param(
    [string]$WindowTitle = "BRQOSH ERP",
    [string]$OutDir = "dist/ui-sweep",
    [int]$SettleMs = 1100,
    [int]$TimeoutSeconds = 60
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms

$AE   = [System.Windows.Automation.AutomationElement]
$TS   = [System.Windows.Automation.TreeScope]
$CT   = [System.Windows.Automation.ControlType]

function Find-MainWindow {
    $cond = New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $WindowTitle)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $w = $AE::RootElement.FindFirst($TS::Children, $cond)
        if ($null -ne $w) { return $w }
        Start-Sleep -Milliseconds 400
    }
    throw "لم تُعثر على نافذة '$WindowTitle' — شغّل التطبيق أولاً."
}

# نافذة رسالة معيارية (MessageBox) تظهر فوق النافذة الرئيسية = عيب يُسجَّل ثم يُغلق،
# وإلا حجبت بقية المسح.
function Get-ModalDialog {
    param($Win)
    $cond = New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $CT::Window)
    $Win.FindFirst($TS::Children, $cond)
}

function Close-ModalDialog {
    param($Dlg)
    $btnCond = New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $CT::Button)
    $btns = $Dlg.FindAll($TS::Descendants, $btnCond)
    foreach ($b in $btns) {
        if ($b.Current.Name -match '^(موافق|OK|إغلاق|Close|نعم|Yes)$') {
            $b.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
            Start-Sleep -Milliseconds 350
            return $true
        }
    }
    return $false
}

function Save-WindowShot {
    param($Win, [string]$Path)
    $r = $Win.Current.BoundingRectangle
    if ($r.Width -le 0 -or $r.Height -le 0) { return $false }
    # النافذة المكبَّرة تمتدّ خارج الشاشة (‎-8) — القصّ يمنع شريطاً أسود على الحافة.
    $vs = [System.Windows.Forms.SystemInformation]::VirtualScreen
    $x = [int][Math]::Max($r.X, $vs.X)
    $y = [int][Math]::Max($r.Y, $vs.Y)
    $w = [int][Math]::Min($r.Right  - $x, $vs.Right  - $x)
    $h = [int][Math]::Min($r.Bottom - $y, $vs.Bottom - $y)
    if ($w -le 0 -or $h -le 0) { return $false }
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($x, $y, 0, 0, (New-Object System.Drawing.Size($w, $h)))
    $g.Dispose()
    $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return $true
}

# جرد كل ما يراه المستخدم: نصوص، أزرار، حقول، قوائم، رؤوس أعمدة.
function Get-PageInventory {
    param($Scope)
    $items = @()
    foreach ($t in @($CT::Text, $CT::Button, $CT::Edit, $CT::ComboBox, $CT::CheckBox,
                     $CT::RadioButton, $CT::Header, $CT::HeaderItem, $CT::TabItem, $CT::Hyperlink)) {
        $cond = New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $t)
        foreach ($e in $Scope.FindAll($TS::Descendants, $cond)) {
            $name = $e.Current.Name
            if ([string]::IsNullOrWhiteSpace($name)) {
                # زرّ بلا اسم = عيب وصول **إن كان مرئياً**. أزرار PageUp/PageDown داخل
                # شريط التمرير بلا اسم بالتصميم وبارتفاع صفر؛ عدّها عيباً أنتج 120
                # اكتشافاً كلّها ضجيج (2 لكل صفحة). فلا يُعدّ إلا ما له مساحة فعلية.
                $r = $e.Current.BoundingRectangle
                if ($t -eq $CT::Button -and -not $e.Current.IsOffscreen `
                    -and [double]::IsFinite($r.Width) -and $r.Width -ge 8 -and $r.Height -ge 8) {
                    $items += [pscustomobject]@{ Type = 'Button'; Text = '<بلا اسم>' }
                }
                continue
            }
            $items += [pscustomobject]@{ Type = $t.ProgrammaticName -replace '^ControlType\.', ''; Text = $name }
        }
    }
    return $items
}

function Set-ScrollPercent {
    param($Scope, [double]$Percent)
    $cond = New-Object System.Windows.Automation.PropertyCondition($AE::IsScrollPatternAvailableProperty, $true)
    $sv = $Scope.FindFirst($TS::Subtree, $cond)
    if ($null -eq $sv) { return $false }
    $p = $sv.GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern)
    if (-not $p.Current.VerticallyScrollable) { return $false }
    $p.SetScrollPercent([System.Windows.Automation.ScrollPattern]::NoScroll, $Percent)
    Start-Sleep -Milliseconds 450
    return $true
}

$root = Resolve-Path -LiteralPath (Split-Path -Parent $PSCommandPath) | Split-Path -Parent | Split-Path -Parent
$out = Join-Path $root $OutDir
New-Item -ItemType Directory -Force -Path $out | Out-Null

$win = Find-MainWindow
Write-Host "[OK] النافذة: $($win.Current.Name)" -ForegroundColor Green

# نافذة خطأ عالقة من قبل المسح
$pre = Get-ModalDialog -Win $win
if ($null -ne $pre) {
    Write-Host "[!] نافذة مفتوحة قبل المسح: $($pre.Current.Name) — تُغلق." -ForegroundColor Yellow
    Close-ModalDialog -Dlg $pre | Out-Null
}

function Get-NavButtons {
    param($Win)
    $cond = New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $CT::Button)
    $all = $Win.FindAll($TS::Descendants, $cond)
    $nav = @()
    foreach ($b in $all) {
        if ($b.Current.AutomationId -like 'Nav_*') { $nav += $b }
    }
    return $nav
}

function Find-ByName {
    param($Win, [string]$Name)
    $cond = New-Object System.Windows.Automation.AndCondition(
        (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $CT::Button)),
        (New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $Name)))
    return $Win.FindFirst($TS::Descendants, $cond)
}

# ArrayList على نطاق السكربت: `+=` داخل دالة يُنشئ متغيّراً محلياً فتضيع الاكتشافات.
$script:report = [System.Collections.ArrayList]::new()

function Invoke-Sweep {
    param([string]$Lang)

    $langDir = Join-Path $out $Lang
    New-Item -ItemType Directory -Force -Path $langDir | Out-Null

    $navs = Get-NavButtons -Win $win
    Write-Host "[$Lang] عدد صفحات التنقّل: $($navs.Count)" -ForegroundColor Cyan

    $keys = @()
    foreach ($n in $navs) { $keys += $n.Current.AutomationId }

    foreach ($key in $keys) {
        # إعادة البحث في كل دورة: BuildNavigation يعيد إنشاء الأزرار عند تبديل اللغة.
        $cond = New-Object System.Windows.Automation.PropertyCondition($AE::AutomationIdProperty, $key)
        $btn = $win.FindFirst($TS::Descendants, $cond)
        if ($null -eq $btn) {
            [void]$script:report.Add([pscustomobject]@{ Lang=$Lang; Page=$key; Kind='MISSING_NAV'; Detail='زرّ التنقّل اختفى' })
            continue
        }

        $label = $btn.Current.Name
        try {
            $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        } catch {
            [void]$script:report.Add([pscustomobject]@{ Lang=$Lang; Page=$key; Kind='NAV_INVOKE_FAILED'; Detail=$_.Exception.Message })
            continue
        }
        Start-Sleep -Milliseconds $SettleMs

        # نافذة خطأ ظهرت بمجرّد فتح الصفحة = عيب من الطراز الأول
        $dlg = Get-ModalDialog -Win $win
        if ($null -ne $dlg) {
            $txtCond = New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $CT::Text)
            $msg = ($dlg.FindAll($TS::Descendants, $txtCond) | ForEach-Object { $_.Current.Name }) -join ' | '
            [void]$script:report.Add([pscustomobject]@{ Lang=$Lang; Page=$key; Kind='DIALOG_ON_OPEN'; Detail="$($dlg.Current.Name) :: $msg" })
            Save-WindowShot -Win $win -Path (Join-Path $langDir "$key`__DIALOG.png") | Out-Null
            Close-ModalDialog -Dlg $dlg | Out-Null
            Start-Sleep -Milliseconds 300
        }

        # PageHost هو ContentControl ولا يظهر في «عرض التحكّم» الافتراضي لـUIA، فالبحث
        # عنه بالمعرّف يفشل ويقع الجرد على النافذة كاملة (شريط جانبي + لوحة مساعد).
        # التصريح بذلك مقصود: الأرقام أدناه **على مستوى النافذة** لا الصفحة وحدها.
        $pageHost = $win

        Set-ScrollPercent -Scope $pageHost -Percent 0 | Out-Null
        Save-WindowShot -Win $win -Path (Join-Path $langDir "$key`__01_top.png") | Out-Null

        $inv = @(Get-PageInventory -Scope $pageHost)

        $scrolled = Set-ScrollPercent -Scope $pageHost -Percent 100
        if ($scrolled) {
            Save-WindowShot -Win $win -Path (Join-Path $langDir "$key`__02_bottom.png") | Out-Null
            $inv += @(Get-PageInventory -Scope $pageHost)      # عناصر لا تظهر إلا بعد التمرير
        }

        $inv = @($inv | Sort-Object Type, Text -Unique)
        $inv | Export-Csv -Path (Join-Path $langDir "$key.csv") -NoTypeInformation -Encoding UTF8

        $texts = @($inv | Where-Object { $_.Text -ne '<بلا اسم>' })
        if ($texts.Count -lt 3) {
            [void]$script:report.Add([pscustomobject]@{ Lang=$Lang; Page=$key; Kind='EMPTY_PAGE'; Detail="عناصر مرئية: $($texts.Count)" })
        }

        $unnamed = @($inv | Where-Object { $_.Text -eq '<بلا اسم>' })
        if ($unnamed.Count -gt 0) {
            [void]$script:report.Add([pscustomobject]@{ Lang=$Lang; Page=$key; Kind='UNNAMED_BUTTON'; Detail="أزرار بلا اسم: $($unnamed.Count)" })
        }

        if ($Lang -eq 'en') {
            # النصّ العربيّ الباقي بعد التبديل = ترجمة ناقصة يراها المستخدم.
            $arabic = @($inv | Where-Object { $_.Text -match '[؀-ۿ]' })
            if ($arabic.Count -gt 0) {
                $sample = ($arabic | Select-Object -First 12 | ForEach-Object { $_.Text }) -join ' ¦ '
                [void]$script:report.Add([pscustomobject]@{ Lang=$Lang; Page=$key; Kind='UNTRANSLATED'; Detail="$($arabic.Count) نصّاً — $sample" })
            }
        }

        Write-Host "  [$Lang] $key — $label" -ForegroundColor DarkGray
    }
}

Invoke-Sweep -Lang 'ar'

$toggle = Find-ByName -Win $win -Name 'EN'
if ($null -eq $toggle) { throw "زرّ تبديل اللغة 'EN' غير موجود." }
$toggle.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Start-Sleep -Milliseconds 1200
Write-Host "[OK] بُدِّلت اللغة إلى الإنجليزية" -ForegroundColor Green

$dlgAfterToggle = Get-ModalDialog -Win $win
if ($null -ne $dlgAfterToggle) {
    $txtCond = New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $CT::Text)
    $msg = ($dlgAfterToggle.FindAll($TS::Descendants, $txtCond) | ForEach-Object { $_.Current.Name }) -join ' | '
    [void]$script:report.Add([pscustomobject]@{ Lang='en'; Page='<TOGGLE>'; Kind='DIALOG_ON_LANGUAGE_TOGGLE'; Detail=$msg })
    Close-ModalDialog -Dlg $dlgAfterToggle | Out-Null
}

Invoke-Sweep -Lang 'en'

# إعادة الحالة كما كانت
$back = Find-ByName -Win $win -Name 'ع'
if ($null -ne $back) {
    $back.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 800
}

$reportPath = Join-Path $out 'findings.csv'
$script:report | Export-Csv -Path $reportPath -NoTypeInformation -Encoding UTF8

Write-Host ""
Write-Host "=== الخلاصة ===" -ForegroundColor Cyan
$script:report | Group-Object Kind | Sort-Object Count -Descending |
    ForEach-Object { Write-Host ("  {0,-28} {1}" -f $_.Name, $_.Count) }
Write-Host ""
Write-Host "اللقطات: $out" -ForegroundColor Green
Write-Host "الاكتشافات: $reportPath" -ForegroundColor Green
