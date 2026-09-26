using System.Text.RegularExpressions;

namespace Brqosh.Application.Tests.Platform;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٦٥ · evidence-integrity#7) <b>رأسُ <c>OPEN_ITEMS</c> وعنوانُ §1ل يبلغان آخرَ إغلاقٍ في جدولها.</b>
///
/// <para><b>ما كان:</b> السطرُ الثالثُ من الملفّ — «آخر تحديث» — يقول «اثنان وعشرون إغلاقاً ح-١…ح-٢٢»، وعنوانُ
/// §1ل مثلُه، <b>وجدولُ §1ل تحتهما يبلغ ح-٦٤</b>: اثنان وأربعون إغلاقاً سُجّلت صفوفُها ولم يتحرّك الرأس، كلُّها بدفعات
/// وثائقَ أُودعت بعد بوّاباتٍ خضراء. والتدقيقُ وجده قبلها بتاريخٍ أقدم (<c>2026-09-05</c> والمداخلُ إلى
/// <c>09-15</c>). <b>فالرأسُ ذاكرةٌ لا تُحدَّث إلّا إن تذكّرها أحد</b> — والحارسُ يجعل الصفَّ والرأسَ يتحرّكان معاً.</para>
///
/// <para><b>وحدُّه:</b> يقرأ مدى <c>ح-١…ح-N</c> لا العددَ المكتوبَ بالحروف ولا التاريخ؛ وصفُّ الإغلاق صفٌّ يبدأ بـ
/// <c>| ح-N |</c> ثمّ إيداعٌ بين <c>`…`</c>.</para>
/// </summary>
public class TheHeaderReachesTheLastClosureTests
{
    private const string Document = "docs/OPEN_ITEMS.md";
    private const string Section = "## 1ل.";

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

    private static string[] Lines() =>
        File.ReadAllText(Path.Combine(RepoRoot(), Document.Replace('/', Path.DirectorySeparatorChar)))
            .Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .ToArray();

    private static int ArabicIndic(string digits) =>
        digits.Aggregate(0, (n, c) => n * 10 + (int)char.GetNumericValue(c));

    /// <summary>آخرُ إغلاقٍ في §1ل — أكبرُ N في صفٍّ <c>| ح-N | `إيداع` |</c>.</summary>
    private static int LastClosure(string[] lines)
    {
        var start = Array.FindIndex(lines, l => l.StartsWith(Section, StringComparison.Ordinal));
        Assert.True(start >= 0, $"‏لا عنوانَ «{Section}» في {Document}.");
        var end = Array.FindIndex(lines, start + 1, l => l.StartsWith("## ", StringComparison.Ordinal));

        var closures = lines[start..(end < 0 ? lines.Length : end)]
            .Select(l => Regex.Match(l, @"^\| ح-([٠-٩]+) \| `[0-9a-f]{7,40}` \|"))
            .Where(m => m.Success)
            .Select(m => ArabicIndic(m.Groups[1].Value))
            .ToList();

        // ‏شاهدُ ضبط: القارئُ يرى صفوفَ الإغلاق فعلاً — وإلّا فالمقارنةُ بصفرٍ تمرّ على أيّ رأس.
        Assert.True(closures.Count > 0, $"‏لا صفَّ إغلاقٍ «| ح-N | `إيداع` |» تحت «{Section}».");
        return closures.Max();
    }

    /// <summary>
    /// <b>القرارُ في دالّةٍ واحدة</b> — ‏<c>null</c> إن بلغ المدى المكتوبُ آخرَ إغلاق، وإلّا السبب.
    /// </summary>
    internal static string? Lag(string line, int last)
    {
        var range = Regex.Match(line, "ح-١…ح-([٠-٩]+)");
        if (!range.Success)
            return $"لا مدى «ح-١…ح-N» في السطر — فلا يُعرف أيَّ إغلاقٍ يبلغ: {line}";

        var reached = ArabicIndic(range.Groups[1].Value);
        return reached == last ? null : $"يبلغ ح-{reached} والجدولُ يبلغ ح-{last}: {line}";
    }

    [Fact]
    public void TheLastUpdatedLineReachesTheLastClosure()
    {
        var lines = Lines();
        var header = lines.FirstOrDefault(l => l.StartsWith("**آخر تحديث:**", StringComparison.Ordinal));
        Assert.True(header is not null, $"‏لا سطرَ «آخر تحديث» في {Document}.");
        Assert.Null(Lag(header!, LastClosure(lines)));
    }

    [Fact]
    public void TheSectionTitleReachesTheLastClosure()
    {
        var lines = Lines();
        var title = lines.First(l => l.StartsWith(Section, StringComparison.Ordinal));
        Assert.Null(Lag(title, LastClosure(lines)));
    }

    /// <summary><b>الاختبارُ الذاتيُّ — بدالّة القرار نفسِها.</b> الصفُّ الأوّلُ الرأسُ كما كان والجدولُ عند ح-٦٤.</summary>
    [Theory]
    [InlineData("**آخر تحديث:** 2026-09-21 (جاهزيّةُ الإطلاق · **اثنان وعشرون إغلاقاً ح-١…ح-٢٢**)", 64, true)]
    [InlineData("## 1ل. جاهزيّةُ الإطلاق · اثنان وعشرون إغلاقاً (2026-09-20…21)", 64, true)]
    [InlineData("**آخر تحديث:** 2026-09-26 (**أربعةٌ وستّون إغلاقاً ح-١…ح-٦٤**)", 64, false)]
    [InlineData("**آخر تحديث:** 2026-09-26 (**أربعةٌ وستّون إغلاقاً ح-١…ح-٦٤**)", 65, true)]
    [InlineData("## 1ل. جاهزيّةُ الإطلاق · ح-١…ح-٦٤ (2026-09-20…26)", 64, false)]
    public void TheDecisionDiscriminates(string line, int last, bool lags) =>
        Assert.Equal(lags, Lag(line, last) is not null);
}
