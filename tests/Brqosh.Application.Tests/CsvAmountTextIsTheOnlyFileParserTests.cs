using System.IO;
using System.Text.RegularExpressions;

namespace Brqosh.Application.Tests;

/// <summary>
/// ‏(م-٥ · ن-٤٨) <b>بوّابةُ مواضعِ الاستدعاء في البنية التحتيّة — الحدُّ الذي أعلنته
/// بوّابةُ سطح المكتب عن نفسِها.</b>
///
/// <para><c>NumericInputIsTheOnlyParserTests</c> (م-٣ · ن-٢٩) سمَّى هذا العطلَ بعينِه —
/// «<c>1,5</c> تُقرأ <b>15</b> بسبب <c>AllowThousands</c>» — <b>ثمّ كتب نطاقَه بيده</b>:
/// «ونطاقُ البوّابة <b>سطحُ المكتب وحدَه</b> … أمّا الواجهةُ البرمجيّة فمدخلاتُها JSON
/// مُنمَّطة، <b>ولا تمرّ بتحليلِ نصٍّ حرّ</b>». <b>وفي تلك الجملة بالضبط نجا ن-٤٨</b>:
/// ملفُّ CSV ليس نصّاً يكتبه إنسانٌ في حقل، ولا JSON مُنمَّطاً — <b>بل صنفٌ ثالث</b>.</para>
///
/// <para><b>ولمَ بوّابةٌ نصّيّةٌ لا اختبارُ سلوك:</b> اختبارُ السلوك يقيس ما يُستدعى ولا يرى
/// ما لا يُستدعى. <b>ومستوردٌ جديدٌ يُكتب غداً</b> — كشفُ رواتب، ورقةُ جرد، قائمةُ أسعارِ
/// مورّد — <b>بـ<c>decimal.TryParse</c> عارٍ يمرّ بمجموعةٍ خضراءَ كاملة</b>، كما مرّ هذا.</para>
///
/// <para><b>وهذه البوّابةُ تُعلن حدَّها هي أيضاً</b>، لئلّا يُقرأ خُضرتُها أوسعَ ممّا تقيس:
/// تقيس <b>تحويلَ نصٍّ إلى رقمٍ أو تاريخٍ بلا ثقافةٍ صريحة</b> داخل
/// <c>src/Brqosh.Infrastructure</c> وحدَها. <b>ولا تقيس</b> شكلَ الصفّ ولا الفواصلَ ولا
/// أيَّ مستوردٍ آخرَ في المستودع — ولا تدّعي أنّ كلَّ استيرادٍ صار سليماً.</para>
/// </summary>
public class CsvAmountTextIsTheOnlyFileParserTests
{
    /// <summary>‏تحويلُ نصٍّ إلى عددٍ كسريّ — بأيّ من صيغتَيه.</summary>
    private static readonly Regex BareNumericParse = new(
        @"\b(?:decimal|double|float|Single)\.(?:Try)?Parse\b|\bConvert\.To(?:Decimal|Double|Single)\b",
        RegexOptions.Compiled);

    /// <summary>‏تحويلُ نصٍّ إلى تاريخٍ <b>بلا وسيطِ ثقافة</b> — <c>(value, out x)</c>.</summary>
    private static readonly Regex CultureLessDateParse = new(
        @"\b(?:DateOnly|DateTime|DateTimeOffset)\.Try(?:Parse|ParseExact)\([^,]+,\s*out\b",
        RegexOptions.Compiled);

    private static readonly string TheOnlyLegitimateCaller =
        Path.Combine("Treasury", "CsvAmountText.cs");

    // ── القياس ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>لا ملفَّ في البنية التحتيّة يُحوّل نصَّ ملفٍّ إلى رقمٍ بنداءٍ عارٍ</b> — إلّا
    /// الحارسَ نفسَه. والرسالةُ تُسمّي المخالفَ بملفِّه وسطرِه ونصِّه.
    /// </summary>
    [Fact]
    public void NoInfrastructureFile_TurnsTextIntoANumberOutsideTheGuard()
    {
        var offenders = Offenders(BareNumericParse, skipTheGuard: true);

        Assert.True(offenders.Count == 0,
            $"‏{offenders.Count} موضعاً يُحوّل نصّاً إلى رقمٍ خارجَ CsvAmountText.\n"
            + "‏و«1,5» تُقرأ 15 و«1.234,56» تُقرأ 1.23456 — بلا خطأٍ ولا تحذير.\n\n"
            + string.Join("\n", offenders));
    }

    /// <summary>
    /// <b>ولا تاريخَ يُقرأ بإعدادات ويندوز.</b> فـ«2026-01-05» تصير <c>2647-03-25</c> على
    /// <c>fa-IR</c> و<c>1483-01-05</c> على <c>th-TH</c>، <b>وتُرفَض على <c>ar-SA</c></b> —
    /// والنتيجةُ دالّةٌ في جهاز المُشغّل لا في الملفّ.
    /// </summary>
    [Fact]
    public void NoInfrastructureFile_ReadsADateWithTheMachineCulture()
    {
        var offenders = Offenders(CultureLessDateParse, skipTheGuard: false);

        Assert.True(offenders.Count == 0,
            $"‏{offenders.Count} موضعاً يقرأ تاريخاً بلا ثقافةٍ صريحة — فالنتيجةُ تتبدّل "
            + "‏بتبدّل إعدادات الجهاز:\n\n" + string.Join("\n", offenders));
    }

