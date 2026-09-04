<#
.SYNOPSIS
    (CEEF-236 · ر-ك) يُنفَّذ **داخل** الآلة الافتراضية النظيفة — من المُنصِّب إلى الصدفة.

.DESCRIPTION
    الترتيبُ مقصود: تُقاس بوّابةُ المُنصِّب **قبل** تنصيب .NET، لأنّ غيابَ الإطار هو
    بالضبط ما تحرسه — وهي T-07، مُعلَّقةٌ NOT TESTED منذ CEEF-234 بعبارة «لم يُثبَت
    حيّاً على جهازٍ فعليٍّ بلا .NET 9».

    المراحل:
      1 NoDotnet   — msiexec على جهازٍ بلا .NET 9 ⟵ **يُتوقَّع رفضٌ برسالتنا لا برسالة ويندوز**
      2 Dotnet     — تنصيبُ .NET 9 Desktop Runtime
      3 Install    — تنصيبُ MSI ⟵ التحقّقُ من BRQOSH_ENV ومن علامة البيئة
      4 Postgres   — تنصيبُ PostgreSQL **بـssl=on** ⟵ فيُقاس اتصالٌ مشفَّرٌ فعليّ
      5 All        — الأربعُ بالترتيب

    كلُّ مرحلةٍ تكتب سطراً في rk-findings.csv في المجلّد المشترك — فالنتيجةُ تخرج من
    الآلة حتى لو تعثّرت المرحلةُ التالية.
#>
[CmdletBinding()]
param(
    [ValidateSet("NoDotnet", "Dotnet", "Install", "Postgres", "All")]
    [string]$Stage = "All",
    [string]$Share = "",
    # ‏(CEEF-236 · مراجعة) كلمةُ مرور PostgreSQL **لا تُثبَّت في المستودع**: تُمرَّر أو تُولَّد
    # ‏لكلّ تشغيلة. كانت `RkPg!2026` حرفيّةً في الشيفرة، فمن قرأ المستودعَ ملك مستخدمَ
    # ‏postgres الأعلى في كلّ آلةٍ بُنيت بهذا الكتيّب.
    [string]$PgPassword = ""
)

$ErrorActionPreference = "Stop"

function Resolve-Share {
    if ($Share -and (Test-Path $Share)) { return $Share }
    # ‏‏`\\vboxsvr\rk` أوّلاً — والأقراصُ بعده، وقد يكون أحدُها قرصَ التنصيب لا المشترك،
    # ‏فيُشترط وجودُ سكربت الضيف نفسِه معه لا الحزمةِ وحدَها.
    foreach ($c in @("\\vboxsvr\rk", "E:\", "F:\", "G:\", "D:\")) {
        if ((Test-Path (Join-Path $c "BRQOSH-Setup.msi")) -and (Test-Path (Join-Path $c "rk-guest.ps1"))) { return $c }
    }
    throw "لم يُعثر على المجلّد المشترك الذي يحمل BRQOSH-Setup.msi"
}
$share = Resolve-Share
$work = "C:\rk"
New-Item -ItemType Directory -Force -Path $work | Out-Null
# ‏**ملفُّ نتائجَ لكلّ تشغيلة.** ملفٌّ واحدٌ يُلحَق به يخلط تشغيلةً قاست T-07 على جهازٍ بلا
# ‏.NET بتشغيلةٍ تاليةٍ تقول إنّ القياسَ لم يعد صالحاً — ويخرج الاثنان معاً كدليلٍ واحد
# ‏لا يميّز القارئُ أيَّهما أنتج أيَّ صفّ.
$runId = Get-Date -Format "yyyyMMdd-HHmmss"
$csv = Join-Path $share "rk-findings-$runId.csv"
$script:failCount = 0

function Add-Finding {
    param([string]$Check, [string]$Expected, [string]$Actual, [bool]$Ok)
    if (-not $Ok) { $script:failCount++ }
    $row = [pscustomobject]@{
        Run = $runId; Time = (Get-Date -Format "HH:mm:ss"); Check = $Check
        Expected = $Expected; Actual = $Actual; Result = $(if ($Ok) { "PASS" } else { "FAIL" })
    }
    $row | Export-Csv -LiteralPath $csv -NoTypeInformation -Encoding UTF8 -Append
    Write-Host "[$(if($Ok){'OK  '}else{'FAIL'})] $Check — $Actual"
}

function Invoke-Stage {
    param([string]$Name, [scriptblock]$Body)
    # ‏**إجهاضُ مرحلةٍ يُكتب صفّاً، لا يُترك فراغاً.** بلا هذا تسقط المرحلةُ فتغيب صفوفُها
    # ‏كلُّها من الدليل، ويبدو الملفُّ كأنّ تلك الفحوصَ لم تُطلَب أصلاً.
    try { & $Body }
    catch { Add-Finding -Check "المرحلة «$Name» اكتملت" -Expected "بلا إجهاض" -Actual $_.Exception.Message -Ok $false }
}

function Test-DotnetDesktop {
    $dir = "$env:ProgramFiles\dotnet\shared\Microsoft.WindowsDesktop.App"
    return (Test-Path $dir) -and @(Get-ChildItem $dir -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like "9.*" }).Count -gt 0
}

