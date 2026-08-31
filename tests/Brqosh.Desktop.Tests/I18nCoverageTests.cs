using System.IO;
using System.Text.RegularExpressions;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (CEEF-213) تغطية التوطين: كل نصّ عربيّ يُعرض في XAML له ترجمة في السجل.
///
/// `XamlUiLocalizer` يترجم وقت التشغيل بالبحث في `UiBilingualRegistry`؛ فنصّ بلا مدخل
/// **يبقى عربياً عند التبديل للإنجليزية** — واجهة نصف مترجَمة. القياس عند كتابة هذا
/// الحارس: 13 نصاً بلا ترجمة، منها عمود «الفئة الضريبية» المضاف حديثاً.
///
/// مطبّ مثبت: نصّان قد يبدوان متطابقين ويختلفان في **تشكيل خفيّ** (شدّة U+0651 مقابل
/// فتحة U+064E). المقارنة هنا حرفية بالضبط كما يفعل البحث وقت التشغيل.
/// </summary>
public class I18nCoverageTests
{
    private static readonly Regex DisplayAttribute = new(
        @"(?:Header|Content|Text|Title|ToolTip|Watermark|PlaceholderText)=""([^""]*[؀-ۿ][^""]*)""",
        RegexOptions.Compiled);

    /// <summary>
    /// (CEEF-229 · م2-٢٣) إسنادُ نصٍّ عربيّ إلى خاصيّة عنصرٍ من <b>الشيفرة الخلفية</b>.
    ///
    /// <para><b>كانت البوّابةُ عمياء عنه.</b> تمسح الوسومَ وحدها، فنصٌّ يُضبط في
    /// <c>.xaml.cs</c> يبقى عربياً عند التبديل بلا شكوى. وكشفه أنّي وسمتُ صنفاً موقوفاً
    /// بكلمة «موقوف» هناك فمرّت البوّابةُ صامتة، ونقلُها إلى XAML أسقطها فوراً.</para>
    ///
    /// <para><b>والمسحُ محدودٌ بخصائص العناصر عمداً:</b> <c>MessageBox.Show</c> نافذةُ Win32
    /// لا عنصرٌ في شجرة الواجهة، ولا يبلغها <c>XamlUiLocalizer</c> مهما وُضع في السجلّ.
    /// فإدخالُ نصوصها هنا يُخضِّر البوّابةَ والنصُّ عربيٌّ كما هو — أي يجعل الحارسَ يكذب.
    /// وعددُها مقيسٌ (‏144‎) ومسجَّلٌ بنداً مفتوحاً، وعلاجُها آليّةٌ أخرى.</para>
    ///
    /// <para>ويُقابله مسحٌ نظيرٌ في <c>tools/I18nRegistryGen</c> — فالمولّدُ يملأ السجلّ
    /// وهذه البوّابةُ تقيسه، وهما ماسحان مستقلّان يجب أن يريا الشيءَ نفسه.</para>
    /// </summary>
    private static readonly Regex CodeBehindAssignment = new(
        @"\.(?:Text|Content|Header|Title|ToolTip|PlaceholderText)\s*=\s*""([^""]*[؀-ۿ][^""]*)""",
        RegexOptions.Compiled);

    private static string DesktopRoot()
    {
        var root = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Brqosh.Desktop"));
        Assert.True(Directory.Exists(root), $"Desktop source root not found: {root}");
        return root;
    }

