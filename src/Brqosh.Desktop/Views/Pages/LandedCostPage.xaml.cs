using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Security;
using Brqosh.Domain.Inventory;
using Brqosh.Infrastructure.Inventory;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class LandedCostPage : UserControl
{
  public class ProductShareRow
  {
    public long ProductId { get; init; }
    public long WarehouseId { get; init; }
    public string Code { get; init; } = "";
    public string NameAr { get; init; } = "";
    public decimal OnHand { get; init; }
    public decimal Wac { get; init; }
    public decimal Share { get; set; }
  }

  public LandedCostPage()
  {
    InitializeComponent();
    DateBox.SelectedDate = DateTime.Today;
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (AllocateBtn, PermissionService.CanPostAccounting));
            await UiFeedback.RunSafeAsync(ReloadAsync);
        };
  }

  private async Task ReloadAsync()
  {
    using var facade = new DesktopAccountingFacade();
    var db = facade.Db;
    var inv = facade.GetService<InventoryService>();
    var wh = await inv.GetDefaultWarehouseAsync(AppServices.CompanyId);
    var products = await db.Products
      .WhereActiveStockable(AppServices.CompanyId)
      .OrderBy(p => p.Code)
      .ToListAsync();

    var rows = new List<ProductShareRow>();
    foreach (var p in products)
    {
      var onHand = await inv.GetCompanyOnHandAsync(AppServices.CompanyId, p.Id);
      if (onHand <= 0) continue;
      rows.Add(new ProductShareRow
      {
        ProductId = p.Id,
        WarehouseId = wh.Id,
        Code = p.Code,
        NameAr = p.NameAr,
        OnHand = onHand,
        Wac = p.WeightedAverageCost,
        Share = onHand,
      });
    }

    Grid.ItemsSource = rows;

    UiTestAutomation.PublishGridCount(Grid, rows.Count);
    var totalOnHand = rows.Sum(r => r.OnHand);
    LandedCostSummaryText.Text =
      $"أصناف برصيد: {rows.Count} · إجمالي كمية قابلة للتوزيع: {totalOnHand:N2}";
  }

  private async void OnAllocate(object sender, RoutedEventArgs e) =>
    await UiFeedback.RunSafeAsync(AllocateAsync);

  private async Task AllocateAsync()
  {
    if (DateBox.SelectedDate is null)
      throw new InvalidOperationException("التاريخ مطلوب.");
    if (!NumericInput.TryParse(AmountBox.Text, out var amount))
      throw new InvalidOperationException("مبلغ التكلفة غير صالح.");
    if (string.IsNullOrWhiteSpace(DescBox.Text))
      throw new InvalidOperationException("البيان مطلوب.");

    var selected = (Grid.ItemsSource as IEnumerable<ProductShareRow> ?? [])
      .Where(r => r.Share > 0).ToList();
    if (selected.Count == 0)
      throw new InvalidOperationException("حدّد حصة توزيع لصنف واحد على الأقل.");

    var shares = selected.Select(r => (r.ProductId, r.WarehouseId, r.Share)).ToList();
    using var facade = new DesktopAccountingFacade();
    await facade.GetService<LandedCostAllocationService>().AllocateAsync(
      AppServices.CompanyId,
      DateOnly.FromDateTime(DateBox.SelectedDate.Value),
      DescBox.Text.Trim(),
      amount,
      shares,
      AppServices.CurrentUser?.Username ?? "admin",
      creditCash: !CreditPayableBox.IsChecked.GetValueOrDefault());

    UiFeedback.ShowSuccess("تم توزيع التكلفة الإضافية وترحيل القيد.");
    await ReloadAsync();
  }
}
