# Static security scan for BRQOSH — fails CI on critical findings in src/.
#
# (CEEF-229 . م2-6) WHAT THIS SCRIPT CAN AND CANNOT PROVE — read before adding a check here.
#
# This is a TEXT scanner. It proves a string is present or absent in a file. That is the right
# tool for one job only: finding secrets, because a secret IS text. It is the WRONG tool for
# proving that a guard runs, and it used to claim exactly that:
#
#   * It searched each *PostingService.cs for the substring "EnsureCanPost" and reported PASS.
#     A call commented out, unreachable, or placed AFTER the database write all pass that check.
#     Measured: commenting out the guard in SalesInvoicePostingService.PostAsync left this scan
#     reporting "Posting EnsureCanPost* (soft) | PASS" while an unauthorised caller went through.
#
#   * Its strictest check searches for the literal "admin123". A back door under any other name
#     passes untouched.
#
# Guard ENFORCEMENT is now measured by execution, not by text:
#   tests/Brqosh.Application.Tests/Security/EveryPostingServiceRefusesAViewerTests.cs
#       - reflects over every *PostingService, invokes every public operation as a Viewer,
#         and asserts the refusal is an AUTHORISATION refusal rather than an incidental crash.
#   tests/Brqosh.Api.Tests/{ApiKeyGuardTests,ApiRoleEscalationTests,ProductionApiSecurityStartupTests}.cs
#       - send real requests with and without headers.
#
# So: add SECRET patterns here. Add GUARD checks to those test files instead.
param(
    [string]$SourceRoot = "src",
    [string]$ApiProgram = "src/Brqosh.Api/Program.cs"
)

$ErrorActionPreference = "Stop"
$failures = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()
$findings = [System.Collections.Generic.List[string]]::new()

function Add-Failure([string]$Message) { $failures.Add($Message) }
function Add-Warning([string]$Message) { $warnings.Add($Message) }

Write-Host "== BRQOSH security static scan =="

# (CEEF-236 - m-25) The gate looked at src/ only.
#
# Measured: 59bdb5d added a PostgreSQL superuser password and a VM account password to
# scripts/ops/, and this scan still reported PASS - which the same campaign then cited as
# security evidence. Deploy, backup and installer scripts are exactly where real credentials
# accumulate, and none of them were ever read.
#
# Two widenings: the roots below, and the pattern list in section 2 - a variable named
# $pgPass was invisible to a pattern anchored on the literal token "password".
$selfPath = (Resolve-Path -LiteralPath $PSCommandPath).Path

function Get-SourceFiles {
    # ‏(م-٤ · ن-١٣) ‏**جذرُ المستودع نفسُه لم يكن مقروءاً، و`*.config` لم يكن ممسوحاً.**
    #
    # ‏فملفٌّ في الجذر — و`NuGet.config` أوّلُه — **يولد خارج بصر هذا الماسح**. و
    # ‏`<packageSourceCredentials>` تُكتب فيه نصّاً صريحاً، **وهي سرٌّ بكلّ معنى الكلمة**.
    # ‏فالماسحُ كان يحرس كلَّ مكانٍ إلّا الذي يُرجَّح أن يُكتب فيه سرُّ مستودعِ حزم.
    # ‏ويُمسح الجذرُ بلا `-Recurse` فلا يُكرَّر ما تحته من جذورٍ مذكورةٍ أصلاً.
    $roots = @($SourceRoot, "scripts", "tools", "installer", ".github") | Where-Object { Test-Path $_ }
    $includes = @("*.cs","*.xaml","*.json","*.csproj","*.ps1","*.psm1","*.sh","*.yml","*.yaml","*.bat","*.wxs","*.config")
    $rootFiles = Get-ChildItem -Path (Join-Path "." "*") -File -Include $includes
    @($rootFiles) + @(Get-ChildItem -Path $roots -Recurse -File -Include $includes) |
        # ‏‏[\\/] لا [\/]: مساراتُ ويندوز بشرطةٍ خلفيّة، والصنفُ ذو الشرطة الأمامية
        # ‏وحدَها **لا يُطابق شيئاً هنا** — فتوسيعي حوّل استثناءً عاملاً إلى استثناءٍ ميّت.
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj|\.venv|node_modules|TestResults)[\\/]' } |
        # ‏الماسحُ لا يمسح نفسَه: أنماطُه تحوي بالتعريف ما يبحث عنه (‏admin123‎).
        # ‏**بالمسار الكامل لا بالاسم المجرّد**: استثناءُ الاسم يُخفي أيّ ملفٍّ
        # ‏يُسمّى كذلك في أيّ جذرٍ مفحوص — فيكفي وضعُ الأسرار في
        # ‏installer/security-static-scan.ps1 لتمرّ بلا قراءة.
        Where-Object { $_.FullName -ne $selfPath }
}

