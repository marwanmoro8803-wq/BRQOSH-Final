using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Brqosh.Desktop.Automation;
using Brqosh.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>فئات الأصناف: الكود والاسم وحسابات الإيراد والمخزون وتكلفة المبيعات الافتراضية لأصناف الفئة.</summary>
public partial class ProductCategoriesPage : UserControl
{
    public record Row(long Id, string Code, string NameAr, string Revenue, string Inventory, string Cogs);
    private List<ProductCategory> _items = [];

    public ProductCategoriesPage() { InitializeComponent(); Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (AddBtn, PermissionService.CanPostAccounting),
                (DeactivateBtn, PermissionService.CanPostAccounting));
            await ReloadAsync();
        }; }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        _items = await db.ProductCategories.Where(c => c.CompanyId == AppServices.CompanyId && c.IsActive)
            .OrderBy(c => c.Code).ToListAsync();
        var ids = _items.SelectMany(c => new[] { c.RevenueAccountId, c.InventoryAccountId, c.CogsAccountId })
            .OfType<long>().Distinct().ToList();
        var names = await db.Accounts.Where(a => ids.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Code + " " + a.NameAr);
        string Name(long? id) => id is long v && names.TryGetValue(v, out var n) ? n : "—";
        var rows = _items.Select(c => new Row(c.Id, c.Code, c.NameAr,
            Name(c.RevenueAccountId), Name(c.InventoryAccountId), Name(c.CogsAccountId))).ToList();
        Grid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(Grid, rows.Count);
        CategorySummaryText.Text = $"فئات نشطة: {rows.Count:N0}";
    }

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        if (new ProductCategoryDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnEdit(object sender, MouseButtonEventArgs e)
    {
        if (Grid.SelectedItem is not Row row) return;
        var category = _items.Single(c => c.Id == row.Id);
        if (new ProductCategoryDialog(category) { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnDeactivate(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row) return;
        if (!PermissionService.CanPostAccounting(AppServices.CurrentUser))
        {
            UiFeedback.ShowWarning(PermissionService.DeniedActionMessage("الإيقاف", "deactivate"));
            return;
        }
        if (UiDialog.Show("إيقاف الفئة المحددة؟", "BRQOSH",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            await using var db = AppServices.CreateDb();
            var category = await db.ProductCategories.SingleAsync(c => c.Id == row.Id);
            category.Deactivate();
            await db.SaveChangesAsync();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }
}
