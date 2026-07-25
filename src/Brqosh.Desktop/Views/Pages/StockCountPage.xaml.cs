using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Infrastructure.Inventory;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Automation;

using Brqosh.Infrastructure.Security;
using Brqosh.Desktop.Security;

namespace Brqosh.Desktop.Views.Pages;

public partial class StockCountPage : UserControl
{
    public class CountRow
    {
        public long ProductId { get; init; }
        public long WarehouseId { get; init; }
        public string ProductCode { get; init; } = "";
        public string ProductName { get; init; } = "";
        public string WarehouseName { get; init; } = "";
        public string Unit { get; init; } = "";
        public decimal SystemQty { get; init; }
        public decimal CountedQty { get; set; }
        public decimal Variance => CountedQty - SystemQty;
        public decimal UnitCost { get; init; }
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
        foreach (var b in balances)
        {
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
            MessageBox.Show("اختر مستودعاً.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var variances = _rows.Count(r => r.Variance != 0);
        if (variances == 0)
        {
            MessageBox.Show("لا توجد فروقات للترحيل.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show($"ترحيل {variances} فرق جرد؟", "BRQOSH",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            var username = SecurityContext.ResolveUsername();
            var lines = _rows.Select(r => (r.ProductId, r.SystemQty, r.CountedQty)).ToList();
            var posted = await facade.GetService<StockCountAdjustmentService>().PostVariancesAsync(
                AppServices.CompanyId, whId, DateOnly.FromDateTime(DateTime.Today), lines, username);

            MessageBox.Show($"تم ترحيل {posted} صنف.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