# 1) Hard fail: default admin credentials in application source
$sourceFiles = @(Get-SourceFiles)
$adminHits = @(
    $sourceFiles | Select-String -Pattern "admin123" |
        Where-Object { $_.Path -notmatch "PasswordPolicy\.cs$" }
)
if ($adminHits.Count -gt 0) {
    foreach ($hit in $adminHits) {
        Add-Failure "admin123 in $($hit.Path):$($hit.LineNumber)"
    }
}

$adminPairHits = @(
    $sourceFiles | Select-String -Pattern "admin\s*/\s*admin123"
)
if ($adminPairHits.Count -gt 0) {
    foreach ($hit in $adminPairHits) {
        Add-Failure "admin/admin123 in $($hit.Path):$($hit.LineNumber)"
    }
}

# ‏(CEEF-237 · م-٣٧ب) **‏بايتاتُ تحكّمٍ في المصدر — عطلٌ عضّ ثلاثَ مرّات.**
#
# ‏كتابةُ نصٍّ فيه `\b` عبر أداةٍ وسيطةٍ تُحوّله بايتَ 0x08 حقيقياً. وقع في كتلة تعليقٍ
# ‏في هذا الملفّ نفسِه (م-٣٧)، ثمّ في `.github/workflows/ci.yml` حيث **أبطل YAML كلَّه**.
# ‏والبايتُ لا يُرى في محرّر، ويُخفي ما قبله في الطرفيّة، ولا يجده بحثٌ عن `\b`.
# ‏فليُمنع شحنُه: أيُّ محرفِ تحكّمٍ عدا التبويب والسطر يُسقط البوّابة.
$controlCharHits = @()
foreach ($file in $sourceFiles) {
    $bytes = [IO.File]::ReadAllBytes($file.FullName)
    for ($i = 0; $i -lt $bytes.Length; $i++) {
        $b = $bytes[$i]
        # ‏قائمةٌ ضيّقةٌ عمداً: 0x1F مثلاً **يُستعمل قصداً** فاصلَ حقولٍ في تقنين
        # ‏بصمة سجلّ التدقيق، ومنعُه يكسر شيئاً صحيحاً. والمقصودُ هنا ما لا معنى له
        # ‏في مصدرٍ أبداً: الفراغُ الخلفيّ (0x08) الذي وقع ثلاثَ مرّات، والعادم،
        # ‏وفاصلُ الصفحة، ونهايةُ الملفّ في DOS.
        if ($b -eq 0x00 -or $b -eq 0x08 -or $b -eq 0x0C -or $b -eq 0x1A) {
            $rel = $file.FullName.Replace((Get-Location).Path + [IO.Path]::DirectorySeparatorChar, "")
            $controlCharHits += ("{0} عند البايت {1}: 0x{2:X2}" -f $rel, $i, $b)
            break
        }
    }
}
foreach ($hit in $controlCharHits) { Add-Failure "control character in source: $hit" }

