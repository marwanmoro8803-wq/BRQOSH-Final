using System.Windows;
using System.Windows.Controls;
using Brqosh.Application.Reporting;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Reporting;
using Brqosh.Desktop.Ui;
using Microsoft.Extensions.DependencyInjection;

namespace Brqosh.Desktop.Views.Pages;

public partial class CashFlowStatementPage : UserControl
{
    public record DisplayRow(string Section, string Label, decimal Amount, bool IsWarning = false);

    private CashFlowStatementResultDto? _lastResult;

    public CashFlowStatementPage()
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
        if (FromDate.SelectedDate is null || ToDate.SelectedDate is null)
            return;

        if (_lastResult is null)
        {
            UiFeedback.ShowInfo("اعرض التقرير أولاً.");
            return;
        }

        var from = DateOnly.FromDateTime(FromDate.SelectedDate.Value);
        var to = DateOnly.FromDateTime(ToDate.SelectedDate.Value);
        var username = Infrastructure.Security.SecurityContext.ResolveUsername();
        await DocumentPrintHelper.ExportAndDeliverAsync(
            s => s.ExportCashFlowStatementAsync(AppServices.CompanyId, from, to, username),
            Window.GetWindow(this),
            new DocumentDeliverOptions(OfferSaveAs: true));
    }

    private async Task RunAsync()
    {
        if (FromDate.SelectedDate is null || ToDate.SelectedDate is null)
            return;

        using var scope = AppServices.CreateInfrastructureScope();
        var query = scope.ServiceProvider.GetRequiredService<ICashFlowStatementQuery>();
        var result = await query.ExecuteAsync(
            AppServices.CompanyId,
            DateOnly.FromDateTime(FromDate.SelectedDate.Value),
            DateOnly.FromDateTime(ToDate.SelectedDate.Value));

        _lastResult = result;

        var rows = new List<DisplayRow>();
        AppendSection(rows, result.Operating);
        AppendSection(rows, result.Investing);
        AppendSection(rows, result.Financing);

        var difference = result.UnreconciledDifference;
        rows.Add(new DisplayRow(
            LocaleManager.T("مطابقة", "Reconciliation"),
            LocaleManager.T("فرق غير مطابق", "Unreconciled difference"),
            difference,
            IsWarning: difference != 0m));

        ReportGrid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(ReportGrid, rows.Count);
        TotalsText.Text =
            $"صافي التغيّر: {result.NetCashChange:N2} · افتتاحي: {result.OpeningCash:N2} · إغلاق: {result.ClosingCash:N2}"
            + (difference != 0m ? $" · {LocaleManager.T("فرق غير مطابق", "Unreconciled difference")}: {difference:N2}" : "");
        UiTestAutomation.PublishSummary(TotalsText, TotalsText.Text);
    }

    private static void AppendSection(List<DisplayRow> rows, CashFlowSectionDto section)
    {
        foreach (var line in section.Lines)
            rows.Add(new DisplayRow(section.NameAr, line.Label, line.Amount));
        rows.Add(new DisplayRow(section.NameAr, $"إجمالي {section.NameAr}", section.Subtotal));
    }
}
