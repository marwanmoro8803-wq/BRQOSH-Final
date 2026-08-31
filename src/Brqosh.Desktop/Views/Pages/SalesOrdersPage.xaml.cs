using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Security;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class SalesOrdersPage : UserControl
{
    public record OrderRow(long Id, string? OrderNumber, string CustomerName, string OrderDate,
        decimal GrandTotal, string Status, SalesOrderStatus RawStatus);

    public SalesOrdersPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (NewBtn, PermissionService.CanPost),
                (InvoiceBtn, PermissionService.CanPostAccounting));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var rows = await db.SalesOrders
            .Where(o => o.CompanyId == AppServices.CompanyId)
            .OrderByDescending(o => o.OrderDate)
            .Take(200)
            .Join(db.Customers, o => o.CustomerId, c => c.Id, (o, c) => new { o, c })
            .Select(x => new OrderRow(
                x.o.Id,
                x.o.OrderNumber,
                x.c.NameAr,
                x.o.OrderDate.ToString("yyyy-MM-dd"),
                x.o.Subtotal + x.o.VatAmount,
                StatusLabel(x.o.Status),
                x.o.Status))
            .ToListAsync();
        Grid.ItemsSource = rows;
    }

    private static string StatusLabel(SalesOrderStatus status) => status switch
    {
        SalesOrderStatus.Confirmed => "مؤكد",
        SalesOrderStatus.Invoiced => "مفوتر",
        SalesOrderStatus.Cancelled => "ملغى",
        _ => "مسودة",
    };

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        if (new SalesOrderDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnCreateInvoice(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not OrderRow row)
        {
            UiDialog.Show("اختر أمر بيع مؤكد.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (row.RawStatus != SalesOrderStatus.Confirmed)
        {
            UiDialog.Show("يُفوتر أمر مؤكد فقط.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            using var facade = new DesktopAccountingFacade();
            var invoice = await facade.GetService<SalesOrderService>().CreateInvoiceFromOrderAsync(AppServices.CompanyId, row.Id);
            UiDialog.Show($"تم إنشاء الفاتورة {invoice.InvoiceNumber}.", "BRQOSH",
                MessageBoxButton.OK, MessageBoxImage.Information);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiDialog.Show(ex.Message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
