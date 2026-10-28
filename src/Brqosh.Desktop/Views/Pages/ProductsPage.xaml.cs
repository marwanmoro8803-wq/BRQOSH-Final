using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Brqosh.Desktop.Automation;
using Brqosh.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;
using Brqosh.Desktop.ViewModels;

namespace Brqosh.Desktop.Views.Pages;

public partial class ProductsPage : UserControl
{
    public record Row(long Id, string Code, string NameAr, string Unit, string TypeAr, decimal SalePrice, decimal WeightedAverageCost, string Barcode);
    private readonly ProductsViewModel _vm = new();
    private readonly SearchDebouncer _searchDebouncer;

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
        await _vm.LoadAsync(SearchBox.Text.Trim(), ShowInactiveCheck.IsChecked == true);
        Grid.ItemsSource = _vm.Rows;
        UiTestAutomation.PublishGridCount(Grid, _vm.Rows.Count);
        InventorySummaryText.Text =
            $"أصناف نشطة: {_vm.TotalCount:N0} · معروض: {_vm.Rows.Count:N0} · مخزني: {_vm.StockableCount:N0} · خدمات: {_vm.Rows.Count - _vm.StockableCount:N0}";
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
        var product = _vm.Find(row.Id);
        if (product is null) return;
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
            await _vm.DeactivateAsync(row.Id);
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
        // ‏(ن-١٨) **والدورُ شرطٌ هنا لا في `Loaded` وحدَه** — انظر CustomersPage للسبب.
        ReactivateBtn.Visibility = ShowInactiveCheck.IsChecked == true
                                   && PermissionService.CanPostAccounting(AppServices.CurrentUser)
            ? Visibility.Visible
            : Visibility.Collapsed;
        await ReloadAsync();
    }

    /// <summary>(م2-٢٣) إعادة تفعيل صنفٍ موقوف — الطريق الذي لم يكن موجوداً.</summary>
    private async void OnReactivate(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row) return;

        // ‏(ن-١٨) **الحارسُ عند نقطة الكتابة — وهو الذي يصمد.** و`IsActive` هنا **بوّابةُ
        // ‏البيع نفسُها** (PosSessionPage:91 · SalesInvoiceDialog:128 · StockMovementDialog:46):
        // ‏صنفٌ سحبه المحاسبُ من البيع كان الصرّافُ يُعيده إلى نقطة البيع بضغطة.
        if (!PermissionService.CanPostAccounting(AppServices.CurrentUser))
        {
            UiFeedback.ShowWarning(PermissionService.DeniedActionMessage("إعادة التفعيل", "reactivate"));
            return;
        }

        try
        {
            await _vm.ReactivateAsync(row.Id);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }
}
