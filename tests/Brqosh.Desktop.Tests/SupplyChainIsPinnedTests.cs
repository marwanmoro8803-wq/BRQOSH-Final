using System.IO;
using System.Text.RegularExpressions;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (م-٤ · ن-١٣) <b>سلسلةُ التوريد كانت بلا تثبيتٍ في أيّ طبقة.</b>
///
/// <para><b>والمقيسُ عند الفتح:</b> <b>سبعةُ</b> إجراءاتٍ بوسمٍ متحرّك وصفرٌ مثبَّتٌ ببصمة ·
/// <b>صفرُ</b> مقاطع <c>permissions</c> · <b>إحدى عشرة</b> حزمةً من ثمانٍ وثلاثين على مدًى
/// عائم · لا <c>NuGet.config</c> · لا تدقيقَ ثغراتٍ · ولا مُحدِّثَ تبعيّاتٍ إطلاقاً.</para>
///
/// <para><b>وتصحيحٌ لنطاق البند — وهو الأهمّ:</b> قيل «ثلاثةُ إجراءاتٍ» في ملفٍّ واحد.
/// <b>والمقيسُ سبعةٌ في ملفَّين</b>: الثاني <c>tools/desktop-qa-army/.github/workflows/qa-audit.yml</c>،
/// و<b>سطرُه الأوّل يدعو إلى نسخه إلى الجذر</b>. وهو خاملٌ حيث هو — GitHub لا تقرأ إلّا
/// <c>.github/workflows</c> الجذر — <b>لكنّ قالباً بأوسامٍ متحرّكة يُوَرِّث العطلَ إلى كلّ
/// من نسخه</b>. ولذلك يمسح هذا الحارسُ <b>كلَّ</b> مجلّد <c>.github/workflows</c> في الشجرة.</para>
///
/// <para><b>ولماذا الأدنويّة لا الوجود:</b> <c>permissions: write-all</c> يُرضي أيَّ بحثٍ عن
/// وجود المقطع <b>وهو أسوأُ من غيابه</b>. فيُقاس أنّ المقطع لا يمنح كتابةً — لا أنّه مكتوب.</para>
///
/// <para><b>وللحارسَين اختبارٌ ذاتيّ</b>: طفراتٌ على نصٍّ في الذاكرة يجب أن تُرفض. فحارسٌ
/// يقرأ ملفّاً صحيحاً لا يُثبت أنّه يرفض ملفّاً خاطئاً، <b>وذلك هو الفرقُ بين بوّابةٍ وزينة</b>.</para>
/// </summary>
public class SupplyChainIsPinnedTests
{
    /// <summary>
    /// بصمةٌ من أربعين خانةً ست عشريّة، <b>يتبعها وسمٌ وتاريخٌ في تعليق</b>.
    ///
    /// <para><b>والتعليقُ جزءٌ من الحارس لا زينة:</b> بصمةٌ عاريةٌ تمرّ على أيّ فحصِ شكلٍ
    /// <b>ولا يعرف قارئُها أيَّ إصدارٍ تُشغّل</b>، فلا يستطيع أحدٌ أن يحكم أهي متقادمةٌ أم لا.
    /// فيُشترط أن تُعلن نيّتَها.</para>
    /// </summary>
    private static readonly Regex PinnedUse = new(
        @"uses:\s*[A-Za-z0-9._-]+/[A-Za-z0-9._/-]+@[0-9a-f]{40}\s+#\s*v\d\S*\s+—\s+\d{4}-\d{2}-\d{2}",
        RegexOptions.Compiled);

    private static readonly Regex AnyUse = new(@"^\s*-?\s*uses:\s*\S+", RegexOptions.Compiled);

