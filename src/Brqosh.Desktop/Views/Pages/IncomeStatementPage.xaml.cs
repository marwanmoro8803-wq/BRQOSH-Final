using System.Windows;
using System.Windows.Controls;
using Brqosh.Application.Reporting;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Reporting;
using Brqosh.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class IncomeStatementPage : UserControl
{
    public record DisplayRow(string Section, string AccountCode, string AccountNameAr, decimal Amount);

    private IncomeStatementResultDto? _lastResult;

    public IncomeStatementPage()
    {
        InitializeComponent();
        var today = DateTime.Today;
        FromDate.SelectedDate = new DateTime(today.Year, 1, 1);
        ToDate.SelectedDate = today;
        Loaded += async (_, _) => await UiFeedback.RunSafeAsync(RunAsync);
    }

    private async void OnRun(object sender, RoutedEventArgs e) => await UiFeedback.RunSafeAsync(RunAsync);

    private async void OnExportPdf(object sender, RoutedEventArgs e) => await UiFeedback.RunSafeAsync(ExportPdfAsync);

    private async Task ExportPdfAsync()
    {
        if (_lastResult is null)
        {
            UiDialog.Show("اعرض التقرير أولاً.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var username = SecurityContext.ResolveUsername();
        await DocumentPrintHelper.ExportAndDeliverAsync(
            s => s.ExportIncomeStatementAsync(AppServices.CompanyId, _lastResult.FromDate, _lastResult.ToDate, username),
            Window.GetWindow(this),
            new DocumentDeliverOptions(OfferSaveAs: true));
    }

    private async Task RunAsync()
    {
        if (FromDate.SelectedDate is null || ToDate.SelectedDate is null)
            return;

        using var scope = AppServices.CreateInfrastructureScope();
        var query = scope.ServiceProvider.GetRequiredService<IIncomeStatementQuery>();
        _lastResult = await query.ExecuteAsync(
            AppServices.CompanyId,
            DateOnly.FromDateTime(FromDate.SelectedDate.Value),
            DateOnly.FromDateTime(ToDate.SelectedDate.Value));

        var rows = _lastResult.RevenueRows.Select(x => new DisplayRow("إيرادات", x.AccountCode, x.AccountNameAr, x.Amount))
            .Concat(_lastResult.ExpenseRows.Select(x => new DisplayRow("مصروفات", x.AccountCode, x.AccountNameAr, x.Amount)))
            .ToList();

        ReportGrid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(ReportGrid, rows.Count);
        TotalsText.Text =
            $"إجمالي الإيرادات: {_lastResult.TotalRevenue:N2}    |    إجمالي المصروفات: {_lastResult.TotalExpenses:N2}    |    صافي الدخل: {_lastResult.NetIncome:N2}";
        UiTestAutomation.PublishSummary(TotalsText, TotalsText.Text);
    }
}
