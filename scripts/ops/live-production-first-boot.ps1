<#
.SYNOPSIS
    (CEEF-236) الإقلاعُ الإنتاجيُّ الحيّ — أوّلُ تشغيلٍ في وضع الإنتاج بلا تجاوزات.

.DESCRIPTION
    لماذا هذا السكربت: كلُّ الجولات الحيّة السابقة (CEEF-232/235) جرت بـ
    BRQOSH_ENV=development و BRQOSH_UITEST=1 — **وهما يتخطّيان بالضبط البابَين اللذَين
    أخفقا** (الترخيص، وملفّ الاتصال). فبقي أوّلُ إقلاعٍ إنتاجيٍّ بلا شاهدٍ في أيّ حملة،
    ومرّت ت-١ وت-٣ سنةً كاملةً وراء ذلك العمى.

    وهذا السكربت يقود التطبيقَ في **وضع الإنتاج، بلا BRQOSH_UITEST**، على جذرِ بياناتٍ
    معزولٍ فارغ وقاعدةٍ جديدة — فيمرّ بالأبواب نفسِها التي يمرّ بها العميل:
    معالجُ التهيئة بخطوة ④، ثمّ نافذةُ التفعيل بترخيصٍ **موقَّعٍ بالمفتاح الخاصّ الحقيقيّ
    ومُتحقَّقٍ منه بالمفتاح المُضمَّن**، ثمّ الدخول وتغييرُ كلمة المرور، ثمّ الصدفة.

    وثلاثةُ فحوصٍ مسمّاة:
      أ) اشتراطُ TLS على خادمٍ بلا ssl ⟵ رفضٌ **بسببٍ مسمّى** لا «تعذّر الاتصال»   (م-١)
      ب) الإقلاعُ الإنتاجيُّ كاملاً حتى الصدفة                                    (ت-١ · ت-٣)
      ج) إتلافُ database.json ثمّ إعادةُ التشغيل ⟵ **نافذةُ إنقاذٍ لا إغلاق**        (م-١٣)

.NOTES
    **وحدُّه مُعلَنٌ صراحةً:** هذا **ليس ر-ك**. ر-ك تشترط جهازاً نظيفاً يُنصَّب عليه
    dist\BRQOSH-Setup.msi ولا يحمل .NET 9 — فتُقاس معه بوّابةُ المُنصِّب. وهذا يقيس
    **ما بعد التنصيب**: مسارَ المستخدم من أوّل نافذةٍ إلى الصدفة في وضع الإنتاج.
    ما لا يقيسه: المُنصِّب · LaunchCondition لـ.NET · جهازٌ بلا إطار · اتصالٌ مشفَّرٌ فعليّ.
#>
[CmdletBinding()]
param(
    [string]$OutDir = "dist\evidence\CEEF-236-prodboot",
    [string]$PgHost = "localhost",
    [int]$PgPort = 5433,
    [string]$PgUser = "postgres",
    [switch]$KeepDatabase
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms

$repoRoot = Resolve-Path -LiteralPath (Split-Path -Parent $PSCommandPath) | Split-Path -Parent | Split-Path -Parent
$exe = Join-Path $repoRoot "src\Brqosh.Desktop\bin\Debug\net9.0-windows\Brqosh.Desktop.exe"
if (-not (Test-Path $exe)) { throw "لم يُبنَ التطبيق: $exe — شغّل dotnet build src/Brqosh.Desktop -c Debug أولاً." }

$pwPath = Join-Path $env:APPDATA "BRQOSH\pg_password.txt"
if (-not (Test-Path $pwPath)) { throw "لا يوجد $pwPath — يلزم PostgreSQL محليّاً." }
$pgPassword = (Get-Content -LiteralPath $pwPath -Raw).Trim()

$vendorKey = Join-Path $env:APPDATA "BRQOSH\vendor-signing\license-private.pem"
if (-not (Test-Path $vendorKey)) {
    throw "لا يوجد مفتاحُ المورِّد الخاصّ: $vendorKey — ولّده بـ tools/LicenseIssuer gen-keys (ويلزم أن يطابق عامَّه المُضمَّن)."
}

$psqlExe = Get-ChildItem "$env:ProgramFiles\PostgreSQL\*\bin\psql.exe" -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
if (-not $psqlExe) { throw "psql.exe غير موجود — يلزم عميل PostgreSQL." }

$out = Join-Path $repoRoot $OutDir
New-Item -ItemType Directory -Force -Path $out | Out-Null

$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]

