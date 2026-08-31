using System.Windows;
using System.Windows.Controls;
using Brqosh.Application.Reporting;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Reporting;
using Brqosh.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class BalanceSheetPage : UserControl
{
    public record DisplayRow(string Section, string AccountCode, string AccountNameAr, decimal Amount);

    private BalanceSheetResultDto? _lastResult;

    public BalanceSheetPage()
    {
        InitializeComponent();
        AsOfDate.SelectedDate = DateTime.Today;
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
            s => s.ExportBalanceSheetAsync(AppServices.CompanyId, _lastResult.AsOfDate, username),
            Window.GetWindow(this),
            new DocumentDeliverOptions(OfferSaveAs: true));
    }

    private async Task RunAsync()
    {
        if (AsOfDate.SelectedDate is null)
            return;

        using var scope = AppServices.CreateInfrastructureScope();
        var query = scope.ServiceProvider.GetRequiredService<IBalanceSheetQuery>();
        _lastResult = await query.ExecuteAsync(
            AppServices.CompanyId,
            DateOnly.FromDateTime(AsOfDate.SelectedDate.Value));

        var rows = _lastResult.Assets.Select(x => new DisplayRow("أصول", x.AccountCode, x.AccountNameAr, x.Amount))
            .Concat(_lastResult.Liabilities.Select(x => new DisplayRow("التزامات", x.AccountCode, x.AccountNameAr, x.Amount)))
            .Concat(_lastResult.Equity.Select(x => new DisplayRow("حقوق ملكية", x.AccountCode, x.AccountNameAr, x.Amount)))
            .ToList();

        ReportGrid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(ReportGrid, rows.Count);
        var balanced = _lastResult.IsBalanced ? "✓ الميزانية متوازنة" : "✗ غير متوازنة — راجع القيود";
        TotalsText.Text =
            $"الأصول: {_lastResult.TotalAssets:N2}    |    الالتزامات + حقوق الملكية: {_lastResult.TotalLiabilitiesAndEquity:N2}    |    {balanced}";
        UiTestAutomation.PublishSummary(TotalsText, TotalsText.Text);
    }
}
