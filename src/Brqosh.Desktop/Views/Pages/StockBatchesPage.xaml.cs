using System.Windows.Controls;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Ui;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط6-٣) <b>الدفعاتُ والصلاحية — الشاشةُ التي بلا وجودها بقي `ط1-٣` نصفَ ميزة.</b>
///
/// <para><b>ما كان:</b> تتبّعُ الدفعات والصلاحية بُني في <c>ط1-٣</c> ومُقيس بالطفرات،
/// <b>ولا شاشةَ تعرض رصيدَ دفعةٍ ولا تاريخَ انتهائها</b>. فالصيدليةُ والمصنعُ يُدخلان
/// أرقامَ الدفعات <b>ولا يستطيعان رؤيةَ ما يقارب الانتهاء</b> — وهو الغرضُ الأوّل من
/// التتبّع. <b>وبضاعةٌ تنتهي في الرفّ خسارةٌ كاملة</b>، ودواءٌ منتهٍ يُصرف بلاغُ سلامة.</para>
///
/// <para><b>وأُعلن في `ط1-٣` أنّه «لا تقريرَ لما يقارب الانتهاء»</b> — وهذه الشاشةُ هي
/// إغلاقُ ذلك البند، لا وصفُه.</para>
/// </summary>
public partial class StockBatchesPage : UserControl
{
    /// <summary>مدّةُ الإنذار — <b>تسعون يوماً</b>، وهي مهلةُ تصريفٍ معقولةٌ لأغلب السلع.</summary>
    private const int WarningDays = 90;

    public record Row(string ProductName, string WarehouseName, string BatchNumber,
        string Expiry, decimal Quantity, string Status);

    public StockBatchesPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await ReloadAsync();
    }

    /// <summary>
    /// <b>ولا يُحمَّل شيءٌ قبل أن تُحمَّل الشاشة.</b>
    ///
    /// <para><c>SelectionChanged</c> يقع <b>أثناء تحليل XAML</b> حين يُطبَّق
    /// <c>IsSelected="True"</c> على أوّل عنصر — <b>قبل <c>Loaded</c> وقبل أن تكون هناك
    /// قاعدةُ بيانات</b>. ورميٌ في <c>async void</c> لا يلتقطه أحد: <b>أسقط تشغيلةَ
    /// الاختبارات كلَّها</b> لا هذه الشاشةَ وحدَها.</para>
    /// </summary>
    private async void OnFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        try
        {
            await LoadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async Task LoadAsync()
    {
        await using var db = AppServices.CreateDb();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var horizon = today.AddDays(WarningDays);

        var batches = await db.StockBatches.AsNoTracking()
            .Where(b => b.CompanyId == AppServices.CompanyId && b.Quantity != 0m)
            .ToListAsync();

        var productNames = await db.Products.AsNoTracking().IgnoreQueryFilters()
            .Where(p => p.CompanyId == AppServices.CompanyId)
            .ToDictionaryAsync(p => p.Id, p => p.NameAr);

        var warehouseNames = await db.Warehouses.AsNoTracking()
            .Where(w => w.CompanyId == AppServices.CompanyId)
            .ToDictionaryAsync(w => w.Id, w => w.NameAr);

        var expired = batches.Count(b => b.ExpiryDate is DateOnly d && d < today);
        var soon = batches.Count(b => b.ExpiryDate is DateOnly d && d >= today && d <= horizon);

        var filtered = FilterBox.SelectedIndex switch
        {
            1 => batches.Where(b => b.ExpiryDate is DateOnly d && d >= today && d <= horizon),
            2 => batches.Where(b => b.ExpiryDate is DateOnly d && d < today),
            _ => batches,
        };

        var rows = filtered
            .OrderBy(b => b.ExpiryDate ?? DateOnly.MaxValue)
            .ThenBy(b => b.BatchNumber, StringComparer.Ordinal)
            .Select(b => new Row(
                productNames.TryGetValue(b.ProductId, out var product) ? product : $"#{b.ProductId}",
                warehouseNames.TryGetValue(b.WarehouseId, out var warehouse) ? warehouse : $"#{b.WarehouseId}",
                b.BatchNumber,
                b.ExpiryDate is DateOnly e ? e.ToString("yyyy-MM-dd") : "—",
                b.Quantity,
                StatusOf(b.ExpiryDate, today, horizon)))
            .ToList();

        Grid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(Grid, rows.Count);

        // ‏**والعنوانُ يقول ما في الرفوف لا ما في الجدول المعروض**: المستخدمُ قد يُرشّح
        // ‏على «المنتهي» فيرى صفراً، **وذلك لا يعني أنّ شيئاً لا يقارب الانتهاء**.
        SummaryText.Text = expired == 0 && soon == 0
            ? $"دفعات برصيد: {batches.Count:N0} — ولا شيءَ منها قارب الانتهاء."
            : $"دفعات برصيد: {batches.Count:N0} · ⚠️ منتهية: {expired:N0} · "
              + $"تقارب الانتهاء خلال {WarningDays} يوماً: {soon:N0}";
    }

    private static string StatusOf(DateOnly? expiry, DateOnly today, DateOnly horizon)
    {
        if (expiry is not DateOnly date) return "بلا صلاحية";
        if (date < today) return "انتهت الصلاحية";
        return date <= horizon ? "تقارب الانتهاء" : "سارية";
    }
}
