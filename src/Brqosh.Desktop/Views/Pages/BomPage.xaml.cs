using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Automation;
using Brqosh.Domain.Manufacturing;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class BomPage : UserControl
{
    public record BomRow(long Id, string Code, string NameAr, string ProductName, decimal OutputQuantity, int LineCount);

    public BomPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (AddBomBtn, PermissionService.CanPostAccounting),
                (EditBtn, PermissionService.CanPostAccounting),
                (RenameBtn, PermissionService.CanPostAccounting),
                (DeactivateBtn, PermissionService.CanPostAccounting));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var boms = await db.BillsOfMaterials
            .Where(b => b.CompanyId == AppServices.CompanyId && b.IsActive)
            .OrderBy(b => b.Code)
            .Take(500)
            .ToListAsync();

        var productIds = boms.Select(b => b.FinishedProductId).Distinct().ToList();
        var products = await db.Products.Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.NameAr);

        var rows = boms.Select(b => new BomRow(
            b.Id, b.Code, b.NameAr,
            products.GetValueOrDefault(b.FinishedProductId, "—"),
            b.OutputQuantity, b.Lines.Count)).ToList();
        Grid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(Grid, rows.Count);
        var totalLines = rows.Sum(r => r.LineCount);
        BomSummaryText.Text = $"قوائم مواد نشطة: {rows.Count} · إجمالي مكوّنات: {totalLines}";
    }

    private async void OnAddBom(object sender, RoutedEventArgs e)
    {
        if (new BomDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnEdit(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not BomRow row) return;
        if (new BomDialog(row.Id) { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnRename(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not BomRow row) return;
        var dialog = new TextInputDialog("تعديل BOM", "الاسم:", row.NameAr)
            { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.Value))
            return;

        await using var db = AppServices.CreateDb();
        var bom = await db.BillsOfMaterials.SingleAsync(b => b.Id == row.Id);
        bom.Rename(dialog.Value);
        await db.SaveChangesAsync();
        await ReloadAsync();
    }

    private async void OnDeactivate(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not BomRow row) return;
        if (UiDialog.Show($"إيقاف BOM {row.Code}؟", "BRQOSH",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            await using var db = AppServices.CreateDb();
            var bom = await db.BillsOfMaterials.SingleAsync(b => b.Id == row.Id);
            bom.Deactivate();
            await db.SaveChangesAsync();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }
}
