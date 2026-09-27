using System.Text.RegularExpressions;

namespace Brqosh.Application.Tests.Platform;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٧٢ · <c>evidence-integrity#8</c>) <b>تحليلُ فجوات الإطلاق لقطةٌ تاريخيّة — ولا يُستشهَد به حالةً
/// حاضرة.</b>
///
/// <para><b>ما كان:</b> <c>docs/MARKET_LAUNCH_GAP_ANALYSIS.md</c> كُتب في 2026-07-10 ولم يُمسّ منذ، و<c>OPEN_ITEMS</c> يُحيل
/// إليه «تفصيلاً» للإطلاق، وخارطةُ التنفيذ مثلُه. <b>وقِيس في الاتّجاهين:</b> يقول «صفر» لملفّ حماية الأجور والدفعات والموازنات
/// وقائمة التدفّقات النقديّة وتحديد المعدّل — وكلُّها في الشجرة؛ ويقول «V12 مغلق» و«458/458» وتغطيةَ Application «≥ 80.64%»
/// — والحكمُ NO-GO والمقيسُ بالبوّابة 77.36%.</para>
///
/// <para><b>والحرّاسُ ثلاثة:</b> لافتةٌ في رأس الملفّ تُسمّي مرجعَي الحالة الحاضرة، وهما موجودان؛ ولا رابطَ إليه تحت
/// <c>docs/</c> إلّا موسوماً «تاريخيّ»؛ <b>وكلُّ استشهادٍ بسطرٍ منه (<c>…md:N</c>) يقع على الصفّ الذي يُسمّيه</b> — فاللافتةُ
/// نفسُها أزاحت الأسطر، والاستشهادُ القديمُ بصفّ G-30 كان سيقع على صفٍّ آخر بعدها.</para>
///
/// <para><b>وحدُّه:</b> لا يُعيد قياسَ صفوف G واحداً واحداً — يمنع أن تُقرأ حالةً حاضرة. والمقارنةُ بعد نزع التشكيل: ترتيبُ
/// العلامات يختلف بين المحرّرات فيُفشل المطابقةَ الحرفيّة.</para>
/// </summary>
public class TheStaleGapAnalysisIsNotCitedAsCurrentTests
{
    private const string GapAnalysis = "docs/MARKET_LAUNCH_GAP_ANALYSIS.md";
    private const string Historical = "تاريخي";
    private static readonly string[] CurrentAuthorities =
        ["release-readiness/MASTER.md", "release-readiness/REMAINING-MAJORS.md"];

    private static readonly Regex LinkToGapAnalysis = new(@"\]\([^)]*MARKET_LAUNCH_GAP_ANALYSIS\.md[^)]*\)");
    private static readonly Regex LineCitation = new(@"MARKET_LAUNCH_GAP_ANALYSIS\.md:(\d+)");
    private static readonly Regex GapId = new(@"\bG-\d+\b");

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

    private static string[] Read(string path) =>
        File.ReadAllText(path).Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

    private static string Bare(string s) => Regex.Replace(s, "[ً-ْ]", "");

    /// <summary>كلُّ سطرٍ في وثائق <c>docs/</c> عدا التحليلِ نفسِه — بمساره النسبيّ ورقمه.</summary>
    private static List<(string File, int Line, string Text)> DocumentLines()
    {
        var root = RepoRoot();
        var gap = Path.GetFullPath(Path.Combine(root, GapAnalysis));
        return Directory.EnumerateFiles(Path.Combine(root, "docs"), "*.md", SearchOption.AllDirectories)
            .Where(p => !string.Equals(Path.GetFullPath(p), gap, StringComparison.OrdinalIgnoreCase))
            .SelectMany(p => Read(p).Select((text, i) =>
                (Path.GetRelativePath(root, p).Replace('\\', '/'), i + 1, text)))
            .ToList();
    }