# 2) List likely hardcoded secrets (warning / informational)
# (CEEF-236 - m-25) Broadened: a credential is rarely named exactly "password".
# (CEEF-236 - m-25/m-30) Broadened, then corrected: the leading [A-Za-z_]* constrained nothing
# (Select-String is unanchored, so it backtracks to zero width) and matched "pass" as a substring -
# `compass = "CognitiveQa/COMPASS.yaml"` was reported as a possible secret. \b anchors the token.
# (CEEF-236 - m-25/m-30) Two shapes, because one misses half the ground:
#   1) a SHELL/PS VARIABLE whose name contains pass/pwd/secret/token anywhere - $pgPass,
#      $GuestPassword. A \b anchor alone misses these (no boundary inside "pgPass").
#   2) a PLAIN assignment to a credential-named key - password=, secret=, api_key=.
#      Anchored with \b so `compass = "CognitiveQa/COMPASS.yaml"` is not a secret.
# The first shape is what 59bdb5d actually committed; the second is the classic form.
#
# DELIBERATE TRADE, measured: shape 1 also hits identifiers that merely contain the token,
# e.g. $bypass = "always-on". That noise is accepted because the alternative - anchoring with
# \b - makes the pattern miss $pgPass, which is the exact literal that walked past this gate
# and got committed. In an informational bucket a false positive costs a glance; a false
# negative costs a published credential.
# ‏(م-٣٤) وكان هنا سطرٌ يقول «Verified cases: $pgPass HIT …» — **ولم يكن صحيحاً**:
# ‏الأنماطُ كانت ميّتةً يومَ كُتِب ذاك السطر. فلا تُكتب هنا حالاتٌ «متحقَّقٌ منها» باليد:
# ‏$patternSelfTest أدناه يُجريها في كلّ تشغيل، ويُسقِط البوّابة إن اختلفت نتيجة.
$passwordPatterns = @(
    '(?i)\x24(env:)?[A-Za-z0-9_]*(pass(word|wd)?|pwd|secret|token)[A-Za-z0-9_]*\s*=\s*[''"][^''"]{3,}[''"]',
    '(?i)\b(password|passwd|pwd|secret|token|api[_-]?key)\s*[:=]\s*[''"][^''"]{3,}[''"]',
    '(?i)-{1,2}(super)?(password|pwd|secret|token)\s+[''"][^''"]{3,}[''"]',
    # ‏سلسلةُ اتّصالٍ لا إسنادٌ في C#‏: يلزم مقطعُ Host/Server قبلها على السطر نفسه،
    # ‏وإلا طابق `passwordBox.Password = plainBox.Text;` وهو ليس سرّاً.
    '(?i)\b(host|server|data source)\s*=\s*[^;]{1,64};[^\r\n]{0,200}?\bpassword\s*=\s*[^;\s''"{}$%]{3,}',
    '(?i)ConvertTo-SecureString\s+[''"][^''"]{3,}[''"]',
    '(?i)Id="[^"]*(password|secret|key)[^"]*"\s+Value="[^"]{3,}"',
    '(?i)^\s*[A-Za-z0-9_]*(password|secret|token|api[_-]?key)\s*:\s*[^\s#]{3,}$'
)

# ‏(CEEF-236 · م-٣٤) **اختبارٌ ذاتيٌّ للأنماط — ونمطٌ ميّت يُسقِط البوّابة لا يمرّ منها.**
#
# ‏الجولةُ السابقة كتبت الأنماط في سلاسل PowerShell مزدوجةِ الاقتباس بشرطتين
# ‏خلفيّتين — والشرطةُ الخلفيّة ليست مهرباً في PowerShell، فصار النمطُ يطلب شرطةً حرفيّةً
# ‏في المصدر. فماتت الأنماطُ الثلاثة جميعاً، وظلّ الماسحُ يقول PASS، واستُشهِد به
# ‏في جدولِ جاهزيّةِ الإطلاق. والنتيجةُ الخضراء تبدو واحدةً سواءٌ فُحِص الشيءُ فلم يُوجد،
# ‏أم لم يُفحص أصلاً.
$patternSelfTest = @(
    @{ Line = '$pgPass = "Hunter22!"'; Expect = $true }
    @{ Line = 'password = "Hunter22!"'; Expect = $true }
    @{ Line = 'api_key="sk-live-abcdef"'; Expect = $true }
    @{ Line = '$env:PGPASSWORD = "Hunter22"'; Expect = $true }
    @{ Line = 'Host=db;Password=Hunter22!;Database=x'; Expect = $true }
    @{ Line = '  default_password: "Hunter22!"'; Expect = $true }
    @{ Line = 'compass = "CognitiveQa/COMPASS.yaml"'; Expect = $false }
    @{ Line = '$tokenizer = "ws"'; Expect = $false }
)
foreach ($case in $patternSelfTest) {
    $matched = $false
    foreach ($pattern in $passwordPatterns) { if ($case.Line -match $pattern) { $matched = $true } }
    if ($matched -ne $case.Expect) {
        Add-Failure ("pattern self-test: السطر [$($case.Line)] منتظَر $($case.Expect) والناتج $matched")
    }
}

