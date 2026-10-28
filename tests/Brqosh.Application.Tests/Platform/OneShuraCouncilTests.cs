using System.Text.RegularExpressions;

namespace Brqosh.Application.Tests.Platform;

/// <summary>
/// (م-٥ · ن-٢٠) <b>مجلسُ الشورى نسخةٌ واحدة — والمُشغَّلةُ هي التي تُسمّيها الوثائق.</b>
///
/// <para><b>ما كان قبلها — والتسميةُ مقلوبةٌ على الواقع:</b> نسختان،
/// <c>ShuraCouncil/</c> (٢٣ ملفّاً · محرّك v5 · مُدقّقٌ خصميّ · ٢٨ تخصّصاً · بوّابةُ Supreme)
/// و<c>tools/ShuraCouncil/</c> (٨ ملفّات · v2). <b>والوثائقُ كلُّها تُشير إلى الجذر</b>
/// (٣٧ إشارةً من ٣٨ في <c>.cursor/**</c>) — <b>والمُشغَّلُ كان tools</b>.</para>
///
/// <para><b>والمفتاحُ سطرٌ واحدٌ داخل مُشغِّل الجذر نفسِه:</b>
/// <c>ShuraCouncil/run-shura-council.ps1:13</c> كان
/// <c>if (Test-Path "tools/ShuraCouncil/ShuraCouncil.csproj")</c> — <b>فيُفضّل tools متى
/// وُجدت</b>، وهي موجودة. فالجذرُ لم يُبنَ قطّ على أيّ مسارٍ حيّ: تقاريرُ
/// <c>dist/evidence/shura-council/</c> تحمل ختمَ <c>tools/ShuraCouncil</c> في ٢٢ تشغيلةً
/// من ٣٠، و<c>bin/</c> و<c>obj/</c> لم يُنشآ قطّ تحت الجذر.</para>
///
/// <para><b>ولماذا لم يُكتشف:</b> خطوةُ CI «بناءُ أدوات التشغيل»
/// (<c>.github/workflows/ci.yml:112</c>) تسرد <c>tools</c> بنمطٍ شامل — <b>فكانت تبني
/// الظلَّ كلَّ مرّة ولا تبني الأصلَ أبداً</b>. فالازدواجُ لم يكن حشواً زائداً: كان
/// <b>الشيءَ الوحيدَ الذي يُبقي محرّكاً في CI، وهو يحجب المحرّكَ الحقيقيّ</b>.</para>
///
/// <para><b>وحدُّ هذه البوّابة مكتوبٌ فيها:</b> تحرس <b>وحدانيّةَ النسخة ووجهةَ المُشغِّل
/// وقدرةَ القارئ على ما يكتبه الكاتب</b>. ولا تحرس صحّةَ أحكام المجلس نفسِه.</para>
/// </summary>
public class OneShuraCouncilTests
{
    private const string Canonical = "ShuraCouncil/ShuraCouncil.csproj";
    private const string Shadow = "tools/ShuraCouncil";

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("لم يُعثر على جذر المستودع (Brqosh.sln).");
    }

    private static string Read(string relative)
    {
        var path = Path.Combine(RepoRoot(), relative.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"‏ملفٌّ مفقود: {relative}");
        return File.ReadAllText(path);
    }

    // ───────────────────────── وحدانيّةُ النسخة ─────────────────────────

    [Fact]
    public void OnlyOneShuraCouncilProjectExists()
    {
        var root = RepoRoot();
        var projects = Directory
            .EnumerateFiles(root, "ShuraCouncil.csproj", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Select(p => Path.GetRelativePath(root, p).Replace('\\', '/'))
            // **النسخُ المؤقّتةُ ليست منافسين**: `.claude/worktrees/**` مسارُ تنفيذٍ يُنشئ نسخةً
            // كاملةً من نفس المستودع لعملِ وكيلٍ متوازٍ — والمقصودُ بوحدانيّة المحرّك ألّا يكون
            // في **المستودع** محرّكان حيّان، لا أن يمنع العملَ المتوازي. (٢٠٢٦-١٠-٢٧: بوّابةٌ
            // سقطت على مخلفاتِ ورشتين مهجورتين، لا على عيبٍ في المنتج.)
            .Where(p => !IsTransientCheckout(p))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        Assert.True(projects.Count == 1,
            "‏نسختان من مجلس الشورى تتنافسان، والمُشغَّلةُ ليست التي تُسمّيها الوثائق:\n  "
            + string.Join("\n  ", projects));

        Assert.Equal(Canonical, projects[0]);
    }

    /// <summary>مسارٌ نسبيٌّ بعُدّة '/'. تُستثنى منه نسخُ الورشات المؤقّتة ومرآة Git.</summary>
    private static bool IsTransientCheckout(string relativePath) =>
        relativePath.StartsWith(".claude/worktrees/", StringComparison.OrdinalIgnoreCase)
        || relativePath.StartsWith(".git/", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// <b>الاختبارُ الذاتيُّ للاستثناء</b>: نسخةٌ داخل ورشةٍ مؤقّتةٍ لا تُحسب، ونسخةٌ ثانيةٌ في
    /// المستودع نفسِه تُحسب — وإلا لكان الاستثناءُ إطفاءً صامتاً للبوّابة.
    /// </summary>
    [Theory]
    [InlineData(".claude/worktrees/agent-a/ShuraCouncil/ShuraCouncil.csproj", true)]
    [InlineData(".git/worktrees/x/ShuraCouncil/ShuraCouncil.csproj", true)]
    [InlineData("ShuraCouncil/ShuraCouncil.csproj", false)]
    [InlineData("tools/ShuraCouncil/ShuraCouncil.csproj", false)]
    public void TransientCheckoutsAreNotSecondCopies(string relativePath, bool transient) =>
        Assert.Equal(transient, IsTransientCheckout(relativePath));

    /// <summary>
    /// <b>المُشغِّلُ يُسمّي مشروعاً واحداً ولا يختار.</b> اختيارٌ داخل المُشغِّل يعني أنّ
    /// وجودَ مجلّدٍ على القرص يُغيّر أيَّ محرّكٍ يعمل — <b>وهو ما وقع فعلاً</b>.
    /// </summary>
    [Fact]
    public void TheLauncherNamesTheCanonicalProjectAndDoesNotChoose()
    {
        var launcher = Read("ShuraCouncil/run-shura-council.ps1");
        Assert.Contains(Canonical, launcher, StringComparison.Ordinal);

        // ‏التعليقاتُ تُمحى: المُشغِّلُ **يشرح** لماذا زال الظلّ، وذلك الشرحُ هو المقصود بقاؤه.
        var code = string.Join('\n', launcher.Split('\n').Select(StripHashComment));

        Assert.False(code.Contains(Shadow, StringComparison.OrdinalIgnoreCase),
            "‏مُشغِّلُ المجلس ما زال يعرف مساراً ثانياً في كوده — وسطرُ تفضيلٍ واحدٌ كافٍ ليعود الظلّ.");

        // ‏ولا اختيارَ أصلاً: `if` حول المشروع يعني أنّ وجودَ مجلّدٍ يُقرّر أيُّ محرّكٍ يحكم.
        Assert.DoesNotContain("Test-Path", code, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// <b>ما يُمنَع إشارةٌ حيّة، لا ذكرٌ لتاريخ.</b>
    ///
    /// <para><b>وهذا حدٌّ تعلّمتُه بسقوط هذه البوّابة على كودي أنا:</b> أوّلُ صيغةٍ منعت
    /// <b>كلَّ ذكرٍ</b> للمسار المحذوف، <b>فأسقطت التعليقَين اللذين يشرحان لماذا حُذف</b> —
    /// في <c>ci.yml</c> ومُشغِّل المجلس. <b>وسقوطُ حارسٍ على الصواب أسوأُ من مروره على
    /// الخطأ</b>: يُعلّم قارئَه أن يمحوَ الذاكرةَ المؤسّسيّة ليُرضيَه.</para>
    ///
    /// <para>فصار التمييز: في الملفّات التنفيذيّة تُمحى تعليقاتُ <c>#</c> قبل المسح؛ وفي
    /// الوثائق لا يُمنَع إلّا ما كان <b>في سياق أمر</b> (<c>dotnet</c> · <c>--project</c> ·
    /// <c>Test-Path</c> · اسمُ المُشغِّل) — فجملةٌ تروي ما كان تمرّ، وسطرٌ يأمر بتشغيله يسقط.</para>
    ///
    /// <para><b>ثمّ سقط ثانيةً على الشيء نفسِه — و<c>docs/</c> خرج من النطاق.</b> «سياقُ الأمر»
    /// حيلةٌ نصّيّة، <b>واقتباسُ الكود المحذوف في سجلّ الدروس يحمل <c>Test-Path</c> حرفيّاً</b>
    /// فيبدو أمراً حيّاً. فصار المسحُ النثريُّ على <c>.cursor/**</c> وحدَه — <b>وهو سطحُ
    /// تعليمِ الوكلاء، وهناك كان المُخالفُ الأصليّ</b> (<c>dream-team.md</c>) — و<c>docs/</c>
    /// سطحُ روايةِ التاريخ فلا يُفحص نثرُه. <b>ولا يضيع بذلك سنٌّ واحدة</b>:
    /// <c>docs/PROJECT-MEMORY.yaml</c> ملفٌّ تنفيذيٌّ فيبقى محروساً بقيمه لا بتعليقاته.</para>
    ///
    /// <para><b>وحدُّه المعترَفُ به:</b> لا يُميَّز <c>#</c> داخل نصٍّ من <c>#</c> تعليقاً،
    /// ولا يُفحص نثرُ <c>docs/</c> إطلاقاً. <b>والحارسُ الأقوى ليس هذا</b> بل
    /// <see cref="OnlyOneShuraCouncilProjectExists"/>: نسخةٌ ثانيةٌ تعود تُسقطه مهما قيل عنها.</para>
    /// </summary>
    [Fact]
    public void NoScriptOrDocumentStillPointsAtTheShadowCopy()
    {
        var root = RepoRoot();
        string[] folders = ["scripts", ".github", "docs", ".cursor"];

        var offenders = new List<string>();
        var scanned = 0;
        var agentProse = 0;

        foreach (var folder in folders)
        {
            var dir = Path.Combine(root, folder);
            if (!Directory.Exists(dir)) continue;

            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (!IsScanned(relative)) continue;

                scanned++;
                if (IsAgentFacingProse(relative)) agentProse++;

                foreach (var (line, number) in File.ReadAllLines(file).Select((l, i) => (l, i + 1)))
                    if (IsLivePointer(relative, line))
                        offenders.Add($"{relative}:{number}  {line.Trim()}");
            }
        }

        // ‏شاهدا ضبط: الماسحُ قرأ ملفّاتٍ فعلاً، **وبلغ سطحَ تعليم الوكلاء بعينه** — فالصفرُ
        // ‏عن فحصٍ لا عن عمى. وبلا الثاني يصير تضييقُ النطاق إلى `.cursor/**` إطفاءً صامتاً.
        Assert.True(scanned > 50, $"‏الماسحُ قرأ {scanned} ملفّاً فقط — النطاقُ خطأ، لا المستودعُ نظيف.");
        Assert.True(agentProse > 10,
            $"‏لم يبلغ الماسحُ إلّا {agentProse} وثيقةَ وكيلٍ في `.cursor/**` — وهناك كان المُخالفُ الأصليّ.");

        Assert.True(offenders.Count == 0,
            $"‏إشارةٌ حيّةٌ إلى «{Shadow}» بعد حذفه — ووثيقةٌ تُرشد وكيلاً إلى مسارٍ محذوف "
            + "أسوأُ من مسارٍ مكرّر:\n  " + string.Join("\n  ", offenders));
    }

    private static string StripHashComment(string line)
    {
        var marker = line.IndexOf('#');
        return marker < 0 ? line : line[..marker];
    }

    private static bool IsCommandContext(string line) =>
        line.Contains("dotnet", StringComparison.OrdinalIgnoreCase)
        || line.Contains("--project", StringComparison.OrdinalIgnoreCase)
        || line.Contains("Test-Path", StringComparison.OrdinalIgnoreCase)
        || line.Contains("run-shura-council", StringComparison.OrdinalIgnoreCase);

    private static bool IsExecutable(string relativePath) =>
        Path.GetExtension(relativePath).ToLowerInvariant()
            is ".ps1" or ".sh" or ".yml" or ".yaml" or ".json";

    /// <summary>نثرٌ يُقرأ تعليماً لوكيل — وهو <c>.cursor/**</c> وحدَه، لا <c>docs/</c>.</summary>
    private static bool IsAgentFacingProse(string relativePath) =>
        Path.GetExtension(relativePath).ToLowerInvariant() is ".md" or ".mdc"
        && relativePath.StartsWith(".cursor/", StringComparison.Ordinal);

    private static bool IsScanned(string relativePath) =>
        IsExecutable(relativePath) || IsAgentFacingProse(relativePath);

    /// <summary>
    /// <b>القرارُ في دالّةٍ واحدة</b> — يستدعيها المسحُ والاختبارُ الذاتيُّ معاً، فلا يُثبَت
    /// تمييزٌ غيرُ الذي يعمل.
    /// </summary>
    private static bool IsLivePointer(string relativePath, string line)
    {
        if (!IsScanned(relativePath)) return false;

        var text = IsExecutable(relativePath) ? StripHashComment(line) : line;
        if (!text.Contains(Shadow, StringComparison.OrdinalIgnoreCase)) return false;

        return IsExecutable(relativePath) || IsCommandContext(text);
    }

    /// <summary>
    /// <b>الاختبارُ الذاتيُّ — ويمرّ بدالّة القرار نفسِها، وكلُّ حالةٍ فيه أسقطت نسخةً.</b>
    /// </summary>
    [Theory]
    // ‏تعليقٌ يشرح الحذفَ في ملفٍّ تنفيذيّ — **أسقط النسخةَ الأولى**:
    [InlineData(".github/workflows/ci.yml", "      # ‏كانت نسختان: ShuraCouncil/ و tools/ShuraCouncil/", false)]
    [InlineData("ShuraCouncil/run-shura-council.ps1", "# ‏كان هنا tools/ShuraCouncil — المحذوفة.", false)]
    // ‏وسردُ التاريخ في `docs/` مقتبساً الكودَ المحذوف حرفيّاً — **أسقط النسخةَ الثانية**:
    [InlineData("docs/OPEN_ITEMS.md",
        "$proj = if (Test-Path \"tools/ShuraCouncil/ShuraCouncil.csproj\") { \"tools/...\" }", false)]
    [InlineData("docs/OPEN_ITEMS.md", "وكانت نسخةٌ ثانيةٌ تحت tools/ShuraCouncil حُذفت في ن-٢٠.", false)]
    // ‏وأمرٌ حيٌّ في ملفٍّ تنفيذيّ — يجب أن يسقط:
    [InlineData("scripts/ops/x.ps1", "dotnet build tools/ShuraCouncil/ShuraCouncil.csproj -c Release", true)]
    [InlineData("scripts/ops/agent-skill-regression.ps1",
        "$shuraTool = Join-Path $RepoRoot \"tools/ShuraCouncil/Program.cs\"", true)]
    [InlineData("docs/PROJECT-MEMORY.yaml", "      - tools/ShuraCouncil", true)]
    // ‏وتعليمُ وكيلٍ بأمرٍ حيّ — وهو المُخالفُ الأصليّ:
    [InlineData(".cursor/skills/brqosh-agents/dream-team.md",
        "**+ Shura:** agent auto-runs `dotnet run --project tools/ShuraCouncil`", true)]
    // ‏وروايةُ تاريخٍ في وثيقةِ وكيل — تمرّ:
    [InlineData(".cursor/skills/brqosh-agents/dream-team.md",
        "وكانت نسخةٌ تحت tools/ShuraCouncil قبل ن-٢٠.", false)]
    public void ThePointerScannerDiscriminates(string relativePath, string line, bool shouldBeCaught) =>
        Assert.Equal(shouldBeCaught, IsLivePointer(relativePath, line));

    /// <summary>
    /// <b>وCI يبني المحرّكَ الباقي.</b> خطوةُ «بناءُ أدوات التشغيل» تسرد <c>tools</c> بنمطٍ
    /// شامل، والجذرُ خارجَها — <b>فحذفُ الظلّ يترك المحرّكَ الحقيقيَّ بلا بناءٍ في CI أصلاً</b>،
    /// وخطأُ ترجمةٍ فيه لا يراه أحد.
    /// </summary>
    [Fact]
    public void ContinuousIntegrationBuildsTheSurvivingEngine()
    {
        var ci = Read(".github/workflows/ci.yml");

        // ‏شاهدُ ضبط: التقنيةُ ترى خطوةَ بناء الأدوات في هذا الملفّ فعلاً.
        Assert.Contains("Build ops tools", ci, StringComparison.Ordinal);

        Assert.True(ci.Contains(Canonical, StringComparison.Ordinal),
            $"‏CI لا يبني «{Canonical}». ونمطُ `Get-ChildItem tools` لا يبلغه لأنّه خارج `tools/` "
            + "وليس في `Brqosh.sln` — فلا شيءَ يترجمه بعد حذف الظلّ.");
    }

    // ───────── ما يكتبه الكاتبُ يقرؤه القارئ ─────────

    /// <summary>يستخرج صيغَ سطر الخلاصة التي يكتبها مُدقّقُ الواجهة فعلاً.</summary>
    private static List<string> WriterSummaryFormats()
    {
        var source = Read("tools/UiAudit/UiAuditResult.cs");

        return Regex.Matches(source, """WriteLine\(\$"(===[^"]*===)"\)""")
            .Select(m => m.Groups[1].Value)
            .Where(f => f.Contains("فحص", StringComparison.Ordinal))
            .Select(f => Regex.Replace(f, @"\{[^{}]*\}", "7"))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>يستخرج التعابيرَ النمطيّة الحرفيّة من قارئ الأدلّة.</summary>
    private static List<string> ReaderPatterns()
    {
        var source = Read("ShuraCouncil/EvidenceLoader.cs");

        // ‏نمطُ السلسلة الحرفيّة في C#‏: ‎@"…"‎ مع ‎""‎ اقتباساً داخليّاً.
        const string VerbatimString = "@\"((?:[^\"]|\"\")*)\"";

        return Regex.Matches(source, VerbatimString)
            .Select(m => m.Groups[1].Value.Replace("\"\"", "\""))
            .Where(p => p.Contains("===", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// <b>كلُّ صيغةٍ يكتبها مُدقّقُ الواجهة يقرؤها المجلس.</b>
    ///
    /// <para>وهذا هو الشقُّ الذي كان يُفقَد بالحذف الساذج: <c>tools</c> يقرأ أربعَ صيغ،
    /// والجذرُ يقرأ صيغةً قديمةً <b>لم يعد الكاتبُ يكتبها إطلاقاً</b> — فيسقط إلى عدّ
    /// علامات <c>[PASS]</c>/<c>[FAIL]</c> <b>ويُسقط <c>[SKIP]</c> من المجموع</b>. ومقيسٌ
    /// على سجلّات حقيقيّة: ١٢ من ١٨ سجلّاً بالصيغة الحديثة التي لا يقرؤها الجذر.</para>
    ///
    /// <para><b>وهو «أدواتُ القياس تكذب صامتةً» بعينه</b>: لا رسالةَ خطأ، بل رقمٌ أصغرُ
    /// من الحقيقة في تقريرِ حوكمة.</para>
    /// </summary>
    [Fact]
    public void EverySummaryTheAuditWritesIsReadableByTheCouncil()
    {
        var formats = WriterSummaryFormats();
        var patterns = ReaderPatterns();

        // ‏شاهدا ضبط: الاستخراجُ أصاب الطرفَين — **فالنجاحُ عن تطابقٍ لا عن قائمتَين فارغتَين**.
        Assert.True(formats.Count >= 2,
            $"‏لم تُستخرج صيغُ الكاتب ({formats.Count}) — الاستخراجُ أعمى، لا الكاتبُ صامت.");
        Assert.True(patterns.Count >= 1,
            $"‏لم تُستخرج أنماطُ القارئ ({patterns.Count}) — الاستخراجُ أعمى.");

        var unreadable = formats
            .Where(f => !patterns.Any(p => SafeMatch(p, f)))
            .ToList();

        Assert.True(unreadable.Count == 0,
            "‏صيغةٌ يكتبها مُدقّقُ الواجهة ولا يقرؤها المجلس — فيسقط إلى عدّ العلامات ويُبلّغ "
            + "رقماً أصغرَ من الحقيقة بلا شكوى:\n  "
            + string.Join("\n  ", unreadable)
            + "\n‏أنماطُ القارئ المتاحة:\n  " + string.Join("\n  ", patterns));
    }

    private static bool SafeMatch(string pattern, string input)
    {
        try { return Regex.IsMatch(input, pattern); }
        catch (ArgumentException) { return false; }
    }

    /// <summary>
    /// <b>الاختبارُ الذاتيُّ — يمرّ بآلةِ الاستخراج والمطابقة نفسِها.</b> بلا هذا تكون
    /// «كلُّ الصيغ مقروءة» دالّةً لا تشتكي أبداً.
    /// </summary>
    [Theory]
    [InlineData(@"===\s*(\d+)\s*فشل\s*من\s*(\d+)\s*فحص\s*===", "=== 7 فشل من 7 فحص ===", true)]
    [InlineData(@"===\s*(\d+)\s*فشل\s*·\s*(\d+)\s*ناجح\s*·\s*(\d+)\s*متخطى\s*من\s*(\d+)\s*فحص\s*===",
        "=== 7 فشل · 7 ناجح · 7 متخطى من 7 فحص ===", true)]
    // ‏والصيغةُ الحديثةُ **لا** يقرؤها النمطُ القديم — وهي الحالةُ التي كانت قائمةً فعلاً:
    [InlineData(@"===\s*(\d+)\s*فشل\s*من\s*(\d+)\s*فحص\s*===",
        "=== 7 فشل · 7 ناجح · 7 متخطى من 7 فحص ===", false)]
    [InlineData(@"===\s*SUCCESS\s*—\s*(\d+)\s*ناجح", "=== SUCCESS — 7 ناجح · 7 متخطى · 7 فحص ===", true)]
    [InlineData(@"===\s*\d+\s*فشل", "=== BRQOSH Live UI Audit — 2026-09-14 ===", false)]
    public void TheFormatMatcherDiscriminates(string pattern, string sample, bool shouldMatch) =>
        Assert.Equal(shouldMatch, SafeMatch(pattern, sample));

    /// <summary>واستخراجُ صيغِ الكاتب يُصيب العددَ المعروف — فلو عمي لسقط هنا لا هناك.</summary>
    [Fact]
    public void TheWriterExtractorFindsBothSummaryShapes()
    {
        var formats = WriterSummaryFormats();

        Assert.Equal(2, formats.Count);
        Assert.Contains(formats, f => f.Contains("SUCCESS", StringComparison.Ordinal));
        Assert.Contains(formats, f => f.Contains("فشل", StringComparison.Ordinal));
        Assert.DoesNotContain(formats, f => f.Contains("Live UI Audit", StringComparison.Ordinal));
    }
}
