# تحقّقٌ حيٌّ عبر ثمانية ملفّات قطاع — على قواعد tools/SectorProbe المبذورة (CEEF-235)
#
# لماذا هذا السكربت: أثبتت CEEF-232 أنّ التطبيق يعمل حيّاً بقيادة UIAutomationClient،
# لكن على جذورِ بياناتٍ فارغة — فالشاشاتُ القطاعيّة فُتحت ولم تحمل رقماً. وبذرت
# tools/SectorProbe (CEEF-230…234) بياناتٍ محاسبيّةً حقيقيّةً في ثماني قواعد PostgreSQL
# منفصلة (brqosh_sectorprobe_<profile>) — قاعدةٌ لكلّ BusinessProfile. والفجوةُ المتبقّية
# (ر-أ في release-readiness/MASTER.md) هي إعادةُ التشغيل الحيّ على هذه القواعد بالذات.
#
# الآلية:
#   ١. جذرُ بياناتٍ معزولٌ لكلّ قطاع تحت %TEMP% — لا يمسّ %APPDATA%\BRQOSH الحقيقيّ.
#   ٢. database.json يُكتب مسبقاً في ذلك الجذر ويُشير إلى قاعدة PostgreSQL المبذورة
#      لذلك القطاع (راجع AppServices.LoadDatabaseConfig).
#   ٣. setup.json **لا يُكتب مسبقاً** عمداً — فيظهر معالجُ التهيئة، ونقودُه لاختيار نمط
#      العمل الصحيح (يقرّر أيّ شاشاتٍ تظهر في التنقّل، NavigationCatalog.ForProfile)
#      وإنشاء حساب مدير. DatabaseInitializer.InitializeAsync أُحاديُّ المنشأة بالتصميم:
#      يُعيد الشركةَ الموجودة بلا مساس إن وُجدت — فبيانات SectorProbe تبقى كما هي، ويُضاف
#      إليها مستخدمٌ مديرٌ فقط (لم يكن SectorProbe يُنشئ مستخدمين).
#   ٤. BRQOSH_UITEST=1 يتخطّى شاشة الدخول بعد ذلك (يختار المديرَ الجديد تلقائياً).
#   ٥. لكلّ قطاع: التنقّل إلى الشاشات التي بذرها SectorProbe فعلاً (لا كلّ الشاشات
#      المُعلَنة في §2ب من MASTER.md — بعضها لم يمسّه مسبارُ الوحدة الحالي، انظر
#      tools/SectorReadout الذي يقرأ نفس القواعد بمسارٍ مستقلّ للمقارنة)، والتقاطُ
#      AutomationId=BrqoshMainGrid (Name="GridRows:N") و BrqoshReportSummary.
#
# الاستخدام:
#   pwsh -File scripts/ops/live-ui-sector-walkthrough.ps1
#   pwsh -File scripts/ops/live-ui-sector-walkthrough.ps1 -Profiles Healthcare,Manufacturing

[CmdletBinding()]
param(
    [ValidateSet("General","Construction","Manufacturing","Retail","Healthcare","Logistics","RealEstate","Hospitality")]
    [string[]]$Profiles = @("General","Construction","Manufacturing","Retail","Healthcare","Logistics","RealEstate","Hospitality"),
    [string]$OutDir = "dist/live-ui-sector-walkthrough",
    [int]$WindowTimeoutSeconds = 45
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms

$repoRoot = Resolve-Path -LiteralPath (Split-Path -Parent $PSCommandPath) | Split-Path -Parent | Split-Path -Parent
$exe = Join-Path $repoRoot "src\Brqosh.Desktop\bin\Debug\net9.0-windows\Brqosh.Desktop.exe"
if (-not (Test-Path $exe)) { throw "لم يُبنَ التطبيق: $exe — شغّل dotnet build src/Brqosh.Desktop -c Debug أولاً." }

$pwPath = Join-Path $env:APPDATA "BRQOSH\pg_password.txt"
if (-not (Test-Path $pwPath)) { throw "لا يوجد $pwPath — يلزم PostgreSQL محلياً مهيّأً (راجع SectorProbe)." }
$pgPassword = (Get-Content -LiteralPath $pwPath -Raw).Trim()

$out = Join-Path $repoRoot $OutDir
New-Item -ItemType Directory -Force -Path $out | Out-Null

$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]
$CT = [System.Windows.Automation.ControlType]

$ProfileIndex = @{
    General = 0; Construction = 1; Manufacturing = 2; Retail = 3
    Healthcare = 4; Logistics = 5; RealEstate = 6; Hospitality = 7
}

# فقط الشاشات التي بذرها tools/SectorProbe فعلاً بأثرٍ مقيس — أُثبتت عبر
# tools/SectorReadout قبل تشغيل هذا السكربت. شاشاتٌ أخرى مُعلَنةٌ في §2ب من MASTER.md
# (كالمستخلصات/المحتجزات والحجوزات/الغرف) لا تحمل صفوفاً من هذا المسبار بالذات.
$SectorNavTargets = @{
    General       = @("trial_balance", "sales_invoices")
    Construction  = @("trial_balance", "projects")
    Manufacturing = @("trial_balance", "work_orders", "bom")
    Retail        = @("trial_balance", "pos_shifts")
    Healthcare    = @("trial_balance", "patients", "visits", "insurance_claims")
    Logistics     = @("trial_balance", "shipments", "delivery_trips")
    RealEstate    = @("trial_balance", "leases", "maintenance_orders")
    Hospitality   = @("trial_balance", "pos_shifts", "menu_items", "tip_pools")
}