# ‏قائمةُ السماح: ما لا يطابق مدخلاً هنا يُسقِط البوّابة، لا يُكتب للعلم وحسب.
$secretAllowlist = @(
    @{ Pattern = 'Database=brqosh_design;Username=postgres;Password=design'
       Reason  = 'مصنعُ EF وقتَ التصميم: قاعدةٌ محلّيّةٌ على المنفذ 5433 تُولّد منها الهجراتُ على جهازِ المطوِّر؛ لا تُشحن ولا تُستعمل وقتَ التشغيل.' }
    @{ Pattern = 'PGPASSWORD = ''root'''
       Reason  = 'كلمةُ postgres الموثّقة على صورة windows-latest من GitHub؛ تُستعمل مرّةً واحدةً لتبديلها بمولَّدةٍ على عدّاءٍ يُتلف بعد التشغيلة.' }
    # ‏(م-٦ · تبعيّةُ ن-٢٢) ‏**وحُذف من هنا مدخلُ `ProdBoot!2026`** بعد أن نُزعت الكلمتان
    # ‏من `live-production-first-boot.ps1` و`rk-prodboot-guest.ps1` إلى متغيّر بيئةٍ برميٍ
    # ‏صريح (والثانيةُ صارت تُولَّد، فلا تُخزَّن أصلاً). **ومدخلُ سماحٍ لا يُطابق شيئاً
    # ‏رَوثٌ**: يُغري من يأتي بإعادة الكلمة ظانّاً أنّ السماحَ قائم — **والفحصُ أدناه
    # ‏يُسقط البوّابةَ على كلّ مدخلٍ ميّت**، فلا يبقى واحدٌ صامتاً.
    #
    # ‏(م-٥ · ن-٢٢) ‏**وتعليلُ هذا السماح كان كاذباً، والماسحُ صدّقه فبقي أخضر.**
    #
    # ‏كان يقول «بديلٌ احتياطيٌّ لـBRQOSH_TEST_PASSWORD؛ **لا تُستعمل في إنتاج**» —
    # ‏و`FullScenarioAudit` التي تحمله **هي الخطوةُ ٦ في كتيّب التنصيب**، ويستدعيها
    # ‏`docs/ops/O-01_ZATCA_PRODUCTION_RUNBOOK.md:14`، ويُشغّلها `run-external-gates.ps1` يوميّاً
    # ‏على ما يُسمّيه `database.json`. **فكانت تُستعمل في إنتاجٍ بالضبط.**
    #
    # ‏وأثرُها أسوأُ من «كلمةِ حساب»: الأداةُ **تُنشئ** حساباً بدور «مدير» بهذه الكلمة،
    # ‏**والإنشاءُ صامتٌ بخلاف إعادة التعيين** — لا أحدَ يُقفَل خارج حسابه فلا أحدَ يسأل.
    #
    # ‏فنُزعت الكلمةُ من كلّ مسارٍ موصولٍ ببوّابةٍ أو كتيّب — **ولا كلمةَ احتياطيّةَ بعدها بل رفض**.
    #
    # ‏(م-٦ · تبعيّةُ ن-٢٢) ‏**ثمّ حُذف مدخلُ `UiAudit9!` نفسُه — وكان ميّتاً منذ نُزعت الكلمة.**
    #
    # ‏كشفه فحصُ «المدخل الميّت» أدناه في أوّل تشغيلٍ له: **الماسحُ نفسُه عدّ له صفرَ سطورٍ
    # ‏سُمح بها** — وهو القياسُ الحاكم، وشاهدُ ضبطه في التشغيلة نفسِها: المدخلان الآخران
    # ‏عُدّا مستعمَلَين (`ci.yml` و`BrqoshDesignTimeDbContextFactory.cs`). **والتفسيرُ**: كلُّ
    # ‏ملفٍّ يحمل الكلمةَ اليومَ `.py` (قواميسُ التخمين) أو `.md` (تقارير) أو `.txt` (سجلّاتُ
    # ‏تدقيق)، **وكلُّها خارج امتدادات `Get-SourceFiles`** — والرابعُ هذا الملفُّ وهو لا يمسح
    # ‏نفسَه. فالمدخلُ لم يُسكت شيئاً قطّ منذ ن-٢٢، **وحذفُه لا يُغيّر حكماً واحداً** — لكنّ
    # ‏بقاءَه كان يَعِدُ بسماحٍ قائمٍ لمن يُعيد الكلمةَ إلى ملفٍّ ممسوح.
)