# ── ١) بوّابةُ المُنصِّب على جهازٍ بلا .NET 9 (T-07) ──────────────────────────
function Invoke-NoDotnetGate {
    Write-Host "=== ١) المُنصِّب على جهازٍ بلا .NET 9 ==="
    $present = Test-DotnetDesktop
    Add-Finding -Check "الجهازُ بلا .NET 9 Desktop Runtime قبل القياس" -Expected "غائب" `
        -Actual $(if ($present) { "موجود — القياسُ لا يصلح" } else { "غائب" }) -Ok (-not $present)
    if ($present) { return }

    $log = Join-Path $work "msi-nodotnet.log"
    $p = Start-Process msiexec.exe -Wait -PassThru -ArgumentList @(
        "/i", "`"$(Join-Path $share 'BRQOSH-Setup.msi')`"", "/qn", "/l*v", "`"$log`"")
    $logText = if (Test-Path $log) { Get-Content $log -Raw } else { "" }

    # ‏الرفضُ **برسالتنا** لا برسالة ويندوز — وهذا ما وعد به T-07.
    $ourMessage = $logText -match "\.NET Desktop Runtime 9" -or $logText -match "dotnet\.microsoft\.com"
    Add-Finding -Check "(T-07) المُنصِّب يرفض التثبيت بلا .NET 9" -Expected "خروجٌ غيرُ صفريّ" `
        -Actual "exit=$($p.ExitCode)" -Ok ($p.ExitCode -ne 0)
    Add-Finding -Check "(T-07) الرسالةُ رسالتُنا لا رسالةُ ويندوز" -Expected "تسمّي .NET 9 ورابطَ التنزيل" `
        -Actual $(if ($ourMessage) { "حاضرة في السجلّ" } else { "غائبة" }) -Ok $ourMessage
    Copy-Item $log (Join-Path $share "msi-nodotnet.log") -Force -ErrorAction SilentlyContinue

    $installed = Test-Path "$env:ProgramFiles\BRQOSH\Brqosh.Desktop.exe"
    Add-Finding -Check "(T-07) لا يُنصَّب شيءٌ عند الرفض" -Expected "لا ملفّات" `
        -Actual $(if ($installed) { "نُصِّب رغم الرفض" } else { "لم يُنصَّب" }) -Ok (-not $installed)
}

# ── ٢) .NET 9 Desktop Runtime ────────────────────────────────────────────────
function Install-DotnetDesktop {
    Write-Host "=== ٢) .NET 9 Desktop Runtime ==="
    if (Test-DotnetDesktop) { Add-Finding "‏.NET 9 حاضر" "موجود" "كان موجوداً" $true; return }
    # ‏النسخةُ المُهيّأةُ في المشترك أوّلاً — تنزيلٌ من داخل الضيف سببُ إخفاقٍ كاملٍ إن
    # ‏تعثّرت الشبكة في منتصف المراحل، والملفُّ نفسُه من المصدر الرسميّ نفسِه.
    $exe = Join-Path $work "windowsdesktop-runtime-9-x64.exe"
    $staged = Join-Path $share "windowsdesktop-runtime-9-x64.exe"
    if ((Test-Path $staged) -and -not (Test-Path $exe)) { Copy-Item $staged $exe -Force }
    if (-not (Test-Path $exe)) {
        Invoke-WebRequest -Uri "https://aka.ms/dotnet/9.0/windowsdesktop-runtime-win-x64.exe" -OutFile $exe -UseBasicParsing
    }
    $p = Start-Process $exe -Wait -PassThru -ArgumentList "/install", "/quiet", "/norestart"
    Add-Finding -Check "تنصيبُ .NET 9 Desktop Runtime" -Expected "exit=0" -Actual "exit=$($p.ExitCode)" `
        -Ok ($p.ExitCode -eq 0 -or $p.ExitCode -eq 3010)
    Add-Finding -Check "‏.NET 9 صار حاضراً" -Expected "موجود" `
        -Actual $(if (Test-DotnetDesktop) { "موجود" } else { "غائب" }) -Ok (Test-DotnetDesktop)
}

# ── ٣) تنصيبُ MSI والتحقّقُ من إعلان البيئة ──────────────────────────────────
function Install-Brqosh {
    Write-Host "=== ٣) تنصيبُ BRQOSH ==="
    $log = Join-Path $work "msi-install.log"
    $p = Start-Process msiexec.exe -Wait -PassThru -ArgumentList @(
        "/i", "`"$(Join-Path $share 'BRQOSH-Setup.msi')`"", "/qn", "/l*v", "`"$log`"")
    Copy-Item $log (Join-Path $share "msi-install.log") -Force -ErrorAction SilentlyContinue
    Add-Finding -Check "تنصيبُ MSI بعد توفّر .NET 9" -Expected "exit=0" -Actual "exit=$($p.ExitCode)" `
        -Ok ($p.ExitCode -eq 0)

    $exe = "$env:ProgramFiles\BRQOSH\Brqosh.Desktop.exe"
    Add-Finding -Check "الملفُّ التنفيذيُّ مُنصَّب" -Expected $exe `
        -Actual $(if (Test-Path $exe) { "موجود" } else { "غائب" }) -Ok (Test-Path $exe)

    # ‏(CEEF-228) المُنصِّب يُعلن الإنتاجَ بنطاق الجهاز — يُقاس لا يُفترض.
    $machineEnv = [Environment]::GetEnvironmentVariable("BRQOSH_ENV", "Machine")
    Add-Finding -Check "(CEEF-228) المُنصِّب يضبط BRQOSH_ENV=production بنطاق الجهاز" `
        -Expected "production" -Actual "$machineEnv" -Ok ($machineEnv -eq "production")

    $reg = (Get-ItemProperty "HKLM:\Software\BRQOSH" -Name "environment" -ErrorAction SilentlyContinue).environment
    Add-Finding -Check "علامةُ البيئة في السجلّ" -Expected "production" -Actual "$reg" -Ok ($reg -eq "production")
}

# ── ٤) PostgreSQL بـssl=on ───────────────────────────────────────────────────
function Install-Postgres {
    Write-Host "=== ٤) PostgreSQL بـTLS ==="
    $pgPass = if ([string]::IsNullOrWhiteSpace($PgPassword)) {
        # ‏تُولَّد لهذه التشغيلة وحدَها، وتُكتب في المشترك بملفٍّ مستقلٍّ يُحذف بعد الاستعمال.
        -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 20 | ForEach-Object { [char]$_ }) + "!aA1"
    } else { $PgPassword }
    $installer = Join-Path $work "postgresql-x64.exe"
    if (-not (Test-Path "$env:ProgramFiles\PostgreSQL\17\bin\psql.exe")) {
        $stagedPg = Join-Path $share "postgresql-x64.exe"
        if ((Test-Path $stagedPg) -and -not (Test-Path $installer)) { Copy-Item $stagedPg $installer -Force }
        if (-not (Test-Path $installer)) {
            Invoke-WebRequest -UseBasicParsing -OutFile $installer `
                -Uri "https://get.enterprisedb.com/postgresql/postgresql-17.6-1-windows-x64.exe"
        }
        $p = Start-Process $installer -Wait -PassThru -ArgumentList @(
            "--mode", "unattended", "--unattendedmodeui", "none",
            "--superpassword", $pgPass, "--serverport", "5432",
            "--disable-components", "stackbuilder,pgAdmin")
        Add-Finding -Check "تنصيبُ PostgreSQL" -Expected "exit=0" -Actual "exit=$($p.ExitCode)" -Ok ($p.ExitCode -eq 0)
    }
    $pgRoot = "$env:ProgramFiles\PostgreSQL\17"
    $dataDir = "$pgRoot\data"

    # ‏شهادةٌ ذاتيّةُ التوقيع — تكفي لإثبات أنّ القناة مشفّرةٌ فعلاً (‏Require لا VerifyFull‎).
    #
    # ‏**ولا تُولَّد هنا:** بناءُ EnterpriseDB لويندوز **لا يشحن openssl.exe** — قيس على
    # ‏نسختَي 16 و17 على المُضيف فالملفُّ غيرُ موجود. وكان التوليدُ يُتخطّى بصمت ثمّ يُكتب
    # ‏ssl = on بلا شهادة، **فيرفض الخادمُ الإقلاعَ أصلاً** وتسقط المرحلةُ بلا سببٍ مفهوم.
    # ‏فتُهيَّأ الشهادةُ على المُضيف وتُنسخ من المشترك.
    # ‏(وفحصُ صلاحيّات ملفّ المفتاح في PostgreSQL محصورٌ بغير ويندوز — ifndef WIN32.)
    foreach ($f in @("server.crt", "server.key")) {
        $src = Join-Path $share $f
        if ((Test-Path $src) -and -not (Test-Path "$dataDir\$f")) { Copy-Item $src "$dataDir\$f" -Force }
    }
    $haveCert = (Test-Path "$dataDir\server.crt") -and (Test-Path "$dataDir\server.key")
    Add-Finding -Check "شهادةُ TLS مُهيّأةٌ في مجلّد البيانات" -Expected "server.crt + server.key" `
        -Actual $(if ($haveCert) { "موجودتان" } else { "غائبتان — لن يُفعَّل ssl" }) -Ok $haveCert
    if ($haveCert -and (Test-Path "$dataDir\postgresql.conf")) {
        $conf = Get-Content "$dataDir\postgresql.conf" -Raw
        if ($conf -notmatch "(?m)^\s*ssl\s*=\s*on") {
            Add-Content "$dataDir\postgresql.conf" "`nssl = on`nssl_cert_file = 'server.crt'`nssl_key_file = 'server.key'`n"
        }
        # ‏**إعادةُ التشغيل تُقاس.** ابتلاعُها بـSilentlyContinue يعني أنّ خادماً رفض الإقلاعَ
        # ‏بعد `ssl = on` يمرّ صامتاً، ثمّ يُنسَب الإخفاقُ إلى psql بعده.
        $svc = Get-Service postgresql-x64-17 -ErrorAction SilentlyContinue
        if ($null -eq $svc) {
            Add-Finding -Check "خدمةُ PostgreSQL موجودة" -Expected "postgresql-x64-17" -Actual "غير موجودة" -Ok $false
            return
        }
        try { Restart-Service postgresql-x64-17 -Force -ErrorAction Stop } catch { }
        Start-Sleep -Seconds 8
        $svc.Refresh()
        $running = $svc.Status -eq 'Running'
        Add-Finding -Check "الخادمُ يُقلع بعد تفعيل ssl" -Expected "Running" -Actual "$($svc.Status)" -Ok $running
        if (-not $running) { return }
    }
    # ‏**المقارنةُ بالمساواة لا بالاحتواء، ورمزُ الخروج يُفحص.** `-match "on"` يصدق على
    # ‏«connection ... failed» أيضاً (فيها «on»)، و`2>&1` مع ErrorActionPreference=Stop
    # ‏يحوّل stderr إلى إنهاءٍ قاطعٍ قبل أن يُكتب أيُّ صفّ.
    $env:PGPASSWORD = $pgPass
    $ssl = (& "$pgRoot\bin\psql.exe" -h localhost -p 5432 -U postgres -d postgres -tAc "show ssl;" 2>$null | Out-String).Trim()
    $sslOk = ($LASTEXITCODE -eq 0) -and ($ssl -eq "on")
    Add-Finding -Check "PostgreSQL يقدّم TLS داخل الآلة" -Expected "on" `
        -Actual $(if ($LASTEXITCODE -ne 0) { "psql أخفق (exit=$LASTEXITCODE)" } else { $ssl }) -Ok $sslOk

    & "$pgRoot\bin\psql.exe" -h localhost -p 5432 -U postgres -d postgres -tAc "CREATE DATABASE brqosh_rk;" 2>$null | Out-Null

    # ‏**كلمةُ المرور لا تُكتب على القرص.** المجلّدُ المشترك يُنسخ إلى شجرة الأدلّة، وقد
    # ‏فُتح استثناءٌ لها في .gitignore — فالكتابةُ هنا ارتكابُ سرٍّ بعد خطوتين.
    Set-Content -LiteralPath (Join-Path $share "pg-info.txt") -Encoding UTF8 `
        -Value "host=localhost`nport=5432`ndb=brqosh_rk`nuser=postgres`n# كلمة المرور تُمرَّر بـ-PgPassword ولا تُكتب هنا"
    Set-Content -LiteralPath (Join-Path $work "pg-secret.txt") -Encoding UTF8 -Value $pgPass
    Write-Host "كلمةُ مرور PostgreSQL لهذه التشغيلة: C:\rk\pg-secret.txt (داخل الآلة، لا في المشترك)"
}

switch ($Stage) {
    "NoDotnet" { Invoke-Stage "NoDotnet" { Invoke-NoDotnetGate } }
    "Dotnet"   { Invoke-Stage "Dotnet"   { Install-DotnetDesktop } }
    "Install"  { Invoke-Stage "Install"  { Install-Brqosh } }
    "Postgres" { Invoke-Stage "Postgres" { Install-Postgres } }
    "All"      {
        Invoke-Stage "NoDotnet" { Invoke-NoDotnetGate }
        Invoke-Stage "Dotnet"   { Install-DotnetDesktop }
        Invoke-Stage "Install"  { Install-Brqosh }
        Invoke-Stage "Postgres" { Install-Postgres }
    }
}

Write-Host ""
Write-Host "النتائج: $csv"
if (Test-Path $csv) { Import-Csv $csv | Format-Table -AutoSize }

# ‏**حكمٌ صريحٌ ورمزُ خروج.** بلا هذا يخرج السكربتُ بصفرٍ مهما أخفق، فتقرؤه أيُّ أتمتةٍ نجاحاً.
Write-Host ""
if ($script:failCount -gt 0) {
    Write-Host "=== FAIL: $($script:failCount) فحصاً خالف المتوقَّع في التشغيلة $runId ==="
    exit 1
}
Write-Host "=== PASS: كلُّ فحوص التشغيلة $runId مرّت ==="
exit 0
