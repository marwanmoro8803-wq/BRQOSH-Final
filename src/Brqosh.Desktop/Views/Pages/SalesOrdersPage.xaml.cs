using Brqosh.Desktop.Automation;
﻿using System.Windows;
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
                (InvoiceBtn, PermissionService.CanPostAccounting),
                (WarehousePanel, PermissionService.CanPostAccounting));
            await LoadWarehousesAsync();
            await ReloadAsync();
        };
    }

    private const string DefaultWarehouseChoice = "(المستودع الافتراضي)";

    private async Task LoadWarehousesAsync()
    {
        // ‏(جاهزيّةُ الإطلاق · ح-١١٣ · ‏`order-to-cash-procure-to-pay-inventory#12`) ‏**المستودعاتُ الفعّالة — والأوّلُ «الافتراضيّ» (null) فلا يتغيّر شيءٌ لمن لا يختار.**
        await using var db = AppServices.CreateDb();
        var choices = new List<SalesInvoiceDialog.WarehouseOption> { new(null, DefaultWarehouseChoice) };
        choices.AddRange(await db.Warehouses
            .Where(w => w.CompanyId == AppServices.CompanyId && w.IsActive)
            .OrderBy(w => w.Code)
            .Select(w => new SalesInvoiceDialog.WarehouseOption(w.Id, w.Code + " — " + w.NameAr))
            .ToListAsync());
        WarehouseCombo.ItemsSource = choices;
        WarehouseCombo.SelectedIndex = 0;
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
        UiTestAutomation.PublishGridCount(Grid, rows.Count, pageKey: "sales_orders");
    }

    private static string StatusLabel(SalesOrderStatus status) => status switch
    {
        SalesOrderStatus.Confirmed => LocaleManager.T("مؤكد", "Confirmed"),
        SalesOrderStatus.Invoiced => LocaleManager.T("مفوتر", "Invoiced"),
        SalesOrderStatus.PartInvoiced => LocaleManager.T("مفوتر جزئياً", "Partly invoiced"),
        SalesOrderStatus.Cancelled => LocaleManager.T("ملغى", "Cancelled"),
        _ => LocaleManager.T("مسودة", "Draft"),
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
            // ‏(جاهزيّةُ الإطلاق · ح-١١٣ · ‏`order-to-cash-procure-to-pay-inventory#12`) ‏**المستودعُ المختار يصل الخدمة (null = الافتراضيّ).**
            var warehouseId = WarehouseCombo.SelectedValue as long?;
            var invoice = await facade.GetService<SalesOrderService>().CreateInvoiceFromOrderAsync(
                AppServices.CompanyId, row.Id, DateOnly.FromDateTime(DateTime.Today), warehouseId: warehouseId);
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