# ‏(م-٦) ‏**يُعدّ لكلّ مدخلٍ بموضعه لا بنصّه.** كان العدُّ في جدولٍ مفتاحُه نصُّ النمط، وجداولُ
# ‏PowerShell لا تُفرّق حالةَ الحروف — فمدخلٌ مكرّرٌ حرفيّاً أو بحالةِ حروفٍ أخرى **يُشارك سابقَه عدَّه
# ‏فلا يُعلَن ميّتاً أبداً**، خلافاً للوعد أعلاه. كشفته عدسةُ دلالة البوّابة قبل الإيداع. والسطرُ يُنسب
# ‏إلى **أوّل** مدخلٍ يُطابقه، فمدخلٌ بعده لا يُسمح به شيءٌ وحدَه — **والمكرّرُ يُعلَن كما يُعلَن الميّت**.
$allowlistUse = New-Object 'int[]' $secretAllowlist.Count

foreach ($pattern in $passwordPatterns) {
    $hits = @($sourceFiles | Select-String -Pattern $pattern)
    foreach ($hit in $hits) {
        $rel = $hit.Path.Replace((Get-Location).Path + [IO.Path]::DirectorySeparatorChar, "")
        $allowedAt = @(for ($i = 0; $i -lt $secretAllowlist.Count; $i++) {
            if ($hit.Line -match [regex]::Escape($secretAllowlist[$i].Pattern)) { $i }
        })
        if ($allowedAt.Count -gt 0) {
            $allowlistUse[$allowedAt[0]]++
            $findings.Add("possible secret (مسموح: $($secretAllowlist[$allowedAt[0]].Reason)): $rel`:$($hit.LineNumber)")
        } else {
            Add-Failure "possible secret ($pattern): $rel`:$($hit.LineNumber)"
        }
    }
}

# ‏(م-٦ · تبعيّةُ ن-٢٢) ‏**مدخلُ سماحٍ لا يُطابق شيئاً يُسقط البوّابة.**
#
# ‏ن-٢٢ أدانت مدخلاً **بتعليلٍ كاذب**. وهذا وجهُه الآخر: مدخلٌ **بلا موضوع**. فالسرُّ
# ‏يُنزع من الشيفرة ويبقى سماحُه في القائمة، **فيقرؤه من يأتي إذناً قائماً ويُعيد الكلمة**
# ‏— والماسحُ يمرّ أخضر لأنّ السماحَ ما زال هناك.
#
# ‏**والحذفُ لا يُترك للانتباه**: يُحرَس. وهو النمطُ نفسُه الذي فرضه هذا المستودعُ على
# ‏أنماط الأسرار — **لكلّ بوّابةٍ اختبارٌ ذاتيٌّ يُسقطها إن عميت**.
$deadAllowlist = @(for ($i = 0; $i -lt $secretAllowlist.Count; $i++) {
    if ($allowlistUse[$i] -eq 0) { $secretAllowlist[$i].Pattern }
})
if ($deadAllowlist.Count -gt 0) {
    Add-Failure ("dead allowlist entry (لم يُسمح به سطرٌ واحد: سرُّه نُزع، أو هو تكرارٌ لمدخلٍ قبله — احذف المدخل): " + ($deadAllowlist -join " | "))
}

# 3) Posting guards: NOT checked here — see the header. A text search cannot tell a live guard
#    from a commented-out one. EveryPostingServiceRefusesAViewerTests invokes every operation as a
#    Viewer and asserts an authorisation refusal; that test is the gate. What remains useful here is
#    the far weaker signal that a posting service does not even MENTION a guard, which is worth a
#    warning line because it is cheap — but it is reported as what it is, not as a PASS.
$postingServices = @(
    Get-ChildItem -Path (Join-Path $SourceRoot "Brqosh.Infrastructure") -Recurse -Filter "*PostingService.cs" -File
)
$noGuardMention = @()
foreach ($file in $postingServices) {
    $text = Get-Content -LiteralPath $file.FullName -Raw
    if ($text -notmatch "EnsureCanPost") {
        $noGuardMention += $file.Name
    }
}
if ($noGuardMention.Count -gt 0) {
    $guardNote = "Posting services that never mention a guard (text only; enforcement is proven by EveryPostingServiceRefusesAViewerTests): "
    Add-Warning ($guardNote + ($noGuardMention -join ", "))
}