    // ── الإجراءات ─────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>كلُّ إجراءٍ في كلّ سير عمل — مثبَّتٌ ببصمةٍ ومُعلِنٌ لوسمه.</b>
    /// </summary>
    [Fact]
    public void EveryActionInEveryWorkflow_IsPinnedToADigest()
    {
        var files = WorkflowFiles();

        // ‏شاهدُ ضبطٍ موجب: لو عمي المسحُ عن الملفّات لَمرّ التوكيدُ على شجرةٍ معطوبة.
        Assert.Equal(2, files.Count);

        var offenders = files
            .SelectMany(f => File.ReadAllLines(f)
                .Select((line, i) => (File: Path.GetFileName(f), No: i + 1, Line: line))
                .Where(x => AnyUse.IsMatch(x.Line) && !PinnedUse.IsMatch(x.Line)))
            .Select(x => $"{x.File}:{x.No} ⟵ {x.Line.Trim()}")
            .ToList();

        Assert.True(offenders.Count == 0,
            "‏إجراءاتٌ بوسمٍ متحرّك — والوسمُ مؤشّرٌ يملكه صاحبُه ويستطيع تحريكَه:\n  "
            + string.Join("\n  ", offenders));

        // ‏وشاهدٌ ثانٍ: العددُ المقيس. فمسحٌ يجد صفرَ أسطرٍ يمرّ خضراءَ بلا أن يفحص شيئاً.
        var total = files.Sum(f => File.ReadAllLines(f).Count(l => AnyUse.IsMatch(l)));
        Assert.Equal(7, total);
    }

    /// <summary>
    /// <b>اختبارٌ ذاتيٌّ للحارس:</b> ثلاثُ صيغٍ يجب أن تُرفض، وواحدةٌ تُقبل.
    ///
    /// <para>فبغيره لا يُميَّز «حارسٌ يقرأ البنية» عن «تعبيرٍ يقبل كلَّ شيء» — وهو ما جعل
    /// أنماطَ الأسرار تموت صامتةً في هذا المستودع مرّةً من قبل.</para>
    /// </summary>
    [Theory]
    [InlineData("      - uses: actions/checkout@v4", false)]
    [InlineData("      - uses: actions/checkout@11d5960a326750d5838078e36cf38b85af677262", false)]
    [InlineData("      - uses: actions/checkout@11d5960a326750d5838078e36cf38b85af677262 # pinned", false)]
    [InlineData("      - uses: actions/checkout@11d5960a326750d5838078e36cf38b85af677262 # v4 — 2026-09-13", true)]
    public void ThePinChecker_AcceptsOnlyADigestThatDeclaresItsTag(string line, bool accepted) =>
        Assert.Equal(accepted, PinnedUse.IsMatch(line));

    // ── الصلاحيّات ────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>كلُّ سيرِ عملٍ يُعلن صلاحيّاتِه، ولا يمنح كتابةً.</b>
    ///
    /// <para>وقِيس أنّ الرمزَ لا يُستعمل إطلاقاً في سير العمل الحيّ: بحثٌ عن
    /// <c>GITHUB_TOKEN|secrets.|github.token|gh api|gh pr|git push</c> يُعيد صفراً،
    /// <b>وشاهدُ ضبطٍ موجب</b> أنّ <c>env:</c> يظهر خمسَ مرّاتٍ في الملفّ نفسِه.</para>
    /// </summary>
    [Fact]
    public void EveryWorkflow_DeclaresLeastPrivilege()
    {
        foreach (var file in WorkflowFiles())
        {
            var text = File.ReadAllText(file);

            Assert.True(GrantsOnlyRead(text),
                $"‏{Path.GetFileName(file)}: لا مقطعَ صلاحيّاتٍ أدنى — فالرمزُ يأخذ افتراضَ المستودع.");
        }
    }

