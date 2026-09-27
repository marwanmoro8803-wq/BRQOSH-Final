#Requires -Version 7.0
<#
.SYNOPSIS
  (م-٠ · ن-١١) «كم نُفِّذ فعلاً؟» — بوّابةُ `run-external-gates.ps1` نفسُها، يُشغّلها هو و`verify-gates.ps1` معاً.

.DESCRIPTION
  **رمزُ الخروج وحدَه لا يُثبت أنّ اختباراً واحداً نُفِّذ.** قِيس:
  `dotnet test --filter "FullyQualifiedName~ZZZNoSuchTestExists"` ⟸ **exit=0**. فأيُّ بوّابةٍ تحكم
  برمز الخروج وحدَه تُخضّر تشغيلةً نفّذت صفرَ اختبار — واسمُ اختبارٍ يتغيّر أو مُرشِّحٌ يخطئ يكفي.
  فالحكمُ على **العدد المُنفَّذ** مقروءاً من الـtrx، وعلى أنّ لكلّ مشروعٍ ملفَّه.

  (ح-٧٣ · evidence-integrity#4) **كانت نصّاً داخل المُشغّل، و`verify-gates.ps1` يختبر نسخةً منها.**
  صارت ملفّاً واحداً، ويسقط بـ`throw` — والمُشغّلُ يقف عنده (قِيس: رميُ الابن يوقف أباه تحت
  `Continue` و`Stop` كليهما، برمز 1).

.PARAMETER ResultsDir
  مجلّدُ ملفّات trx.

.PARAMETER TestsDir
  مجلّدُ مشاريع الاختبار — العددُ مشتقٌّ منه.
#>
param(
    [Parameter(Mandatory)][string]$ResultsDir,
    [string]$TestsDir = "tests"
)

$ErrorActionPreference = "Stop"

$trx = @(Get-ChildItem $ResultsDir -Recurse -Filter *.trx -ErrorAction SilentlyContinue)
$projects = @(Get-ChildItem $TestsDir -Recurse -Filter *.Tests.csproj)
if ($trx.Count -ne $projects.Count) {
    throw "‏ملفّاتُ trx $($trx.Count) لا تساوي مشاريعَ الاختبار $($projects.Count) — تغطيةُ الحكم أضيقُ ممّا يدّعي."
}
$executed = 0
foreach ($f in $trx) {
    $counters = ([xml](Get-Content $f.FullName)).TestRun.ResultSummary.Counters
    if ($null -eq $counters -or $null -eq $counters.total) {
        throw "‏$($f.Name): لا عدّاداتٍ في الـtrx — لا يُقرأ غيابُها صفراً."
    }
    $executed += [int]$counters.total
}
if ($executed -le 0) { throw "‏رمزُ الخروج صفرٌ ولم يُنفَّذ اختبارٌ واحد — بوّابةٌ تُخضّر العدم." }
Write-Host "‏نُفِّذ $executed اختباراً على $($trx.Count) مشروعاً." -ForegroundColor Green