function Find-WindowByName {
    param([string]$Name, [int]$TimeoutSeconds)
    $cond = New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $Name)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $w = $AE::RootElement.FindFirst($TS::Children, $cond)
        if ($null -ne $w) { return $w }
        Start-Sleep -Milliseconds 400
    }
    return $null
}

function Find-ById { param($Scope, [string]$Id)
    $cond = New-Object System.Windows.Automation.PropertyCondition($AE::AutomationIdProperty, $Id)
    return $Scope.FindFirst($TS::Descendants, $cond)
}

function Find-ByName { param($Scope, [string]$Name, $ControlType = $null)
    if ($null -ne $ControlType) {
        $cond = New-Object System.Windows.Automation.AndCondition(
            (New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $Name)),
            (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $ControlType)))
    } else {
        $cond = New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $Name)
    }
    return $Scope.FindFirst($TS::Descendants, $cond)
}

function Set-TextBoxValue { param($Element, [string]$Value)
    $vp = $Element.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    $vp.SetValue($Value)
}

function Set-PasswordValue { param($Element, [string]$Value)
    # PasswordBox لا يدعم ValuePattern عمداً (أمن) — تركيزٌ ثمّ محاكاةُ لوحة مفاتيح.
    $Element.SetFocus()
    Start-Sleep -Milliseconds 200
    [System.Windows.Forms.SendKeys]::SendWait($Value)
    Start-Sleep -Milliseconds 150
}