    /// <summary>
    /// <b>واختبارٌ ذاتيٌّ ثانٍ — وهو الذي يمنع «الوجودَ» من أن يُغني عن «الأدنويّة».</b>
    /// </summary>
    [Theory]
    [InlineData("name: x\non: push\njobs:\n  a:\n    runs-on: x\n", false)]
    [InlineData("name: x\npermissions: write-all\njobs:\n  a:\n", false)]
    [InlineData("name: x\npermissions:\n  contents: write\njobs:\n  a:\n", false)]
    [InlineData("name: x\npermissions:\n  contents: read\njobs:\n  a:\n", true)]
    [InlineData("name: x\npermissions:\n  contents: read\n  actions: read\njobs:\n  a:\n", true)]
    public void ThePermissionChecker_RejectsPresenceWithoutMinimality(string yaml, bool accepted) =>
        Assert.Equal(accepted, GrantsOnlyRead(yaml.Replace("\n", Environment.NewLine)));

    // ── الحزم ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>لا إصدارَ عائمٍ في أيّ مشروع.</b>
    ///
    /// <para>فمدًى مثل <c>9.0.*</c> يُرقّى من نفسه في كلّ استعادة <b>بلا مراجعةٍ ولا أثرٍ في
    /// التاريخ</b> — وكان أحدَ عشرَ منها، فيها مزوّدُ قاعدة البيانات ومكتبتا تشفير.</para>
    /// </summary>
    [Fact]
    public void NoProject_CarriesAFloatingVersion()
    {
        var projects = ProjectFiles();
        Assert.True(projects.Count > 20, $"‏المسحُ وجد {projects.Count} مشروعاً — عددٌ لا يُصدَّق.");

        var floating = projects
            .SelectMany(p => File.ReadAllLines(p)
                .Select((l, i) => (P: Path.GetFileName(p), No: i + 1, L: l))
                .Where(x => Regex.IsMatch(x.L, "Version=\"[^\"]*\\*")))
            .Select(x => $"{x.P}:{x.No} ⟵ {x.L.Trim()}")
            .ToList();

        Assert.True(floating.Count == 0, "‏إصداراتٌ عائمة:\n  " + string.Join("\n  ", floating));

        // ‏شاهدُ ضبطٍ موجب: الماسحُ يجد إصداراتٍ حقيقيّةً في الملفّات نفسِها.
        Assert.Contains(projects, p => Regex.IsMatch(File.ReadAllText(p), "Version=\"[0-9]"));
    }

