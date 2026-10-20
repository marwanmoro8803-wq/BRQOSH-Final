using System.IO;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (جاهزيّةُ الإطلاق · <c>sector-modules#11</c>) <b>شاشةُ أوامر الصيانة تُرحّل بفاتورة المورّد المختارة</b> — فالخدمةُ وحدَها لا تُغلق
/// البند: بلا اختيارٍ في الشاشة يبقى كلُّ أمرٍ يُدفع نقداً بلا ضريبة مدخلات. ويُقاس بنيةً كأخواته.
/// </summary>
public class TheMaintenanceScreenPostsFromTheSupplierInvoiceTests
{
    private static string Source(string file)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(dir!.FullName, "src", "Brqosh.Desktop", "Views", "Pages", file));
    }

    [Fact]
    public void ThePostButton_PassesTheChosenSupplierInvoice()
    {
        Assert.Contains("x:Name=\"SupplierInvoiceBox\"", Source("MaintenanceOrdersPage.xaml"), StringComparison.Ordinal);
        Assert.Contains("purchaseInvoiceId: (SupplierInvoiceBox.SelectedItem as SupplierInvoiceOption)?.Id",
            Source("MaintenanceOrdersPage.xaml.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheList_OffersOnlyPostedUnusedInvoices_AfterTheCashChoice()
    {
        var text = Source("MaintenanceOrdersPage.xaml.cs");

        Assert.Contains("PurchaseInvoiceStatus.Posted", text, StringComparison.Ordinal);
        Assert.Contains("!used.Contains(i.Id)", text, StringComparison.Ordinal);
        Assert.Contains("new SupplierInvoiceOption(null,", text, StringComparison.Ordinal);
    }
}
