using System.Windows;
using System.Windows.Controls;
using Brqosh.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class CustomersPage : UserControl
{
    public record CustomerRow(long Id, string NameAr, string VatRegistrationNumber,
        string Phone, string Address, string InvoiceKind);

    private List<Customer> _customers = [];

    public CustomersPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        _customers = await db.Customers
            .Where(c => c.CompanyId == AppServices.CompanyId && c.IsActive)
            .OrderBy(c => c.NameAr)
            .Take(500)
            .ToListAsync();
        Render(SearchBox.Text.Trim());
    }

    private void Render(string filter)
    {
        var visible = string.IsNullOrEmpty(filter)
            ? _customers
            : _customers.Where(c => c.NameAr.Contains(filter) || c.VatRegistrationNumber.Contains(filter)).ToList();

        CustomersGrid.ItemsSource = visible.Select(c => new CustomerRow(
            c.Id, c.NameAr, c.VatRegistrationNumber, c.Phone, c.Address,
            c.SupportsStandardTaxInvoice ? "ضريبية (B2B)" : "مبسطة (B2C)")).ToList();
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => Render(SearchBox.Text.Trim());

    private async void OnAddCustomer(object sender, RoutedEventArgs e)
    {
        var dialog = new CustomerDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true)
            await ReloadAsync();
    }
}
