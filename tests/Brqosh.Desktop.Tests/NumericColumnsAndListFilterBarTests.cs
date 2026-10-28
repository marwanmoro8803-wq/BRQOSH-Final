using System.IO;
using System.Text.RegularExpressions;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// أعمدة الأرقام في القوائم الرئيسية تستعمل نمط الثيم المشترك (محاذاة يمنى وأرقام بعرض ثابت)،
/// وفواتير المبيعات والمشتريات تستعمل شريط البحث والفلترة المشترك ListFilterBar.
/// </summary>
public class NumericColumnsAndListFilterBarTests
{
    private static readonly string[] ListPages =
    {
        "SalesInvoicesPage", "PurchaseInvoicesPage", "PaymentVouchersPage",
        "ReceiptVouchersPage", "CustomersPage", "ProductsPage",
    };

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

    private static string Desktop => Path.Combine(FindRepoRoot(), "src", "Brqosh.Desktop");

    private static string Page(string name) =>
        File.ReadAllText(Path.Combine(Desktop, "Views", "Pages", name + ".xaml"));

    [Fact]
    public void ThemeDefinesTheNumericCellStyles()
    {
        var theme = File.ReadAllText(Path.Combine(Desktop, "Themes", "BrqoshTheme.xaml"));
        Assert.Contains("x:Key=\"NumericCellText\"", theme, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"DataGridNumericCell\"", theme, StringComparison.Ordinal);
        Assert.Contains("Property=\"TextAlignment\"               Value=\"Right\"", theme, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryFormattedNumberColumnInTheListPagesUsesTheNumericStyle()
    {
        var comment = new Regex("<!--.*?-->", RegexOptions.Singleline);
        var column = new Regex("<DataGridTextColumn\\b[^>]*StringFormat=N\\d[^>]*>", RegexOptions.Singleline);
        var missing = new List<string>();
        foreach (var name in ListPages)
        {
            var text = comment.Replace(Page(name), string.Empty);
            var columns = column.Matches(text);
            Assert.True(columns.Count > 0, $"{name}: لا أعمدة أرقام — تغيّر الجدول؟");
            foreach (Match m in columns)
            {
                if (!m.Value.Contains("{StaticResource NumericCellText}", StringComparison.Ordinal) ||
                    !m.Value.Contains("{StaticResource DataGridNumericCell}", StringComparison.Ordinal))
                    missing.Add($"{name}: {Regex.Replace(m.Value, "\\s+", " ")}");
            }
        }

        Assert.True(missing.Count == 0, "أعمدة أرقام بلا نمط الثيم:\n" + string.Join("\n", missing));
    }

    [Theory]
    [InlineData("SalesInvoicesPage")]
    [InlineData("PurchaseInvoicesPage")]
    public void InvoiceListsUseTheSharedFilterBar(string name)
    {
        Assert.True(File.Exists(Path.Combine(Desktop, "Controls", "ListFilterBar.xaml")));
        Assert.Contains("<ctl:ListFilterBar x:Name=\"FilterBar\" FilterChanged=\"OnFilterChanged\"", Page(name), StringComparison.Ordinal);
        var code = File.ReadAllText(Path.Combine(Desktop, "Views", "Pages", name + ".xaml.cs"));
        Assert.Contains("void OnFilterChanged(", code, StringComparison.Ordinal);
    }
}
