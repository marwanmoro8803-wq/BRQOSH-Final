using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Infrastructure.Inventory;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class StockMovementDialog : Window
{
    public record WarehouseOption(long Id, string NameAr);
    public record ProductOption(long Id, string NameAr, decimal Wac);

    public class LineRow
    {
        public long ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
    }

    private readonly ObservableCollection<LineRow> _rows = [];
    private List<ProductOption> _products = [];

    public StockMovementDialog()
    {
        InitializeComponent();
        DateBox.SelectedDate = DateTime.Today;
        TypeCombo.SelectedIndex = 0;
        LinesGrid.ItemsSource = _rows;
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        await using var db = AppServices.CreateDb();
        var warehouses = await db.Warehouses.Where(w => w.CompanyId == AppServices.CompanyId && w.IsActive)
            .Select(w => new WarehouseOption(w.Id, w.NameAr)).ToListAsync();
        WarehouseCombo.ItemsSource = warehouses;
        ToWarehouseCombo.ItemsSource = warehouses;
        if (warehouses.Count > 0) { WarehouseCombo.SelectedIndex = 0; ToWarehouseCombo.SelectedIndex = warehouses.Count > 1 ? 1 : 0; }

        _products = await db.Products.Where(p => p.CompanyId == AppServices.CompanyId && p.IsActive && p.Type == ProductType.Stockable)
            .Select(p => new ProductOption(p.Id, $"{p.Code} — {p.NameAr}", p.WeightedAverageCost)).ToListAsync();

        ProductColumn.ItemsSource = _products;
    }

    private void OnTypeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TypeCombo.SelectedItem is not ComboBoxItem item) return;
        ToWarehousePanel.Visibility = item.Tag?.ToString() == "Transfer" ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void OnPost(object sender, RoutedEventArgs e)
    {
        try
        {
            LinesGrid.CommitEdit(DataGridEditingUnit.Row, true);
            if (DateBox.SelectedDate is null) throw new AccountingException("التاريخ مطلوب.");
            if (WarehouseCombo.SelectedValue is not long whId) throw new AccountingException("اختر المستودع.");
            if (TypeCombo.SelectedItem is not ComboBoxItem typeItem) throw new AccountingException("اختر نوع الحركة.");

            var type = Enum.Parse<StockMovementType>(typeItem.Tag!.ToString()!);
            long? toWh = type == StockMovementType.Transfer && ToWarehouseCombo.SelectedValue is long tw ? tw : null;

            var desc = type switch
            {
                StockMovementType.Opening => "رصيد افتتاحي للمخزون",
                StockMovementType.AdjustmentIn => "تسوية جرد — زيادة",
                StockMovementType.AdjustmentOut => "تسوية جرد — نقص",
                StockMovementType.Transfer => "تحويل مخزني",
                _ => "حركة مخزنية",
            };

            if (_rows.All(r => r.ProductId <= 0 || r.Quantity <= 0))
                throw new AccountingException("أضف سطراً واحداً على الأقل.");

            using var facade = new DesktopAccountingFacade();
            var posting = facade.GetService<StockMovementPostingService>();

            if (type == StockMovementType.Transfer)
            {
                if (toWh is null || toWh == whId)
                    throw new AccountingException("اختر مستودع وجهة مختلفاً عن المصدر.");

                var movement = StockMovement.CreateDraft(AppServices.CompanyId,
                    DateOnly.FromDateTime(DateBox.SelectedDate.Value), StockMovementType.Transfer,
                    whId, desc, toWh);
                foreach (var row in _rows.Where(r => r.ProductId > 0 && r.Quantity > 0))
                {
                    var wac = _products.First(p => p.Id == row.ProductId).Wac;
                    movement.AddLine(row.ProductId, row.Quantity, wac);
                }
                await posting.PostAsync(movement);
            }
            else
            {
                var movement = StockMovement.CreateDraft(AppServices.CompanyId,
                    DateOnly.FromDateTime(DateBox.SelectedDate.Value), type, whId, desc, toWh);
                foreach (var row in _rows.Where(r => r.ProductId > 0 && r.Quantity > 0))
                {
                    var cost = type == StockMovementType.AdjustmentOut
                        ? _products.First(p => p.Id == row.ProductId).Wac
                        : row.UnitCost;
                    movement.AddLine(row.ProductId, row.Quantity, cost);
                }
                await posting.PostAsync(movement);
            }

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
}
