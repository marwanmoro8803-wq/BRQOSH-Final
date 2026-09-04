# (CEEF-229 · م2-٣) بوابة تغطية صادقة — دمجٌ حقيقيّ، وحكمٌ لكل تجميعة على حدة.
#
# ما كان: السكربت يختار **ملفاً واحداً** من ملفات cobertura (الأحدث كتابةً) ويعلن معدّله
# «تغطية المشروع». وكلُّ مشروع اختبارٍ يُنتج ملفاً يقيس مجموعةَ أسطرٍ مختلفة، فالرقم المنشور
# كان يتغيّر تبعاً لأيّ مشروعٍ أنهى أخيراً — لا لأيّ تغيّرٍ في الكود. وقياسٌ نتيجتُه تعتمد على
# ترتيب انتهاء العمليات ليس قياساً.
#
# ولا يصحّ جمع الملفات جمعاً حسابياً: السطر الواحد يظهر في عدّة ملفات فيُحتسب مرّات. الصواب
# **اتّحاد** مجموعات الأسطر: سطرٌ مغطّى إن أصابه أيّ مشروع اختبار. وهذا ما يفعله السكربت الآن.
#
# والأرقام تُنشر كما هي لكل تجميعة، بعتبةٍ خاصّة بها تعمل **سقّاطةً**: لا تُخفَّض، وتُرفع عمداً.
# فعتبةٌ واحدة عامّة إمّا تُسقط البناء بلا فائدة أو تُرضي أضعف تجميعة فتُخفي الباقي.

# ─────────────────────────────────────────────────────────────────────────────
# (CEEF-229 · م2-٤) وتصحيحٌ ثانٍ: **الرقم المدموج كان مضخّماً بالحذف.**
#
# نُشر «المدموج 68.55%» وهو محسوبٌ على **أربع تجميعات من خمس**. والخامسة —
# Brqosh.Desktop، وهي **أكبر تجميعة في المستودع** (13,234 سطراً قابلاً للقياس، أكثر من
# Infrastructure) — لم تكن تدخل الحساب إطلاقاً، لأنّ مشروع اختبارها لا يحمل حزمة
# coverlet.collector. فلا يُنتَج لها ملفُّ cobertura، ولا يراها الدمج، ولا يشكو أحد:
# السكربت يجمع ما يجده، وما لا يجده لا وجود له عنده.
#
# وحُذفت أسوأُ تجميعةٍ تغطيةً (22.55%) من متوسّطٍ يزعم أنه «تغطية المشروع» — فارتفع
# الرقم ستّ عشرة نقطة بلا سببٍ إلا الغياب. والمدموج الصادق اليوم **52.78%**.
#
# فأُضيفت الحزمة، وأُضيفت عتبةُ Desktop بقيمتها المقيسة، وخُفّض المدموج إلى المقيس —
# **وهذا ليس تخفيفاً بل تصحيحُ رقمٍ لم يكن صحيحاً قط.** والسقّاطة ترتفع من هنا.
#
# ومعها حارسٌ بنيويّ: أيُّ تجميعةٍ متوقَّعة لا يظهر لها تقريرٌ **تُسقط البوّابة** بدل أن
# تختفي بصمت — فلا يتكرّر أن يخرج ربعُ المستودع من القياس دون أن ينتبه أحد.
# ─────────────────────────────────────────────────────────────────────────────

param(
    [string]$ResultsDir = "TestResults",

    # سقّاطة مقيسة على الشجرة يوم 2026-08-28 بعد اطّراد استثناء الترحيلات
    # (Infrastructure 79.75% · Domain 47.53% · Application 74.61% · Api 70.35%)،
    # ويوم 2026-08-29 بعد إدخال Desktop إلى القياس لأوّل مرّة (22.55%).
    # كلُّ عتبةٍ دون المقيس بهامشٍ ضيّق يمنع الرفرفة ولا يسمح بانحدارٍ حقيقيّ.
    [hashtable]$Floors = @{
        "Brqosh.Infrastructure" = 0.78
        "Brqosh.Domain"         = 0.46
        "Brqosh.Application"    = 0.73
        "Brqosh.Api"            = 0.68
        "Brqosh.Desktop"        = 0.21
    },

    # التجميعات التي **يجب** أن يظهر لها قياس. غيابُ أيٍّ منها إسقاطٌ للبوّابة لا تجاهل.
    [string[]]$RequiredAssemblies = @(
        "Brqosh.Infrastructure", "Brqosh.Domain", "Brqosh.Application", "Brqosh.Api", "Brqosh.Desktop"
    ),

    # يقبله المستدعون القدامى (run-external-gates.ps1) ويُطبَّق على المجموع الكلّي المدموج.
    # 52.78% مقيسة يوم 2026-08-29 بخمس تجميعات — وكانت 68.55% بأربع.
    [double]$MinimumLineRate = 0.52
)

$ErrorActionPreference = "Stop"

