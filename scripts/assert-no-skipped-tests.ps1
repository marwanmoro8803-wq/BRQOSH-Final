#Requires -Version 7.0
<#
.SYNOPSIS
  (CEEF-237 · ج-٢ · م-٠ · ن-٠٦) «صفرُ متخطًّى» — بوّابةُ CI نفسُها، يُشغّلها CI و`verify-gates.ps1` معاً.

.DESCRIPTION
  **صفرُ متخطًّى شرطٌ لا زينة.** لولاه لعاد التخطّي صامتاً بأيّ تغييرٍ في البيئة، وعاد الأخضرُ
  يعني «لم يُفحص». وحارسان بنيويّان — لأنّ عدَّ المتخطّى لا يعني شيئاً إن لم يُعلم **كم مشروعاً
  قيس أصلاً**:
    ١) عددُ ملفّات trx يساوي عددَ مشاريع الاختبار. نقصانُه يعني دهساً أو مشروعاً لم يُشغَّل —
       وكلاهما يُبطل الحكم، والغيابُ الصامت أخطرُ من رقمٍ سيّئ.
    ٢) سمةُ `notExecuted` **حاضرة**. غيابُها يُقرأ صفراً في PowerShell فتمرّ trx مشوَّهةٌ بلا
       اعتراض — قِيس بملفٍّ اصطناعيٍّ بلا السمة فمرّ.

  (ح-٧٣ · evidence-integrity#4) **كانت نصّاً داخل `ci.yml`، و`verify-gates.ps1` يختبر نسخةً
  منها** — فتعديلُ البوّابة الحقيقيّة لا يراه اختبارُ حساسيّتها، وحالتُه تبقى «عضّت». صارت
  ملفّاً واحداً يُشغّله الاثنان، ويسقط بـ`throw` فيسقط معه كلُّ مَن يستدعيه.

.PARAMETER ResultsDir
  مجلّدُ ملفّات trx (في CI: `TestResults`).

.PARAMETER TestsDir
  مجلّدُ مشاريع الاختبار — **العددُ مشتقٌّ منه لا مكتوبٌ بيد**: مشروعٌ جديدٌ يُحتسب من نفسِه.
#>
param(
    [string]$ResultsDir = "TestResults",
    [string]$TestsDir = "tests"
)

$ErrorActionPreference = "Stop"

$projects = @(Get-ChildItem $TestsDir -Recurse -Filter *.Tests.csproj)
$trx = @(Get-ChildItem $ResultsDir -Recurse -Filter *.trx -ErrorAction SilentlyContinue)
if ($trx.Count -eq 0) { throw 'لا ملفَّ نتائج (.trx) — لا يمكن إثباتُ عدم التخطّي.' }
if ($trx.Count -ne $projects.Count) {
    throw ("‏عددُ ملفّات trx $($trx.Count) لا يساوي عددَ مشاريع الاختبار $($projects.Count). " +
           "‏إمّا دُهست ملفّاتٌ (تحقّق من LogFilePrefix) أو لم يُشغَّل مشروع — " +
           "‏وفي الحالتين الحكمُ أدناه يغطّي أقلَّ ممّا يدّعي.")
}
$skipped = 0
foreach ($f in $trx) {
    $x = [xml](Get-Content $f.FullName)
    $counters = $x.TestRun.ResultSummary.Counters
    if ($null -eq $counters -or $null -eq $counters.notExecuted) {
        throw "‏$($f.Name): لا سمةَ notExecuted في Counters — trx مشوَّهةٌ لا تُقرأ صفراً."
    }
    $skipped += [int]$counters.notExecuted
}
if ($skipped -gt 0) { throw "‏$skipped اختباراً تُخطّى — والبوّابةُ تشترط صفراً." }
Write-Host "‏صفرُ متخطًّى — على $($trx.Count) مشروعاً من $($projects.Count)."