# 4) API key middleware must exist when BRQOSH_API_KEY is configured
if (-not (Test-Path -LiteralPath $ApiProgram)) {
    Add-Failure "API Program.cs not found at $ApiProgram"
}
else {
    $program = Get-Content -LiteralPath $ApiProgram -Raw
    if ($program -notmatch "BRQOSH_API_KEY|ApiSecuritySettings\.ApiKeyEnv") {
        Add-Failure "Program.cs missing BRQOSH_API_KEY configuration"
    }
    if ($program -notmatch "X-Api-Key") {
        Add-Failure "Program.cs missing X-Api-Key header check"
    }
    if ($program -notmatch "EnsureProductionApiKey") {
        Add-Failure "Program.cs missing Production API key enforcement (EnsureProductionApiKey)"
    }
    if ($program -notmatch "SecretComparer") {
        Add-Failure "Program.cs missing timing-safe SecretComparer for API keys"
    }
    if ($program -notmatch "X-Provisioning-Key") {
        Add-Failure "Program.cs missing X-Provisioning-Key guard on tenant provision"
    }
}

$tenantGuard = Join-Path $SourceRoot "Brqosh.Infrastructure/Companies/TenantGuard.cs"
if (Test-Path -LiteralPath $tenantGuard) {
    $tg = Get-Content -LiteralPath $tenantGuard -Raw
    if ($tg -notmatch "EnforceTenantIsolation") {
        Add-Failure "TenantGuard missing SaaS default-deny (EnforceTenantIsolation)"
    }
}

$webhookValidator = Join-Path $SourceRoot "Brqosh.Infrastructure/Webhooks/WebhookUrlValidator.cs"
if (-not (Test-Path -LiteralPath $webhookValidator)) {
    Add-Failure "Missing WebhookUrlValidator.cs (SSRF guard)"
}

$apiKeyPass = -not ($failures | Where-Object { $_ -match "Program\.cs" })
$summary = @"
## Security static scan

This scan proves the presence or absence of TEXT. Guard ENFORCEMENT is proven by execution in
EveryPostingServiceRefusesAViewerTests and the Brqosh.Api.Tests security suite - see the header.

| Check | What it proves | Result |
|-------|----------------|--------|
| admin123 in src/ | that one literal is absent | $(if ($adminHits.Count -eq 0 -and $adminPairHits.Count -eq 0) { 'PASS' } else { 'FAIL' }) |
| Program.cs security wiring | the strings are present, not that middleware runs | $(if ($apiKeyPass) { 'present' } else { 'MISSING' }) |
| Posting guard mentioned | text only, not that the guard executes | $(if ($noGuardMention.Count -eq 0) { 'all mention one' } else { "$($noGuardMention.Count) service(s) flagged" }) |
| Possible hardcoded secrets | pattern matches to review | $($findings.Count) match(es) listed below |

### Failures ($($failures.Count))
$(if ($failures.Count -eq 0) { '- none' } else { ($failures | ForEach-Object { "- $_" }) -join "`n" })

### Warnings ($($warnings.Count))
$(if ($warnings.Count -eq 0) { '- none' } else { ($warnings | ForEach-Object { "- $_" }) -join "`n" })

### Possible secrets (informational)
$(if ($findings.Count -eq 0) { '- none' } else { ($findings | Select-Object -First 20 | ForEach-Object { "- $_" }) -join "`n" })
"@

Write-Host $summary

if ($env:GITHUB_STEP_SUMMARY) {
    Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value $summary
}

if ($failures.Count -gt 0) {
    Write-Host "SECURITY SCAN FAILED with $($failures.Count) critical finding(s)."
    exit 1
}

Write-Host "SECURITY SCAN PASSED ($($warnings.Count) warning(s), $($findings.Count) informational match(es))."
exit 0
