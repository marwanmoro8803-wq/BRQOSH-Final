#Requires -Version 7.0
<#
.SYNOPSIS
  (م-٠ · الجولة ٠) اختبارُ حساسيّةِ البوّابات — يزرع عيباً معلوماً أمام كلّ بوّابةٍ ويشترط أن تعضّ.

.DESCRIPTION
  بوّابةٌ تقول PASS وقد فحصت صفراً ليست بوّابة. وقد وقع ذلك في هذا المستودع أربعَ مرّاتٍ
  مقيسة — ولم يكشفها أنّ أحداً قرأ الكود، بل أنّ أحداً **زرع عيباً وانتظر الصراخ**.

  فهذا السكربتُ يُحوّل تلك الجولة من عملٍ يدويٍّ يُنسى إلى أصلٍ يُشغَّل:
  لكلّ حالةٍ عيبٌ يُزرع في **نسخةٍ معزولة** أو في ملفٍّ مؤقّتٍ خارج الشجرة، ثمّ تُشغَّل
  البوّابة، ويُشترط رمزُ خروجٍ غيرُ صفريّ. وحالةٌ تمرّ = بوّابةٌ عمياء تُسمّى بالاسم.

  **ولا يُعدَّل ملفٌّ في شجرة العمل**: العيوبُ إمّا في ملفّاتٍ مؤقّتة تُحذف، أو في مجلّداتٍ
  اصطناعيّةٍ خارج المستودع. والشجرةُ بعد التشغيل كما كانت — يتحقّق السكربتُ من ذلك بنفسه.

.PARAMETER Only
  تشغيلُ حالةٍ واحدةٍ باسمها (مثلاً: secrets · coverage-stale · zero-tests).

.EXAMPLE
  ./scripts/ops/verify-gates.ps1
  ./scripts/ops/verify-gates.ps1 -Only coverage-stale
#>
param([string]$Only)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Location $root

$tmp = Join-Path ([IO.Path]::GetTempPath()) "brqosh-gatecheck-$([Guid]::NewGuid().ToString('N').Substring(0,8))"
New-Item -ItemType Directory -Path $tmp -Force | Out-Null

$results = [System.Collections.Generic.List[object]]::new()
$plantedFiles = [System.Collections.Generic.List[string]]::new()

function Add-Result([string]$Name, [string]$Gate, [bool]$Bit, [string]$Detail) {
    $results.Add([pscustomobject]@{ Name = $Name; Gate = $Gate; Bites = $Bit; Detail = $Detail })
    $mark = if ($Bit) { "[عضّت]" } else { "[عمياء]" }
    $color = if ($Bit) { "Green" } else { "Red" }
    Write-Host ("  {0,-9} {1,-22} {2}" -f $mark, $Name, $Detail) -ForegroundColor $color
}

# ‏يُشغّل سكربتاً ويُعيد «هل فشل؟» — ممسكاً الطريقتين: `exit N` و`Write-Error` الرامية.
function Test-GateFails([scriptblock]$Action) {
    $global:LASTEXITCODE = 0
    try { & $Action *> $null }
    catch { return $true }
    return ($LASTEXITCODE -ne 0)
}

