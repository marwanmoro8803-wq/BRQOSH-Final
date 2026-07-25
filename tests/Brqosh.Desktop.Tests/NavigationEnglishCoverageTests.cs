using Brqosh.Desktop.Navigation;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (CEEF-223، O-25) كل عنصر تنقّل وكل عنوان مجموعة له ترجمة إنجليزية حقيقية.
///
/// <c>PageTitleEn</c> و<c>GroupTitleEn</c> يرجعان **المدخل كما هو** عند غياب الترجمة:
/// مفتاحاً تقنياً (<c>vat_return</c>) أو النصّ العربيّ نفسه. فالنقص لا يظهر خطأً —
/// يظهر شريطَ تنقّل نصفه عربيّ ونصفه إنجليزيّ، وهو ما رُصد في المسح الحيّ
/// (`dist/ui-sweep`): كل عناوين المجموعات وعدّة عناصر بقيت عربية بعد التبديل.
///
/// الحارس يقرأ الكتالوج الحيّ لا قائمةً منسوخة، فأيّ صفحة جديدة بلا ترجمة تُسقطه.
/// </summary>
public class NavigationEnglishCoverageTests
{
    private static bool HasArabic(string text) =>
        text.Any(ch => ch is >= '؀' and <= 'ۿ');

    [Fact]
    public void EveryNavigationPage_HasEnglishTitle()
    {
        var missing = new List<string>();

        foreach (var group in NavigationCatalog.Groups)
        {
            foreach (var page in group.Pages)
            {
                var en = NavigationCatalog.PageTitleEn(page.Key);

                if (string.Equals(en, page.Key, StringComparison.Ordinal))
                    missing.Add($"{page.Key} → (لا مدخل — يُعرض المفتاح التقني) «{page.TitleAr}»");
                else if (HasArabic(en))
                    missing.Add($"{page.Key} → «{en}» (عربيّ)");
            }
        }

        Assert.True(missing.Count == 0,
            $"عناصر تنقّل بلا ترجمة إنجليزية ({missing.Count}):\n" +
            string.Join("\n", missing.Select(x => "  - " + x)));
    }

    [Fact]
    public void EveryNavigationGroup_HasEnglishTitle()
    {
        var missing = new List<string>();

        foreach (var group in NavigationCatalog.Groups)
        {
            var en = NavigationCatalog.GroupTitleEn(group.TitleAr);
            if (string.Equals(en, group.TitleAr, StringComparison.Ordinal) || HasArabic(en))
                missing.Add($"«{group.TitleAr}» → «{en}»");
        }

        Assert.True(missing.Count == 0,
            $"عناوين مجموعات بلا ترجمة إنجليزية ({missing.Count}):\n" +
            string.Join("\n", missing.Select(x => "  - " + x)));
    }

    /// <summary>حارس للحارس: الكتالوج غير فارغ — وإلا مرّ الاختباران بلا فحص.</summary>
    [Fact]
    public void Catalog_IsNotEmpty()
    {
        var pages = NavigationCatalog.Groups.Sum(g => g.Pages.Count);
        Assert.True(NavigationCatalog.Groups.Count >= 10, $"عدد المجموعات {NavigationCatalog.Groups.Count}.");
        Assert.True(pages >= 60, $"عدد الصفحات {pages} — يُتوقع 60+.");
    }
}
