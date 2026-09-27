using System.Text.RegularExpressions;

namespace Brqosh.Application.Tests.Platform;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٧٦ · <c>accounting-core#10</c> وصنفُه) <b>لا دليلَ يُرسل أحداً ليُشغّل أداةَ التدقيق على دفاترَ
/// حقيقيّة.</b>
///
/// <para><b>ما كان:</b> <c>tools/FullScenarioAudit</c> تكتب في الدفاتر التي يحلّها الجهاز — حساباً بدور «مدير»، ومستنداتٍ
/// تستهلك أرقامَ المستندات وتدخل الميزانَ والإقرار. ومنذ ن-٢٢ يرفض <c>LiveBooksGuard</c> بلا إذنٍ يُسمّي القاعدة — <b>ومَن يتبع
/// الدليلَ يأذن</b>. وقِيس: سبعُ تعليماتٍ في الأدلّة الحيّة بلا عزل — حزمةُ مراجع SOCPA، و<b>الخطوةُ السادسة في دليل
/// التنصيب</b>، و<b>قائمةُ تحقّق الإنتاج «بعد الترقية»</b>، وكتيّبا البوّابات والـVM (بـ«نفس قاعدة البيانات أو نسخة PostgreSQL
/// مستوردة»)، وخارطةُ الطريق.</para>
///
/// <para><b>والحارس:</b> كلُّ سطرٍ في <c>docs/</c> و<c>installer/</c> يأمر بتشغيل الأداة (فعلُ تشغيلٍ أو بندُ قائمةٍ لم
/// يُعلَّم) يُسمّي عزلاً في جواره (±٣ أسطر). والمطابقةُ بعد نزع التشكيل — «نفّذ» و«نفِّذ» تُكتبان بلا ترتيبٍ ثابت.</para>
/// </summary>
public class NoGuideRunsTheAuditToolOnRealBooksTests
{
    private const string Tool = "FullScenarioAudit";
    private const string ReviewerPackage = "docs/ops/O-02_SOCPA_REVIEWER_PACKAGE.md";
    private const int Near = 3;

    private static readonly Regex RunInstruction =
        new(@"dotnet run|شغل|نفذ|تشغيل|\bVerify with\b|\brun\b|^\s*- \[ \]", RegexOptions.IgnoreCase);

    private static readonly Regex Isolation = new(@"BRQOSH_DATA_ROOT|معزول|عزل|isolated", RegexOptions.IgnoreCase);

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

    private static string Bare(string s) => Regex.Replace(s, "[ً-ْ]", "");

    private static string[] Lines(string path) =>
        File.ReadAllText(path).Split('\n').Select(l => Bare(l.TrimEnd('\r'))).ToArray();

    /// <summary>سطورُ «شغّل الأداة» التي لا تُسمّي عزلاً في جوارها — في نصٍّ واحد.</summary>
    internal static List<int> Unisolated(string[] lines, out int instructions)
    {
        var found = new List<int>();
        instructions = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            if (!lines[i].Contains(Tool, StringComparison.Ordinal) || !RunInstruction.IsMatch(lines[i]))
                continue;
            instructions++;
            var window = string.Join("\n", lines[Math.Max(0, i - Near)..Math.Min(lines.Length, i + Near + 1)]);
            if (!Isolation.IsMatch(window))
                found.Add(i + 1);
        }

        return found;
    }

    /// <summary>كلُّ تعليمةٍ بتشغيل الأداة في الأدلّة الحيّة تُسمّي عزلاً في جوارها.</summary>
    [Fact]
    public void EveryInstructionToRunTheAuditTool_NamesIsolation()
    {
        var root = RepoRoot();
        var total = 0;
        var offenders = new List<string>();
        foreach (var dir in new[] { "docs", "installer" })
        foreach (var path in Directory.EnumerateFiles(Path.Combine(root, dir), "*.md", SearchOption.AllDirectories))
        {
            offenders.AddRange(Unisolated(Lines(path), out var n)
                .Select(line => $"{Path.GetRelativePath(root, path).Replace('\\', '/')}:{line}"));
            total += n;
        }

        // ‏شاهدُ ضبط: الماسحُ يرى التعليماتِ فعلاً — O-01 والحزمةُ ودليلُ التنصيب وقائمةُ الإنتاج وغيرُها.
        Assert.True(total >= 5, $"‏الماسحُ لا يرى تعليماتِ تشغيل {Tool} ({total}).");
        Assert.True(offenders.Count == 0,
            $"‏دليلٌ يأمر بتشغيل {Tool} بلا عزل — تكتب في الدفاتر التي يحلّها الجهاز: {string.Join(" · ", offenders)}");
    }

    /// <summary>وحزمةُ المراجع تُعيد الإنتاجَ باختباراتٍ لا تلمس قاعدة — لا بتشغيل الأداة.</summary>
    [Fact]
    public void TheReviewerPackage_ReproducesWithoutAnyBooks()
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot(), ReviewerPackage.Replace('/', Path.DirectorySeparatorChar)));

        Assert.Contains("dotnet test tests/Brqosh.Application.Tests", text, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet run --project tools", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// شاهدُ ضبطٍ للمصنِّف نفسِه: ما كان في كتيّب الـVM يُسقَط، وما في O-01 يمرّ، وذِكرٌ بلا فعلٍ ليس تعليمة.
    /// </summary>
    [Fact]
    public void TheClassifier_TellsAnInstructionFromAMention()
    {
        string[] Text(params string[] l) => l.Select(Bare).ToArray();

        Assert.Equal(new[] { 1 }, Unisolated(Text("5. شغّل FullScenarioAudit ضد نفس قاعدة البيانات (أو استورد نسخة PostgreSQL)"), out _));
        Assert.Equal(new[] { 1 }, Unisolated(Text("- [ ] FullScenarioAudit SUCCESS بعد الترقية"), out _));
        Assert.Empty(Unisolated(Text("وهو يعزل جذرَ البيانات قبل أن يُشغّل `FullScenarioAudit`."), out _));
        Assert.Empty(Unisolated(Text("| FullScenarioAudit | 13/13 |"), out var mentions));
        Assert.Equal(0, mentions);
    }
}
