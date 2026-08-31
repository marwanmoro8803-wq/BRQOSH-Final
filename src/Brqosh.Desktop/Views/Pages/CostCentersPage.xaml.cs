using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Brqosh.Domain.Accounting;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class CostCentersPage : UserControl
{
    public record Row(long Id, string Code, string NameAr);
    private List<CostCenter> _items = [];

    public CostCentersPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (AddBtn, PermissionService.CanPostAccounting),
                (DeactivateBtn, PermissionService.CanPostAccounting));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        _items = await db.CostCenters
            .Where(c => c.CompanyId == AppServices.CompanyId && c.IsActive)
            .OrderBy(c => c.Code)
            .ToListAsync();
        var rows = _items.Select(c => new Row(c.Id, c.Code, c.NameAr)).ToList();
        Grid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(Grid, rows.Count);
        CostCenterSummaryText.Text = $"مراكز تكلفة نشطة: {rows.Count:N0}";
    }

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        if (new CostCenterDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnEdit(object sender, MouseButtonEventArgs e)
    {
        if (Grid.SelectedItem is not Row row) return;
        var center = _items.Single(c => c.Id == row.Id);
        if (new CostCenterDialog(center) { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnDeactivate(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row) return;
        if (UiDialog.Show($"إيقاف مركز التكلفة {row.Code}؟", "BRQOSH",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            await using var db = AppServices.CreateDb();
            var center = await db.CostCenters.SingleAsync(c => c.Id == row.Id);
            center.Deactivate();
            await db.SaveChangesAsync();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }
}
