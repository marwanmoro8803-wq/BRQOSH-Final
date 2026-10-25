using System.IO;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// («دفتر الحبر والنحاس») <b>جدولٌ فارغ بلا كلمة يُقرأ عطلاً.</b> الحالةُ الفارغة (<see cref="EmptyStateHelper"/>) تُربط من
/// <c>UiTestAutomation.PublishGridCount</c> أو مباشرةً؛ وكانت خمسُ صفحاتٍ قوائمُ (أوامر البيع · عروض الأسعار · أنشطة CRM · فرص CRM ·
/// المنشآت) تُحمّل الشبكةَ وتسكت، فيرى المستخدمُ مستطيلاً أبيض. يحرس هذا الملفُّ أنّ كلَّ صفحةٍ (لا حوار) فيها <c>DataGrid</c> تربطها،
/// وأنّ اسم الشبكة العامّ «Grid» لا يطغى على نصّ الصفحة الخاصّ.
/// </summary>
public class EveryListPageShowsAnEmptyStateTests
{
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

    [Fact]
    public void EveryPageWithAGrid_WiresTheEmptyState()
    {
        var pages = Path.Combine(FindRepoRoot(), "src", "Brqosh.Desktop", "Views", "Pages");
        var missing = new List<string>();
        var checkedPages = 0;
        foreach (var xaml in Directory.EnumerateFiles(pages, "*Page.xaml"))
        {
            if (!File.ReadAllText(xaml).Contains("<DataGrid", StringComparison.Ordinal))
                continue;

            checkedPages++;
            var code = xaml + ".cs";
            var text = File.Exists(code) ? File.ReadAllText(code) : "";
            if (!text.Contains("PublishGridCount", StringComparison.Ordinal)
                && !text.Contains("EmptyStateHelper", StringComparison.Ordinal))
                missing.Add(Path.GetFileName(xaml));
        }

        Assert.True(checkedPages > 80, $"فُحصت {checkedPages} صفحةً فقط — تغيّر موضعُ الصفحات؟");
        Assert.True(missing.Count == 0, "صفحاتُ قوائمَ بلا حالةٍ فارغة: " + string.Join("، ", missing));
    }

    [Theory]
    [InlineData("sales_orders", "أوامر بيع")]
    [InlineData("sales_quotations", "عروض أسعار")]
    [InlineData("crm_activities", "أنشطة")]
    [InlineData("crm_opportunities", "فرص")]
    [InlineData("organizations", "منشآت")]
    public void TheGenericGridName_DoesNotOverrideThePageSpecificText(string pageKey, string expectedFragment)
    {
        var spec = EmptyStateCatalog.Resolve("Grid", pageKey);

        Assert.Contains(expectedFragment, spec.TitleAr, StringComparison.Ordinal);
    }

    [Fact]
    public void ANamedGrid_StillWinsWhenNoPageKeyIsGiven()
    {
        Assert.Equal("لا يوجد عملاء", EmptyStateCatalog.Resolve("CustomersGrid").TitleAr);
        Assert.Equal("لا توجد سجلات", EmptyStateCatalog.Resolve("Grid").TitleAr);
    }
}
