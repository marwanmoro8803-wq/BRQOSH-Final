using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Security;
using Brqosh.Domain.Purchasing;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Automation;

namespace Brqosh.Desktop.Views.Pages;

public partial class GoodsReceiptsPage : UserControl
{
    public record Row(long Id, string? ReceiptNumber, DateOnly ReceiptDate, string OrderNumber,
        string SupplierName, string StatusAr, GoodsReceiptStatus Status);

    public GoodsReceiptsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (NewBtn, PermissionService.CanPost),
                (CancelBtn, PermissionService.CanCancelOrReverse));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var receipts = await db.GoodsReceipts
            .Where(g => g.CompanyId == AppServices.CompanyId)
            .OrderByDescending(g => g.ReceiptDate).ThenByDescending(g => g.Id)
            .Take(200)
            .ToListAsync();

        var poIds = receipts.Select(r => r.PurchaseOrderId).Distinct().ToList();
        var pos = await db.PurchaseOrders.Where(p => poIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.OrderNumber ?? p.Id.ToString());
        var supplierIds = receipts.Select(r => r.SupplierId).Distinct().ToList();
        var suppliers = await db.Suppliers.Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.NameAr);

        var rows = receipts.Select(r => new Row(
            r.Id, r.ReceiptNumber, r.ReceiptDate,
            pos.GetValueOrDefault(r.PurchaseOrderId, "—"),
            suppliers.GetValueOrDefault(r.SupplierId, "—"),
            r.Status switch
            {
                GoodsReceiptStatus.Posted => "مرحّل",
                GoodsReceiptStatus.Cancelled => "ملغى",
                _ => "مسودة",
            },
            r.Status)).ToList();
        Grid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(Grid, rows.Count);

        var posted = receipts.Count(r => r.Status == GoodsReceiptStatus.Posted);
        ReceiptSummaryText.Text =
            $"أذون: {receipts.Count} · مرحّل: {posted} · مسودة: {receipts.Count - posted}";
    }

    private async void OnNew(object sender, RoutedEventArgs e)
    {
        if (new GoodsReceiptDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnCancel(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row)
        {
            UiFeedback.ShowInfo("اختر إذن استلام للإلغاء.");
            return;
        }

        if (row.Status != GoodsReceiptStatus.Posted)
        {
            UiFeedback.ShowWarning("لا يُلغى إلا إذن استلام مرحّل.");
            return;
        }

        if (!UiFeedback.Confirm($"إلغاء الاستلام {row.ReceiptNumber}؟ سيتم عكس المخزون."))
            return;

        var cancelDate = UiDialogs.PromptAccountingDate(
            Window.GetWindow(this)!, "تاريخ الإلغاء", "تاريخ إلغاء الاستلام:");
        if (cancelDate is null)
            return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            await facade.GetService<GoodsReceiptCancellationService>().CancelAsync(
                AppServices.CompanyId, row.Id, cancelDate.Value, SecurityContext.ResolveUsername());
            await ReloadAsync();
            UiFeedback.ShowSuccess("تم إلغاء إذن الاستلام.");
        }
        catch (Exception ex)
        {
            UiFeedback.ShowError(ex.Message);
        }
    }
}