function Save-WindowShot {
    param($Win, [string]$Path)
    $r = $Win.Current.BoundingRectangle
    if ($r.Width -le 0 -or $r.Height -le 0) { return }
    $vs = [System.Windows.Forms.SystemInformation]::VirtualScreen
    $x = [int][Math]::Max($r.X, $vs.X)
    $y = [int][Math]::Max($r.Y, $vs.Y)
    $w = [int][Math]::Min($r.Right - $x, $vs.Right - $x)
    $h = [int][Math]::Min($r.Bottom - $y, $vs.Bottom - $y)
    if ($w -le 0 -or $h -le 0) { return }
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($x, $y, 0, 0, (New-Object System.Drawing.Size($w, $h)))
    $g.Dispose()
    $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

function Stop-StrayDesktopProcesses {
    Get-Process -Name "Brqosh.Desktop" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 300
}

$script:report = [System.Collections.ArrayList]::new()

function Invoke-SectorWalkthrough {
    param([string]$Profile)

    Write-Host ""
    Write-Host "══ $Profile ══" -ForegroundColor Cyan

    Stop-StrayDesktopProcesses

    $dbName = "brqosh_sectorprobe_$($Profile.ToLowerInvariant())"
    $dataRoot = Join-Path $env:TEMP "brqosh-live-ui\$Profile"
    if (Test-Path $dataRoot) { Remove-Item -Recurse -Force $dataRoot }
    New-Item -ItemType Directory -Force -Path $dataRoot | Out-Null

    $sectorOut = Join-Path $out $Profile
    New-Item -ItemType Directory -Force -Path $sectorOut | Out-Null

    $dbConfig = @{
        Provider = "postgresql"
        ConnectionString = "Host=localhost;Port=5433;Database=$dbName;Username=postgres;Password=$pgPassword"
    } | ConvertTo-Json
    [System.IO.File]::WriteAllText((Join-Path $dataRoot "database.json"), $dbConfig, (New-Object System.Text.UTF8Encoding($false)))

    # setup.json عمداً غير مكتوب — معالجُ التهيئة يظهر ويُنشئ المدير (انظر رأس الملفّ).

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $exe
    $psi.UseShellExecute = $false
    $psi.EnvironmentVariables["BRQOSH_DATA_ROOT"] = $dataRoot
    $psi.EnvironmentVariables["BRQOSH_ENV"] = "development"
    $psi.EnvironmentVariables["BRQOSH_TEST"] = "1"
    $psi.EnvironmentVariables["BRQOSH_UITEST"] = "1"
    $proc = [System.Diagnostics.Process]::Start($psi)

    try {
        $wizard = Find-WindowByName -Name "BRQOSH — إعداد النظام" -TimeoutSeconds $WindowTimeoutSeconds
        if ($null -eq $wizard) {
            [void]$script:report.Add([pscustomobject]@{ Profile=$Profile; Step='WIZARD'; Kind='NOT_FOUND'; Detail='معالج التهيئة لم يظهر' })
            return
        }

        # ١) اختيار نمط العمل الصحيح — يقرّر أيّ شاشاتٍ تظهر لاحقاً في التنقّل.
        $combo = Find-ById -Scope $wizard -Id "ProfileCombo"
        $ecp = $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
        $ecp.Expand()
        Start-Sleep -Milliseconds 400
        $itemCond = New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $CT::ListItem)
        $items = $combo.FindAll($TS::Descendants, $itemCond)
        $target = $items | Where-Object { $_.Current.Name -match "BusinessProfile = $Profile," } | Select-Object -First 1
        if ($null -eq $target) {
            [void]$script:report.Add([pscustomobject]@{ Profile=$Profile; Step='WIZARD'; Kind='PROFILE_ITEM_NOT_FOUND'; Detail="لم يُعثر على $Profile في القائمة" })
            $ecp.Collapse()
            return
        }
        $target.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
        Start-Sleep -Milliseconds 300
        try { $ecp.Collapse() } catch {}

        # ٢) بيانات المنشأة — تُتجاهَل قيمتاها فعلياً (DatabaseInitializer يُعيد الشركةَ
        #    الموجودة)، لكن الحقل مطلوبٌ لعبور تحقّق الشاشة.
        Set-TextBoxValue -Element (Find-ById -Scope $wizard -Id "CompanyNameBox") -Value "منشأة $Profile — تحقّق حيّ (CEEF-235)"

        # ٣) حساب المدير — هذا هو الأثر الحقيقيّ: مستخدمٌ جديد على شركة SectorProbe.
        Set-TextBoxValue -Element (Find-ById -Scope $wizard -Id "AdminUsernameBox") -Value "sectorprobe-$($Profile.ToLowerInvariant())"
        Set-PasswordValue -Element (Find-ById -Scope $wizard -Id "AdminPasswordBox") -Value "SectorProbe2026!"
        Set-PasswordValue -Element (Find-ById -Scope $wizard -Id "AdminPasswordConfirmBox") -Value "SectorProbe2026!"

        $startBtn = Find-ByName -Scope $wizard -Name "ابدأ العمل الآن" -ControlType $CT::Button
        $startBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()

        $main = Find-WindowByName -Name "BRQOSH ERP" -TimeoutSeconds $WindowTimeoutSeconds
        if ($null -eq $main) {
            # قد يكون حوارَ خطأٍ عالقاً على المعالج نفسه — يُلتقط للتشخيص.
            Save-WindowShot -Win $wizard -Path (Join-Path $sectorOut "WIZARD_STUCK.png")
            [void]$script:report.Add([pscustomobject]@{ Profile=$Profile; Step='MAIN_WINDOW'; Kind='NOT_FOUND'; Detail='لم تظهر BRQOSH ERP بعد المعالج' })
            return
        }
        Write-Host "  [OK] دخول ناجح — $($main.Current.Name)" -ForegroundColor Green
        Save-WindowShot -Win $main -Path (Join-Path $sectorOut "00_dashboard.png")

        foreach ($key in $SectorNavTargets[$Profile]) {
            $navBtn = Find-ById -Scope $main -Id "Nav_$key"
            if ($null -eq $navBtn) {
                [void]$script:report.Add([pscustomobject]@{ Profile=$Profile; Step=$key; Kind='NAV_NOT_FOUND'; Detail="لا زرّ Nav_$key" })
                continue
            }
            $navBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
            Start-Sleep -Milliseconds 1500

            $grid = Find-ById -Scope $main -Id "BrqoshMainGrid"
            $summary = Find-ById -Scope $main -Id "BrqoshReportSummary"

            $gridVal = if ($null -ne $grid) { $grid.Current.Name } else { $null }
            $summaryVal = if ($null -ne $summary) { $summary.Current.Name } else { $null }

            if ($null -eq $grid -and $null -eq $summary) {
                [void]$script:report.Add([pscustomobject]@{ Profile=$Profile; Step=$key; Kind='NO_PROBE'; Detail='لا BrqoshMainGrid ولا BrqoshReportSummary' })
            } else {
                [void]$script:report.Add([pscustomobject]@{ Profile=$Profile; Step=$key; Kind='OK'; Detail="Grid=$gridVal · Summary=$summaryVal" })
            }
            Write-Host "  [$key] Grid=$gridVal Summary=$summaryVal" -ForegroundColor DarkGray
            Save-WindowShot -Win $main -Path (Join-Path $sectorOut "$key.png")
        }
    }
    finally {
        try {
            if (-not $proc.HasExited) {
                $proc.CloseMainWindow() | Out-Null
                $proc.WaitForExit(5000) | Out-Null
            }
        } catch {}
        if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
    }
}

foreach ($p in $Profiles) { Invoke-SectorWalkthrough -Profile $p }

$reportPath = Join-Path $out "findings.csv"
$script:report | Export-Csv -Path $reportPath -NoTypeInformation -Encoding UTF8

Write-Host ""
Write-Host "=== الخلاصة ===" -ForegroundColor Cyan
$script:report | Group-Object Kind | Sort-Object Count -Descending |
    ForEach-Object { Write-Host ("  {0,-24} {1}" -f $_.Name, $_.Count) }
Write-Host ""
Write-Host "اللقطات والتقرير: $out" -ForegroundColor Green
