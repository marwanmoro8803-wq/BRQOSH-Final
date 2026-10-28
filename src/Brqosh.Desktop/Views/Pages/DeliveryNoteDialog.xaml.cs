using System.Windows;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Inventory;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class DeliveryNoteDialog : Window
{
    public class LineRow
    {
        public int Index { get; init; }
        public string Description { get; init; } = "";
        public decimal Ordered { get; init; }
        public string Quantity { get; set; } = "";
        public string Batch { get; set; } = "";
        public bool Tracked { get; init; }
        public string TrackedLabel => Tracked ? LocaleManager.T("نعم", "Yes") : "";
    }

    private readonly long _orderId;
    private readonly long? _warehouseId;
    private List<LineRow> _rows = [];

    public DeliveryNoteDialog(long orderId, long? warehouseId)
    {
        InitializeComponent();
        _orderId = orderId;
        _warehouseId = warehouseId;
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        await using var db = AppServices.CreateDb();
        var order = await db.SalesOrders.Include(o => o.Lines)
            .FirstAsync(o => o.Id == _orderId && o.CompanyId == AppServices.CompanyId);
        var ids = order.Lines.Where(l => l.ProductId != null).Select(l => l.ProductId!.Value).ToList();
        var tracked = await db.Products
            .Where(p => ids.Contains(p.Id) && p.Tracking != InventoryTracking.None)
            .Select(p => p.Id).ToListAsync();
        _rows = order.Lines.Select((l, i) => new LineRow
        {
            Index = i,
            Description = l.Description,
            Ordered = l.Quantity,
            Quantity = l.Quantity.ToString("0.###"),
            Tracked = l.ProductId is long pid && tracked.Contains(pid),
        }).ToList();
        LinesGrid.ItemsSource = _rows;
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        if (!PermissionService.CanPostAccounting(AppServices.CurrentUser)) return;
        LinesGrid.CommitEdit();
        LinesGrid.CommitEdit();
        var quantities = new Dictionary<int, decimal>();
        var batches = new Dictionary<int, string>();
        foreach (var r in _rows)
        {
            if (!NumericInput.TryParseNonNegative(r.Quantity, out var q) || q > r.Ordered)
            {
                UiDialog.Show("كمية غير صالحة.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (q == 0) continue;
            quantities[r.Index] = q;
            if (r.Tracked)
            {
                if (string.IsNullOrWhiteSpace(r.Batch))
                {
                    UiDialog.Show("أدخل رقم الدفعة للأصناف المتتبعة.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                batches[r.Index] = r.Batch.Trim();
            }
        }

        if (quantities.Count == 0)
        {
            UiDialog.Show("أدخل كمية للتسليم.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            using var facade = new DesktopAccountingFacade();
            await facade.GetService<DeliveryNoteService>().DeliverFromOrderAsync(
                AppServices.CompanyId, _orderId, DateOnly.FromDateTime(DateTime.Today),
                quantities, _warehouseId, batches.Count == 0 ? null : batches);
            DialogResult = true;
        }
        catch (Exception)
        {
            UiDialog.Show("تعذّر إنشاء إذن التسليم.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}