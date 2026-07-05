using System.Windows;
using System.Windows.Controls;
using Brqosh.Domain.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class ReceiptVouchersPage : UserControl
{
    public record VoucherRow(string? VoucherNumber, DateOnly VoucherDate, string CustomerName,
        decimal Amount, string MethodAr, string Reference, string Description);

    public ReceiptVouchersPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var vouchers = await db.ReceiptVouchers
            .Where(v => v.CompanyId == AppServices.CompanyId)
            .OrderByDescending(v => v.VoucherDate).ThenByDescending(v => v.Id)
            .Take(100)
            .ToListAsync();

        var customerIds = vouchers.Select(v => v.CustomerId).Distinct().ToList();
        var customers = await db.Customers
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.NameAr);

        var total = await db.ReceiptVouchers
            .Where(v => v.CompanyId == AppServices.CompanyId && v.Status == ReceiptVoucherStatus.Posted)
            .SumAsync(v => (decimal?)v.Amount) ?? 0m;

        VouchersGrid.ItemsSource = vouchers.Select(v => new VoucherRow(
            v.VoucherNumber, v.VoucherDate,
            customers.GetValueOrDefault(v.CustomerId, "—"),
            v.Amount,
            v.Method == ReceiptMethod.Cash ? "نقداً" : "بنك",
            v.Reference, v.Description)).ToList();

        TotalText.Text = $"إجمالي التحصيلات: {total:N2}";
    }

    private async void OnNewVoucher(object sender, RoutedEventArgs e)
    {
        var dialog = new ReceiptVoucherDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true)
            await ReloadAsync();
    }
}
