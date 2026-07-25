using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Brqosh.Desktop.Automation;
using Brqosh.Domain.Purchasing;
using Brqosh.Infrastructure.Purchasing;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Security;

namespace Brqosh.Desktop.Views.Pages;

public partial class SuppliersPage : UserControl
{
    public record SupplierRow(long Id, string NameAr, string VatRegistrationNumber,
        string Phone, string VatStatus, decimal Balance);

    private List<Supplier> _suppliers = [];
    private IReadOnlyDictionary<long, decimal> _balances = new Dictionary<long, decimal>();
    private readonly SearchDebouncer _searchDebouncer;
    private const int PageSize = 200;

    public SuppliersPage()
    {
        InitializeComponent();
        _searchDebouncer = new SearchDebouncer(TimeSpan.FromMilliseconds(300), ReloadAsync);
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (AddSupplierBtn, PermissionService.CanPostAccounting),
                (DeactivateBtn, PermissionService.CanPostAccounting));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        var filter = SearchBox.Text.Trim();
        await using var db = AppServices.CreateDb();
        var query = db.Suppliers
            .Where(s => s.CompanyId == AppServices.CompanyId && s.IsActive);
        if (!string.IsNullOrEmpty(filter))
            query = query.Where(s => s.NameAr.Contains(filter) || s.VatRegistrationNumber.Contains(filter));

        _suppliers = await query.OrderBy(s => s.NameAr).Take(PageSize).ToListAsync();
        var total = await db.Suppliers.CountAsync(s => s.CompanyId == AppServices.CompanyId && s.IsActive);
        _balances = await new SupplierBalanceQuery(db).AllBalancesAsync(AppServices.CompanyId);
        Render(filter);
        var totalAp = _balances.Values.Sum();
        SupplierSummaryText.Text =
            $"موردون نشطون: {total:N0} · معروض: {_suppliers.Count:N0} · إجمالي AP: {totalAp:N2} ر.س";
    }

    private void Render(string filter)
    {
        var list = _suppliers.Select(s => new SupplierRow(
            s.Id, s.NameAr, s.VatRegistrationNumber, s.Phone,
            s.IsVatRegistered ? "نعم" : "لا",
            _balances.GetValueOrDefault(s.Id))).ToList();
        SuppliersGrid.ItemsSource = list;
        UiTestAutomation.PublishGridCount(SuppliersGrid, list.Count);
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => _searchDebouncer.Trigger();

    private async void OnAddSupplier(object sender, RoutedEventArgs e)
    {
        if (new SupplierDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnEdit(object sender, MouseButtonEventArgs e)
    {
        if (SuppliersGrid.SelectedItem is not SupplierRow row) return;
        var supplier = _suppliers.Single(s => s.Id == row.Id);
        if (new SupplierDialog(supplier) { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnDeactivate(object sender, RoutedEventArgs e)
    {
        if (SuppliersGrid.SelectedItem is not SupplierRow row) return;
        if (MessageBox.Show($"إيقاف المورد {row.NameAr}؟", "BRQOSH",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            await using var db = AppServices.CreateDb();
            var supplier = await db.Suppliers.SingleAsync(s => s.Id == row.Id);
            supplier.Deactivate();
            await db.SaveChangesAsync();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }
}
