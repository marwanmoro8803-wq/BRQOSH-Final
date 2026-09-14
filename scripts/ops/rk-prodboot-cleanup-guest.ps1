<#
.SYNOPSIS
    (م-٦ · تبعيّةُ ن-٢٢) تنظيفُ قواعد القياس المتروكة `brqosh_prodboot_*` داخل الضيف.

.DESCRIPTION
    `rk-prodboot-guest.ps1` لم يكن يُسقط قاعدتَه قبل م-٦، فتركت كلُّ تشغيلةٍ قاعدةً فيها حسابُ مديرٍ
    بكلمةٍ كانت منشورةً في المستودع. وصار يُسقطها عند النجاح (`81e2fc4`) — **وما تُرك قبل ذلك يبقى**.

    فهذا السكربتُ يسرد تلك القواعدَ **ولا يُسقط شيئاً بلا `-Drop`**. والاسمُ يُطابَق بصيغة التشغيلة
    بالضبط (`brqosh_prodboot_yyyyMMdd_HHmmss`) — فلا تُمسّ قاعدةُ الإنتاج `brqosh` ولا اسمٌ يدويٌّ يُشبهها،
    والاسمُ المُطابَقُ لا يحمل إلّا حروفاً وأرقاماً وشُرَطاً سفليّة فلا يُحقن في الجملة. وبعد الإسقاط يُعاد
    السردُ ويُتحقَّق أنّ لا شيءَ بقي، وإلّا خرج بغير صفر.

.EXAMPLE
    ./rk-prodboot-cleanup-guest.ps1 -PgPassword <postgres superuser password>          # سردٌ فقط
    ./rk-prodboot-cleanup-guest.ps1 -PgPassword <postgres superuser password> -Drop    # إسقاطٌ ثمّ تحقّق
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PgPassword,
    [string]$PgHost = "localhost",
    [int]$PgPort = 5432,
    [string]$PgUser = "postgres",
    [switch]$Drop
)

$ErrorActionPreference = "Stop"
$psqlExe = "$env:ProgramFiles\PostgreSQL\17\bin\psql.exe"

# ‏كما في `rk-prodboot-guest.ps1`: `2>&1` تحت `Stop` في Windows PowerShell 5.1 يُنهي السكربتَ عند أوّل
# ‏سطرِ إشعارٍ من psql، فالتفضيلُ يُخفَّض محلّيّاً والحكمُ من رمز الخروج.
function Invoke-Psql { param([string]$Database, [string]$Sql, [switch]$AllowFailure)
    $env:PGPASSWORD = $PgPassword
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $out = & $psqlExe -h $PgHost -p $PgPort -U $PgUser -d $Database -tAc $Sql 2>&1
        $code = $LASTEXITCODE
    } finally { $ErrorActionPreference = $prev }
    $text = ($out | ForEach-Object { "$_" }) -join "`n"
    if ($code -ne 0 -and -not $AllowFailure) { throw "psql خرج بـ$code على [$Sql]: $text" }
    return $text
}

# ‏صيغةُ الاسم كما يُولّدها `rk-prodboot-guest.ps1` (`yyyyMMdd-HHmmss` بشرطةٍ سفليّة) — بلا زيادةٍ ولا نقص.
$script:ProdbootName = '^brqosh_prodboot_\d{8}_\d{6}$'

function Get-ProdbootDatabases {
    $text = Invoke-Psql -Database "postgres" -Sql "SELECT datname FROM pg_database WHERE datname LIKE 'brqosh%' ORDER BY datname;"
    return @($text -split "`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ -match $script:ProdbootName })
}

function Invoke-ProdbootCleanup { param([switch]$Drop)
    $found = @(Get-ProdbootDatabases)
    if ($found.Count -eq 0) {
        Write-Host "لا قاعدةَ قياسٍ متروكة."
        return 0
    }
    Write-Host "قواعدُ قياسٍ متروكة ($($found.Count)):"
    foreach ($db in $found) { Write-Host "  $db" }
    if (-not $Drop) {
        Write-Host "لم يُسقَط شيء — أعد التشغيلَ بـ -Drop للإسقاط."
        return 0
    }
    foreach ($db in $found) {
        Invoke-Psql -Database "postgres" -Sql "DROP DATABASE IF EXISTS $db WITH (FORCE);" -AllowFailure | Out-Null
    }
    # ‏**والإسقاطُ يُتحقَّق منه**: إسقاطٌ يفشل صامتاً يُبقي الحسابَ ورسالةً تقول «تمّ».
    $left = @(Get-ProdbootDatabases)
    if ($left.Count -gt 0) {
        Write-Host "بقيت بعد الإسقاط ($($left.Count)): $($left -join ', ')"
        return 1
    }
    Write-Host "أُسقطت $($found.Count)، وتُحقِّق أنّ لا شيءَ بقي."
    return 0
}

# ‏يُشغَّل حين يُستدعى السكربت، لا حين يُحمَّل بالنقطة — فيُختبر منطقُه بلا خادم.
if ($MyInvocation.InvocationName -ne '.') {
    if (-not (Test-Path $psqlExe)) { throw "لا psql: $psqlExe — شغّل rk-guest.ps1 -Stage Postgres أوّلاً." }
    exit (Invoke-ProdbootCleanup -Drop:$Drop)
}
