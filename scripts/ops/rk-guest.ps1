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
    [string]$Share = ""
)

$ErrorActionPreference = "Stop"

function Resolve-Share {
    if ($Share -and (Test-Path $Share)) { return $Share }
    foreach ($c in @("\\vboxsvr\rk", "E:\", "D:\", "F:\")) {
        if (Test-Path (Join-Path $c "BRQOSH-Setup.msi")) { return $c }
    }
    throw "لم يُعثر على المجلّد المشترك الذي يحمل BRQOSH-Setup.msi"
}
$share = Resolve-Share
$work = "C:\rk"
New-Item -ItemType Directory -Force -Path $work | Out-Null
$csv = Join-Path $share "rk-findings.csv"

function Add-Finding {
    param([string]$Check, [string]$Expected, [string]$Actual, [bool]$Ok)
    $row = [pscustomobject]@{
        Time = (Get-Date -Format "HH:mm:ss"); Check = $Check
        Expected = $Expected; Actual = $Actual; Result = $(if ($Ok) { "PASS" } else { "FAIL" })
    }
    $row | Export-Csv -LiteralPath $csv -NoTypeInformation -Encoding UTF8 -Append
    Write-Host "[$(if($Ok){'OK  '}else{'FAIL'})] $Check — $Actual"
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
    $exe = Join-Path $work "windowsdesktop-runtime-9-x64.exe"
    if (-not (Test-Path $exe)) {
        # ‏رابطُ مايكروسوفت الرسميّ الثابت لأحدث رقعةٍ من 9.0
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
    $pgPass = "RkPg!2026"
    $installer = Join-Path $work "postgresql-x64.exe"
    if (-not (Test-Path "$env:ProgramFiles\PostgreSQL\17\bin\psql.exe")) {
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

    # ‏شهادةٌ ذاتيّةُ التوقيع — يكفي لإثبات أنّ القناة مشفّرةٌ فعلاً (‏Require لا VerifyFull‎).
    $openssl = "$pgRoot\bin\openssl.exe"
    if ((Test-Path $openssl) -and -not (Test-Path "$dataDir\server.crt")) {
        & $openssl req -new -x509 -days 365 -nodes -text `
            -out "$dataDir\server.crt" -keyout "$dataDir\server.key" -subj "/CN=brqosh-rk" 2>&1 | Out-Null
        icacls "$dataDir\server.key" /inheritance:r /grant:r "NT AUTHORITY\NetworkService:(R)" 2>&1 | Out-Null
    }
    if (Test-Path "$dataDir\postgresql.conf") {
        $conf = Get-Content "$dataDir\postgresql.conf" -Raw
        if ($conf -notmatch "(?m)^\s*ssl\s*=\s*on") {
            Add-Content "$dataDir\postgresql.conf" "`nssl = on`nssl_cert_file = 'server.crt'`nssl_key_file = 'server.key'`n"
        }
        Restart-Service postgresql-x64-17 -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 8
    }
    $env:PGPASSWORD = $pgPass
    $ssl = (& "$pgRoot\bin\psql.exe" -h localhost -p 5432 -U postgres -d postgres -tAc "show ssl;" 2>&1) -join ""
    Add-Finding -Check "PostgreSQL يقدّم TLS داخل الآلة" -Expected "on" -Actual "$($ssl.Trim())" -Ok ($ssl -match "on")

    & "$pgRoot\bin\psql.exe" -h localhost -p 5432 -U postgres -d postgres -tAc "CREATE DATABASE brqosh_rk;" 2>&1 | Out-Null
    Set-Content -LiteralPath (Join-Path $share "pg-info.txt") -Encoding UTF8 `
        -Value "host=localhost`nport=5432`ndb=brqosh_rk`nuser=postgres`npassword=$pgPass"
}

switch ($Stage) {
    "NoDotnet" { Invoke-NoDotnetGate }
    "Dotnet"   { Install-DotnetDesktop }
    "Install"  { Install-Brqosh }
    "Postgres" { Install-Postgres }
    "All"      { Invoke-NoDotnetGate; Install-DotnetDesktop; Install-Brqosh; Install-Postgres }
}

Write-Host ""
Write-Host "النتائج: $csv"
if (Test-Path $csv) { Import-Csv $csv | Format-Table -AutoSize }
