using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Purchasing;
using Brqosh.Infrastructure.Purchasing;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-231 · ط1-٣ب) <b>إذنُ الاستلام — والدفعةُ لم يكن لها بابٌ أصلاً.</b>
///
/// <para><b>ما كان:</b> الشاشةُ تنسخ سطورَ أمر الشراء حرفاً: المستلَمُ = المطلوبُ دائماً،
/// <b>ولا حقلَ لرقم الدفعة ولا للصلاحية</b>. وأثرُه اثنان، وكلاهما يمنع عملاً حقيقياً:</para>
///
/// <para><b>الأوّل — الصنفُ المتتبَّع لا يُستلَم من الشاشة إطلاقاً.</b> فالمَخنَقُ في
/// <c>InventoryService</c> يرفضه باسمه: «يُتتبَّع بالدفعة ولا يحمل هذا السطرُ هويّة».
/// <b>فصيدليةٌ أو مطعمٌ لا يستطيع إدخالَ بضاعتِه</b> — وكلُّ ما بُني من تتبّعِ صلاحيةٍ
/// وصرفِ دواءٍ ودفعاتٍ قائمٌ خلف بابٍ مقفل.</para>
///
/// <para><b>والثاني — لا استلامَ جزئيّ.</b> والاستلامُ الجزئيُّ هو الحالةُ الغالبة:
/// يُطلب مئةٌ فيصل ستّون. <b>وقفلُ المستلَم على المطلوب يُثبت في المخزون أربعين لم تصل</b>،
/// ويُقيَّد للمورّد التزامٌ بما لم يُسلّم.</para>
/// </summary>
public partial class GoodsReceiptDialog : Window
{
    public record OrderOption(long Id, long SupplierId, string Display, PurchaseOrder Order);
    public record WarehouseOption(long Id, string Display);

    /// <summary>سطرُ استلامٍ يُحرَّر — <b>والمطلوبُ يُعرض ولا يُعدَّل</b>.</summary>
    public class LineRow : NotifyingRow
    {
        private decimal _receivedQuantity;
        private string? _batchNumber;
        private DateTime? _expiryDate;

        public long? ProductId { get; init; }
        public string Description { get; init; } = "";
        public decimal OrderedQuantity { get; init; }
        public decimal UnitCost { get; init; }
        public string? UnitCode { get; init; }

        /// <summary>هل الصنفُ متتبَّع — <b>ويُقرأ من الصنف لا من السطر</b>.</summary>
        public bool IsTracked { get; init; }

        public decimal ReceivedQuantity
        {
            get => _receivedQuantity;
            set => Set(ref _receivedQuantity, value);
        }

        public string? BatchNumber
        {
            get => _batchNumber;
            set => Set(ref _batchNumber, value);
        }

        public DateTime? ExpiryDate
        {
            get => _expiryDate;
            set => Set(ref _expiryDate, value);
        }
    }

    private readonly ObservableCollection<LineRow> _rows = [];

    public GoodsReceiptDialog()
    {
        InitializeComponent();
        DateBox.SelectedDate = DateTime.Today;
        LinesGrid.ItemsSource = _rows;
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

    private async void OnOrderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        await FillLinesAsync();
    }

