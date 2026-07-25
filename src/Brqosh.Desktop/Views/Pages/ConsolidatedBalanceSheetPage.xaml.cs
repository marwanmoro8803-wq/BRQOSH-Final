using System.Windows;
using System.Windows.Controls;
using Brqosh.Application.Reporting;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Reporting;
using Brqosh.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Brqosh.Desktop.Views.Pages;

public partial class ConsolidatedBalanceSheetPage : UserControl
{
    public record DisplayRow(string Section, string AccountCode, string AccountNameAr, decimal Amount, int CompanyCount);

    private ConsolidatedBalanceSheetResultDto? _lastResult;

    public ConsolidatedBalanceSheetPage()
    {
        InitializeComponent();
        AsOfDate.SelectedDate = DateTime.Today;
        Loaded += async (_, _) => { await LoadGroupsAsync(); await UiFeedback.RunSafeAsync(RunAsync); };
    }

    private async void OnRun(object sender, RoutedEventArgs e) => await UiFeedback.RunSafeAsync(RunAsync);

    private async void OnExportPdf(object sender, RoutedEventArgs e) => await UiFeedback.RunSafeAsync(ExportPdfAsync);

    private async Task ExportPdfAsync()
    {
        if (_lastResult is null || GroupCombo.SelectedItem is not ConsolidatedReportPageHelper.GroupItem group)
        {
            UiFeedback.ShowInfo("اعرض التقرير أولاً.");
            return;
        }

        var username = SecurityContext.ResolveUsername();
        await DocumentPrintHelper.ExportAndDeliverAsync(
            s => s.ExportConsolidatedBalanceSheetAsync(
                AppServices.CompanyId, group.Id, _lastResult.AsOfDate, username),
            Window.GetWindow(this),
            new DocumentDeliverOptions(OfferSaveAs: true));
    }

    private async Task LoadGroupsAsync()
    {
        await using var db = AppServices.CreateDb();
        GroupCombo.ItemsSource = await ConsolidatedReportPageHelper.LoadGroupsAsync(db, AppServices.CompanyId);
        if (GroupCombo.Items.Count > 0) GroupCombo.SelectedIndex = 0;
    }

    private async Task RunAsync()
    {
        if (GroupCombo.SelectedItem is not ConsolidatedReportPageHelper.GroupItem group) return;
        if (AsOfDate.SelectedDate is null) return;

        using var scope = AppServices.CreateInfrastructureScope();
        var query = scope.ServiceProvider.GetRequiredService<IConsolidatedBalanceSheetQuery>();
        _lastResult = await query.ExecuteAsync(
            group.Id, DateOnly.FromDateTime(AsOfDate.SelectedDate.Value));

        var result = _lastResult;
        var rows = result.Assets.Select(x => new DisplayRow("أصول", x.AccountCode, x.AccountNameAr, x.Amount, x.CompanyCount))
            .Concat(result.Liabilities.Select(x => new DisplayRow("التزامات", x.AccountCode, x.AccountNameAr, x.Amount, x.CompanyCount)))
            .Concat(result.Equity.Select(x => new DisplayRow("حقوق ملكية", x.AccountCode, x.AccountNameAr, x.Amount, x.CompanyCount)))
            .ToList();
        ReportGrid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(ReportGrid, rows.Count);
        var balanced = result.IsBalanced ? "✓ متوازن" : "✗ غير متوازن";
        TotalsText.Text =
            $"أصول: {result.TotalAssets:N2} | التزامات+ملكية: {result.TotalLiabilities + result.TotalEquity:N2} | {balanced} ({result.ReportingCurrencyCode})";
    }
}
