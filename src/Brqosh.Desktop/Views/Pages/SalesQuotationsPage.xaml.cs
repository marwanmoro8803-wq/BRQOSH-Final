using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Security;
using Brqosh.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class SalesQuotationsPage : UserControl
{
    public record QuotationRow(string? QuotationNumber, string CustomerName, string QuotationDate,
        string ValidUntil, decimal GrandTotal, string Status);

    public SalesQuotationsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (AddBtn, PermissionService.CanPost));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var rows = await db.SalesQuotations
            .Where(q => q.CompanyId == AppServices.CompanyId)
            .OrderByDescending(q => q.QuotationDate)
            .Take(200)
            .Join(db.Customers, q => q.CustomerId, c => c.Id, (q, c) => new { q, c })
            .Select(x => new QuotationRow(
                x.q.QuotationNumber,
                x.c.NameAr,
                x.q.QuotationDate.ToString("yyyy-MM-dd"),
                x.q.ValidUntil.ToString("yyyy-MM-dd"),
                x.q.Subtotal + x.q.VatAmount,
                StatusLabel(x.q.Status)))
            .ToListAsync();
        Grid.ItemsSource = rows;
    }

    private static string StatusLabel(SalesQuotationStatus status) => status switch
    {
        SalesQuotationStatus.Sent => "مُرسَل",
        SalesQuotationStatus.Accepted => "مقبول",
        SalesQuotationStatus.Rejected => "مرفوض",
        SalesQuotationStatus.Expired => "منتهي",
        _ => "مسودة",
    };

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        if (new SalesQuotationDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }
}
