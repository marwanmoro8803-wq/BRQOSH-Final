<#
.SYNOPSIS
    (CEEF-236 · ر-ك · الدرز) الإقلاعُ الإنتاجيُّ من التطبيق **المُنصَّب** على الآلة النظيفة — حتى الصدفة.

.DESCRIPTION
    يُنفَّذ **داخل** الآلة الافتراضية، برمزٍ **غيرِ مرفوع** (التطبيقُ يُشغَّل كما يُشغّله العميل)،
    بعد أن نصّب rk-guest.ps1 الحزمةَ وPostgreSQL بـssl=on. وهو أخو live-production-first-boot.ps1
    على جهاز التطوير — بفارقَين حاكمَين: الملفُّ التنفيذيُّ هو المُنصَّب في Program Files لا بناءُ Debug،
    **ولا مفتاحَ مورِّدٍ خاصّاً هنا قطّ**: معرّفُ الجهاز يُكتب إلى المجلّد المشترك، والمُضيفُ يوقّع
    ويُعيد الترخيصَ ملفاً، فلا يغادر المفتاحُ الخاصُّ جهازَ المورِّد.

    ما يُقاس: المعالجُ في الإنتاج · SQLite مخفيّ · **اشتراطُ TLS ينجح على خادمٍ يقدّم ssl فعلاً** ·
    database.json محميّ · نافذةُ التفعيل · ترخيصُ المورِّد يُقبل بالمفتاح المُضمَّن · الدخول ·
    تغييرُ كلمة المرور · الصدفة · (م-١٣) ملفُّ اتصالٍ تالف ⟵ نافذةُ إنقاذ.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PgPassword,
    [string]$Exe = "$env:ProgramFiles\BRQOSH\Brqosh.Desktop.exe",
    [string]$PgHost = "localhost",
    [int]$PgPort = 5432,
    [string]$PgUser = "postgres",
    [int]$LicenseWaitMinutes = 15
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms

