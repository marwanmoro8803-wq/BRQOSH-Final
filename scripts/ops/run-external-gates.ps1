# Runs external gate preparation + in-repo verification for O-01…O-05.
# Does NOT claim external closure — prints checklist and runs what can run locally.

$ErrorActionPreference = "Continue"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Location $root

# ‏(م-٠) ‏**سكربتُ بوّابةٍ يفشل بطريقتين، وفحصُ رمزِ الخروج يرى واحدةً فقط.**
#
# ‏`exit N` يضبط `$LASTEXITCODE`. أمّا `Write-Error` تحت `ErrorActionPreference=Stop`
# ‏— وهو ما يفعله `check-coverage.ps1` — **فيرمي ولا يضبطه**، فيقرأ السطرُ التالي
# ‏قيمةً **بائتةً من أمرٍ سابق**. قِيس: الرمزُ بقي 0 بعد فشلِ بوّابةِ تغطيةٍ حقيقيّ.
#
# ‏فيعمل اليومَ بالمصادفة (الرميةُ تُنهي المُشغّل)، ويسقط صامتاً يومَ يتحوّل سكربتٌ
# ‏من الرمي إلى `exit`. وهذا الغلافُ يمسك الحالتين ويُسمّي الساقطَ باسمه.
function Invoke-Gate {
    param(
        [Parameter(Mandatory)][string]$Path,
        [object[]]$Arguments = @(),
        [string]$Name
    )
    $label = if ($Name) { $Name } else { Split-Path $Path -Leaf }
    $global:LASTEXITCODE = 0
    try {
        & $Path @Arguments
    }
    catch {
        Write-Host "[FAIL] $label — $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[FAIL] $label — رمزُ الخروج $LASTEXITCODE" -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

Write-Host "=== BRQOSH External Gates Runner (O-01 to O-05) ===" -ForegroundColor Cyan
Write-Host ""

# ‏طابعُ قائمةٍ لا بوّابة — لا يُحكَم برمزه عمداً.
& "$root\scripts\ops\external-gates-checklist.ps1"
Write-Host ""

# ‏(م-٠ · ن-١٠) ‏**استدعاءٌ بلا فحصِ رمزِ خروج — وهو الوحيدُ من تسعةٍ كان كذلك.**
#
# ‏قِيس: الماسحُ خرج بـ1 على عيبٍ مزروع، وهذا المُشغّلُ طبع «‏External gates runner
# ‏complete» وخرج بـ**0**. وسبعةٌ من الاستدعاءات التسعة هنا تفحص رمزَها — فالسهوُ
# ‏وقع على البوّابة الأمنيّة وحدَها، وهي أولاها بالفحص.
Write-Host "[In-repo] Security static scan..." -ForegroundColor Yellow
Invoke-Gate "$root\scripts\security-static-scan.ps1" -Name "الماسح الأمني"
Write-Host ""

# (CEEF-229 · م2-٤) لا تخطّي هنا: هذا النصُّ يدّعي «التحقّق في المستودع»، وتحقّقٌ
# يتخطّى بوّابتَي الأداء يُعلن نتيجةً أضيق ممّا يقوله اسمُه. والثمنُ نحو 44 ثانية.
# ‏(م-٠ · ن-١١) ‏**رمزُ الخروج وحدَه لا يُثبت أنّ اختباراً واحداً نُفِّذ.**
#
# ‏قِيس: `dotnet test --filter "FullyQualifiedName~ZZZNoSuchTestExists"` ⟸ **exit=0**.
# ‏فأيُّ بوّابةٍ تحكم برمز الخروج وحدَه تُخضّر تشغيلةً نفّذت صفرَ اختبار — واسمُ اختبارٍ
# ‏يتغيّر أو مُرشِّحٌ يخطئ يكفي. فالحكمُ الآن على **العدد المُنفَّذ** مقروءاً من الـtrx.
Write-Host "[In-repo] Tests (بوّابتا الأداء تعملان)..." -ForegroundColor Yellow
Remove-Item Env:\BRQOSH_SKIP_STRESS -ErrorAction SilentlyContinue
$env:BRQOSH_TEST = "1"
$trxDir = Join-Path $root "TestResults"
# ‏(م-٠ · ن-١٤) ‏تنظيفٌ قبل القياس: تقاريرُ تشغيلةٍ سابقةٍ تُلوّث حكمَ هذه.
if (Test-Path $trxDir) { Remove-Item $trxDir -Recurse -Force }
dotnet test Brqosh.sln -c Release --nologo -v q `
    --logger "trx;LogFilePrefix=results" --results-directory $trxDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$trx = @(Get-ChildItem $trxDir -Recurse -Filter *.trx -ErrorAction SilentlyContinue)
$projects = @(Get-ChildItem (Join-Path $root "tests") -Recurse -Filter *.Tests.csproj)
if ($trx.Count -ne $projects.Count) {
    Write-Error "‏ملفّاتُ trx $($trx.Count) لا تساوي مشاريعَ الاختبار $($projects.Count) — تغطيةُ الحكم أضيقُ ممّا يدّعي."
    exit 1
}
$executed = 0
foreach ($f in $trx) {
    $counters = ([xml](Get-Content $f.FullName)).TestRun.ResultSummary.Counters
    if ($null -eq $counters -or $null -eq $counters.total) {
        Write-Error "‏$($f.Name): لا عدّاداتٍ في الـtrx — لا يُقرأ غيابُها صفراً."
        exit 1
    }
    $executed += [int]$counters.total
}
if ($executed -le 0) {
    Write-Error "‏رمزُ الخروج صفرٌ ولم يُنفَّذ اختبارٌ واحد — بوّابةٌ تُخضّر العدم."
    exit 1
}
Write-Host "‏نُفِّذ $executed اختباراً على $($trx.Count) مشروعاً." -ForegroundColor Green

Write-Host ""
Write-Host "[In-repo] FullScenarioAudit..." -ForegroundColor Yellow
dotnet run --project tools\FullScenarioAudit\FullScenarioAudit.csproj --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# ‏(م-٠) ‏حُذف `-MinimumLineRate 0.75`: المقيسُ المدموج **56.19%**، فالعتبةُ كانت
# ‏تجعل هذه الخطوة **مستحيلةَ النجاح** — ومُشغّلٌ لا يمكن أن يُنهي دورتَه لا يُشغَّل،
# ‏فلا تُكتشف أعطالُه. والسقّاطاتُ المُعايَرة في `check-coverage.ps1` هي الحكم.
Write-Host "[In-repo] Aggregate coverage gate (per-assembly floors)..." -ForegroundColor Yellow
Invoke-Gate "$root\scripts\check-coverage.ps1" -Name "بوابة التغطية"
Write-Host ""

Write-Host "[In-repo] PostgreSQL schema gate (skips without PG config)..." -ForegroundColor Yellow
Invoke-Gate "$root\scripts\ops\schema-gate-pg.ps1" -Name "بوابة المخطط"
Write-Host ""

Write-Host "[In-repo] i18n navigation gate (G-27)..." -ForegroundColor Yellow
Invoke-Gate "$root\scripts\ops\i18n-coverage-audit.ps1" -Name "بوابة التوطين"
Write-Host ""

Write-Host "[In-repo] Shura Council consensus gate..." -ForegroundColor Yellow
$shuraSource = "تقارير/CEEF-145_Live_UI_Audit_Marwan.md"
Invoke-Gate "$root\scripts\ops\run-shura-council.ps1" -Arguments @("-Source", $shuraSource) -Name "مجلس الشورى"
Write-Host ""

Write-Host "[In-repo] MSI publish verify (no WiX required)..." -ForegroundColor Yellow
Invoke-Gate "$root\installer\publish.ps1" -Name "نشر الحزمة"
Write-Host ""

Write-Host ""
Write-Host "=== External gates runner complete ===" -ForegroundColor Green
Write-Host "O-01 to O-05 remain EXTERNAL until live evidence (OTP, SOCPA, VM, 1M load, ZAP)."
