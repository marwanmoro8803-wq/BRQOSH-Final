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

param(
    [string]$ResultsDir = "TestResults",

    # سقّاطة مقيسة على الشجرة يوم 2026-08-28 بعد اطّراد استثناء الترحيلات
    # (Infrastructure 79.75% · Domain 47.53% · Application 74.61% · Api 70.35%).
    # كلُّ عتبةٍ دون المقيس بهامشٍ ضيّق يمنع الرفرفة ولا يسمح بانحدارٍ حقيقيّ.
    [hashtable]$Floors = @{
        "Brqosh.Infrastructure" = 0.78
        "Brqosh.Domain"         = 0.46
        "Brqosh.Application"    = 0.73
        "Brqosh.Api"            = 0.68
    },

    # يقبله المستدعون القدامى (run-external-gates.ps1) ويُطبَّق على المجموع الكلّي المدموج.
    [double]$MinimumLineRate = 0.65
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

if ($aggregate -lt $MinimumLineRate) {
    Write-Error "Merged coverage $aggregatePct% is below the $([math]::Round($MinimumLineRate * 100, 0))% floor."
    exit 1
}

if ($failures.Count -gt 0) {
    Write-Error "Assemblies below their floor: $($failures -join ', ')"
    exit 1
}

Write-Host "Coverage gate passed."
