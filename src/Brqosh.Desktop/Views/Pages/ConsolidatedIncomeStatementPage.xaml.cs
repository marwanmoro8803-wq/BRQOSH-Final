using System.Windows;
using System.Windows.Controls;
using Brqosh.Application.Reporting;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Reporting;
using Brqosh.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Brqosh.Desktop.Views.Pages;

public partial class ConsolidatedIncomeStatementPage : UserControl
{
    public record DisplayRow(string Section, string AccountCode, string AccountNameAr, decimal Amount, int CompanyCount);

    private ConsolidatedIncomeStatementResultDto? _lastResult;

    public ConsolidatedIncomeStatementPage()
    {
        InitializeComponent();
        var today = DateTime.Today;
        FromDate.SelectedDate = new DateTime(today.Year, 1, 1);
        ToDate.SelectedDate = today;
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
            s => s.ExportConsolidatedIncomeStatementAsync(
                AppServices.CompanyId, group.Id, _lastResult.FromDate, _lastResult.ToDate, username),
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
        if (FromDate.SelectedDate is null || ToDate.SelectedDate is null) return;

        using var scope = AppServices.CreateInfrastructureScope();
        var query = scope.ServiceProvider.GetRequiredService<IConsolidatedIncomeStatementQuery>();
        _lastResult = await query.ExecuteAsync(
            group.Id,
            DateOnly.FromDateTime(FromDate.SelectedDate.Value),
            DateOnly.FromDateTime(ToDate.SelectedDate.Value));

        var rows = _lastResult.RevenueRows.Select(x => new DisplayRow("إيرادات", x.AccountCode, x.AccountNameAr, x.Amount, x.CompanyCount))
            .Concat(_lastResult.ExpenseRows.Select(x => new DisplayRow("مصروفات", x.AccountCode, x.AccountNameAr, x.Amount, x.CompanyCount)))
            .ToList();
        ReportGrid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(ReportGrid, rows.Count);
        TotalsText.Text =
            $"إجمالي الإيرادات: {_lastResult.TotalRevenue:N2} | المصروفات: {_lastResult.TotalExpenses:N2} | صافي الدخل: {_lastResult.NetIncome:N2} ({_lastResult.ReportingCurrencyCode})";
    }
}