    /// <summary>
    /// <b>ومصدرُ الحزم مُعلَنٌ في المستودع لا موروثٌ من الجهاز.</b>
    ///
    /// <para><b>ويُقال بلا تهوين:</b> أثرُه اليوم صفر — قِيس أنّ المصدر الفعّال واحدٌ هو
    /// <c>nuget.org</c>. <b>وقيمتُه في الغد</b>: مصدرٌ يُضاف إلى إعداد جهازٍ أو صورةِ عدّاءٍ
    /// يُوسّع الاستعادةَ صامتاً، و<c>&lt;clear/&gt;</c> يمنعه.</para>
    /// </summary>
    [Fact]
    public void ThePackageSource_IsDeclaredInTheRepository()
    {
        var config = Path.Combine(FindRepoRoot(), "NuGet.config");
        Assert.True(File.Exists(config), "‏لا NuGet.config — فمجموعةُ المصادر خاصّيّةُ جهازٍ لا مشروع.");

        var text = File.ReadAllText(config);
        Assert.Contains("<clear />", text, StringComparison.Ordinal);
        Assert.Contains("https://api.nuget.org/v3/index.json", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>وثغرةٌ معروفةٌ تُسقط الاستعادةَ — لا تُحذّر.</b>
    ///
    /// <para>و<c>NuGetAudit</c> وحدَه يُصدر تحذيراً يمرّ، <b>فالترقيةُ إلى خطأٍ هي البوّابة</b>.
    /// و<c>all</c> لا <c>direct</c>: الثغرةُ تأتي غالباً من تبعيّةٍ لم يذكرها أحد.</para>
    /// </summary>
    [Fact]
    public void KnownVulnerabilities_FailTheRestore()
    {
        var props = File.ReadAllText(Path.Combine(FindRepoRoot(), "Directory.Build.props"));

        Assert.Contains("<NuGetAuditMode>all</NuGetAuditMode>", props, StringComparison.Ordinal);
        Assert.Contains("NU1901;NU1902;NU1903;NU1904", props, StringComparison.Ordinal);
        Assert.Contains("<WarningsAsErrors>", props, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>وللبصماتِ مُحدِّثٌ — وبغيره يصير التثبيتُ تجميداً.</b>
    ///
    /// <para><b>وهذا التوكيدُ هو ما يمنع الإصلاحَ من أن ينقلب انحداراً:</b> قِيس أنّ
    /// <c>v4</c> قناةُ ترقيعٍ أمنيٍّ حيّة — قمّتُها <c>backport fixes to releases-v4</c>
    /// بتاريخ 2026-07-16. <b>فبصمةٌ بلا مُحدِّثٍ تقطع تلك القناة</b>، ويُبدَّل انجرافٌ صامتٌ
    /// بتقادمٍ صامت. ويشمل المُحدِّثُ مجلّدَي سيرِ العمل معاً — فالقالبُ يتقادم كما يتقادم الحيّ.</para>
    /// </summary>
    [Fact]
    public void ThePins_HaveAnUpdater()
    {
        var path = Path.Combine(FindRepoRoot(), ".github", "dependabot.yml");
        Assert.True(File.Exists(path), "‏بصماتٌ بلا مُحدِّث — تجميدٌ لا تثبيت.");

        var text = File.ReadAllText(path);
        Assert.Contains("package-ecosystem: github-actions", text, StringComparison.Ordinal);
        Assert.Contains("package-ecosystem: nuget", text, StringComparison.Ordinal);
        Assert.Contains("/tools/desktop-qa-army", text, StringComparison.Ordinal);
    }

    // ── الأدوات ───────────────────────────────────────────────────────────────

    /// <summary>
    /// هل يمنح المقطعُ قراءةً فقط؟ — <b>غيابُه رفضٌ، و<c>write</c> في أيّ موضعٍ منه رفض</b>.
    /// </summary>
    private static bool GrantsOnlyRead(string yaml)
    {
        var lines = yaml.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

        var start = Array.FindIndex(lines, l =>
            l.StartsWith("permissions:", StringComparison.Ordinal));
        if (start < 0) return false;

        // ‏`permissions: write-all` على السطر نفسِه.
        var inline = lines[start]["permissions:".Length..].Trim();
        if (inline.Length > 0) return inline is "read-all";

        // ‏وإلّا فالمقطعُ هو الأسطرُ المُزاحةُ بعده.
        var body = lines.Skip(start + 1)
            .TakeWhile(l => l.Length == 0 || char.IsWhiteSpace(l[0]))
            .Where(l => l.Trim().Length > 0)
            .ToList();

        return body.Count > 0 && body.All(l => l.TrimEnd().EndsWith(": read", StringComparison.Ordinal));
    }

    /// <summary>كلُّ مجلّد <c>.github/workflows</c> في الشجرة — لا الجذرَ وحدَه.</summary>
    private static List<string> WorkflowFiles() =>
        Directory.EnumerateFiles(FindRepoRoot(), "*.yml", SearchOption.AllDirectories)
            .Where(f => f.Contains(
                $"{Path.DirectorySeparatorChar}.github{Path.DirectorySeparatorChar}workflows{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Where(NotBuildOutput)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

    private static List<string> ProjectFiles() =>
        Directory.EnumerateFiles(FindRepoRoot(), "*.csproj", SearchOption.AllDirectories)
            .Where(NotBuildOutput)
            .ToList();

    private static bool NotBuildOutput(string path) =>
        !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        && !path.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        && !path.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        // ‏رميةٌ لا تخطٍّ صامت: حارسٌ لا يجد ما يحرسه يجب أن يسقط، لا أن يمرّ أخضر.
        throw new InvalidOperationException("لم يُعثر على جذر المستودع (Brqosh.sln).");
    }
}
