using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Brqosh.Desktop.Automation;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class StockMovementsPage : UserControl
{
    public record Row(long Id, string? MovementNumber, DateOnly MovementDate, string TypeAr,
        string Description, decimal TotalValue, long? JournalEntryId, string StatusAr);

    public StockMovementsPage() { InitializeComponent(); Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (NewBtn, PermissionService.CanPostAccounting),
                (CancelBtn, PermissionService.CanCancelOrReverse));
            await ReloadAsync();
        }; }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var list = await db.StockMovements.Where(m => m.CompanyId == AppServices.CompanyId)
            .OrderByDescending(m => m.MovementDate).ThenByDescending(m => m.Id).Take(100).ToListAsync();
        var rows = list.Select(m => new Row(m.Id, m.MovementNumber, m.MovementDate,
            TypeLabel(m.Type), m.Description, m.TotalValue, m.JournalEntryId, StatusLabel(m.Status))).ToList();
        Grid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(Grid, rows.Count);
    }

    private static string StatusLabel(StockMovementStatus s) => s switch
    {
        StockMovementStatus.Posted => LocaleManager.T("مرحّلة", "Posted"),
        StockMovementStatus.Cancelled => LocaleManager.T("ملغاة", "Cancelled"),
        _ => LocaleManager.T("مسودة", "Draft"),
    };

    private static string TypeLabel(StockMovementType t) => t switch
    {
        StockMovementType.Opening => LocaleManager.T("رصيد افتتاحي", "Opening balance"),
        StockMovementType.Receipt => LocaleManager.T("إدخال", "Receipt"),
        StockMovementType.Issue => LocaleManager.T("إخراج", "Issue"),
        StockMovementType.Transfer => LocaleManager.T("تحويل", "Transfer"),
        StockMovementType.AdjustmentIn => LocaleManager.T("تسوية (+)", "Adjustment (+)"),
        StockMovementType.AdjustmentOut => LocaleManager.T("تسوية (−)", "Adjustment (−)"),
        StockMovementType.SalesReturn => LocaleManager.T("إرجاع مبيعات", "Sales return"),
        _ => t.ToString(),
    };

    private async void OnNew(object sender, RoutedEventArgs e)
    {
        if (new StockMovementDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnView(object sender, RoutedEventArgs e) => await ViewSelectedAsync();

    private async void OnGridDoubleClick(object sender, MouseButtonEventArgs e) => await ViewSelectedAsync();

    private async void OnCancel(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row)
        {
            UiDialog.Show("اختر حركة من الجدول.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (row.StatusAr != "مرحّلة")
        {
            UiDialog.Show("لا يُلغى إلا حركة مرحّلة.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (UiDialog.Show($"إلغاء الحركة {row.MovementNumber}؟", "BRQOSH",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        var cancelDate = UiDialogs.PromptAccountingDate(
            Window.GetWindow(this)!, "تاريخ الإلغاء", "تاريخ إلغاء حركة المخزون وقيد العكس:");
        if (cancelDate is null)
            return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            await facade.GetService<StockMovementCancellationService>().CancelAsync(
                AppServices.CompanyId, row.Id, cancelDate.Value,
                SecurityContext.ResolveUsername());
            await ReloadAsync();
            UiDialog.Show("تم إلغاء الحركة.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            UiDialog.Show(ex.Message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnViewJournal(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row)
        {
            UiDialog.Show("اختر حركة من الجدول.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (row.JournalEntryId is long entryId)
            new JournalEntryViewDialog(entryId) { Owner = Window.GetWindow(this) }.ShowDialog();
        else
            UiDialog.Show("لا يوجد قيد محاسبي مرتبط بهذه الحركة.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async Task ViewSelectedAsync()
    {
        if (Grid.SelectedItem is not Row row)
        {
            UiDialog.Show("اختر حركة من الجدول.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await using var db = AppServices.CreateDb();
        var movement = await db.StockMovements.SingleAsync(m => m.Id == row.Id);
        var productIds = movement.Lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await db.Products.Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.NameAr);

        var lines = movement.Lines.Select(l =>
            new SalesInvoiceLine($"{products.GetValueOrDefault(l.ProductId, "—")} (كمية {l.Quantity:N2})",
                l.Quantity, l.UnitCost, l.ProductId)).ToList();

        var header = $"رقم: {movement.MovementNumber ?? "—"}  |  {row.TypeAr}  |  {movement.MovementDate:yyyy-MM-dd}\n{movement.Description}";
        new DocumentLinesViewDialog("حركة مخزنية", header, lines,
            movement.TotalValue, 0m, movement.TotalValue) { Owner = Window.GetWindow(this) }.ShowDialog();
    }
}
