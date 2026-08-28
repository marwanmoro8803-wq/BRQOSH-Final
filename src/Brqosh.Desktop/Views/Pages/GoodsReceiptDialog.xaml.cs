using System.Windows;
using System.Windows.Controls;
using Brqosh.Domain.Purchasing;
using Brqosh.Infrastructure.Purchasing;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class GoodsReceiptDialog : Window
{
    public record OrderOption(long Id, long SupplierId, string Display, PurchaseOrder Order);
    public record WarehouseOption(long Id, string Display);

    public GoodsReceiptDialog()
    {
        InitializeComponent();
        DateBox.SelectedDate = DateTime.Today;
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        await using var db = AppServices.CreateDb();
        var orders = await db.PurchaseOrders
            .Where(o => o.CompanyId == AppServices.CompanyId && o.Status == PurchaseOrderStatus.Approved)
            .OrderByDescending(o => o.OrderDate)
            .Take(100)
            .ToListAsync();

        var supplierIds = orders.Select(o => o.SupplierId).Distinct().ToList();
        var suppliers = await db.Suppliers.Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.NameAr);

        OrderBox.ItemsSource = orders.Select(o => new OrderOption(
            o.Id, o.SupplierId,
            $"{o.OrderNumber} — {suppliers.GetValueOrDefault(o.SupplierId, "?")} ({o.OrderDate:yyyy-MM-dd})",
            o)).ToList();
        if (OrderBox.Items.Count > 0) OrderBox.SelectedIndex = 0;

        var wh = await db.Warehouses.Where(w => w.CompanyId == AppServices.CompanyId && w.IsActive)
            .OrderByDescending(w => w.IsDefault).ThenBy(w => w.Code).ToListAsync();
        WarehouseBox.ItemsSource = wh.Select(w => new WarehouseOption(w.Id, $"{w.Code} — {w.NameAr}")).ToList();
        if (WarehouseBox.Items.Count > 0) WarehouseBox.SelectedIndex = 0;
    }

    private void OnOrderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OrderBox.SelectedItem is not OrderOption opt)
        {
            LinesPreview.Text = "";
            return;
        }

        LinesPreview.Text = "الأسطر: " + string.Join(" · ",
            opt.Order.Lines.Select(l => $"{l.Description} × {l.Quantity:N2} @ {l.UnitPrice:N2}"));
    }

    private async void OnPost(object sender, RoutedEventArgs e)
    {
        // (CEEF-229 · م0-١٦) ضغطة مزدوجة كانت تُنشئ مستندين — الزرّ لا يُعطَّل أثناء الانتظار.
        using var busy = UiBusyGuard.Enter(sender);
        if (!busy.Acquired) return;

        try
        {
            if (OrderBox.SelectedItem is not OrderOption order)
                throw new InvalidOperationException("اختر أمر شراء.");
            if (WarehouseBox.SelectedValue is not long warehouseId)
                throw new InvalidOperationException("اختر مستودعاً.");
            if (DateBox.SelectedDate is null)
                throw new InvalidOperationException("التاريخ مطلوب.");

            var receipt = GoodsReceipt.CreateDraft(
                AppServices.CompanyId, order.SupplierId, order.Id, warehouseId,
                DateOnly.FromDateTime(DateBox.SelectedDate.Value));

            foreach (var line in order.Order.Lines)
                receipt.AddLine(line.Description, line.Quantity, line.Quantity, line.UnitPrice, line.ProductId);

            using var facade = new DesktopAccountingFacade();
            await facade.GetService<GoodsReceiptPostingService>().PostAsync(receipt);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
