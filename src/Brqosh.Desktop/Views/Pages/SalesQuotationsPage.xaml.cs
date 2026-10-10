using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Security;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Sales;
using Brqosh.Desktop.Ui;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class SalesQuotationsPage : UserControl
{
    public record QuotationRow(long Id, string? QuotationNumber, string CustomerName, string QuotationDate,
        string ValidUntil, decimal GrandTotal, string Status, SalesQuotationStatus RawStatus);

    public SalesQuotationsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (AddBtn, PermissionService.CanPost),
                (AcceptBtn, PermissionService.CanPost),
                (RejectBtn, PermissionService.CanPost),
                (ConvertBtn, PermissionService.CanPost));
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
                x.q.Id,
                x.q.QuotationNumber,
                x.c.NameAr,
                x.q.QuotationDate.ToString("yyyy-MM-dd"),
                x.q.ValidUntil.ToString("yyyy-MM-dd"),
                x.q.Subtotal + x.q.VatAmount,
                StatusLabel(x.q.Status),
                x.q.Status))
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

    /// <summary>
    /// ‏(جاهزيّةُ الإطلاق · ح-١١٥ · ‏`order-to-cash-procure-to-pay-inventory#8`) ‏**قبولٌ ورفضٌ وتحويلٌ للعرض المختار** —
    /// ‏والنطاقُ يحرس الانتقال، وتُعرض رسالتُه العربيّةُ في سطر الحالة (مترجَمةً عند الإنجليزية).
    /// </summary>
    private async Task RunOnSelectedAsync(Func<SalesQuotationService, SalesOrderService, long, Task<string>> action)
    {
        if (Grid.SelectedItem is not QuotationRow row)
        {
            ShowStatus("اختر عرض سعر.", isError: true);
            return;
        }

        try
        {
            using var facade = new DesktopAccountingFacade();
            var message = await action(
                facade.GetService<SalesQuotationService>(), facade.GetService<SalesOrderService>(), row.Id);
            ShowStatus(message, isError: false);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            ShowStatus(ex.Message, isError: true);
        }
    }

    private void ShowStatus(string message, bool isError)
    {
        StatusText.Text = UiDialog.Localize(message);
        StatusText.Foreground = (System.Windows.Media.Brush)FindResource(isError ? "DangerBrush" : "PrimaryBrush");
        StatusText.Visibility = Visibility.Visible;
    }

    private async void OnAccept(object sender, RoutedEventArgs e) =>
        await RunOnSelectedAsync(async (quotations, _, id) =>
        {
            await quotations.AcceptAsync(AppServices.CompanyId, id);
            return "تم قبول العرض.";
        });

    private async void OnReject(object sender, RoutedEventArgs e) =>
        await RunOnSelectedAsync(async (quotations, _, id) =>
        {
            await quotations.RejectAsync(AppServices.CompanyId, id);
            return "تم رفض العرض.";
        });

    private async void OnConvert(object sender, RoutedEventArgs e) =>
        await RunOnSelectedAsync(async (_, orders, id) =>
        {
            var order = await orders.CreateFromQuotationAsync(AppServices.CompanyId, id);
            return LocaleManager.T($"تم إنشاء أمر البيع {order.OrderNumber}.", $"Sales order {order.OrderNumber} was created.");
        });
}