$script:findings = [System.Collections.ArrayList]::new()
function Add-Finding {
    param([string]$Check, [string]$Expected, [string]$Actual, [bool]$Ok)
    [void]$script:findings.Add([pscustomobject]@{
        Check = $Check; Expected = $Expected; Actual = $Actual; Result = $(if ($Ok) { "PASS" } else { "FAIL" })
    })
    $tag = if ($Ok) { "OK  " } else { "FAIL" }
    Write-Host "[$tag] $Check — $Actual"
}

function Find-WindowByName {
    param([string]$Name, [int]$TimeoutSeconds = 60)
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
    # ‏**الطريقُ المسدودُ يصير معلومة.** «لم يُعثر على العنصر» لا يقول ما الموجودُ فعلاً،
    # ‏فتُسكب كلُّ المعرّفات والأنواع والأسماء ليُقرأ منها الصواب.
    try {
        $rows = foreach ($e in $Scope.FindAll($TS::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) {
            "{0,-28} | {1,-16} | {2}" -f $e.Current.AutomationId, $e.Current.ControlType.ProgrammaticName.Replace('ControlType.',''), $e.Current.Name
        }
        $rows | Set-Content -LiteralPath (Join-Path $out "$Name.txt") -Encoding UTF8
    } catch { }
}

function Wait-ForEitherWindow { param([string]$Expected, [string]$Unwanted, $Process, [int]$TimeoutSeconds)
    # ‏**الحكمُ إيجابيّ لا مستنتَجٌ من غياب.** «لم تظهر نافذةُ التفعيل خلال ٢٥ ثانية» يصدق
    # ‏أيضاً حين لا يكون التطبيقُ قد بلغها بعد، أو حين سقط قبلها — فيُكتب PASS لرفضٍ واقع.
    # ‏فيُنتظر ما يجب أن يظهر، ويُراقَب معه ما يجب ألّا يظهر، وموتُ العملية إخفاقٌ مُسمّى.
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $bad = $AE::RootElement.FindFirst($TS::Children,
            (New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $Unwanted)))
        if ($null -ne $bad) { return @{ Outcome = "unwanted"; Window = $bad } }
        $good = $AE::RootElement.FindFirst($TS::Children,
            (New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $Expected)))
        if ($null -ne $good) { return @{ Outcome = "expected"; Window = $good } }
        if ($null -ne $Process -and $Process.HasExited) { return @{ Outcome = "exited"; Window = $null } }
        Start-Sleep -Milliseconds 500
    }
    return @{ Outcome = "timeout"; Window = $null }
}
function Find-ById { param($Scope, [string]$Id, [int]$TimeoutSeconds = 0)
    # ‏**انتظارٌ للعنصر لا افتراضُ حضوره.** النافذةُ قد تُوجَد وشجرةُ عناصرها لم تكتمل بعد،
    # ‏فيعود null ثمّ يسقط الاستدعاءُ بـ«method on a null-valued expression» — رسالةٌ لا
    # ‏تقول أيَّ عنصرٍ غاب. فيُنتظر، ويُسمّى الغائبُ باسمه عند المهلة.
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
    # ‏SendKeys يُفسّر + ^ % ~ ( ) { } [ ] — وكلمةُ مرورٍ فيها أيٌّ منها تُرسَل مشوَّهةً
    # ‏أو تُفسَّر اختصاراً. الهروبُ بلفّ كلٍّ منها بقوسَين معقوفَين.
    $sb = New-Object System.Text.StringBuilder
    foreach ($c in $Value.ToCharArray()) {
        if ('+^%~(){}[]'.Contains($c)) { [void]$sb.Append('{').Append($c).Append('}') }
        else { [void]$sb.Append($c) }
    }
    return $sb.ToString()
}