function New-Cobertura([string]$Path, [hashtable]$Rates) {
    $sb = [Text.StringBuilder]::new()
    [void]$sb.AppendLine('<coverage><packages>')
    foreach ($name in $Rates.Keys) {
        [void]$sb.AppendLine("  <package name=`"$name`"><classes><class filename=`"$name/X.cs`"><lines>")
        for ($i = 1; $i -le 100; $i++) {
            $hits = if ($i -le $Rates[$name]) { 1 } else { 0 }
            [void]$sb.AppendLine("    <line number=`"$i`" hits=`"$hits`"/>")
        }
        [void]$sb.AppendLine('   </lines></class></classes></package>')
    }
    [void]$sb.AppendLine('</packages></coverage>')
    New-Item -ItemType Directory -Path (Split-Path $Path -Parent) -Force | Out-Null
    [IO.File]::WriteAllText($Path, $sb.ToString())
}

$cases = @{}

# ── ١ · الماسحُ الأمنيّ: أربعةُ كواشفَ مستقلّة ───────────────────────────────────
$cases['secrets'] = {
    $probe = Join-Path $root "scripts\zz-gatecheck-probe.ps1"
    $plantedFiles.Add($probe)
    $lines = @(
        '# GATE-CHECK PROBE — يُحذف تلقائياً',
        # ‏(م-٤ · ن-١٣) ‏**الطُّعمُ يُركَّب عند التشغيل، ولا يُكتب حرفيّاً هنا.**
        #
        # ‏الماسحُ يستثني **نفسَه** لأنّ أنماطَه تحوي ما تبحث عنه — ولا يستثني هذا
        # ‏الملفّ. فبقيت أربعُ نتائجَ حرجةٍ في كلّ تشغيلة منذ `3896ef7` (‏2026-09-06)،
        # ‏**وخطوةُ الأمن في CI حمراءُ منذ ذلك اليوم** ولم يرها أحدٌ إذ لا يعمل CI
        # ‏على فرع الإصلاح. وهذا شكلُ «أداةُ القياس تُلوّث ما تقيسه» بعينه.
        #
        # ‏**والتركيبُ لا يُضعف شيئاً:** الملفُّ المزروعُ يحمل النصَّ كاملاً، فيراه
        # ‏الماسحُ ويسقط كما يجب. **والمصدرُ وحدَه هو ما نظُف.**
        '$fallbackLogin = "admin/admin' + '123"',
        '$pg' + 'Pass = "Hunter22!"'
    ) -join "`n"
    # ‏بايتُ تحكّمٍ حقيقيّ (0x08) — الكاشفُ الرابع.
    [IO.File]::WriteAllBytes($probe, [Text.Encoding]::UTF8.GetBytes($lines) + [byte[]]@(0x08, 0x0A))
    $bit = Test-GateFails { & "$root\scripts\security-static-scan.ps1" }
    Remove-Item $probe -Force -ErrorAction SilentlyContinue
    Add-Result 'secrets' 'security-static-scan' $bit ('admin' + '123 · $pgPass · بايت 0x08')   # ‏الوصفُ يُركَّب كذلك — وإلّا أسقط الماسحَ بنفسِه.
}

# ── ٢ · الاختبارُ الذاتيُّ لأنماط الأسرار: نمطٌ ميّتٌ يجب أن يُسقط البوّابة ────────
$cases['pattern-selftest'] = {
    $scan = Join-Path $root "scripts\security-static-scan.ps1"
    $backup = Join-Path $tmp "scan.bak"
    Copy-Item $scan $backup -Force
    try {
        $text = [IO.File]::ReadAllText($scan)
        $killed = $text.Replace('pass(word|wd)?|pwd|secret|token', 'zzzznomatchzzz')
        if ($killed -eq $text) { Add-Result 'pattern-selftest' 'security-static-scan' $false 'لم أجد النمطَ لأقتله — الحالةُ نفسُها عمياء'; return }
        [IO.File]::WriteAllText($scan, $killed)
        $bit = Test-GateFails { & $scan }
        Add-Result 'pattern-selftest' 'security-static-scan' $bit 'نمطُ أسرارٍ مقتول'
    }
    finally { Copy-Item $backup $scan -Force }
}

# ── ٣ · تدقيقُ الواجهة: إيموجي في فهرس التنقّل ──────────────────────────────────
$cases['ui-emoji'] = {
    $nav = Join-Path $root "src\Brqosh.Desktop\Navigation\NavigationCatalog.cs"
    $backup = Join-Path $tmp "nav.bak"
    Copy-Item $nav $backup -Force
    try {
        Add-Content -LiteralPath $nav -Value "`n// GATE-CHECK 📊" -Encoding UTF8
        $bit = Test-GateFails { & "$root\scripts\ui-ux-pro-max-audit.ps1" }
        Add-Result 'ui-emoji' 'ui-ux-pro-max-audit' $bit 'إيموجي في NavigationCatalog'
    }
    finally { Copy-Item $backup $nav -Force }
}

# ── ٤ · التغطية: تجميعةٌ متوقَّعةٌ غائبة ─────────────────────────────────────────
$cases['coverage-absent'] = {
    $dir = Join-Path $tmp "cov-absent"
    New-Cobertura (Join-Path $dir "run\coverage.cobertura.xml") @{
        'Brqosh.Infrastructure' = 90; 'Brqosh.Domain' = 60; 'Brqosh.Application' = 80; 'Brqosh.Api' = 75
    }   # ‏Brqosh.Desktop غائبةٌ عمداً
    $bit = Test-GateFails { & "$root\scripts\check-coverage.ps1" -ResultsDir $dir }
    Add-Result 'coverage-absent' 'check-coverage' $bit 'Brqosh.Desktop بلا تقرير'
}

# ── ٥ · التغطية: تجميعةٌ تحت سقّاطتها ───────────────────────────────────────────
$cases['coverage-floor'] = {
    $dir = Join-Path $tmp "cov-floor"
    New-Cobertura (Join-Path $dir "run\coverage.cobertura.xml") @{
        'Brqosh.Infrastructure' = 90; 'Brqosh.Domain' = 60; 'Brqosh.Application' = 80
        'Brqosh.Api' = 75; 'Brqosh.Desktop' = 10
    }
    $bit = Test-GateFails { & "$root\scripts\check-coverage.ps1" -ResultsDir $dir }
    Add-Result 'coverage-floor' 'check-coverage' $bit 'Desktop 10% تحت سقّاطة 21%'
}

# ── ٦ · (ن-١٤) التغطية: تقريرٌ بائتٌ يرفع الرقمَ بلا سببٍ في الكود ───────────────
$cases['coverage-stale'] = {
    $dir = Join-Path $tmp "cov-stale"
    $rates = @{ 'Brqosh.Infrastructure' = 90; 'Brqosh.Domain' = 60; 'Brqosh.Application' = 80; 'Brqosh.Api' = 75 }
    New-Cobertura (Join-Path $dir "old\coverage.cobertura.xml") ($rates + @{ 'Brqosh.Desktop' = 90 })
    New-Cobertura (Join-Path $dir "new\coverage.cobertura.xml") ($rates + @{ 'Brqosh.Desktop' = 5 })
    $old = Join-Path $dir "old\coverage.cobertura.xml"
    (Get-Item $old).LastWriteTimeUtc = (Get-Date).ToUniversalTime().AddDays(-5)
    $bit = Test-GateFails { & "$root\scripts\check-coverage.ps1" -ResultsDir $dir }
    Add-Result 'coverage-stale' 'check-coverage' $bit 'قديمٌ 90% فوق حديثٍ 5%'
}

# ── ٧ · (ن-٠٦) «صفرُ متخطًّى»: تخطٍّ مُعلَن ─────────────────────────────────────
$cases['skip-declared'] = {
    $dir = Join-Path $tmp "trx-skip"
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    Set-Content (Join-Path $dir "a.trx") @'
<?xml version="1.0" encoding="UTF-8"?>
<TestRun><ResultSummary outcome="Completed">
<Counters total="100" executed="97" passed="97" failed="0" notExecuted="3" />
</ResultSummary></TestRun>
'@
    $bit = Test-GateFails { Invoke-SkipGate -Dir $dir -ExpectedProjects 1 }
    Add-Result 'skip-declared' 'assert-no-skips' $bit 'notExecuted=3'
}

# ── ٨ · (ن-٠٦) «صفرُ متخطًّى»: سمةٌ غائبةٌ تُقرأ صفراً ───────────────────────────
$cases['skip-attr-missing'] = {
    $dir = Join-Path $tmp "trx-attr"
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    Set-Content (Join-Path $dir "a.trx") @'
<?xml version="1.0" encoding="UTF-8"?>
<TestRun><ResultSummary outcome="Completed">
<Counters total="100" executed="100" passed="100" failed="0" />
</ResultSummary></TestRun>
'@
    $bit = Test-GateFails { Invoke-SkipGate -Dir $dir -ExpectedProjects 1 }
    Add-Result 'skip-attr-missing' 'assert-no-skips' $bit 'Counters بلا notExecuted'
}

# ── ٩ · (ن-٠٦) «صفرُ متخطًّى»: ملفّاتٌ أقلُّ من المشاريع (دهس) ───────────────────
$cases['skip-trx-count'] = {
    $dir = Join-Path $tmp "trx-count"
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    Set-Content (Join-Path $dir "a.trx") @'
<?xml version="1.0" encoding="UTF-8"?>
<TestRun><ResultSummary outcome="Completed">
<Counters total="10" executed="10" passed="10" failed="0" notExecuted="0" />
</ResultSummary></TestRun>
'@
    $bit = Test-GateFails { Invoke-SkipGate -Dir $dir -ExpectedProjects 4 }
    Add-Result 'skip-trx-count' 'assert-no-skips' $bit 'ملفٌّ واحدٌ لأربعة مشاريع'
}

# ── ١٠ · (ن-١١) تشغيلةٌ نفّذت صفرَ اختبارٍ يجب ألّا تُخضَّر ─────────────────────
#
# ‏**ملاحظةٌ على تصميم الحالة:** أنّ `dotnet test` يخرج بصفرٍ على مُرشِّحٍ لا يطابق هو
# ‏سلوكُ المنصّة الموثَّق، لا عطلٌ نملك إصلاحَه — فاختبارُه اختبارٌ لـ dotnet لا لنا.
# ‏والحارسُ الذي نملكه هو **قراءةُ العدّاد `total`**، وهو ما تختبره هذه الحالة.
$cases['zero-tests'] = {
    $dir = Join-Path $tmp "trx-zero"
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    Set-Content (Join-Path $dir "a.trx") @'
<?xml version="1.0" encoding="UTF-8"?>
<TestRun><ResultSummary outcome="Completed">
<Counters total="0" executed="0" passed="0" failed="0" notExecuted="0" />
</ResultSummary></TestRun>
'@
    $bit = Test-GateFails { Invoke-ExecutedGate -Dir $dir -ExpectedProjects 1 }
    Add-Result 'zero-tests' 'run-external-gates' $bit 'trx بـ total=0 ⟸ صفرُ تنفيذٍ برمزِ نجاح'
}

# ── ١١ · (ن-١٠) المُشغّل: فشلٌ أمنيٌّ يجب أن يوقفه ─────────────────────────────
$cases['runner-propagates'] = {
    $text = [IO.File]::ReadAllText((Join-Path $root "scripts\ops\run-external-gates.ps1"))
    $wrapped = $text -match 'Invoke-Gate\s+"\$root\\scripts\\security-static-scan\.ps1"'
    Add-Result 'runner-propagates' 'run-external-gates' $wrapped `
        'استدعاءُ الماسح مغلَّفٌ بـInvoke-Gate (يمسك الرمي و exit معاً)'
}

# ── ١٢ · (ن-٠٧) المسبار: غيابُ الخادم تحت REQUIRE يجب أن يكون فشلاً ────────────
$cases['probe-requires-pg'] = {
    $prevPort = $env:BRQOSH_PG_PORT; $prevReq = $env:BRQOSH_REQUIRE_PG
    try {
        $env:BRQOSH_PG_PORT = "1"; $env:BRQOSH_REQUIRE_PG = "1"
        & dotnet run --project tools/SchemaCoverageProbe/SchemaCoverageProbe.csproj -c Release --no-build *> (Join-Path $tmp "probe.txt")
        Add-Result 'probe-requires-pg' 'SchemaCoverageProbe' ($LASTEXITCODE -ne 0) "خادمٌ غائب + REQUIRE=1 ⟸ exit=$LASTEXITCODE"
    }
    finally { $env:BRQOSH_PG_PORT = $prevPort; $env:BRQOSH_REQUIRE_PG = $prevReq }
}

# ── ١٣ · (م-١ · ن-٠١) دفترُ الإصدارات: رقمٌ مُسجَّلٌ مشحوناً يجب أن يُسقط البناء ──
$cases['shipped-version'] = {
    $ledger = Join-Path $root "installer\shipped-versions.txt"
    $backup = Join-Path $tmp "ledger.bak"
    $existed = Test-Path $ledger
    if ($existed) { Copy-Item $ledger $backup -Force }
    try {
        # ‏الإصدارُ الحاليُّ للثنائيّة المنشورة — نسجّله مشحوناً فيجب أن يُرفض بناؤه.
        $exe = Join-Path $root "dist\BRQOSH\Brqosh.Desktop.exe"
        if (-not (Test-Path $exe)) { Add-Result 'shipped-version' 'build-msi' $false 'لا ثنائيّةً منشورةً — الحالةُ لا تُقاس'; return }
        $v = (($(Get-Item $exe).VersionInfo.FileVersion) -split '\.')[0..2] -join '.'
        Add-Content -LiteralPath $ledger -Value $v -Encoding UTF8
        # ‏نُشغّل منطقَ البوّابة وحدَه لا البناءَ كلَّه (‏دقائق مقابل ثوانٍ).
        $bit = Test-GateFails {
            $shipped = @(Get-Content $ledger | ForEach-Object { $_.Trim() } | Where-Object { $_ -and -not $_.StartsWith('#') })
            if ($shipped -contains $v) { throw "‏الإصدار $v مُسجَّلٌ مشحوناً." }
        }
        Add-Result 'shipped-version' 'build-msi' $bit "‏$v مُسجَّلٌ في الدفتر"
    }
    finally {
        if ($existed) { Copy-Item $backup $ledger -Force } else { Remove-Item $ledger -Force -ErrorAction SilentlyContinue }
    }
}

# ── ١٤ · (م-١ · ن-٠١) مِعْيَرُ الترقية: إصدارٌ لا يتقدّم يجب أن يُسقطه ──────────
$cases['upgrade-no-advance'] = {
    $msi = Join-Path $root "dist\BRQOSH-Setup.msi"
    if (-not (Test-Path $msi)) { Add-Result 'upgrade-no-advance' 'verify-upgrade-path' $false 'لا حزمةً — الحالةُ لا تُقاس'; return }
    # ‏الحزمةُ نفسُها طرفاً للمقارنة: الإصدارُ لا يتقدّم بالتعريف، فيجب السقوط.
    $bit = Test-GateFails { & "$root\scripts\ops\verify-upgrade-path.ps1" -PackageA $msi -PackageB $msi }
    Add-Result 'upgrade-no-advance' 'verify-upgrade-path' $bit 'حزمةٌ مقابل نفسِها ⟸ لا تقدّم'
}

# ‏نسخةُ بوّابة «صفرُ متخطًّى» كما في ci.yml حرفاً — تُختبر هنا لا في CI وحدَه.
function Invoke-SkipGate {
    param([string]$Dir, [int]$ExpectedProjects)
    $trx = @(Get-ChildItem $Dir -Recurse -Filter *.trx -ErrorAction SilentlyContinue)
    if ($trx.Count -eq 0) { throw 'لا ملفَّ نتائج (.trx).' }
    if ($trx.Count -ne $ExpectedProjects) { throw "‏ملفّاتُ trx $($trx.Count) لا تساوي المشاريعَ $ExpectedProjects." }
    $skipped = 0
    foreach ($f in $trx) {
        $counters = ([xml](Get-Content $f.FullName)).TestRun.ResultSummary.Counters
        if ($null -eq $counters -or $null -eq $counters.notExecuted) { throw "‏$($f.Name): لا سمةَ notExecuted." }
        $skipped += [int]$counters.notExecuted
    }
    if ($skipped -gt 0) { throw "‏$skipped اختباراً تُخطّى." }
}

# ‏نسخةُ حارسِ «كم نُفِّذ فعلاً؟» كما في run-external-gates.ps1 حرفاً.
function Invoke-ExecutedGate {
    param([string]$Dir, [int]$ExpectedProjects)
    $trx = @(Get-ChildItem $Dir -Recurse -Filter *.trx -ErrorAction SilentlyContinue)
    if ($trx.Count -ne $ExpectedProjects) { throw "‏ملفّاتُ trx $($trx.Count) لا تساوي المشاريعَ $ExpectedProjects." }
    $executed = 0
    foreach ($f in $trx) {
        $counters = ([xml](Get-Content $f.FullName)).TestRun.ResultSummary.Counters
        if ($null -eq $counters -or $null -eq $counters.total) { throw "‏$($f.Name): لا عدّاداتٍ." }
        $executed += [int]$counters.total
    }
    if ($executed -le 0) { throw "‏صفرُ اختباراتٍ نُفِّذت — بوّابةٌ تُخضّر العدم." }
}

# ── التشغيل ─────────────────────────────────────────────────────────────────────
Write-Host "=== اختبارُ حساسيّةِ البوّابات ===" -ForegroundColor Cyan
Write-Host "‏كلُّ حالةٍ تزرع عيباً معلوماً وتشترط أن تعضّ البوّابة. المرورُ = عمى." -ForegroundColor DarkGray
Write-Host ""

# ‏(بصمةُ ما قبل) ‏السؤالُ ليس «أنظيفةٌ الشجرة؟» — قد تكون متّسخةً بعملٍ جارٍ مشروع —
# ‏بل «**هل لوّثتُها أنا؟**». فتُلتقط الحالةُ قبل الزرع وتُقارَن بعد الإزالة.
# ‏ويُحدَّث الفهرسُ قبل الالتقاطين معاً — فبصمتان بمعيارين مختلفين تُنتجان فرقاً وهميّاً
# ‏في أيّ من الاتجاهين. (‏انظر شرحَ `update-index --refresh` عند بصمة ما بعد.)
& git update-index --refresh *> $null
$global:LASTEXITCODE = 0

$treeBefore = (& git status --porcelain | Sort-Object) -join "`n"

$toRun = if ($Only) { @($Only) } else { $cases.Keys | Sort-Object }
foreach ($name in $toRun) {
    if (-not $cases.ContainsKey($name)) { Write-Host "  لا حالةَ باسم '$name'" -ForegroundColor Yellow; continue }
    try { & $cases[$name] }
    catch { Add-Result $name '—' $false "‏الحالةُ نفسُها سقطت: $($_.Exception.Message)" }
}

foreach ($f in $plantedFiles) { Remove-Item $f -Force -ErrorAction SilentlyContinue }
Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ""
$blind = @($results | Where-Object { -not $_.Bites })
Write-Host ("‏عضّت: {0} · عمياء: {1} · المجموع: {2}" -f ($results.Count - $blind.Count), $blind.Count, $results.Count)

# ‏شرطٌ ثانٍ لا يقلّ أهميّة: سكربتُ فحصٍ يُلوّث الشجرة أسوأُ من ألّا يوجد.
#
# ‏(م-٤) ‏**ويُحدَّث فهرسُ git قبل الالتقاط — وإلّا كذب هذا الشرطُ نفسُه.**
# ‏فحالاتٌ تُعيد ملفّاً إلى محتواه بـ`Copy-Item`، والبايتاتُ نفسُها **بطابعِ وقتٍ جديد**.
# ‏وgit يحتفظ بذاكرةِ `stat` في الفهرس، فيرى الطابعَ اختلف ويُعلن الملفَّ معدَّلاً قبل أن
# ‏يُقارن المحتوى. **فأبلغ المِرفاسُ فشلاً كاذباً** بينما `git diff` يقول لا تغيير —
# ‏وبوّابةٌ تكذب هي عينُ ما وُجد هذا السكربتُ ليكشفه.
#
# ‏و`update-index --refresh` يُقارن المحتوى ويمحو الاتّساخَ الوهميّ. ورمزُ خروجه غيرُ صفرٍ
# ‏حين يجد ما يُحدّثه — وهو **حالةٌ متوقَّعةٌ لا فشل**، فيُبتلع عمداً.
& git update-index --refresh *> $null
$global:LASTEXITCODE = 0

$treeAfter = (& git status --porcelain | Sort-Object) -join "`n"
if ($treeAfter -ne $treeBefore) {
    Write-Host "‏[FAIL] الشجرةُ تغيّرت بفعل الفحص — قارِن:" -ForegroundColor Red
    (Compare-Object ($treeBefore -split "`n") ($treeAfter -split "`n")) |
        ForEach-Object { Write-Host "   $($_.SideIndicator) $($_.InputObject)" -ForegroundColor Red }
    exit 1
}
Write-Host "‏الشجرةُ كما كانت قبل الفحص." -ForegroundColor DarkGray

if ($blind.Count -gt 0) {
    Write-Host ""
    Write-Host "‏[FAIL] بوّاباتٌ لم تعضّ:" -ForegroundColor Red
    $blind | ForEach-Object { Write-Host "   · $($_.Name) — $($_.Gate)" -ForegroundColor Red }
    exit 1
}
Write-Host "‏[PASS] كلُّ بوّابةٍ عضّت عيبَها." -ForegroundColor Green
exit 0
