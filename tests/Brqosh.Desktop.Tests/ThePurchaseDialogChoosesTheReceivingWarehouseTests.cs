using System.IO;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١١١ · <c>order-to-cash-procure-to-pay-inventory#12</c>) <b>نافذةُ فاتورة المشتريات تضبط مستودعَ الاستلام.</b>
/// بنيويٌّ على سُنّة ح-١١٠: الحقلُ موجودٌ ومربوطٌ بالفاتورة قبل الترحيل، ويعرض المستودعاتِ الفعّالةَ وحدَها وأوّلُها «الافتراضيّ».
/// </summary>
public class ThePurchaseDialogChoosesTheReceivingWarehouseTests
{
    /// <summary>الحقلُ في الشكل، وبانيَ الفاتورة يمرّره إلى <c>SetWarehouse</c>، والقائمةُ فعّالةٌ وأوّلُها الافتراضيّ.</summary>
    [Fact]
    public void TheDialog_BindsAWarehouseChoiceToTheInvoice()
    {
        var pages = Path.Combine(RepoRoot(), "src", "Brqosh.Desktop", "Views", "Pages");
        var xaml = File.ReadAllText(Path.Combine(pages, "PurchaseInvoiceDialog.xaml"));
        var code = File.ReadAllText(Path.Combine(pages, "PurchaseInvoiceDialog.xaml.cs"));

        Assert.Contains("x:Name=\"WarehouseCombo\"", xaml, StringComparison.Ordinal);
        Assert.Contains("invoice.SetWarehouse(WarehouseCombo.SelectedValue as long?)", code, StringComparison.Ordinal);
        Assert.Contains("w.IsActive", code, StringComparison.Ordinal);
        Assert.Contains("new(null, DefaultWarehouseChoice)", code, StringComparison.Ordinal);
        Assert.Contains("await LoadWarehousesAsync();", code, StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Brqosh.sln غير موجود فوق مجلد الاختبار.");
    }
}
