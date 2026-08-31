using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Brqosh.Desktop.Automation;
using Brqosh.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class WarehousesPage : UserControl
{
    public record Row(long Id, string Code, string NameAr, string DefaultAr);
    private List<Warehouse> _items = [];

    public WarehousesPage() { InitializeComponent(); Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (AddBtn, PermissionService.CanPostAccounting),
                (DeactivateBtn, PermissionService.CanPostAccounting));
            await ReloadAsync();
        }; }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        _items = await db.Warehouses.Where(w => w.CompanyId == AppServices.CompanyId && w.IsActive)
            .OrderBy(w => w.Code).ToListAsync();
        var rows = _items.Select(w => new Row(w.Id, w.Code, w.NameAr, w.IsDefault ? "نعم" : "—")).ToList();
        Grid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(Grid, rows.Count);
        WarehouseSummaryText.Text =
            $"مستودعات نشطة: {rows.Count:N0} · افتراضي: {rows.Count(r => r.DefaultAr == "نعم"):N0}";
    }

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        if (new WarehouseDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnEdit(object sender, MouseButtonEventArgs e)
    {
        if (Grid.SelectedItem is not Row row) return;
        var wh = _items.Single(w => w.Id == row.Id);
        if (new WarehouseDialog(wh) { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnDeactivate(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row) return;
        if (UiDialog.Show($"إيقاف المستودع {row.Code}؟", "BRQOSH",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            await using var db = AppServices.CreateDb();
            var wh = await db.Warehouses.SingleAsync(w => w.Id == row.Id);
            wh.Deactivate();
            await db.SaveChangesAsync();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }
}