$files = @(Get-ChildItem -Path $ResultsDir -Recurse -Filter "coverage.cobertura.xml" -ErrorAction SilentlyContinue)
if ($files.Count -eq 0) {
    Write-Error "No coverage.cobertura.xml under $ResultsDir. Run: dotnet test Brqosh.sln --collect:`"XPlat Code Coverage`" --results-directory $ResultsDir"
    exit 1
}

# اتّحادُ الأسطر لكل تجميعة: المفتاح (ملف المصدر، رقم السطر) فلا يُحتسب سطرٌ مرّتين.
$total = @{}
$covered = @{}

foreach ($file in $files) {
    [xml]$doc = Get-Content -LiteralPath $file.FullName
    foreach ($pkg in $doc.coverage.packages.package) {
        $name = $pkg.name
        if (-not $total.ContainsKey($name)) {
            $total[$name] = [System.Collections.Generic.HashSet[string]]::new()
            $covered[$name] = [System.Collections.Generic.HashSet[string]]::new()
        }
        foreach ($cls in $pkg.classes.class) {
            $fn = $cls.filename
            foreach ($line in $cls.lines.line) {
                $key = "$fn#$($line.number)"
                [void]$total[$name].Add($key)
                if ([int]$line.hits -gt 0) { [void]$covered[$name].Add($key) }
            }
        }
    }
}

$rows = @()
$failures = @()
$grandTotal = 0
$grandCovered = 0

foreach ($name in ($total.Keys | Sort-Object)) {
    $t = $total[$name].Count
    if ($t -lt 50) { continue }   # تجميعات هامشية لا تُحكم عليها
    $c = $covered[$name].Count
    $rate = $c / $t
    $grandTotal += $t
    $grandCovered += $c

    $floor = if ($Floors.ContainsKey($name)) { [double]$Floors[$name] } else { $null }
    $gate = if ($null -eq $floor) { "report-only" }
            elseif ($rate -ge $floor) { "PASS" }
            else { $failures += $name; "FAIL" }

    $rows += [pscustomobject]@{
        Assembly = $name
        LineRate = "$([math]::Round($rate * 100, 2))%"
        Lines    = "$c / $t"
        Floor    = $(if ($null -eq $floor) { "—" } else { "$([math]::Round($floor * 100, 0))%" })
        Gate     = $gate
    }
}

# (CEEF-229 · م2-٤) حارسُ الغياب: تجميعةٌ متوقَّعة بلا قياس **تُسقط البوّابة**.
#
# هذا هو ما فات: مشروعُ اختبار سطح المكتب بلا coverlet.collector لا يُنتج تقريراً، فلا
# يظهر في الجدول أصلاً — لا صفراً ولا «report-only» بل **لا شيء**. فبدا كأنّ المستودع
# أربعُ تجميعات، وحُسب «المدموج» على ما بقي. والغيابُ الصامت أخطرُ من رقمٍ منخفض،
# لأنّ الرقم المنخفض يُرى ويُناقَش والغياب لا.
$measured = $rows | ForEach-Object { $_.Assembly }
$absent = @($RequiredAssemblies | Where-Object { $_ -notin $measured })

$aggregate = if ($grandTotal -gt 0) { $grandCovered / $grandTotal } else { 0 }
$aggregatePct = [math]::Round($aggregate * 100, 2)

Write-Host "## Code coverage (merged across $($files.Count) cobertura report(s))"
Write-Host ""
$rows | Format-Table -AutoSize | Out-String | Write-Host
Write-Host "Merged total: $grandCovered / $grandTotal = $aggregatePct% (floor $([math]::Round($MinimumLineRate * 100, 0))%)"
Write-Host ""

if ($env:GITHUB_STEP_SUMMARY) {
    $md = @("## Code coverage (merged)", "", "| Assembly | Line rate | Lines | Floor | Gate |", "|---|---|---|---|---|")
    foreach ($r in $rows) { $md += "| $($r.Assembly) | $($r.LineRate) | $($r.Lines) | $($r.Floor) | $($r.Gate) |" }
    $md += ""
    $md += "**Merged total:** $aggregatePct% ($grandCovered / $grandTotal)"
    $md -join "`n" | Out-File -FilePath $env:GITHUB_STEP_SUMMARY -Append -Encoding utf8
}

if ($absent.Count -gt 0) {
    Write-Error ("Assemblies expected in the coverage report but ABSENT: " + ($absent -join ', ') +
        ". A missing report is not zero coverage - it is no measurement at all, and the merged " +
        "figure above silently excludes it. Add coverlet.collector to that assembly's test project, " +
        "or remove it from -RequiredAssemblies deliberately.")
    exit 1
}

if ($aggregate -lt $MinimumLineRate) {
    Write-Error "Merged coverage $aggregatePct% is below the $([math]::Round($MinimumLineRate * 100, 0))% floor."
    exit 1
}

if ($failures.Count -gt 0) {
    Write-Error "Assemblies below their floor: $($failures -join ', ')"
    exit 1
}

Write-Host "Coverage gate passed."
