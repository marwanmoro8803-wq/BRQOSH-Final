using System.IO;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١٠٩ · <c>order-to-cash-procure-to-pay-inventory#7</c>) <b>ورقةُ الجرد تعرض المتتبَّعَ بدفعاته.</b>
///
/// <para>‏صفٌّ لكلّ صنفٍ كان يجعل فروقَ الدواء تُرفض في المَخنَق «بلا هويّة». <b>والقياسُ في النصّ</b> لأنّ الصفحة تقرأ من <c>AppServices</c>.</para>
/// </summary>
public class TheCountSheetListsTrackedProductsByBatchTests
{
    private static string Page(string file) => File.ReadAllText(Path.Combine(
        RepositoryRoot(), "src", "Brqosh.Desktop", "Views", "Pages", file));

    /// <summary>‏<b>الصفوفُ تتفرّع من دفعات المستودع</b> لكلّ صنفٍ متتبَّع.</summary>
    [Fact]
    public void TrackedRows_AreExpandedFromTheStockBatches()
    {
        var source = Page("StockCountPage.xaml.cs");
        Assert.Contains("db.StockBatches", source, StringComparison.Ordinal);
        Assert.Contains("x.WarehouseId == b.WarehouseId", source, StringComparison.Ordinal);
        Assert.Contains("BatchNumber = batch.BatchNumber", source, StringComparison.Ordinal);
    }

    /// <summary>‏<b>والدفعةُ تبلغ الخدمة</b> مع كلّ سطر.</summary>
    [Fact]
    public void TheBatchNumber_IsPassedToTheService()
    {
        Assert.Contains("(r.ProductId, r.BatchNumber, r.SystemQty, r.CountedQty",
            Page("StockCountPage.xaml.cs"), StringComparison.Ordinal);
    }

    /// <summary>‏<b>وعمودُ «رقم الدفعة»</b> ظاهر.</summary>
    [Fact]
    public void TheGrid_HasABatchColumn() =>
        Assert.Contains("Header=\"رقم الدفعة\" Binding=\"{Binding BatchNumber}\"",
            Page("StockCountPage.xaml"), StringComparison.Ordinal);

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("جذرُ المستودع غير موجود.");
    }
}
