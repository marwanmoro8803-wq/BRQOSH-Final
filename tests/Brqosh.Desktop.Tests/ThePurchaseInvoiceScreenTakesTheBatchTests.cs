using System.IO;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١٠٨ · <c>order-to-cash-procure-to-pay-inventory#7</c>) <b>شاشةُ فاتورة الشراء تأخذ الدفعةَ وانتهاءها.</b>
///
/// <para>‏بلا العمودين لا يُشترى من الشاشة صنفٌ متتبَّعٌ مباشرةً أبداً، ولو قبله النطاق. <b>والقياسُ في النصّ</b> — فالحوارُ يقرأ
/// القاعدةَ من <c>AppServices</c>، وفتحُه في اختبارٍ يبلغ دفاترَ الإنتاج.</para>
/// </summary>
public class ThePurchaseInvoiceScreenTakesTheBatchTests
{
    private static string Page(string file) => File.ReadAllText(Path.Combine(
        RepositoryRoot(), "src", "Brqosh.Desktop", "Views", "Pages", file));

    /// <summary>‏<b>العمودان قائمان</b> في الشبكة.</summary>
    [Fact]
    public void TheGrid_HasTheBatchAndExpiryColumns()
    {
        var xaml = Page("PurchaseInvoiceDialog.xaml");
        Assert.Contains("Binding=\"{Binding BatchNumber}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedDate=\"{Binding ExpiryDate", xaml, StringComparison.Ordinal);
    }

    /// <summary>‏<b>والسطرُ يُبنى بهما</b> — لا يُمرَّران كغائبَين.</summary>
    [Fact]
    public void TheLine_IsAddedWithItsBatchAndExpiry()
    {
        var source = Page("PurchaseInvoiceDialog.xaml.cs");
        var start = source.IndexOf("invoice.AddLine(", StringComparison.Ordinal);
        Assert.True(start >= 0, "AddLine غير موجود — القياسُ أعمى.");
        var call = source[start..source.IndexOf("null);", start, StringComparison.Ordinal)];

        Assert.Contains("batchNumber: row.BatchNumber", call, StringComparison.Ordinal);
        Assert.Contains("expiryDate:", call, StringComparison.Ordinal);
        Assert.Contains("row.ExpiryDate", call, StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("جذرُ المستودع غير موجود.");
    }
}