    private static IReadOnlyList<string> ArabicDisplayLiterals()
    {
        var literals = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(DesktopRoot(), "*.xaml", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                continue;

            foreach (Match m in DisplayAttribute.Matches(File.ReadAllText(file)))
            {
                var text = m.Groups[1].Value.Trim();
                if (text.Length > 0)
                    literals.Add(text);
            }
        }

        foreach (var file in Directory.EnumerateFiles(DesktopRoot(), "*.cs", SearchOption.AllDirectories))
        {
            if (file.EndsWith(".g.cs", StringComparison.Ordinal) ||
                file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                continue;

            var source = File.ReadAllText(file);

            foreach (Match m in CodeBehindAssignment.Matches(source))
            {
                var text = m.Groups[1].Value.Trim();
                // ‏المُستكمَلات (‏$"…{x}…"‎) لا تصلح مفتاحاً ثابتاً، فتُستبعد كما يستبعدها المولّد.
                if (text.Length > 0 && !text.Contains('{') && !text.Contains('\\'))
                    literals.Add(text);
            }

            foreach (var text in LiteralsPassedToUiHelpers(source))
                literals.Add(text);
        }

        return literals.ToList();
    }

    /// <summary>لا نصّ معروض يبقى عربياً بعد التبديل للإنجليزية.</summary>
    [Fact]
    public void EveryArabicDisplayLiteral_HasEnglishTranslation()
    {
        var literals = ArabicDisplayLiterals();
        Assert.NotEmpty(literals);   // حارس للحارس: لو انكسر الاستخراج لا يمرّ الاختبار صامتاً

        // (CEEF-229 · م2-٢٣) **وحارسٌ للثقب الذي أُغلق للتوّ.**
        //
        // «تم الحفظ.» نصٌّ لا يرد في أيّ وسم XAML — مصدرُه الشيفرة الخلفية وحدها. فوجودُه
        // هنا دليلٌ حيّ على أنّ المسحَ ما زال يبلغها. ولولاه لكفى حذفُ حلقة `*.cs` أعلاه
        // ليعود الثقبُ صامتاً والبوّابةُ خضراء — وهو بالضبط ما كان عليه الحال قبل هذه الدفعة.
        Assert.Contains("تم الحفظ.", literals);

        var untranslated = literals
            .Where(l => ReferenceEquals(UiBilingualRegistry.Lookup(l), l) ||
                        UiBilingualRegistry.Lookup(l) == l)
            .OrderBy(l => l, StringComparer.Ordinal)
            .ToList();

        Assert.True(untranslated.Count == 0,
            $"{untranslated.Count} من {literals.Count} نصّاً بلا ترجمة — تبقى عربية عند التبديل:\n" +
            string.Join("\n", untranslated.Select(u => "  - " + u)));
    }

    /// <summary>الترجمة فعلية لا صدى للنصّ العربي نفسه.</summary>
    [Fact]
    public void Translations_AreActuallyEnglish_NotEchoes()
    {
        foreach (var literal in ArabicDisplayLiterals())
        {
            var english = UiBilingualRegistry.Lookup(literal);
            Assert.False(string.IsNullOrWhiteSpace(english), $"ترجمة فارغة لـ: {literal}");
            Assert.NotEqual(literal, english);
        }
    }
    /// <summary>
    /// (CEEF-229) <b>نصٌّ يُمرَّر وسيطاً إلى دالّةٍ تُسنده إلى خاصيّةِ عرض — وكانت البوّابةُ عمياءَ عنه.</b>
    ///
    /// <para><b>ما كشفه:</b> كانت البوّابةُ تمسح شكلين نحويّين فقط — سمةَ XAML
    /// (<c>Text="…"</c>) وإسناداً مباشراً (<c>.Text = "…"</c>) — وتُعلن <b>«100.0% · صفرُ
    /// غير مترجَم»</b>. وشاشةُ الإقرار الضريبيّ تُمرّر كلَّ صفوفها وسائطَ إلى <c>AddRow</c>،
    /// <b>فلم تُترجَم قطّ</b>: شاشةٌ كاملة تبقى عربيةً عند التبديل والبوّابةُ تقول مئة بالمئة.</para>
    ///
    /// <para><b>والقاعدةُ قفزةٌ واحدة داخل الملفّ نفسه:</b> تُلتقط الدوالُّ التي أوّلُ وسيطها
    /// نصٌّ ويُسنَد إلى <c>Text</c> أو <c>Content</c> أو <c>Header</c> — وهي الخصائصُ التي
    /// يترجمها <c>XamlUiLocalizer</c> فعلاً — ثمّ تُلتقط نداءاتُها بنصٍّ عربيّ حرفيّ.</para>
    ///
    /// <para><b>وحدُّها مُصرَّحٌ به:</b> نصٌّ يبلغ الشجرةَ عبر سجلٍّ أو مجموعةٍ أو قفزتين لا
    /// تراه، ونصُّ <c>MessageBox</c> لا يترجمه المُشغِّل أصلاً لأنّه خارج شجرة العناصر.
    /// <b>فالبوّابةُ صارت أصدقَ لا كاملة.</b></para>
    /// </summary>
    private static IEnumerable<string> LiteralsPassedToUiHelpers(string source)
    {
        var helpers = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match declaration in UiHelperDeclaration.Matches(source))
        {
            var parameter = declaration.Groups[2].Value;
            var start = declaration.Index + declaration.Length;
            var body = source[start..Math.Min(source.Length, start + 1500)];

            foreach (Match assignment in UiPropertyFromVariable.Matches(body))
            {
                if (assignment.Groups[2].Value == parameter)
                {
                    helpers.Add(declaration.Groups[1].Value);
                    break;
                }
            }
        }

        foreach (var helper in helpers)
        {
            var call = new Regex(
                Regex.Escape(helper) + @"\(\s*""([^""\\n]*[\u0600-\u06FF][^""\\n]*)""",
                RegexOptions.Compiled);

            foreach (Match m in call.Matches(source))
            {
                var text = m.Groups[1].Value.Trim();
                if (text.Length > 0 && !text.Contains('{'))
                    yield return text;
            }
        }
    }

    private static readonly Regex UiHelperDeclaration = new(
        @"(?:private|public|internal|protected)[^\n(]*\s(\w+)\s*\(\s*string\s+(\w+)[^)]*\)\s*\{",
        RegexOptions.Compiled);

    private static readonly Regex UiPropertyFromVariable = new(
        @"\b(Text|Content|Header)\s*=\s*(\w+)\b",
        RegexOptions.Compiled);

}
