using System.Windows;
using System.Windows.Controls;
using Brqosh.Application.Reporting;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Reporting;
using Brqosh.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>‏قائمة التغيّرات في حقوق الملكية (IAS 1): حركة كل مكوّن للفترة من أول رصيده إلى آخره.</summary>
public partial class EquityStatementPage : UserControl
{
    private StatementOfChangesInEquityDto? _lastResult;

    public EquityStatementPage()
    {
        InitializeComponent();
        FromDate.SelectedDate = new DateTime(DateTime.Today.Year, 1, 1);
        ToDate.SelectedDate = DateTime.Today;
        Loaded += async (_, _) => await UiFeedback.RunSafeAsync(RunAsync);
    }

    private async void OnRun(object sender, RoutedEventArgs e) => await UiFeedback.RunSafeAsync(RunAsync);

    private async void OnExportPdf(object sender, RoutedEventArgs e) => await UiFeedback.RunSafeAsync(ExportPdfAsync);

    private async Task ExportPdfAsync()
    {
        if (_lastResult is null)
        {
            // ‏بلا نافذة حوار جديدة (سقف الحوارات): الرسالة في شريط الإجماليات.
            TotalsText.Text = LocaleManager.T("اعرض التقرير أولاً.", "Run the report first.");
            UiTestAutomation.PublishSummary(TotalsText, TotalsText.Text);
            return;
        }

        var username = SecurityContext.ResolveUsername();
        var result = _lastResult;
        await DocumentPrintHelper.ExportAndDeliverAsync(
            s => s.ExportStatementOfChangesInEquityAsync(AppServices.CompanyId, result.FromDate, result.ToDate, username),
            Window.GetWindow(this),
            new DocumentDeliverOptions(OfferSaveAs: true));
    }

    private async Task RunAsync()
    {
        if (FromDate.SelectedDate is null || ToDate.SelectedDate is null)
            return;

        using var scope = AppServices.CreateInfrastructureScope();
        var query = scope.ServiceProvider.GetRequiredService<IStatementOfChangesInEquityQuery>();
        _lastResult = await query.ExecuteAsync(
            AppServices.CompanyId,
            DateOnly.FromDateTime(FromDate.SelectedDate.Value),
            DateOnly.FromDateTime(ToDate.SelectedDate.Value));

        var rows = _lastResult.Components.ToList();
        ReportGrid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(ReportGrid, rows.Count);
        TotalsText.Text =
            LocaleManager.T("أول الفترة: ", "Opening: ") + _lastResult.TotalOpening.ToString("N2")
            + LocaleManager.T("    |    الدخل الشامل: ", "    |    Comprehensive income: ") + _lastResult.TotalComprehensiveIncome.ToString("N2")
            + LocaleManager.T("    |    معاملات الملّاك: ", "    |    Owners: ") + _lastResult.TotalTransactionsWithOwners.ToString("N2")
            + LocaleManager.T("    |    تحويلات: ", "    |    Transfers: ") + _lastResult.TotalTransfersWithinEquity.ToString("N2")
            + LocaleManager.T("    |    آخر الفترة: ", "    |    Closing: ") + _lastResult.TotalClosing.ToString("N2");
        UiTestAutomation.PublishSummary(TotalsText, TotalsText.Text);
    }
}
