using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Xunit.Sdk;

namespace Brqosh.Application.Tests.Platform;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٦٥ · evidence-integrity#7) <b>صفٌّ في <c>OPEN_ITEMS</c> يقول «ضمنَ الحزمة» يُسمّي صنفاً
/// في الحزمة — بعدد حالاته.</b>
///
/// <para><b>ما كان:</b> §3 «آخر قياس تشغيلي» نشر «FullScenario <b>13/13</b> — ضمنَ الحزمة الكاملة الخضراء». وأسماءُ
/// الحزمة كلُّها (<b>٢٦٤٤</b> حالةً عند <c>e2f2a41</c>، بـ<c>dotnet test --list-tests</c> في المشاريع الأربعة) <b>لا
/// اختبارَ فيها اسمُه FullScenario</b> — المطابقةُ الوحيدةُ صفُّ نظريّةٍ في <c>LiveBooksGuardTests</c> يذكر مسارَ الأداة.
/// والمقصودُ <c>tools/FullScenarioAudit</c>: أداةٌ يدويّةٌ خارجَ الحزمة تفتح دفاترَ المُشغِّل، وخطوتُها في
/// <c>run-external-gates.ps1</c> <c>[SKIP]</c> بتصميم ن-٢٢. <b>وشاهدُ الضبط من الصنف نفسِه</b>: البحثُ نفسُه يجد
/// <c>GoldenAccountingIntegrityTests</c> بثمانٍ، وهو ما يقوله صفُّ «Golden Integrity 8/8» بجواره.</para>
///
/// <para><b>فالشرط:</b> صفُّ جدولٍ يدّعي «ضمن الحزمة» يحمل اسمَ صنفِ الاختبار بين <c>`…`</c> وعدداً <c>n/n</c>،
/// والصنفُ في هذه التجميعة وحالاتُه n — <b>فالعددُ يُقرأ من الحزمة لا من ذاكرة كاتبه</b>. ويُقارَن النصُّ بعد نزع
/// التشكيل، وإلّا مرّ الادّعاءُ نفسُه مكتوباً بلا فتحة.</para>
///
/// <para><b>وحدُّه مكتوب:</b> يرى تجميعةَ <c>Brqosh.Application.Tests</c> وحدَها، ولا يحرس ما لا يدّعي الحزمةَ
/// (التغطية، المسابير، الأدوات اليدويّة) — تلك تُؤرَّخ بإيداعها في نصّها.</para>
/// </summary>
public class TheLastMeasurementNamesWhatTheSuiteRunsTests
{
    private const string Document = "docs/OPEN_ITEMS.md";
    private const string InSuite = "ضمن الحزمة";

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