# ── المجلّدُ المشترك: تعدادٌ لا تخمين (م-٣١) ─────────────────────────────────
$share = $null
foreach ($d in (Get-PSDrive -PSProvider FileSystem -ErrorAction SilentlyContinue | Sort-Object Name)) {
    try { if (Test-Path (Join-Path ($d.Name + ":\") "rk-prodboot-guest.ps1")) { $share = $d.Name + ":\"; break } } catch { }
}
if (-not $share) { throw "لم يُعثر على المجلّد المشترك (rk-prodboot-guest.ps1)." }

$stamp    = Get-Date -Format "yyyyMMdd-HHmmss"
$work     = "C:\rk\prodboot-$stamp"
$out      = Join-Path $work "evidence"
$outShare = Join-Path $share "prodboot-$stamp"
New-Item -ItemType Directory -Force -Path $out, $outShare | Out-Null
$dataRoot = Join-Path $work "data"          # جذرُ بياناتٍ معزولٌ فارغ — لا %APPDATA%
New-Item -ItemType Directory -Force -Path $dataRoot | Out-Null

# ‏(CEEF-236 · ر-ك · م-٣٩) **اسمٌ لا يحتاج اقتباساً أصلاً.**
# ‏الطابعُ `yyyyMMdd-HHmmss` يحمل شرطةً، فلزم `"..."` في SQL — **وPowerShell 5.1
# ‏ينزع علاماتِ الاقتباس عند تمرير الوسيط لبرنامجٍ أصليّ**، فيصل إلى psql:
# ‏  CREATE DATABASE brqosh_prodboot_20260905-013702;
# ‏  ERROR: syntax error at or near "-"
# ‏فالشرطةُ تُبدّل شُرطةً سفليّةً في اسم القاعدة وحده، فيصير مُعرِّفاً مشروعاً بلا اقتباس.
$dbName    = "brqosh_prodboot_" + ($stamp -replace '-', '_')
$company   = "منشأة الإقلاع الإنتاجي"
$adminUser = "prodadmin"
$adminPass = "ProdBoot!2026x"
$newPass   = "ProdBoot!2026y"

$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]

# ── النتائج: صفٌّ لكلّ فحص، يُكتب فوراً إلى القرص المحليّ والمشترك ────────────
$csvLocal = Join-Path $out "findings.csv"
$csvShare = Join-Path $outShare "findings.csv"
$script:fails = 0
function Add-Finding {
    param([string]$Check, [string]$Expected, [string]$Actual, [bool]$Ok)
    if (-not $Ok) { $script:fails++ }
    $row = [pscustomobject]@{ Run = $stamp; Time = (Get-Date -Format "HH:mm:ss"); Check = $Check
                              Expected = $Expected; Actual = $Actual; Result = $(if ($Ok) { "PASS" } else { "FAIL" }) }
    $row | Export-Csv -LiteralPath $csvLocal -NoTypeInformation -Encoding UTF8 -Append
    try { Copy-Item $csvLocal $csvShare -Force } catch { }
    Write-Host "[$(if($Ok){'OK  '}else{'FAIL'})] $Check — $Actual"
}

# ── مساعداتُ UIA (مطابقةٌ لما ثبت في live-production-first-boot.ps1) ───────────
function Find-WindowByName { param([string]$Name, [int]$TimeoutSeconds = 60)
    $cond = New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $Name)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $w = $AE::RootElement.FindFirst($TS::Children, $cond)
        if ($null -ne $w) { return $w }
        Start-Sleep -Milliseconds 400
    }
    return $null
}
function Save-ElementTree { param($Scope, [string]$Name)
    try {
        $rows = foreach ($e in $Scope.FindAll($TS::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) {
            "{0,-28} | {1,-16} | {2}" -f $e.Current.AutomationId, $e.Current.ControlType.ProgrammaticName.Replace('ControlType.',''), $e.Current.Name
        }
        $rows | Set-Content -LiteralPath (Join-Path $out "$Name.txt") -Encoding UTF8
    } catch { }
}
function Wait-ForEitherWindow { param([string]$Expected, [string]$Unwanted, $Process, [int]$TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $bad = $AE::RootElement.FindFirst($TS::Children, (New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $Unwanted)))
        if ($null -ne $bad) { return @{ Outcome = "unwanted"; Window = $bad } }
        $good = $AE::RootElement.FindFirst($TS::Children, (New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $Expected)))
        if ($null -ne $good) { return @{ Outcome = "expected"; Window = $good } }
        if ($null -ne $Process -and $Process.HasExited) { return @{ Outcome = "exited"; Window = $null } }
        Start-Sleep -Milliseconds 500
    }
    return @{ Outcome = "timeout"; Window = $null }
}
function Find-ById { param($Scope, [string]$Id, [int]$TimeoutSeconds = 0)
    $cond = New-Object System.Windows.Automation.PropertyCondition($AE::AutomationIdProperty, $Id)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $e = $Scope.FindFirst($TS::Descendants, $cond)
        if ($null -ne $e) { return $e }
        if ($TimeoutSeconds -le 0) { return $null }
        Start-Sleep -Milliseconds 300
    } while ((Get-Date) -lt $deadline)
    Save-ElementTree $Scope "missing-$Id"
    throw "لم يظهر العنصر «$Id» خلال $TimeoutSeconds ثانية — سُكبت شجرةُ العناصر في الدليل."
}
function Find-ByName { param($Scope, [string]$Name)
    $Scope.FindFirst($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $Name)))
}
function Set-TextBoxValue { param($Element, [string]$Value)
    $Element.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($Value)
}
function ConvertTo-SendKeys { param([string]$Value)
    $sb = New-Object System.Text.StringBuilder
    foreach ($c in $Value.ToCharArray()) {
        if ('+^%~(){}[]'.Contains($c)) { [void]$sb.Append('{').Append($c).Append('}') } else { [void]$sb.Append($c) }
    }
    return $sb.ToString()
}
Add-Type -Namespace Win32 -Name Fg -MemberDefinition @"
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
[DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
"@
function Set-WindowForeground { param($Win)
    try {
        $h = [IntPtr]$Win.Current.NativeWindowHandle
        if ($h -ne [IntPtr]::Zero) { [Win32.Fg]::ShowWindow($h, 9) | Out-Null; [Win32.Fg]::SetForegroundWindow($h) | Out-Null; Start-Sleep -Milliseconds 400 }
    } catch { }
}
function Set-PasswordValue { param($Element, [string]$Value, $Window = $null)
    if ($null -ne $Window) { Set-WindowForeground $Window }
    $Element.SetFocus(); Start-Sleep -Milliseconds 300
    [System.Windows.Forms.SendKeys]::SendWait((ConvertTo-SendKeys $Value)); Start-Sleep -Milliseconds 250
}
function Invoke-Button { param($Element) $Element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
function Get-ElementText { param($Element)
    if ($null -eq $Element) { return "" }
    try { return $Element.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value } catch { }
    try { return $Element.GetCurrentPattern([System.Windows.Automation.TextPattern]::Pattern).DocumentRange.GetText(-1) } catch { }
    return $Element.Current.Name
}
function Set-MultilineText { param($Element, [string]$Value)
    Set-Clipboard -Value $Value
    $Element.SetFocus(); Start-Sleep -Milliseconds 300
    [System.Windows.Forms.SendKeys]::SendWait("^a"); Start-Sleep -Milliseconds 120
    [System.Windows.Forms.SendKeys]::SendWait("^v"); Start-Sleep -Milliseconds 400
}
function Set-ToggleState { param($Element, [bool]$Checked)
    $tp = $Element.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    $guard = 0
    while ($guard -lt 3) {
        $isOn = $tp.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On
        if ($isOn -eq $Checked) { return }
        $tp.Toggle(); Start-Sleep -Milliseconds 200; $guard++
    }
}
function Save-WindowShot { param($Win, [string]$Name)
    try {
        $r = $Win.Current.BoundingRectangle
        if ($r.Width -le 0 -or $r.Height -le 0) { return }
        $vs = [System.Windows.Forms.SystemInformation]::VirtualScreen
        $x = [int][Math]::Max($r.X, $vs.X); $y = [int][Math]::Max($r.Y, $vs.Y)
        $w = [int][Math]::Min($r.Right - $x, $vs.Right - $x); $h = [int][Math]::Min($r.Bottom - $y, $vs.Bottom - $y)
        if ($w -le 0 -or $h -le 0) { return }
        $bmp = New-Object System.Drawing.Bitmap($w, $h)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.CopyFromScreen($x, $y, 0, 0, (New-Object System.Drawing.Size($w, $h))); $g.Dispose()
        $bmp.Save((Join-Path $out "$Name.png"), [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
        Copy-Item (Join-Path $out "$Name.png") $outShare -Force -ErrorAction SilentlyContinue
    } catch { Write-Host "  (تعذّرت اللقطة $Name)" }
}
function Stop-Stray {
    Get-Process -Name "Brqosh.Desktop" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 400
}
$psqlExe = "$env:ProgramFiles\PostgreSQL\17\bin\psql.exe"
# ‏(CEEF-236 · ر-ك · م-٣٨) **‏`2>&1` تحت `$ErrorActionPreference = "Stop"` يقتل السكربت.**
#
# ‏في Windows PowerShell 5.1 يُغلَّف كلُّ سطرِ خطأٍ من برنامجٍ أصليّ في `ErrorRecord`، ومع
# ‏`Stop` يصير **خطأً مُنهياً** — لا تحذيراً. فسطرُ إشعارٍ واحدٌ من `psql` يُسقط التشغيلةَ كلَّها.
# ‏وقد وقع فعلاً: ماتت التشغيلةُ بعد الصفّ السادس مباشرةً، **بلا صفٍّ ولا رسالة** — لأنّ
# ‏النداءَ كان خارجَ `try` أيضاً. فالتفضيلُ يُخفَّض هنا محلّيّاً، والحكمُ من رمز الخروج.
function Invoke-Psql { param([string]$Database, [string]$Sql, [switch]$AllowFailure)
    $env:PGPASSWORD = $PgPassword
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $out = & $psqlExe -h $PgHost -p $PgPort -U $PgUser -d $Database -tAc $Sql 2>&1
        $code = $LASTEXITCODE
    } finally { $ErrorActionPreference = $prev }
    $text = ($out | ForEach-Object { "$_" }) -join "`n"
    if ($code -ne 0 -and -not $AllowFailure) {
        throw "psql خرج بـ$code على [$Sql]: $text"
    }
    return $text
}
function Start-App {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $Exe
    $psi.UseShellExecute = $false
    $psi.EnvironmentVariables["BRQOSH_DATA_ROOT"] = $dataRoot
    $psi.EnvironmentVariables["BRQOSH_ENV"] = "production"
    $psi.EnvironmentVariables.Remove("BRQOSH_UITEST") | Out-Null
    return [System.Diagnostics.Process]::Start($psi)
}

# ─────────────────────────────────────────────────────────────────────────────
Write-Host "=== الإقلاعُ الإنتاجيُّ من التطبيق المُنصَّب (CEEF-236 · ر-ك) ==="
Write-Host "Exe      : $Exe"
Write-Host "DataRoot : $dataRoot"
Write-Host "Database : $dbName"
Write-Host "Evidence : $out  ⟶  $outShare"
Write-Host ""

# ── شروطُ القياس تُسجَّل لا تُفترض ────────────────────────────────────────────
$elevated = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
Add-Finding -Check "السكربتُ والتطبيقُ يعملان برمزٍ غيرِ مرفوع (كالعميل)" -Expected "False" -Actual "$elevated" -Ok (-not $elevated)
Add-Finding -Check "الملفُّ التنفيذيُّ هو المُنصَّب في Program Files" -Expected "$env:ProgramFiles\BRQOSH\Brqosh.Desktop.exe" `
    -Actual $(if (Test-Path $Exe) { $Exe } else { "غائب" }) -Ok ((Test-Path $Exe) -and $Exe.StartsWith("$env:ProgramFiles\BRQOSH"))
$machineEnv = [Environment]::GetEnvironmentVariable("BRQOSH_ENV", "Machine")
Add-Finding -Check "BRQOSH_ENV بنطاق الجهاز" -Expected "production" -Actual "$machineEnv" -Ok ($machineEnv -eq "production")
Add-Finding -Check "BRQOSH_UITEST غيرُ مضبوط في بيئة السكربت" -Expected "غائب" `
    -Actual $(if ($env:BRQOSH_UITEST) { "مضبوط=$env:BRQOSH_UITEST" } else { "غائب" }) -Ok (-not $env:BRQOSH_UITEST)
Add-Finding -Check "psql مُنصَّب (من مرحلة Postgres)" -Expected $psqlExe -Actual $(if (Test-Path $psqlExe) { "موجود" } else { "غائب" }) -Ok (Test-Path $psqlExe)
if (-not (Test-Path $psqlExe)) { throw "لا psql — شغّل rk-guest.ps1 -Stage Postgres أوّلاً." }

$serverSsl = (Invoke-Psql -Database "postgres" -Sql "show ssl;" | Out-String).Trim()
Add-Finding -Check "الخادمُ يقدّم TLS (show ssl)" -Expected "on" -Actual $serverSsl -Ok ($serverSsl -eq "on")
$app = $null
try {
    # ‏(م-٣٨) داخل الحارس، لا قبله: كلُّ ما يمكن أن يُخفق يجب أن يُخرج صفَّ إخفاقٍ لا صمتاً.
    Invoke-Psql -Database "postgres" -Sql "CREATE DATABASE $dbName;" | Out-Null
    Add-Finding -Check "قاعدةُ القياس أُنشئت على الخادم" -Expected $dbName -Actual "أُنشئت" -Ok $true

    Stop-Stray
    $app = Start-App

    # ── معالجُ التهيئة ────────────────────────────────────────────────────────
    $wiz = Find-WindowByName -Name "BRQOSH — إعداد النظام" -TimeoutSeconds 180
    if ($null -eq $wiz) { throw "لم تظهر نافذةُ معالج التهيئة خلال ١٨٠ ثانية." }
    Add-Finding -Check "المعالجُ يظهر في وضع الإنتاج من التطبيق المُنصَّب" -Expected "نافذة الإعداد" -Actual "ظهرت" -Ok $true
    Start-Sleep -Seconds 2
    Set-WindowForeground $wiz

    Set-TextBoxValue (Find-ById $wiz "CompanyNameBox" 30) $company
    Set-TextBoxValue (Find-ById $wiz "AdminUsernameBox" 30) $adminUser
    Set-PasswordValue (Find-ById $wiz "AdminPasswordBox" 30) $adminPass $wiz
    Set-PasswordValue (Find-ById $wiz "AdminPasswordConfirmBox" 30) $adminPass $wiz

    $sqliteBox = Find-ById $wiz "UseSqliteBox"
    $sqliteOffscreen = ($null -eq $sqliteBox) -or $sqliteBox.Current.IsOffscreen
    Add-Finding -Check "خيارُ SQLite مخفيٌّ في الإنتاج" -Expected "مخفيّ" `
        -Actual $(if ($sqliteOffscreen) { "مخفيّ" } else { "ظاهر — تسريبُ مسارِ تطوير" }) -Ok $sqliteOffscreen

    Set-TextBoxValue (Find-ById $wiz "PgHostBox" 30) $PgHost
    Set-TextBoxValue (Find-ById $wiz "PgPortBox" 30) "$PgPort"
    Set-TextBoxValue (Find-ById $wiz "PgDatabaseBox" 30) $dbName
    Set-TextBoxValue (Find-ById $wiz "PgUserBox" 30) $PgUser
    Set-PasswordValue (Find-ById $wiz "PgPasswordBox" 30) $PgPassword $wiz

    # ── اشتراطُ TLS على خادمٍ يقدّم ssl فعلاً ⟵ يجب أن **ينجح** ─────────────────
    $tlsBox = Find-ById $wiz "RequireTlsBox" 30
    Set-ToggleState $tlsBox $true
    Invoke-Button (Find-ById $wiz "TestConnectionBtn" 30)
    Start-Sleep -Seconds 8
    $tlsMsg = (Find-ById $wiz "ConnectionStatusText" 30).Current.Name
    Save-WindowShot $wiz "01-wizard-tls-required"
    Add-Finding -Check "اختبارُ الاتصال باشتراط TLS على خادمٍ ssl=on ينجح ويُسمّي الخادم" -Expected "الاتصال ناجح" `
        -Actual $tlsMsg -Ok ($tlsMsg -match "ناجح")
    if ($tlsMsg -notmatch "ناجح") { throw "الاتصالُ المشفَّر لم ينجح: $tlsMsg" }

    Invoke-Button (Find-ById $wiz "StartButton" 30)

    # ── ملفُّ الاتصال محميّ ───────────────────────────────────────────────────
    $cfgPath = Join-Path $dataRoot "database.json"
    $deadline = (Get-Date).AddSeconds(90)
    while (-not (Test-Path $cfgPath) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 500 }
    if (Test-Path $cfgPath) {
        $raw = Get-Content -LiteralPath $cfgPath -Raw
        $protected = $raw -match "BRQOSH1:"
        $leaks = $raw.Contains($PgPassword)
        $tlsKept = $raw -notmatch '"?SslMode"?\s*[:=]\s*"?(Disable|Allow|Prefer)'
        Add-Finding -Check "database.json يُكتب من الشاشة ومحميّاً (ت-٣)" -Expected "BRQOSH1: ولا كلمة مرور صريحة" `
            -Actual $(if ($protected -and -not $leaks) { "محميّ · لا تسريب" } elseif ($leaks) { "⚠ كلمةُ المرور صريحةٌ على القرص" } else { "غيرُ محميّ" }) `
            -Ok ($protected -and -not $leaks)
    } else {
        Add-Finding -Check "database.json يُكتب من الشاشة (ت-٣)" -Expected "يُكتب" -Actual "لم يُكتب خلال ٩٠ ثانية" -Ok $false
    }

    # ── نافذةُ التفعيل ⟵ معرّفُ الجهاز إلى المُضيف ⟵ ترخيصٌ موقَّعٌ يعود ملفاً ────
    $lic = Find-WindowByName -Name "تفعيل ترخيص BRQOSH" -TimeoutSeconds 240
    if ($null -eq $lic) { throw "لم تظهر نافذةُ التفعيل — أو أُغلق التطبيق." }
    Add-Finding -Check "نافذةُ التفعيل تظهر (لا فترةَ تجربة في الإنتاج)" -Expected "تظهر" -Actual "ظهرت" -Ok $true
    Start-Sleep -Seconds 1
    $machineId = (Get-ElementText (Find-ById $lic "MachineIdBox" 30)).Trim()
    $shownCompany = (Get-ElementText (Find-ById $lic "CompanyBox" 30)).Trim()
    Add-Finding -Check "اسمُ المنشأة في نافذة التفعيل هو اسمُ الإعداد (م-١٤)" -Expected $company -Actual $shownCompany -Ok ($shownCompany -eq $company)
    Add-Finding -Check "معرّفُ الجهاز غيرُ فارغ" -Expected "16 محرفاً+" -Actual "$($machineId.Length) محرفاً" -Ok ($machineId.Length -ge 8)
    Save-WindowShot $lic "02-license-activation-empty"

    # ‏**المفتاحُ الخاصُّ لا يدخل هذه الآلة.** يُكتب طلبٌ، ويُنتظر ترخيصٌ موقَّعٌ من المُضيف.
    $req = [ordered]@{ Stamp = $stamp; CompanyName = $company; MachineId = $machineId }
    ($req | ConvertTo-Json) | Set-Content -LiteralPath (Join-Path $share "prodboot-$stamp-request.json") -Encoding UTF8
    $licFile = Join-Path $share "prodboot-$stamp-license.json"
    Write-Host "  MachineId: $machineId — بانتظار $licFile"
    $deadline = (Get-Date).AddMinutes($LicenseWaitMinutes)
    while (-not (Test-Path $licFile) -and (Get-Date) -lt $deadline) { Start-Sleep -Seconds 3 }
    if (-not (Test-Path $licFile)) { throw "لم يصل ترخيصٌ موقَّعٌ من المُضيف خلال $LicenseWaitMinutes دقيقة." }
    Start-Sleep -Seconds 2
    $signed = (Get-Content -LiteralPath $licFile -Raw).Trim()
    Add-Finding -Check "ترخيصٌ موقَّعٌ وصل من المُضيف (المفتاحُ الخاصُّ لم يغادره)" -Expected "JSON يبدأ بـ{" `
        -Actual "$($signed.Length) محرفاً" -Ok ($signed.StartsWith('{'))

    $keyBox = Find-ById $lic "KeyBox" 30
    $pasted = ""
    foreach ($attempt in 1..3) {
        Set-WindowForeground $lic
        Set-MultilineText $keyBox $signed
        $pasted = (Get-ElementText $keyBox).Trim()
        if ($pasted.StartsWith('{') -and $pasted.Length -ge ($signed.Length - 4)) { break }
        Start-Sleep -Milliseconds 800
    }
    $pasteOk = $pasted.StartsWith('{') -and $pasted.Length -ge ($signed.Length - 4)
    Add-Finding -Check "نصُّ الترخيص يُلصق في حقل نافذة التفعيل (مسارُ الواجهة)" -Expected "$($signed.Length) محرفاً" `
        -Actual "$($pasted.Length) محرفاً" -Ok $pasteOk
    Save-WindowShot $lic "03-license-pasted"

    if ($pasteOk) {
        Invoke-Button (Find-ById $lic "ActivateButton" 30)
        $race = Wait-ForEitherWindow -Expected "BRQOSH — تسجيل الدخول" -Unwanted "تفعيل ترخيص BRQOSH" -Process $app -TimeoutSeconds 240
        # ‏نافذةُ التفعيل قد تبقى لحظةً بعد الضغط؛ فإن بقيت فالحكمُ من نصّ خطئها لا من بقائها.
        if ($race.Outcome -eq "unwanted") {
            Start-Sleep -Seconds 4
            $again = Wait-ForEitherWindow -Expected "BRQOSH — تسجيل الدخول" -Unwanted "تفعيل ترخيص BRQOSH" -Process $app -TimeoutSeconds 120
            if ($again.Outcome -eq "expected") { $race = $again }
            else {
                $errEl = Find-ById $race.Window "ErrorText"
                $licErr = if ($null -ne $errEl) { $errEl.Current.Name } else { "(بلا نصّ خطأ)" }
                Save-WindowShot $race.Window "03b-license-rejected"
                Add-Finding -Check "(ت-١) ترخيصُ المورِّد يُقبل بالمفتاح المُضمَّن على التطبيق المُنصَّب" -Expected "يُقبل وتظهر شاشةُ الدخول" `
                    -Actual "رُفض: $licErr" -Ok $false
                throw "الترخيصُ رُفض — $licErr"
            }
        }
        Add-Finding -Check "(ت-١) ترخيصُ المورِّد يُقبل بالمفتاح المُضمَّن على التطبيق المُنصَّب" -Expected "يُقبل وتظهر شاشةُ الدخول" `
            -Actual $(switch ($race.Outcome) { "expected" { "قُبل — ظهرت شاشةُ الدخول" } "exited" { "أُغلق التطبيق" } default { "لم تظهر شاشةُ الدخول خلال المهلة" } }) `
            -Ok ($race.Outcome -eq "expected")
        if ($race.Outcome -ne "expected") { throw "لم تظهر شاشةُ الدخول بعد التفعيل ($($race.Outcome))." }
    } else {
        # ‏عجزُ اللصق ليس حكماً على المنتج: يُكتب المخزنُ كما يكتبه LicenseStore ويُعاد الإقلاع.
        Stop-Stray
        $licState = [ordered]@{ CompanyName = $company; MachineId = $machineId; SignedLicenseJson = $signed }
        ($licState | ConvertTo-Json -Depth 4) | Set-Content -LiteralPath (Join-Path $dataRoot "license.json") -Encoding UTF8
        $app = Start-App
        $race = Wait-ForEitherWindow -Expected "BRQOSH — تسجيل الدخول" -Unwanted "تفعيل ترخيص BRQOSH" -Process $app -TimeoutSeconds 240
        Add-Finding -Check "(ت-١) المفتاحُ المُضمَّن يقبل ترخيصَ المورِّد (مسارُ الملفّ بعد عجز اللصق)" -Expected "شاشةُ الدخول بلا إعادةِ نافذة التفعيل" `
            -Actual $race.Outcome -Ok ($race.Outcome -eq "expected")
        if ($race.Outcome -ne "expected") { throw "الترخيصُ لم يُقبل من الملفّ ($($race.Outcome))." }
    }

    # ── الدخول ───────────────────────────────────────────────────────────────
    $login = Find-WindowByName -Name "BRQOSH — تسجيل الدخول" -TimeoutSeconds 120
    if ($null -eq $login) { throw "لم تظهر نافذةُ الدخول." }
    Add-Finding -Check "شاشةُ الدخول تظهر (لا تجاوزَ UITEST)" -Expected "تظهر" -Actual "ظهرت" -Ok $true
    Set-WindowForeground $login
    Start-Sleep -Seconds 2
    Set-TextBoxValue (Find-ById $login "LoginUsername" 30) $adminUser
    Set-PasswordValue (Find-ById $login "PasswordBox" 30) $adminPass $login
    Save-WindowShot $login "04-login"
    Invoke-Button (Find-ById $login "LoginSubmit" 30)

    # ── تغييرُ كلمة المرور الإلزاميّ ──────────────────────────────────────────
    $chg = Find-WindowByName -Name "تغيير كلمة المرور" -TimeoutSeconds 120
    if ($null -ne $chg) {
        Add-Finding -Check "تغييرُ كلمة المرور إلزاميٌّ في أوّل دخول" -Expected "يُطلب" -Actual "طُلب" -Ok $true
        Set-WindowForeground $chg
        Set-PasswordValue (Find-ById $chg "CurrentPasswordBox" 30) $adminPass $chg
        Set-PasswordValue (Find-ById $chg "NewPasswordBox" 30) $newPass $chg
        Set-PasswordValue (Find-ById $chg "ConfirmPasswordBox" 30) $newPass $chg
        Save-WindowShot $chg "05-password-change"
        Invoke-Button (Find-ByName $chg "حفظ والمتابعة")
    } else {
        Add-Finding -Check "تغييرُ كلمة المرور إلزاميٌّ في أوّل دخول" -Expected "يُطلب" -Actual "لم يُطلب" -Ok $false
    }

    # ── الصدفة ───────────────────────────────────────────────────────────────
    $shell = Find-WindowByName -Name "BRQOSH ERP" -TimeoutSeconds 240
    Add-Finding -Check "(ر-ك) الإقلاعُ الإنتاجيُّ من المُنصِّب يبلغ الصدفةَ على الآلة النظيفة" -Expected "BRQOSH ERP" `
        -Actual $(if ($null -ne $shell) { "بلغها" } else { "لم يبلغها" }) -Ok ($null -ne $shell)
    if ($null -ne $shell) { Start-Sleep -Seconds 4; Save-WindowShot $shell "06-shell" }

    # ‏الدفاترُ في القاعدة لا في الذاكرة: الشركةُ والمديرُ كُتبا فعلاً.
    $companies = (Invoke-Psql -Database $dbName -Sql "select count(*) from companies;" | Out-String).Trim()
    $users     = (Invoke-Psql -Database $dbName -Sql "select count(*) from app_users;" | Out-String).Trim()
    Add-Finding -Check "الشركةُ والمديرُ كُتبا في PostgreSQL" -Expected "companies=1 · app_users≥1" `
        -Actual "companies=$companies · app_users=$users" -Ok (($companies -eq "1") -and ([int]("0$users") -ge 1))
    $sslConn = (Invoke-Psql -Database $dbName -Sql "select count(*) from pg_stat_ssl s join pg_stat_activity a on a.pid=s.pid where s.ssl and a.datname='$dbName';" | Out-String).Trim()
    Add-Finding -Check "اتصالاتُ التطبيق بالقاعدة مشفَّرة (pg_stat_ssl)" -Expected "≥1" -Actual "$sslConn" -Ok ([int]("0$sslConn") -ge 1)

    Stop-Stray

    # ── (م-١٣) ملفُّ اتصالٍ تالف ⟵ نافذةُ إنقاذٍ لا إغلاق ─────────────────────
    if (Test-Path $cfgPath) {
        $backup = Get-Content -LiteralPath $cfgPath -Raw
        Set-Content -LiteralPath $cfgPath -Value '{ "Provider": "postgresql", "ConnectionString": "BRQOSH1:@@corrupt@@" }' -Encoding UTF8
        $app2 = Start-App
        $rescue = Find-WindowByName -Name "BRQOSH — إعدادات الاتصال بقاعدة البيانات" -TimeoutSeconds 120
        Add-Finding -Check "(م-١٣) ملفُّ اتصالٍ تالف ⟵ نافذةُ إنقاذٍ لا إغلاق — على التطبيق المُنصَّب" -Expected "نافذةُ إعدادات الاتصال" `
            -Actual $(if ($null -ne $rescue) { "ظهرت — التركيبُ قابلٌ للإصلاح" } elseif ($app2.HasExited) { "أُغلق التطبيق — التركيبُ حجر" } else { "لم تظهر خلال ١٢٠ ثانية" }) `
            -Ok ($null -ne $rescue)
        if ($null -ne $rescue) { Save-WindowShot $rescue "07-recovery-window" }
        Stop-Stray
        Set-Content -LiteralPath $cfgPath -Value $backup -Encoding UTF8
    }
}
catch {
    Add-Finding -Check "التشغيلةُ اكتملت" -Expected "بلا إجهاض" -Actual $_.Exception.Message -Ok $false
    try { $shot = $AE::RootElement.FindFirst($TS::Children, (New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $app.Id))); if ($shot) { Save-WindowShot $shot "99-at-abort" } } catch { }
}
finally {
    Stop-Stray
    Write-Host ""
    Write-Host "=== الخلاصة ($stamp) ==="
    Import-Csv $csvLocal | Format-Table Check, Actual, Result -AutoSize | Out-String -Width 200 | Write-Host
    Write-Host "الدليل: $out  ⟶  $outShare"
    if ($script:fails -gt 0) { Write-Host "=== FAIL: $($script:fails) ==="; exit 1 }
    Write-Host "=== PASS — ر-ك حتى الصدفة على الآلة النظيفة ==="
    exit 0
}
