using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Brqosh.Desktop.Automation;
using Brqosh.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class CustomersPage : UserControl
{
    public record CustomerRow(long Id, string NameAr, string VatRegistrationNumber,
        string Phone, string Address, string InvoiceKind, decimal Balance);

    private List<Customer> _customers = [];
    private IReadOnlyDictionary<long, decimal> _balances = new Dictionary<long, decimal>();
    private readonly SearchDebouncer _searchDebouncer;

    public CustomersPage()
    {
        InitializeComponent();
        _searchDebouncer = new SearchDebouncer(TimeSpan.FromMilliseconds(300), ReloadAsync);
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (AddCustomerBtn, PermissionService.CanPost),
                (DeactivateBtn, PermissionService.CanPostAccounting));
            await UiFeedback.RunSafeAsync(ReloadAsync);
        };
    }

    private async Task ReloadAsync()
    {
        // (م2-٢٣) الموقوف يظهر عند طلبه.
        var showInactive = ShowInactiveCheck.IsChecked == true;
        var filter = SearchBox.Text.Trim();
        await using var db = AppServices.CreateDb();
        var query = db.Customers
            .Where(c => c.CompanyId == AppServices.CompanyId && (c.IsActive || showInactive));
        if (!string.IsNullOrEmpty(filter))
            query = query.Where(c => c.NameAr.Contains(filter) || c.VatRegistrationNumber.Contains(filter));

        _customers = await query.OrderBy(c => c.NameAr).Take(200).ToListAsync();
        var total = await db.Customers.CountAsync(c => c.CompanyId == AppServices.CompanyId && (c.IsActive || showInactive));
        _balances = await new Brqosh.Infrastructure.Sales.CustomerBalanceQuery(db)
            .AllBalancesAsync(AppServices.CompanyId);
        Render(filter);
        var totalAr = _balances.Values.Sum();
        CustomerSummaryText.Text =
            $"عملاء نشطون: {total:N0} · معروض: {_customers.Count:N0} · إجمالي AR: {totalAr:N2} ر.س";
    }

    private void Render(string filter)
    {
        var list = _customers.Select(c => new CustomerRow(
            c.Id, c.NameAr, c.VatRegistrationNumber, c.Phone, c.Address,
            c.SupportsStandardTaxInvoice ? "ضريبية (B2B)" : "مبسطة (B2C)",
            _balances.GetValueOrDefault(c.Id))).ToList();
        CustomersGrid.ItemsSource = list;
        UiTestAutomation.PublishGridCount(CustomersGrid, list.Count);
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => _searchDebouncer.Trigger();

    private async void OnAddCustomer(object sender, RoutedEventArgs e)
    {
        if (new CustomerDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnEdit(object sender, MouseButtonEventArgs e)
    {
        if (CustomersGrid.SelectedItem is not CustomerRow row) return;
        var customer = _customers.Single(c => c.Id == row.Id);
        if (new CustomerDialog(customer) { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnDeactivate(object sender, RoutedEventArgs e)
    {
        if (CustomersGrid.SelectedItem is not CustomerRow row) return;
        if (UiDialog.Show($"إيقاف العميل {row.NameAr}؟", "BRQOSH",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            await using var db = AppServices.CreateDb();
            var customer = await db.Customers.SingleAsync(c => c.Id == row.Id);
            customer.Deactivate();
            await db.SaveChangesAsync();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    /// <summary>(م2-٢٣) تبديل إظهار الموقوف.</summary>
    private async void OnToggleInactive(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        ReactivateBtn.Visibility = ShowInactiveCheck.IsChecked == true
            ? Visibility.Visible
            : Visibility.Collapsed;
        await ReloadAsync();
    }

    /// <summary>(م2-٢٣) إعادة التفعيل — الطريق الذي لم يكن موجوداً.</summary>
    private async void OnReactivate(object sender, RoutedEventArgs e)
    {
        if (CustomersGrid.SelectedItem is not CustomerRow row) return;

        try
        {
            await using var db = AppServices.CreateDb();
            var entity = await db.Customers.SingleAsync(x => x.Id == row.Id);
            entity.Reactivate();
            await db.SaveChangesAsync();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }
}
