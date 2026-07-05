using System.Windows;
using System.Windows.Controls;
using Brqosh.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class SalesInvoicesPage : UserControl
{
    private const int PageSize = 100;

    public record InvoiceRow(string? InvoiceNumber, DateOnly InvoiceDate, string CustomerName,
        decimal Subtotal, decimal VatAmount, decimal GrandTotal, string StatusAr);

    public SalesInvoicesPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var invoices = await db.SalesInvoices
            .Where(i => i.CompanyId == AppServices.CompanyId)
            .OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.Id)
            .Take(PageSize)
            .ToListAsync();

        var customerIds = invoices.Select(i => i.CustomerId).Distinct().ToList();
        var customers = await db.Customers
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.NameAr);

        var total = await db.SalesInvoices.CountAsync(i => i.CompanyId == AppServices.CompanyId);

        InvoicesGrid.ItemsSource = invoices.Select(i => new InvoiceRow(
            i.InvoiceNumber, i.InvoiceDate,
            customers.GetValueOrDefault(i.CustomerId, "—"),
            i.Subtotal, i.VatAmount, i.GrandTotal,
            i.Status switch
            {
                SalesInvoiceStatus.Posted => "مرحّلة",
                SalesInvoiceStatus.Cancelled => "ملغاة",
                _ => "مسودة",
            })).ToList();

        CountText.Text = $"إجمالي الفواتير: {total:N0}";
    }

    private async void OnNewInvoice(object sender, RoutedEventArgs e)
    {
        var dialog = new SalesInvoiceDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true)
            await ReloadAsync();
    }
}