function Set-PasswordValue { param($Element, [string]$Value, $Window = $null)
    # ‏PasswordBox لا يدعم ValuePattern عمداً (أمن) — تركيزٌ ثمّ محاكاةُ لوحة مفاتيح.
    # ‏**والتقديمُ شرط**: بلا نافذةٍ في المقدّمة يُخفق SetFocus صامتاً وتُرسَل كلمةُ المرور
    # ‏إلى النافذة النشطة أياً كانت — وقد وقع هذا فعلاً مع لصق الترخيص (٤ محارف من ٦٩٧).
    if ($null -ne $Window) { Set-WindowForeground $Window }
    $Element.SetFocus(); Start-Sleep -Milliseconds 300
    [System.Windows.Forms.SendKeys]::SendWait((ConvertTo-SendKeys $Value)); Start-Sleep -Milliseconds 250
}
function Invoke-Button { param($Element)
    $Element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
}
function Get-ElementText { param($Element)
    if ($null -eq $Element) { return "" }
    try { return $Element.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value } catch { }
    # ‏صندوقُ نصٍّ متعدّدُ الأسطر في WPF **لا يدعم ValuePattern** — يُقرأ بـTextPattern.
    try { return $Element.GetCurrentPattern([System.Windows.Automation.TextPattern]::Pattern).DocumentRange.GetText(-1) } catch { }
    return $Element.Current.Name
}
Add-Type -Namespace Win32 -Name Fg -MemberDefinition @"
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
[DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
"@

function Set-WindowForeground { param($Win)
    # ‏اللصقُ يحتاج نافذةً في المقدّمة. `SetFocus` على عنصرٍ داخليٍّ لا يكفي حين تكون نافذةُ
    # ‏السكربت هي النشطة — وأوّلُ تشغيلةٍ أوصلت **حرفين** بدل سبعمئة لهذا السبب.
    try {
        $h = [IntPtr]$Win.Current.NativeWindowHandle
        if ($h -ne [IntPtr]::Zero) {
            [Win32.Fg]::ShowWindow($h, 9) | Out-Null   # SW_RESTORE
            [Win32.Fg]::SetForegroundWindow($h) | Out-Null
            Start-Sleep -Milliseconds 400
        }
    } catch { }
}

function Set-MultilineText { param($Element, [string]$Value)
    # ‏WPF يمنع ValuePattern على الصناديق متعدّدة الأسطر (AcceptsReturn)، وSendKeys يُفسّر
    # ‏{ } + ^ % ~ ( ) [ ] — وحمولةُ الترخيص Base64 مليئةٌ بها. فاللصقُ أسلمُ طريق.
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
function Save-WindowShot {
    param($Win, [string]$Name)
    try {
        $r = $Win.Current.BoundingRectangle
        if ($r.Width -le 0 -or $r.Height -le 0) { return }
        $vs = [System.Windows.Forms.SystemInformation]::VirtualScreen
        $x = [int][Math]::Max($r.X, $vs.X); $y = [int][Math]::Max($r.Y, $vs.Y)
        $w = [int][Math]::Min($r.Right - $x, $vs.Right - $x); $h = [int][Math]::Min($r.Bottom - $y, $vs.Bottom - $y)
        if ($w -le 0 -or $h -le 0) { return }
        $bmp = New-Object System.Drawing.Bitmap($w, $h)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.CopyFromScreen($x, $y, 0, 0, (New-Object System.Drawing.Size($w, $h)))
        $g.Dispose()
        $bmp.Save((Join-Path $out "$Name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
    } catch { Write-Host "  (تعذّرت اللقطة $Name)" }
}
function Stop-Stray {
    Get-Process -Name "Brqosh.Desktop" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 400
}
function Invoke-Psql { param([string]$Database, [string]$Sql)
    $env:PGPASSWORD = $pgPassword
    & $psqlExe -h $PgHost -p $PgPort -U $PgUser -d $Database -tAc $Sql 2>&1
}
function Start-App { param([string]$DataRoot)
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $exe
    $psi.UseShellExecute = $false
    $psi.EnvironmentVariables["BRQOSH_DATA_ROOT"] = $DataRoot
    $psi.EnvironmentVariables["BRQOSH_ENV"] = "production"   # ← الفارقُ كلُّه
    $psi.EnvironmentVariables.Remove("BRQOSH_UITEST") | Out-Null
    return [System.Diagnostics.Process]::Start($psi)
}

# ─────────────────────────────────────────────────────────────────────────────
$stamp     = Get-Date -Format "yyyyMMdd-HHmmss"
$dbName    = "brqosh_prodboot_$stamp"
$dataRoot  = Join-Path ([System.IO.Path]::GetTempPath()) "brqosh-prodboot-$stamp"
$company   = "منشأة الإقلاع الإنتاجي"
$adminUser = "prodadmin"
# ‏(م-٦ · تبعيّةُ ن-٢٢) **لا كلمةَ افتراضيّةً في سكربتِ تشغيل.**
#
# ‏كانت الكلمتان حرفيّتين هنا، **فحسابُ `prodadmin` مفتوحٌ لكلّ من قرأ المستودع** — وهو
# ‏العطلُ نفسُه الذي أُغلق في `FullScenarioAudit` (ن-٢٢) وفي `DbStartupProbe`. والقاعدةُ
# ‏هنا **مؤقّتةٌ وتُسقَط في نهاية التشغيلة**، فالضررُ أضيقُ — **لكنّ الكلمةَ منشورةٌ على
# ‏أيّ حال، ونافذةُ التشغيلة تكفي**.
#
# ‏**والثانيةُ لا تُطلب أصلاً**: لا شيءَ خارج هذه التشغيلة يحتاجها — تُكتب في مربّع
# ‏«كلمةٌ جديدة» ويُنتهى — فتُولَّد ولا تُخزَّن.
# ‏**ويُفحص قبل أيّ أثرٍ يبقى**: جذرُ البيانات والقاعدةُ وتشغيلُ التطبيق كلُّها بعد هذا السطر.
$adminPass = $env:BRQOSH_PRODBOOT_PASSWORD
if ([string]::IsNullOrWhiteSpace($adminPass)) {
    throw 'BRQOSH_PRODBOOT_PASSWORD غيرُ مضبوط — ولا كلمةَ احتياطيّةً هنا عمداً؛ الاحتياطيّةُ كانت منشورةً في المستودع. اضبطه قبل التشغيل.'
}
# ‏**وبسياسة كلمة المرور نفسِها** — مرآةٌ لـ`src/Brqosh.Infrastructure/Security/PasswordPolicy.cs`.
# ‏فكلمةٌ يرفضها المعالجُ كانت تمرّ هنا ثمّ تُرفض **بعد** إنشاء القاعدة وتشغيل التطبيق، فتضيع
# ‏التشغيلةُ (كشفته المراجعةُ الخصميّةُ قبل الإيداع). **ولماذا مرآةٌ لا الأصل**: شقيقُ هذا السكربت
# ‏يُشغَّل بـWindows PowerShell 5.1 داخل الضيف، والأصلُ تجميعةُ net9.0 لا تُحمَّل هناك — والفحصُ
# ‏واحدٌ في الاثنين. والمرآةُ قد تتأخّر عن الأصل إن تغيّر؛ **وأسوأُ ذلك تشغيلةٌ ضائعةٌ برسالةٍ
# ‏صريحة**، وهو ما كان يقع قبل هذا الفحص أصلاً.
$policyBreaks = @(
    if ($adminPass.Length -lt 12) { '12 محرفاً على الأقلّ' }
    if ($adminPass -cnotmatch '\p{Lu}') { 'حرفٌ كبير' }
    if ($adminPass -cnotmatch '\p{Ll}') { 'حرفٌ صغير' }
    if ($adminPass -notmatch '\p{Nd}') { 'رقم' }
    if ($adminPass -notmatch '[^\p{L}\p{Nd}]') { 'رمزٌ خاصّ' }
)
# ‏**والكلماتُ الممنوعةُ لا تُنسخ هنا عمداً**: قائمتُها في `PasswordPolicy.cs` وحدَه، والماسحُ الأمنيُّ
# ‏يُسقط أيَّ ذكرٍ لأوّلها خارجه — **ووحدةُ المصدر مقصودة**، ونسخةٌ هنا أسقطته فعلاً قبل الإيداع.
# ‏فكلمةٌ تحمل واحدةً منها يرفضها المعالجُ **بعد** الأثر: حالةٌ ضيّقةٌ مُعلَنةٌ في كتيّب O-03.
if ($policyBreaks.Count -gt 0) {
    throw ('BRQOSH_PRODBOOT_PASSWORD لا يُرضي سياسةَ كلمة المرور — ينقصه: ' + ($policyBreaks -join '، '))
}
# ‏و`PasswordPolicy` تمنع `123456`، وعشرةُ محارفَ ستّةَ عشريّةٍ قد تحملها (نحو ٣ في ١٠ ملايين).
# ‏فالتوليدُ يُعاد حتى يخلو منها: **صحّةٌ مضمونةٌ لا مُرجَّحة** في سكربتٍ وظيفتُه أن يُثبت.
do { $newPass = 'Rk' + ([guid]::NewGuid().ToString('N').Substring(0, 10)) + '!9aZ' } while ($newPass -match '123456')

Write-Host "=== الإقلاعُ الإنتاجيُّ الحيّ (CEEF-236) ==="
Write-Host "DataRoot : $dataRoot"
Write-Host "Database : $dbName"
Write-Host "BRQOSH_ENV=production · BRQOSH_UITEST غير مضبوط"
Write-Host ""

Stop-Stray
New-Item -ItemType Directory -Force -Path $dataRoot | Out-Null
Invoke-Psql -Database "postgres" -Sql "CREATE DATABASE `"$dbName`";" | Out-Null

# ‏هل يقدّم الخادمُ TLS؟ يُقاس ولا يُفترض — عليه يتوقّف تفسيرُ الفحص (أ).
$serverSsl = (Invoke-Psql -Database "postgres" -Sql "show ssl;").Trim()
Write-Host "PostgreSQL ssl = $serverSsl"
Write-Host ""

$app = $null
try {
    $app = Start-App -DataRoot $dataRoot

    # ── معالجُ التهيئة ────────────────────────────────────────────────────────
    $wiz = Find-WindowByName -Name "BRQOSH — إعداد النظام" -TimeoutSeconds 90
    if ($null -eq $wiz) { throw "لم تظهر نافذةُ معالج التهيئة." }
    Add-Finding -Check "المعالجُ يظهر في وضع الإنتاج" -Expected "نافذة الإعداد" -Actual "ظهرت" -Ok $true
    Start-Sleep -Seconds 1

    Set-TextBoxValue (Find-ById $wiz "CompanyNameBox" 20) $company
    Set-TextBoxValue (Find-ById $wiz "AdminUsernameBox" 20) $adminUser
    Set-PasswordValue (Find-ById $wiz "AdminPasswordBox" 20) $adminPass $wiz
    Set-PasswordValue (Find-ById $wiz "AdminPasswordConfirmBox" 20) $adminPass $wiz

    # ‏خانةُ SQLite يجب أن تكون **مخفيّةً** في الإنتاج — قياسٌ لا افتراض.
    $sqliteBox = Find-ById $wiz "UseSqliteBox"
    $sqliteOffscreen = ($null -eq $sqliteBox) -or $sqliteBox.Current.IsOffscreen
    Add-Finding -Check "خيارُ SQLite مخفيٌّ في الإنتاج" -Expected "مخفيّ" `
        -Actual $(if ($sqliteOffscreen) { "مخفيّ" } else { "ظاهر — تسريبُ مسارِ تطوير" }) -Ok $sqliteOffscreen

    Set-TextBoxValue (Find-ById $wiz "PgHostBox" 20) $PgHost
    Set-TextBoxValue (Find-ById $wiz "PgPortBox" 20) "$PgPort"
    Set-TextBoxValue (Find-ById $wiz "PgDatabaseBox" 20) $dbName
    Set-TextBoxValue (Find-ById $wiz "PgUserBox" 20) $PgUser
    Set-PasswordValue (Find-ById $wiz "PgPasswordBox" 20) $pgPassword $wiz

    # ── (أ) اشتراطُ TLS على خادمٍ بلا ssl ⟵ سببٌ مسمّى ───────────────────────
    $tlsBox = Find-ById $wiz "RequireTlsBox" 20
    Set-ToggleState $tlsBox $true
    Invoke-Button (Find-ById $wiz "TestConnectionBtn" 20)
    Start-Sleep -Seconds 6
    $tlsMsg = (Find-ById $wiz "ConnectionStatusText" 20).Current.Name
    Save-WindowShot $wiz "01-wizard-tls-required"
    if ($serverSsl -eq "off") {
        $named = $tlsMsg -match "TLS|مشفّر"
        Add-Finding -Check "(أ) اشتراطُ TLS على خادمٍ بلا ssl يُرفض بسببٍ مسمّى" `
            -Expected "رسالةٌ تسمّي TLS" -Actual $tlsMsg -Ok $named
        # ‏الخادمُ المحليُّ بلا ssl، فيُزال الاشتراطُ لإكمال الرحلة — والحدُّ مُسجَّلٌ في الدليل.
        Set-ToggleState $tlsBox $false
    } else {
        Add-Finding -Check "(أ) اشتراطُ TLS مع ssl=on" -Expected "نجاح" -Actual $tlsMsg `
            -Ok ($tlsMsg -match "ناجح")
    }

    # ── اختبارُ الاتصال ثمّ البدء ─────────────────────────────────────────────
    Invoke-Button (Find-ById $wiz "TestConnectionBtn" 20)
    Start-Sleep -Seconds 6
    $okMsg = (Find-ById $wiz "ConnectionStatusText" 20).Current.Name
    Add-Finding -Check "اختبارُ الاتصال ينجح ويُسمّي إصدارَ الخادم" -Expected "الاتصال ناجح" `
        -Actual $okMsg -Ok ($okMsg -match "ناجح")
    Save-WindowShot $wiz "02-wizard-connection-ok"

    Invoke-Button (Find-ById $wiz "StartButton" 20)

    # ── ملفُّ الاتصال يُكتب **محميّاً** — لا كلمةَ مرورٍ صريحةً على القرص ──────
    $cfgPath = Join-Path $dataRoot "database.json"
    $deadline = (Get-Date).AddSeconds(60)
    while (-not (Test-Path $cfgPath) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 500 }
    if (Test-Path $cfgPath) {
        $raw = Get-Content -LiteralPath $cfgPath -Raw
        $protected = $raw -match "BRQOSH1:"
        $leaks = $raw.Contains($pgPassword)
        Add-Finding -Check "database.json يُكتب من الشاشة ومحميّاً (ت-٣)" `
            -Expected "BRQOSH1: ولا كلمة مرور صريحة" `
            -Actual $(if ($protected -and -not $leaks) { "محميّ · لا تسريب" } elseif ($leaks) { "⚠ كلمةُ المرور صريحةٌ على القرص" } else { "غيرُ محميّ" }) `
            -Ok ($protected -and -not $leaks)
    } else {
        Add-Finding -Check "database.json يُكتب من الشاشة (ت-٣)" -Expected "يُكتب" -Actual "لم يُكتب" -Ok $false
    }

    # ── نافذةُ التفعيل — البابُ الذي كان يُغلق التطبيقَ (ت-١) ─────────────────
    $lic = Find-WindowByName -Name "تفعيل ترخيص BRQOSH" -TimeoutSeconds 180
    if ($null -eq $lic) { throw "لم تظهر نافذةُ التفعيل — أو أُغلق التطبيق." }
    Add-Finding -Check "نافذةُ التفعيل تظهر (لا فترةَ تجربة في الإنتاج)" -Expected "تظهر" -Actual "ظهرت" -Ok $true
    Start-Sleep -Seconds 1

    $machineId = (Get-ElementText (Find-ById $lic "MachineIdBox" 20)).Trim()
    $shownCompany = (Get-ElementText (Find-ById $lic "CompanyBox" 20)).Trim()
    Add-Finding -Check "اسمُ المنشأة في نافذة التفعيل هو اسمُ الإعداد (م-١٤)" `
        -Expected $company -Actual $shownCompany -Ok ($shownCompany -eq $company)
    Write-Host "  MachineId: $machineId"

    # ‏ترخيصٌ يُوقّع الآن بالمفتاح الخاصّ الحقيقيّ — ويُتحقَّق منه بالمفتاح **المُضمَّن**.
    # ‏عبر الـexe مباشرةً: `dotnet run … -- sign …` **يبتلع الوسائطَ** فتُطبع الاستعمالةُ بدل الترخيص،
    # ‏وقد أجهض ذلك أوّلَ تشغيلةٍ لهذا المسبار.
    $issuer = Join-Path $repoRoot "tools/LicenseIssuer/bin/Debug/net9.0/LicenseIssuer.exe"
    if (-not (Test-Path $issuer)) {
        & dotnet build (Join-Path $repoRoot "tools/LicenseIssuer") -c Debug --nologo | Out-Null
    }
    if (-not (Test-Path $issuer)) { throw "تعذّر بناءُ LicenseIssuer: $issuer" }
    $signed = (& $issuer sign $company $machineId $vendorKey 365 2>&1 |
               Where-Object { $_ -is [string] -and $_.TrimStart().StartsWith('{') } | Select-Object -Last 1)
    if ([string]::IsNullOrWhiteSpace($signed)) { throw "تعذّر توقيعُ الترخيص." }

    $keyBox = Find-ById $lic "KeyBox" 20
    $pasted = ""
    foreach ($attempt in 1..2) {
        Set-WindowForeground $lic
        Set-MultilineText $keyBox $signed
        $pasted = (Get-ElementText $keyBox).Trim()
        if ($pasted.StartsWith('{')) { break }
        Start-Sleep -Milliseconds 600
    }
    # ‏**يُتحقَّق أنّ النصَّ وصل قبل الضغط** — وإلا نُسب فشلُ اللصق إلى الترخيص ظلماً،
    # ‏وهو بعينه نمطُ «رسالةٌ تُشخّص سبباً غيرَ السبب» الذي تلاحقه هذه الحملة.
    $pasteOk = $pasted.StartsWith('{') -and $pasted.Length -ge ($signed.Length - 4)
    Add-Finding -Check "نصُّ الترخيص يُلصق في حقل نافذة التفعيل (مسارُ الواجهة)" `
        -Expected "$($signed.Length) محرفاً" -Actual "$($pasted.Length) محرفاً" -Ok $pasteOk
    Save-WindowShot $lic "03-license-activation"
    if ($pasteOk) {
        Invoke-Button (Find-ById $lic "ActivateButton" 20)
        Start-Sleep -Seconds 3
        $stillOpen = $null -ne (Find-WindowByName -Name "تفعيل ترخيص BRQOSH" -TimeoutSeconds 3)
        $licErr = if ($stillOpen) { (Find-ById $lic "ErrorText").Current.Name } else { "" }
        Add-Finding -Check "(ت-١) ترخيصُ المورِّد يُقبل على جذرٍ نظيفٍ في الإنتاج" `
            -Expected "يُقبل وتُغلق النافذة" `
            -Actual $(if ($stillOpen) { "رُفض: $licErr" } else { "قُبل" }) -Ok (-not $stillOpen)
        if ($stillOpen) { throw "أُوقف: الترخيصُ رُفض — $licErr" }
    } else {
        # ‏**عجزُ الحقنِ في الواجهة ليس حكماً على المنتج.** يُكتب المخزنُ كما يكتبه
        # ‏`LicenseStore.SaveSignedLicense` ثمّ يُعاد الإقلاع — فيُقاس ما يُقاس فعلاً:
        # ‏هل يقبل **المفتاحُ المُضمَّن** ترخيصَ المورِّد على جذرٍ نظيفٍ في الإنتاج؟
        Stop-Stray
        $licState = [ordered]@{ CompanyName = $company; MachineId = $machineId; SignedLicenseJson = $signed }
        ($licState | ConvertTo-Json -Depth 4) | Set-Content -LiteralPath (Join-Path $dataRoot "license.json") -Encoding UTF8
        $app = Start-App -DataRoot $dataRoot
        $race = Wait-ForEitherWindow -Expected "BRQOSH — تسجيل الدخول" `
            -Unwanted "تفعيل ترخيص BRQOSH" -Process $app -TimeoutSeconds 180
        $verdict = switch ($race.Outcome) {
            "expected" { "بلغ شاشةَ الدخول — الترخيصُ مقبول" }
            "unwanted" { "أُعيدت نافذةُ التفعيل — الترخيصُ مرفوض" }
            "exited"   { "أُغلق التطبيقُ قبل أن يبلغ شاشةَ الدخول" }
            default    { "لم يبلغ شاشةَ الدخول خلال ١٨٠ ثانية" }
        }
        Add-Finding -Check "(ت-١) المفتاحُ المُضمَّن يقبل ترخيصَ المورِّد على جذرٍ نظيفٍ في الإنتاج" `
            -Expected "يبلغ شاشةَ الدخول بلا إعادةِ نافذة التفعيل" `
            -Actual $verdict -Ok ($race.Outcome -eq "expected")
        if ($race.Outcome -ne "expected") { throw "أُوقف: $verdict" }
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
    # ‏«تسجيل الدخول» اسمٌ يحمله زرّان (تبويبٌ وإرسال) — يُقصد الإرسالُ بمعرّفه.
    Invoke-Button (Find-ById $login "LoginSubmit" 30)

    # ── تغييرُ كلمة المرور الإلزاميّ ──────────────────────────────────────────
    $chg = Find-WindowByName -Name "تغيير كلمة المرور" -TimeoutSeconds 90
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
    $shell = Find-WindowByName -Name "BRQOSH ERP" -TimeoutSeconds 180
    Add-Finding -Check "(ب) الإقلاعُ الإنتاجيُّ يبلغ الصدفة" -Expected "BRQOSH ERP" `
        -Actual $(if ($null -ne $shell) { "بلغها" } else { "لم يبلغها" }) -Ok ($null -ne $shell)
    if ($null -ne $shell) { Start-Sleep -Seconds 3; Save-WindowShot $shell "06-shell" }

    Stop-Stray

    # ── (ج) إتلافُ ملفّ الاتصال ⟵ نافذةُ إنقاذٍ لا إغلاق (م-١٣) ──────────────
    $backup = Get-Content -LiteralPath $cfgPath -Raw
    Set-Content -LiteralPath $cfgPath -Value '{ "Provider": "postgresql", "ConnectionString": "BRQOSH1:@@corrupt@@" }' -Encoding UTF8
    $app2 = Start-App -DataRoot $dataRoot
    $rescue = Find-WindowByName -Name "BRQOSH — إعدادات الاتصال بقاعدة البيانات" -TimeoutSeconds 90
    Add-Finding -Check "(ج · م-١٣) ملفُّ اتصالٍ تالف ⟵ نافذةُ إنقاذٍ لا إغلاق" `
        -Expected "نافذةُ إعدادات الاتصال" `
        -Actual $(if ($null -ne $rescue) { "ظهرت — التركيبُ قابلٌ للإصلاح" } else { "لم تظهر — التركيبُ حجر" }) `
        -Ok ($null -ne $rescue)
    if ($null -ne $rescue) { Save-WindowShot $rescue "07-recovery-window" }
    Stop-Stray
    Set-Content -LiteralPath $cfgPath -Value $backup -Encoding UTF8

} catch {
    # ‏**تشغيلةٌ مُجهَضةٌ ليست ناجحة.** كانت الخلاصةُ تُحسب من صفوف الجدول وحدَها، فأوّلُ إجهاضٍ
    # ‏(‏تعذّر توقيعُ الترخيص‎) طبع «PASS» ثمّ رمى — وهو بعينه نمطُ «الصمتُ يُقرأ نجاحاً».
    Add-Finding -Check "التشغيلةُ اكتملت" -Expected "بلا إجهاض" -Actual $_.Exception.Message -Ok $false
} finally {
    Stop-Stray
    $csv = Join-Path $out "findings.csv"
    $script:findings | Export-Csv -LiteralPath $csv -NoTypeInformation -Encoding UTF8
    Write-Host ""
    Write-Host "=== الخلاصة ==="
    $script:findings | Format-Table -AutoSize
    $fails = @($script:findings | Where-Object { $_.Result -eq "FAIL" }).Count
    Write-Host "الدليل: $out"
    if (-not $KeepDatabase) {
        Invoke-Psql -Database "postgres" -Sql "DROP DATABASE IF EXISTS `"$dbName`" WITH (FORCE);" | Out-Null
        Remove-Item -LiteralPath $dataRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
    if ($fails -gt 0) {
        Write-Host "=== FAIL: $fails فحصاً خالف المتوقَّع ==="
        $global:LASTEXITCODE = 1
        exit 1
    } else {
        Write-Host "=== PASS — وهذا ليس ر-ك: لم يُقَس المُنصِّبُ ولا جهازٌ بلا .NET 9 ==="
        exit 0
    }
}
