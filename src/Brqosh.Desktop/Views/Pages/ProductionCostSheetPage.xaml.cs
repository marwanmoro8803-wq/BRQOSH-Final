using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Automation;
using Brqosh.Infrastructure.Manufacturing;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١٢٢ · ‏`manufacturing#9`) <b>كشفُ تكلفة الإنتاج</b> — لكلّ أمرٍ مكتملٍ في المدى: الموادُّ والأجرُ والتحميلُ
/// ‏وتكلفةُ الوحدة والإنتاجيّة، من قيد إكماله (<see cref="ProductionCostSheetQuery"/>)؛ واستهلاكُ موادّ الأمر المحدَّد تحته.
/// ‏والشاشةُ تعرض ولا تحسب.
/// </summary>
public partial class ProductionCostSheetPage : UserControl
{
    public ProductionCostSheetPage()
    {
        InitializeComponent();
        var today = DateTime.Today;
        FromDate.SelectedDate = new DateTime(today.Year, 1, 1);
        ToDate.SelectedDate = today;
        Loaded += async (_, _) => await UiFeedback.RunSafeAsync(ReloadAsync);
    }

    private async void OnRefresh(object sender, RoutedEventArgs e) =>
        await UiFeedback.RunSafeAsync(ReloadAsync);

    private async void OnOrderSelected(object sender, SelectionChangedEventArgs e) =>
        await UiFeedback.RunSafeAsync(LoadUsageAsync);

    private async Task ReloadAsync()
    {
        var from = DateOnly.FromDateTime(FromDate.SelectedDate ?? DateTime.Today);
        var to = DateOnly.FromDateTime(ToDate.SelectedDate ?? DateTime.Today);

        await using var db = AppServices.CreateDb();
        var rows = await new ProductionCostSheetQuery(db).ExecuteAsync(AppServices.CompanyId, from, to);
        Grid.ItemsSource = rows;
        UsageGrid.ItemsSource = null;
        UiTestAutomation.PublishGridCount(Grid, rows.Count);
        CountText.Text = $"{rows.Count:N0} · {rows.Sum(r => r.TotalCost):N2}";
    }

    private async Task LoadUsageAsync()
    {
        if (Grid.SelectedItem is not ProductionCostRow row)
        {
            UsageGrid.ItemsSource = null;
            return;
        }

        await using var db = AppServices.CreateDb();
        UsageGrid.ItemsSource = await new ProductionCostSheetQuery(db).MaterialUsageAsync(AppServices.CompanyId, row.WorkOrderId);
    }
}