    // ── شواهدُ الضبط — وبدونها «صفرُ مخالفين» خبرٌ عن الماسح لا عن الكود ──────

    /// <summary>‏الحارسُ المُسمّى موجودٌ فعلاً — وإلّا فالاستثناءُ يستثني عدماً.</summary>
    [Fact]
    public void TheNamedGuard_ExistsOnDisk()
    {
        var guard = Path.Combine(InfrastructureRoot(), TheOnlyLegitimateCaller);
        Assert.True(File.Exists(guard), $"‏الحارسُ نفسُه غيرُ موجود: {guard}");
    }

    /// <summary>
    /// <b>والماسحُ يجد النداءَ الشرعيَّ داخلَه.</b> فلو عمي الماسحُ لكان «صفرُ مخالفين»
    /// في الاختبار الأوّل خبراً عن الماسح لا عن الكود.
    /// </summary>
    [Fact]
    public void TheScanner_FindsTheOneLegitimateCall_SoAnEmptyResultMeansSomething()
    {
        var guard = Path.Combine(InfrastructureRoot(), TheOnlyLegitimateCaller);
        var hits = MatchesIn(guard, BareNumericParse);

        Assert.True(hits.Count > 0,
            "‏لم يجد الماسحُ تحويلاً عددياً داخل CsvAmountText.cs — فالماسحُ أعمى.");
    }

    /// <summary>‏والشجرةُ المفحوصةُ ليست فارغة — مسارٌ خاطئٌ يُعطي صفرَ ملفّاتٍ وصفرَ مخالفين.</summary>
    [Fact]
    public void TheScan_ActuallyReachesTheInfrastructureSources()
    {
        var files = InfrastructureSources();

        Assert.True(files.Count > 100,
            $"‏الماسحُ رأى {files.Count} ملفّاً فقط في البنية التحتيّة — المسارُ مشكوكٌ فيه.");
    }

    // ── اختبارٌ ذاتيٌّ: أيميّز النمطُ أصلاً؟ ─────────────────────────────────

    [Theory]
    [InlineData("var ok = decimal.TryParse(s, out var v);", true)]
    [InlineData("var ok = decimal.Parse(s);", true)]
    [InlineData("var ok = double.TryParse(s, out var v);", true)]
    [InlineData("var ok = Convert.ToDecimal(cell);", true)]
    [InlineData("var ok = Convert.ToDouble(cell);", true)]
    [InlineData("var ok = int.TryParse(s, out var v);", false)]       // ‏صحيحٌ لا كسريّ
    [InlineData("var ok = Convert.ToInt32(cell);", false)]
    [InlineData("var ok = CsvAmountText.Parse(s, 1, \"Amount\");", false)]
    public void TheNumericPattern_MatchesADecimalParseAndNothingElse(string line, bool expected) =>
        Assert.Equal(expected, BareNumericParse.IsMatch(line));

    [Theory]
    [InlineData("DateOnly.TryParse(v, out var d)", true)]
    [InlineData("DateTime.TryParse(payload.Date, out var d)", true)]
    [InlineData("DateOnly.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)", false)]
    [InlineData("DateOnly.TryParseExact(v, accepted, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)", false)]
    public void TheDatePattern_MatchesOnlyACultureLessParse(string line, bool expected) =>
        Assert.Equal(expected, CultureLessDateParse.IsMatch(line));

    // ── الماسح ───────────────────────────────────────────────────────────────

    private static List<string> Offenders(Regex pattern, bool skipTheGuard)
    {
        var root = InfrastructureRoot();
        var guard = Path.Combine(root, TheOnlyLegitimateCaller);
        var found = new List<string>();

        foreach (var file in InfrastructureSources())
        {
            if (skipTheGuard && string.Equals(file, guard, StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var (line, text) in MatchesIn(file, pattern))
                found.Add($"  {Path.GetRelativePath(root, file)}:{line}  {text}");
        }

        return found;
    }

    /// <summary>
    /// ‏<b>والتعليقاتُ تُستثنى</b> — على العرف المكتوب في البوّابة الشقيقة: تعليقُ الإصلاح
    /// يشرح العطلَ بنصِّه، <b>وشرحُ عطلٍ ليس إعادةً له</b>، وهو ما يمنع «تبسيطاً» يُعيده.
    /// </summary>
    private static List<(int Line, string Text)> MatchesIn(string file, Regex pattern)
    {
        var hits = new List<(int, string)>();
        var lines = File.ReadAllLines(file);

        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith("//", StringComparison.Ordinal)) continue;
            if (trimmed.StartsWith("*", StringComparison.Ordinal)) continue;

            if (pattern.IsMatch(lines[i]))
                hits.Add((i + 1, trimmed));
        }

        return hits;
    }

    private static List<string> InfrastructureSources() =>
        Directory.EnumerateFiles(InfrastructureRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !f.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();

    private static string InfrastructureRoot()
    {
        var root = Path.Combine(FindRepoRoot(), "src", "Brqosh.Infrastructure");
        Assert.True(Directory.Exists(root), $"Infrastructure source root not found: {root}");
        return root;
    }

    /// <summary>‏رميةٌ لا تخطٍّ صامت: حارسٌ لا يجد ما يحرسه يجب أن يسقط، لا أن يمرَّ أخضر.</summary>
    private static string FindRepoRoot()
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
}
