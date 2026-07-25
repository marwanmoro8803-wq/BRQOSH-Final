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

        return literals.ToList();
    }

    /// <summary>لا نصّ معروض يبقى عربياً بعد التبديل للإنجليزية.</summary>
    [Fact]
    public void EveryArabicDisplayLiteral_HasEnglishTranslation()
    {
        var literals = ArabicDisplayLiterals();
        Assert.NotEmpty(literals);   // حارس للحارس: لو انكسر الاستخراج لا يمرّ الاختبار صامتاً

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
}
