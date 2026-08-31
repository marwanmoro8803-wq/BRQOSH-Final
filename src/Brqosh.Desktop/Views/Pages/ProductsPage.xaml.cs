using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Brqosh.Desktop.Automation;
using Brqosh.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class ProductsPage : UserControl
{
    public record Row(long Id, string Code, string NameAr, string Unit, string TypeAr, decimal SalePrice, decimal WeightedAverageCost, string Barcode);
    private List<Product> _items = [];
    private readonly SearchDebouncer _searchDebouncer;
    private const int PageSize = 200;

    public ProductsPage()
    {
        InitializeComponent();
        _searchDebouncer = new SearchDebouncer(TimeSpan.FromMilliseconds(300), ReloadAsync);
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (AddBtn, PermissionService.CanPost),
                (DeactivateBtn, PermissionService.CanPostAccounting));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        var filter = SearchBox.Text.Trim();
        await using var db = AppServices.CreateDb();
        // (CEEF-229 · م2-٢٣) الموقوف يظهر عند طلبه — لا يختفي إلى الأبد.
        var showInactive = ShowInactiveCheck.IsChecked == true;
        var query = db.Products.Where(p =>
            p.CompanyId == AppServices.CompanyId && (p.IsActive || showInactive));
        if (!string.IsNullOrEmpty(filter))
            query = query.Where(p => p.Code.Contains(filter) || p.NameAr.Contains(filter));

        _items = await query.OrderBy(p => p.Code).Take(PageSize).ToListAsync();
        var total = await db.Products.CountAsync(p =>
            p.CompanyId == AppServices.CompanyId && (p.IsActive || showInactive));
        Render(filter);
        var stockable = _items.Count(p => p.Type == ProductType.Stockable);
        InventorySummaryText.Text =
            $"أصناف نشطة: {total:N0} · معروض: {_items.Count:N0} · مخزني: {stockable:N0} · خدمات: {_items.Count - stockable:N0}";
    }

    private void Render(string filter)
    {
        var visible = string.IsNullOrEmpty(filter) ? _items
            : _items.Where(p => p.Code.Contains(filter, StringComparison.OrdinalIgnoreCase) || p.NameAr.Contains(filter)).ToList();
        var list = visible.Select(p => new Row(p.Id, p.Code, p.NameAr, p.Unit,
            p.Type == ProductType.Stockable ? "مخزني" : "خدمة", p.SalePrice, p.WeightedAverageCost, p.Barcode)).ToList();
        Grid.ItemsSource = list;
        UiTestAutomation.PublishGridCount(Grid, list.Count);
    }

    private void OnSearch(object sender, TextChangedEventArgs e) => _searchDebouncer.Trigger();

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        if (new ProductDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnEdit(object sender, MouseButtonEventArgs e)
    {
        if (Grid.SelectedItem is not Row row) return;
        var product = _items.Single(p => p.Id == row.Id);
        if (new ProductDialog(product) { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnDeactivate(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row) return;
        if (UiDialog.Show($"إيقاف الصنف {row.Code}؟", "BRQOSH",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            await using var db = AppServices.CreateDb();
            var product = await db.Products.SingleAsync(p => p.Id == row.Id);
            product.Deactivate();
            await db.SaveChangesAsync();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    /// <summary>(م2-٢٣) تبديل إظهار الموقوف — وزرُّ إعادة التفعيل يظهر معه.</summary>
    private async void OnToggleInactive(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        ReactivateBtn.Visibility = ShowInactiveCheck.IsChecked == true
            ? Visibility.Visible
            : Visibility.Collapsed;
        await ReloadAsync();
    }

    /// <summary>(م2-٢٣) إعادة تفعيل صنفٍ موقوف — الطريق الذي لم يكن موجوداً.</summary>
    private async void OnReactivate(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row) return;

        try
        {
            await using var db = AppServices.CreateDb();
            var product = await db.Products.SingleAsync(p => p.Id == row.Id);
            product.Reactivate();
            await db.SaveChangesAsync();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }
}
