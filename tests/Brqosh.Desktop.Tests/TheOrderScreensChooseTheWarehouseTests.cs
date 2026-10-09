using System.IO;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١١٣ · <c>order-to-cash-procure-to-pay-inventory#12</c>) <b>شاشتا أمر البيع وأمر الشراء تختاران المستودعَ عند الفوترة.</b>
/// بنيويٌّ على سُنّة ح-١١٠: الخدمتان تقبلان <c>warehouseId</c> والشاشتان كانتا تستدعيانهما بلا مستودع فتقع الفاتورةُ على الافتراضيّ أبداً.
/// </summary>
public class TheOrderScreensChooseTheWarehouseTests
{
    [Theory]
    [InlineData("SalesOrdersPage")]
    [InlineData("PurchaseOrdersPage")]
    public void TheOrderPage_OffersActiveWarehousesAndPassesTheChoiceToTheService(string page)
    {
        var pages = Path.Combine(RepoRoot(), "src", "Brqosh.Desktop", "Views", "Pages");
        var xaml = File.ReadAllText(Path.Combine(pages, page + ".xaml"));
        var code = File.ReadAllText(Path.Combine(pages, page + ".xaml.cs"));

        Assert.Contains("x:Name=\"WarehouseCombo\"", xaml, StringComparison.Ordinal);
        Assert.Contains("w.IsActive", code, StringComparison.Ordinal);
        Assert.Contains("new(null, DefaultWarehouseChoice)", code, StringComparison.Ordinal);
        Assert.Contains("WarehouseCombo.SelectedIndex = 0;", code, StringComparison.Ordinal);
        Assert.Contains("var warehouseId = WarehouseCombo.SelectedValue as long?;", code, StringComparison.Ordinal);

        // كلُّ استدعاءٍ لـCreateInvoiceFromOrderAsync يمرّر المستودعَ المختار.
        var calls = 0;
        for (var i = code.IndexOf("CreateInvoiceFromOrderAsync(", StringComparison.Ordinal); i >= 0;
             i = code.IndexOf("CreateInvoiceFromOrderAsync(", i + 1, StringComparison.Ordinal))
        {
            var end = code.IndexOf(';', i);
            Assert.Contains("warehouseId: warehouseId", code[i..end], StringComparison.Ordinal);
            calls++;
        }
        Assert.True(calls >= 1, "لا استدعاءَ لـCreateInvoiceFromOrderAsync في " + page);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Brqosh.sln غير موجود فوق مجلد الاختبار.");
    }
}
