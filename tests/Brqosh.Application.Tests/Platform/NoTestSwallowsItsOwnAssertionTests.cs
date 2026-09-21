using System.Text.RegularExpressions;

namespace Brqosh.Application.Tests.Platform;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٢٢) <b>اختبارٌ يبتلع تأكيدَه يمرّ أخضرَ وهو لا يؤكّد شيئاً —
/// ولا بوّابةَ كانت تراه.</b>
///
/// <para><c>SilentFailureTests</c> يحرس الابتلاعَ الصامت في <c>src/</c> <b>وحدَها</b>،
/// <b>وشجرةُ الاختبارات خارجَ مرماه بالبناء</b>. <b>والابتلاعُ هناك أسوأ</b>: في
/// <c>src/</c> يُخفي عطلاً، <b>وفي الاختبار يُخفي أنّه لم يُقَس شيءٌ أصلاً</b> — فيُعَدّ
/// الاختبارُ في الحزمة الخضراء ويُستند إليه في حكمِ جاهزيّة.</para>
///
/// <para><b>ووقع فعلاً</b>: <c>ZatcaCsidTests.GenerateCsr_CreatesKeyAndCsrFiles</c> كان
/// جسمُه كلُّه داخل <c>try</c> ومعه <c>catch (CryptographicException) { }</c> — <b>فلو
/// عجز توليدُ مفتاح الختم لَمرّ أخضرَ</b>. وأُغلق في ح-٢٠، <b>وهذه البوّابةُ تُغلق
/// صنفَه</b>.</para>
///
/// <para><b>والقاعدةُ دقيقةٌ عمداً</b>: المخالفةُ هي <c>try</c> <b>يحتوي جسمُه على
/// <c>Assert.</c></b> ويعقبه <c>catch</c> بجسمٍ فارغ. <b>فكنسُ المجلّدات المؤقّتة لا
/// يُمَسّ</b> — ولا يلفّ أحدٌ تأكيداً بكنس. <b>وبوّابةٌ ذاتُ إنذاراتٍ كاذبةٍ تُعلِّم
/// الناسَ إضافةَ استثناءات</b>، فتصير أضرَّ من غيابها.</para>
/// </summary>
public class NoTestSwallowsItsOwnAssertionTests
{
    /// <summary><c>try { … Assert… } catch { }</c> — بجسمِ مصيدةٍ فارغٍ أو تعليقٍ فقط.</summary>
    private static readonly Regex SwallowedAssertion = new(
        @"try\s*\{(?<body>(?:[^{}]|\{(?:[^{}]|\{[^{}]*\})*\})*)\}\s*" +
        @"catch\s*(?:\([^)]*\))?\s*\{\s*(?://[^\n]*|/\*.*?\*/|\s)*\}",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static string TestsRoot()
    {
        var root = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", ".."));
        Assert.True(Directory.Exists(root), $"tests root not found: {root}");
        return root;
    }

    private static IEnumerable<string> SourceFiles() =>
        Directory.EnumerateFiles(TestsRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}TestResults{Path.DirectorySeparatorChar}"));

    /// <summary>
    /// يُفرَّغ النصُّ والتعليقُ — <b>فذِكرُ النمط ليس وقوعاً له</b>.
    ///
    /// <para><b>وأوّلُ تشغيلٍ أسقط هذا الملفَّ نفسَه</b>: كان التفريغُ يعرف
    /// <c>"…"</c> و<c>@"…"</c> <b>ولا يعرف النصَّ الخام</b> (<c>"""…"""</c>)، وشاهدُ
    /// الحارس مكتوبٌ به — <b>فقُرئ شكلُ العطل المكتوبُ للبرهان عطلاً واقعاً</b>.
    /// <b>والترتيبُ مقصود</b>: الخامُ أوّلاً، فهو يحتوي علاماتِ اقتباسٍ تُربك ما بعده.</para>
    /// </summary>
    private static string Strip(string source)
    {
        var text = Regex.Replace(source, @"^\s*///.*$", "", RegexOptions.Multiline);
        text = Regex.Replace(text, @"""{3,}.*?""{3,}", @"""""", RegexOptions.Singleline);
        text = Regex.Replace(text, @"@""(?:[^""]|"""")*""", @"""""");
        text = Regex.Replace(text, @"""(?:[^""\\\n]|\\.)*""", @"""""");
        return text;
    }

    private static List<string> Offenders(IEnumerable<(string Path, string Text)> files)
    {
        var offenders = new List<string>();

        foreach (var (path, text) in files)
            foreach (Match m in SwallowedAssertion.Matches(text))
                if (m.Groups["body"].Value.Contains("Assert.", StringComparison.Ordinal))
                    offenders.Add($"{path}:{text[..m.Index].Count(c => c == '\n') + 1}");

        return offenders;
    }

    // ── البوّابة ────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>لا تأكيدَ ملفوفٌ بمصيدةٍ تبتلع.</b>
    ///
    /// <para><b>ولا قائمةَ إقرارٍ هنا</b> — خلافاً لـ<c>SilentFailureTests</c>: ذاك يحرس
    /// دَيناً قائماً فيُقرّ به، <b>وهذا صفرٌ اليوم فيبقى صفراً</b>. <b>وإقرارٌ أوّلُ هو
    /// أوّلُ اختبارٍ يكذب.</b></para>
    /// </summary>
    [Fact]
    public void NoTestBody_WrapsItsAssertionsInASwallowingCatch()
    {
        var offenders = Offenders(
            SourceFiles().Select(f => (Path.GetFileName(f), Strip(File.ReadAllText(f)))));

        Assert.True(offenders.Count == 0,
            "اختباراتٌ تلفّ تأكيداتِها بمصيدةٍ تبتلع — تمرّ خضراءَ ولم تؤكّد شيئاً، "
            + "ويُستند إليها في حكمِ جاهزيّة:\n  " + string.Join("\n  ", offenders));
    }

    // ── حارسُ الحارس ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>والمسحُ يرى الشكلَ فعلاً — وإلّا فبوّابةٌ عمياءُ تمرّ إلى الأبد.</b>
    ///
    /// <para>الشاهدُ هو <b>شكلُ العطل الذي وقع حقّاً</b> في <c>ZatcaCsidTests</c> قبل ح-٢٠،
    /// <b>مكتوباً نصّاً هنا لا مُستحضَراً من التاريخ</b>.</para>
    /// </summary>
    [Fact]
    public void TheScan_CatchesTheShapeThatActuallyHappened()
    {
        const string theBugAsItWas = """
            [Fact]
            public void GenerateCsr_CreatesKeyAndCsrFiles()
            {
                try
                {
                    var material = service.GenerateKeyPairAndCsr(1, "x", "y");
                    Assert.True(File.Exists(material.PrivateKeyPath));
                }
                catch (CryptographicException)
                {
                    // secp256k1 may be unavailable
                }
            }
            """;

        Assert.Single(Offenders([("sample.cs", Strip(theBugAsItWas))]));
    }

    /// <summary>
    /// <b>وشاهدُ ضبط — كنسُ المؤقّتات لا يُمَسّ.</b>
    ///
    /// <para><b>وهو ما يجعل البوّابةَ صالحةً للاستعمال</b>: عشراتُ المصائد في شجرة
    /// الاختبارات تحذف مجلّداتٍ مؤقّتة، <b>ولو أُنذر عنها لَصارت القاعدةُ ضجيجاً يُستثنى
    /// منه</b> حتى يُستثنى منه العطلُ الحقيقيّ.
    /// </summary>
    [Fact]
    public void TemporaryDirectoryCleanup_IsNotReported()
    {
        const string ordinaryCleanup = """
            public void Dispose()
            {
                _db.Dispose();
                try { Directory.Delete(_workDirectory, recursive: true); }
                catch (IOException) { /* ملفٌّ مؤقّتٌ ممسوك */ }
            }
            """;

        Assert.Empty(Offenders([("sample.cs", Strip(ordinaryCleanup))]));
    }
}