    /// <summary>
    /// يملأ سطورَ الاستلام من أمر الشراء — <b>ويقرأ نمطَ تتبّع كلّ صنف</b>.
    /// </summary>
    /// <remarks>
    /// <b>والتتبّعُ يُقرأ هنا ليُطلَب رقمُ الدفعة قبل الترحيل</b> — فالمَخنَقُ يرفض بعده،
    /// <b>ورفضٌ بعد أن يملأ المستخدمُ الشاشةَ كلَّها أسوأُ من طلبٍ قبله</b>.
    /// </remarks>
    private async Task FillLinesAsync()
    {
        _rows.Clear();
        if (OrderBox.SelectedItem is not OrderOption option) return;

        try
        {
            await using var db = AppServices.CreateDb();

            var productIds = option.Order.Lines
                .Where(l => l.ProductId is not null)
                .Select(l => l.ProductId!.Value).Distinct().ToList();

            var tracking = await db.Products.AsNoTracking().IgnoreQueryFilters()
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Tracking);

            foreach (var line in option.Order.Lines)
            {
                _rows.Add(new LineRow
                {
                    ProductId = line.ProductId,
                    Description = line.Description,
                    OrderedQuantity = line.Quantity,
                    ReceivedQuantity = line.Quantity,
                    UnitCost = line.UnitPrice,
                    UnitCode = line.UnitCode,
                    IsTracked = line.ProductId is long id
                                && tracking.TryGetValue(id, out var t)
                                && t != InventoryTracking.None,
                });
            }
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private async void OnPost(object sender, RoutedEventArgs e)
    {
        // (CEEF-229 · م0-١٦) ضغطة مزدوجة كانت تُنشئ مستندين — الزرّ لا يُعطَّل أثناء الانتظار.
        using var busy = UiBusyGuard.Enter(sender);
        if (!busy.Acquired) return;

        try
        {
            LinesGrid.CommitEdit(DataGridEditingUnit.Row, true);
            ErrorText.Visibility = Visibility.Collapsed;

            if (OrderBox.SelectedItem is not OrderOption order)
                throw new AccountingException("اختر أمر شراء.");
            if (WarehouseBox.SelectedValue is not long warehouseId)
                throw new AccountingException("اختر مستودعاً.");
            if (DateBox.SelectedDate is null)
                throw new AccountingException("التاريخ مطلوب.");

            var received = _rows.Where(r => r.ReceivedQuantity > 0m).ToList();
            if (received.Count == 0)
                throw new AccountingException("لا سطرَ مستلَم — واستلامٌ بصفرٍ ليس استلاماً.");

            // ‏**والطلبُ قبل الرفض**: المَخنَقُ يرفض المتتبَّعَ بلا رقمٍ باسمه، **ورفضٌ بعد
            // ‏ملء الشاشة كلِّها أسوأُ من طلبٍ قبله**. والرسالةُ تُسمّي الصنفَ لا العدد.
            var missing = received
                .Where(r => r.IsTracked && string.IsNullOrWhiteSpace(r.BatchNumber))
                .Select(r => r.Description)
                .ToList();

            if (missing.Count > 0)
                throw new AccountingException(
                    $"أصنافٌ متتبَّعةٌ بلا رقم دفعة: {string.Join("، ", missing)} — "
                    + "وبضاعةٌ تدخل بلا هويّةٍ تخرج بلا صلاحية.");

            var receipt = GoodsReceipt.CreateDraft(
                AppServices.CompanyId, order.SupplierId, order.Id, warehouseId,
                DateOnly.FromDateTime(DateBox.SelectedDate.Value));

            foreach (var row in received)
            {
                receipt.AddLine(row.Description, row.OrderedQuantity, row.ReceivedQuantity,
                    row.UnitCost, row.ProductId,
                    string.IsNullOrWhiteSpace(row.BatchNumber) ? null : row.BatchNumber.Trim(),
                    row.ExpiryDate is DateTime d ? DateOnly.FromDateTime(d) : null,
                    row.UnitCode);
            }

            using var facade = new DesktopAccountingFacade();
            await facade.GetService<GoodsReceiptPostingService>().PostAsync(receipt);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError(ex is AccountingException ae ? ae.Message : ex.Message);
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    /// <summary>صفوفُ الشبكة — للاختبار وحده، كي يملأها كما يملؤها المستخدم.</summary>
    internal ObservableCollection<LineRow> RowsForTests => _rows;

    /// <summary>بانِي المستند — <b>وهو ما يستدعيه زرُّ الترحيل نفسُه</b>.</summary>
    internal GoodsReceipt BuildReceiptFromRows(long companyId, long supplierId, long orderId,
        long warehouseId, DateOnly date)
    {
        var receipt = GoodsReceipt.CreateDraft(companyId, supplierId, orderId, warehouseId, date);

        foreach (var row in _rows.Where(r => r.ReceivedQuantity > 0m))
        {
            receipt.AddLine(row.Description, row.OrderedQuantity, row.ReceivedQuantity,
                row.UnitCost, row.ProductId,
                string.IsNullOrWhiteSpace(row.BatchNumber) ? null : row.BatchNumber.Trim(),
                row.ExpiryDate is DateTime d ? DateOnly.FromDateTime(d) : null,
                row.UnitCode);
        }

        return receipt;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
