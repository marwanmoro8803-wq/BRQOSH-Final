using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Domain.Inventory;
using Brqosh.Infrastructure.Inventory;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Ui;

using Brqosh.Infrastructure.Security;
using Brqosh.Desktop.Security;

namespace Brqosh.Desktop.Views.Pages;

public partial class StockCountPage : UserControl
{
    public class CountRow : Brqosh.Desktop.Ui.NotifyingRow
    {
        public long ProductId { get; init; }
        public long WarehouseId { get; init; }
        public string ProductCode { get; init; } = "";
        public string ProductName { get; init; } = "";
        public string WarehouseName { get; init; } = "";
        public string Unit { get; init; } = "";

        /// <summary>(ح-١٠٩) رقمُ الدفعة لصنفٍ متتبَّع — فارغٌ لغيره.</summary>
        public string? BatchNumber { get; init; }
        public decimal SystemQty { get; init; }

        /// <summary>
        /// (CEEF-229 · م2-١٧ب) صنفٌ موقوفٌ ما زالت بضاعتُه قائمة — يظهر في الورقة ليُعَدّ
        /// ويُصفَّى، ويُوسَم كي لا يُظنّ متاحاً للبيع. وقبل هذه الدفعة كان يختفي كلّياً،
        /// فتبقى بضاعتُه محبوسةً لا تُعَدّ ولا تُسوّى.
        /// </summary>
        public bool IsStopped { get; init; }
        private decimal _countedQty;

        /// <summary>
        /// (CEEF-229 · م0-١٧) بدون الإشعار كان عمود «الفرق» يبقى أصفاراً بينما الأرقام مُدخَلة
        /// فعلاً — ثم تظهر رسالة «ترحيل 47 فرقاً» على شاشةٍ تقول إن الجرد مطابق.
        /// </summary>
        public decimal CountedQty
        {
            get => _countedQty;
            set => Set(ref _countedQty, value, [nameof(Variance)]);
        }

        public decimal Variance => CountedQty - SystemQty;
        public decimal UnitCost { get; set; }
        public decimal TotalValue => Math.Round(SystemQty * UnitCost, 2, MidpointRounding.AwayFromZero);
    }

    private readonly ObservableCollection<CountRow> _rows = [];

    public StockCountPage()
    {
        InitializeComponent();
        Grid.ItemsSource = _rows;
        UiTestAutomation.PublishGridCount(Grid, _rows.Count);
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (PostCountBtn, PermissionService.CanPostAccounting));
            
            await using var db = AppServices.CreateDb();
            WarehouseCombo.ItemsSource = await db.Warehouses
                .Where(w => w.CompanyId == AppServices.CompanyId && w.IsActive)
                .OrderBy(w => w.Code).ToListAsync();
            WarehouseCombo.SelectedIndex = 0;
            await RefreshAsync();
        };
    }

    private async void OnFilter(object sender, SelectionChangedEventArgs e) => await RefreshAsync();
    private async void OnRefresh(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async Task RefreshAsync()
    {
        long? whId = WarehouseCombo.SelectedValue is long id ? id : null;
        await using var db = AppServices.CreateDb();
        var balances = await new StockBalanceQuery(db).ExecuteAsync(AppServices.CompanyId, whId, includeZeroBalances: true);
        _rows.Clear();

        // ‏(جاهزيّةُ الإطلاق · ح-١٠٩ · `order-to-cash-procure-to-pay-inventory#7`) ‏**المتتبَّع يُجرد بدفعاته:** صفٌّ لكلّ دفعةٍ رصيدُها موجب
        // ‏في مستودع الصفّ، ولا صفَّ لدفعةٍ رصيدُها صفر. وإلا رُفضت فروقُ دواءٍ في المَخنَق «بلا هويّة».
        var trackedIds = balances.Select(b => b.ProductId).Distinct().ToList();
        var batches = trackedIds.Count == 0 ? [] : await db.StockBatches.AsNoTracking()
            .Where(x => x.CompanyId == AppServices.CompanyId && trackedIds.Contains(x.ProductId) && x.Quantity > 0)
            .OrderBy(x => x.BatchNumber).ToListAsync();
        var trackedProducts = (await db.Products.AsNoTracking()
            .Where(p => trackedIds.Contains(p.Id) && p.Tracking != InventoryTracking.None)
            .Select(p => p.Id).ToListAsync()).ToHashSet();

        foreach (var b in balances)
        {
            if (trackedProducts.Contains(b.ProductId))
            {
                foreach (var batch in batches.Where(x => x.ProductId == b.ProductId && x.WarehouseId == b.WarehouseId))
                    _rows.Add(new CountRow
                    {
                        ProductId = b.ProductId,
                        WarehouseId = b.WarehouseId,
                        ProductCode = b.ProductCode,
                        ProductName = b.ProductName,
                        WarehouseName = b.WarehouseName,
                        Unit = b.Unit,
                        BatchNumber = batch.BatchNumber,
                        SystemQty = batch.Quantity,
                        CountedQty = batch.Quantity,
                        UnitCost = b.UnitCost,
                        IsStopped = b.IsStopped,
                    });
                continue;
            }

            _rows.Add(new CountRow
            {
                ProductId = b.ProductId,
                WarehouseId = b.WarehouseId,
                ProductCode = b.ProductCode,
                ProductName = b.ProductName,
                WarehouseName = b.WarehouseName,
                Unit = b.Unit,
                SystemQty = b.Quantity,
                CountedQty = b.Quantity,
                UnitCost = b.UnitCost,
                IsStopped = b.IsStopped,
            });
        }
        var totalValue = balances.Sum(r => r.TotalValue);
        var variances = _rows.Count(r => r.Variance != 0);
        var summary = $"إجمالي قيمة المخزون: {totalValue:N2} · أصناف: {balances.Count:N0} · فروقات: {variances:N0}";
        CountSummaryText.Text = summary;
        TotalText.Text = summary;
    }

    private async void OnPostCount(object sender, RoutedEventArgs e)
    {
        if (WarehouseCombo.SelectedValue is not long whId)
        {
            UiDialog.Show("اختر مستودعاً.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var variances = _rows.Count(r => r.Variance != 0);
        if (variances == 0)
        {
            UiDialog.Show("لا توجد فروقات للترحيل.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (UiDialog.Show($"ترحيل {variances} فرق جرد؟", "BRQOSH",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            var username = SecurityContext.ResolveUsername();
            // (م2-٢٤) التكلفة المُدخَلة تُمرَّر، والخدمةُ تقبلها فقط لصنفٍ لا تكلفة له.
            var lines = _rows
                .Select(r => (r.ProductId, r.BatchNumber, r.SystemQty, r.CountedQty, (decimal?)r.UnitCost))
                .ToList();
            var posted = await facade.GetService<StockCountAdjustmentService>().PostVariancesAsync(
                AppServices.CompanyId, whId, DateOnly.FromDateTime(DateTime.Today), lines, username);

            UiDialog.Show($"تم ترحيل {posted} صنف.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            UiDialog.Show(ex.Message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