    /// <summary>النصُّ بلا تشكيلٍ ولا محارفِ اتّجاهٍ ولا تطويل — فلا يمرّ «ضمنَ» حيث يُرفض «ضمن».</summary>
    private static string Bare(string text) =>
        new(text.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) is not (UnicodeCategory.NonSpacingMark or UnicodeCategory.Format)
                        && c != 'ـ')
            .ToArray());

    /// <summary>صفوفُ الجداول كلُّها — والمقتبَسُ منها (<c>&gt; |</c>) معها.</summary>
    private static List<string> TableRows(string markdown) =>
        markdown.Split('\n')
            .Select(line => line.TrimEnd('\r').TrimStart('>', ' '))
            .Where(line => line.StartsWith('|'))
            .ToList();

    /// <summary>
    /// حالاتُ صنفٍ كما يعدّها xUnit: <c>[Fact]</c> حالةٌ، و<c>[Theory]</c> بعدد صفوف <c>[InlineData]</c>.
    /// <c>null</c> إن لم يكن الصنفُ في هذه التجميعة.
    /// </summary>
    internal static int? CasesOf(string className)
    {
        var matches = typeof(TheLastMeasurementNamesWhatTheSuiteRunsTests).Assembly.GetTypes()
            .Where(t => t.Name == className)
            .ToList();
        if (matches.Count == 0) return null;
        if (matches.Count > 1)
            throw new InvalidOperationException($"‏«{className}» اسمٌ لـ{matches.Count} أصناف — والصفُّ لا يُعيّن أيَّها.");

        var cases = 0;
        foreach (var method in matches[0].GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
        {
            var fact = method.GetCustomAttributes<FactAttribute>(inherit: true).FirstOrDefault();
            if (fact is null) continue;
            if (fact is not TheoryAttribute)
            {
                cases++;
                continue;
            }

            var sources = method.GetCustomAttributes<DataAttribute>(inherit: true).ToList();
            if (sources.Any(s => s is not InlineDataAttribute))
                throw new InvalidOperationException(
                    $"‏«{className}.{method.Name}» نظريّةٌ بمصدرٍ غير [InlineData] — عددُ حالاتها لا يُعرف ساكناً.");
            cases += sources.Count;
        }

        return cases;
    }

    /// <summary>
    /// <b>القرارُ في دالّةٍ واحدة</b> — يستدعيها الفحصُ والاختبارُ الذاتيُّ معاً. ‏<c>null</c> إن لم يدّعِ الصفُّ
    /// الحزمةَ أو صدق ادّعاؤه، وإلّا سببُ الرفض.
    /// </summary>
    internal static string? Refusal(string row, Func<string, int?> casesOf)
    {
        if (!Bare(row).Contains(InSuite, StringComparison.Ordinal)) return null;

        var named = Regex.Matches(row, "`([A-Za-z_][A-Za-z0-9_]*Tests)`")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (named.Count != 1)
            return $"يدّعي الحزمةَ ويُسمّي {named.Count} صنفاً بين `…` لا واحداً: {row}";

        var count = Regex.Match(row, @"(?<![0-9])([0-9]+)/([0-9]+)(?![0-9])");
        if (!count.Success || count.Groups[1].Value != count.Groups[2].Value)
            return $"يدّعي الحزمةَ بلا عددٍ n/n: {row}";

        var claimed = int.Parse(count.Groups[1].Value, CultureInfo.InvariantCulture);
        var actual = casesOf(named[0]);
        if (actual is null)
            return $"«{named[0]}» ليس صنفاً في Brqosh.Application.Tests: {row}";
        return actual == claimed ? null : $"«{named[0]}» فيه {actual} حالةً والصفُّ يقول {claimed}: {row}";
    }

    [Fact]
    public void EveryRowThatClaimsTheSuiteNamesAClassItHas()
    {
        var path = Path.Combine(RepoRoot(), Document.Replace('/', Path.DirectorySeparatorChar));
        var rows = TableRows(File.ReadAllText(path));

        // ‏شاهدُ ضبط: الفحصُ يرى صفّاً يدّعي الحزمةَ فعلاً — وإلّا فالخضرةُ «لم يُفحص شيء».
        var claims = rows.Where(r => Bare(r).Contains(InSuite, StringComparison.Ordinal)).ToList();
        Assert.True(claims.Count > 0,
            $"‏لا صفَّ في {Document} يدّعي «{InSuite}» — فهذا الحارسُ لا يحرس شيئاً. احذفه عمداً أو أصلح قراءتَه.");

        var refusals = claims.Select(r => Refusal(r, CasesOf)).OfType<string>().ToList();
        Assert.True(refusals.Count == 0,
            $"‏{refusals.Count} صفّاً يدّعي الحزمةَ بما ليس فيها:\n" + string.Join("\n", refusals));
    }

    /// <summary>
    /// <b>الاختبارُ الذاتيُّ — ويمرّ بدالّة القرار نفسِها وبالعدّاد الحقيقيّ.</b> الصفُّ الأوّلُ نصُّ §3 كما كان.
    /// </summary>
    [Theory]
    [InlineData("| FullScenario | **13/13** — *ضمنَ الحزمة الكاملة الخضراء* |", true)]
    [InlineData("| FullScenario | **13/13** — ضمن الحزمة |", true)]
    [InlineData("| Golden Integrity | `GoldenAccountingIntegrityTests` **8/8** — *ضمنَ الحزمة الكاملة الخضراء* |", false)]
    [InlineData("| Golden Integrity | `GoldenAccountingIntegrityTests` **9/9** — *ضمنَ الحزمة الكاملة الخضراء* |", true)]
    [InlineData("| Golden Integrity | `GoldenAccountingIntegrityTests` **7/8** — *ضمنَ الحزمة الكاملة الخضراء* |", true)]
    [InlineData("| سيناريو | `NoSuchScenarioTests` **13/13** — *ضمنَ الحزمة* |", true)]
    [InlineData("| ترجمةُ الواجهة | **100٪** · صفرُ نصٍّ بلا ترجمة |", false)]
    [InlineData("| FullScenarioAudit | **NOT TESTED** — أداةٌ يدويّةٌ خارجَ الحزمة |", false)]
    public void TheDecisionDiscriminates(string row, bool refused) =>
        Assert.Equal(refused, Refusal(row, CasesOf) is not null);
}
