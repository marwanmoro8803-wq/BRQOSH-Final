using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Logistics;
using Brqosh.Infrastructure.Logistics;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط6-٤) <b>البوالصُ والتحصيل — الشاشةُ التي بلا وجودها بقي `ط3-٤` بلا باب.</b>
///
/// <para><b>وما في اليد للتجّار يُعرض في العنوان</b>: هو التزامٌ على الشركة لا مالُها.
/// <b>ورقمٌ يكبر بلا توريدٍ إشارةُ خطرٍ نقديّ</b> — الشركةُ تُنفق من أمانةٍ ثمّ لا تجدها
/// يومَ المطالبة.</para>
/// </summary>
public partial class ShipmentsPage : UserControl
{
    public record Row(long Id, long ShipperId, string Waybill, string ShipperName,
        string ConsigneeName, string City, decimal Freight, decimal Cod, string Status,
        string CodState, bool CanDeliver, bool CanCollect);

    public ShipmentsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (AddBtn, PermissionService.CanPost),
                (DeliverBtn, PermissionService.CanPost),
                (CollectBtn, PermissionService.CanPostAccounting),
                (RemitBtn, PermissionService.CanPostAccounting));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();

        var shipments = await db.Shipments.AsNoTracking()
            .Where(s => s.CompanyId == AppServices.CompanyId)
            .OrderByDescending(s => s.ShipDate)
            .ToListAsync();

        var shippers = await db.Customers.AsNoTracking().IgnoreQueryFilters()
            .Where(c => c.CompanyId == AppServices.CompanyId)
            .ToDictionaryAsync(c => c.Id, c => c.NameAr);

        var rows = shipments.Select(s => new Row(
            s.Id, s.ShipperCustomerId, s.WaybillNumber,
            shippers.TryGetValue(s.ShipperCustomerId, out var name) ? name : $"#{s.ShipperCustomerId}",
            s.ConsigneeName, s.DestinationCity, s.FreightCharge, s.CodAmount,
            StatusText(s.Status), CodStateText(s),
            s.Status is ShipmentStatus.Created or ShipmentStatus.InTransit,
            s.CodCollected && !s.CodRemitted)).ToList();

        Grid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(Grid, rows.Count);

        var held = shipments.Where(s => s.CodCollected && !s.CodRemitted).Sum(s => s.CodAmount);
        var freight = shipments.Where(s => s.Status != ShipmentStatus.Cancelled).Sum(s => s.FreightCharge);

        SummaryText.Text = held == 0m
            ? $"بوالص: {rows.Count:N0} · أجور الشحن: {freight:N2} — وهي وحدها إيراد الشركة."
            : $"بوالص: {rows.Count:N0} · أجور الشحن: {freight:N2} · "
              + $"⚠️ محصَّل لم يُورَّد للتجّار: {held:N2} — وهو التزامٌ على الشركة لا مالُها.";
    }

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        if (new ShipmentDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnDeliver(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row) return;
        if (!row.CanDeliver)
        {
            UiFeedback.ShowWarning("لا تُسلَّم إلا شحنةٌ قائمة.");
            return;
        }

        try
        {
            await using var db = AppServices.CreateDb();
            var shipment = await db.Shipments.SingleAsync(s => s.Id == row.Id);
            shipment.Deliver(DateOnly.FromDateTime(DateTime.Today));
            await db.SaveChangesAsync();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnCollect(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row) return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            await facade.GetService<CodSettlementService>().CollectAsync(
                AppServices.CompanyId, [row.Id], DateOnly.FromDateTime(DateTime.Today));
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnRemit(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row) return;

        if (UiDialog.Show("توريد كل المحصَّل غير المورَّد لهذا التاجر؟", row.ShipperName,
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            var result = await facade.GetService<CodSettlementService>().RemitAsync(
                AppServices.CompanyId, row.ShipperId, DateOnly.FromDateTime(DateTime.Today));

            await ReloadAsync();
            SummaryText.Text = $"وُرِّد {result.CodRemitted:N2} عن {result.Shipments:N0} بوليصة.";
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private static string StatusText(ShipmentStatus status) => status switch
    {
        ShipmentStatus.Created => "منشأة",
        ShipmentStatus.InTransit => "في الطريق",
        ShipmentStatus.Delivered => "سُلّمت",
        ShipmentStatus.Returned => "مرتجعة",
        ShipmentStatus.Cancelled => "ملغاة",
        _ => status.ToString(),
    };

    private static string CodStateText(Shipment shipment)
    {
        if (shipment.CodAmount <= 0m) return "لا تحصيل";
        if (shipment.CodRemitted) return "ورِّد للتاجر";
        return shipment.CodCollected ? "محصَّل في اليد" : "لم يُحصَّل";
    }
}