    /// <summary>اللافتةُ في رأس الملفّ — قبل تاريخه ومنهجه — تقول إنّه لقطة، وتُحيل إلى مرجعَين موجودَين.</summary>
    [Fact]
    public void TheGapAnalysis_OpensWithItsSupersession()
    {
        var root = RepoRoot();
        var head = Read(Path.Combine(root, GapAnalysis)).Take(12).ToList();

        var banner = head.FindIndex(l => Bare(l).Contains("لقطة " + Historical, StringComparison.Ordinal));
        Assert.True(banner is >= 1 and <= 3,
            $"‏لا لافتةَ «لقطة تاريخيّة» تحت عنوان {GapAnalysis} — يُقرأ حالةً حاضرة (السطر: {banner}).");

        var pointer = head.SingleOrDefault(l => Bare(l).Contains("الحالة الحاضرة", StringComparison.Ordinal));
        Assert.True(pointer is not null, $"‏اللافتةُ لا تقول أين الحالةُ الحاضرة في {GapAnalysis}.");
        foreach (var target in CurrentAuthorities)
        {
            Assert.Contains($"]({target})", pointer, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(root, "docs", target.Replace('/', Path.DirectorySeparatorChar))),
                $"‏اللافتةُ تُحيل إلى docs/{target} ولا ملفَّ هناك.");
        }
    }

    /// <summary>كلُّ رابطٍ إلى التحليل تحت <c>docs/</c> يصفه «تاريخيّاً» في سطره.</summary>
    [Fact]
    public void NoDocument_LinksTheGapAnalysis_WithoutCallingItHistorical()
    {
        var links = DocumentLines().Where(x => LinkToGapAnalysis.IsMatch(x.Text)).ToList();

        // ‏شاهدُ ضبط: الماسحُ يرى الروابطَ فعلاً — وإلّا فصفرُ المخالفات يمرّ على أيّ وثيقة.
        Assert.NotEmpty(links);

        var live = links.Where(x => !Bare(x.Text).Contains(Historical, StringComparison.Ordinal))
            .Select(x => $"{x.File}:{x.Line}")
            .ToList();
        Assert.True(live.Count == 0,
            $"‏رابطٌ إلى {GapAnalysis} يُقدّمه حالةً حاضرة — صِفه «تاريخيّاً» أو أحِل إلى MASTER: {string.Join(" · ", live)}");
    }

    /// <summary>
    /// استشهادٌ بسطرٍ من التحليل يقع على صفٍّ يحمل رمزاً من رموز G التي يُسمّيها سطرُ الاستشهاد — فإزاحةُ الأسطر لا تُحيله
    /// إلى صفٍّ آخر صامتاً.
    /// </summary>
    [Fact]
    public void EveryLineCitation_OfTheGapAnalysis_LandsOnTheRowItNames()
    {
        var gap = Read(Path.Combine(RepoRoot(), GapAnalysis));
        var citations = DocumentLines()
            .SelectMany(x => LineCitation.Matches(x.Text)
                .Select(m => (x.File, x.Line, x.Text, Target: int.Parse(m.Groups[1].Value))))
            .ToList();

        // ‏شاهدُ ضبط: استشهادُ OPEN_ITEMS بصفّ G-30 (حمايةُ البيانات الشخصيّة) يُرى.
        Assert.NotEmpty(citations);

        var wrong = new List<string>();
        foreach (var c in citations)
        {
            var named = GapId.Matches(c.Text).Select(m => m.Value).Distinct().ToList();
            Assert.True(named.Count > 0, $"‏{c.File}:{c.Line} يستشهد بالسطر {c.Target} ولا يُسمّي صفّاً (G-N).");

            var lands = c.Target >= 1 && c.Target <= gap.Length
                        && named.Any(g => gap[c.Target - 1].Contains($"**{g}**", StringComparison.Ordinal));
            if (!lands)
                wrong.Add($"{c.File}:{c.Line} ⟵ السطر {c.Target} ليس صفَّ {string.Join("/", named)}");
        }

        Assert.True(wrong.Count == 0, $"‏استشهادٌ بسطرٍ أُزيح: {string.Join(" · ", wrong)}");
    }
}
