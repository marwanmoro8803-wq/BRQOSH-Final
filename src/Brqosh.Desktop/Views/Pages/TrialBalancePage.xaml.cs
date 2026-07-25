using System.IO;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Application.Reporting;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Reporting;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Brqosh.Desktop.Views.Pages;

public partial class TrialBalancePage : UserControl
{
    private IReadOnlyList<TrialBalanceDto> _lastRows = [];

    public TrialBalancePage()
    {
        InitializeComponent();
        var today = DateTime.Today;
        FromDate.SelectedDate = new DateTime(today.Year, 1, 1);
        ToDate.SelectedDate = today;
        Loaded += async (_, _) => await UiFeedback.RunSafeAsync(RunAsync);
    }

    private async void OnRun(object sender, RoutedEventArgs e) => await UiFeedback.RunSafeAsync(RunAsync);

    private async void OnExportPdf(object sender, RoutedEventArgs e) => await UiFeedback.RunSafeAsync(ExportPdfAsync);

    private void OnExportExcel(object sender, RoutedEventArgs e) => ExportExcel();

    private void ExportExcel()
    {
        if (_lastRows.Count == 0)
        {
            UiFeedback.ShowInfo("اعرض التقرير أولاً.");
            return;
        }

        // (CEEF-215) xlsx حقيقي: أرقام بصيغة محاسبية، أعمدة بعرض مناسب، رأس مميّز
        // مجمَّد، وورقة من اليمين. كان CSV بلا أي تنسيق.
        var bytes = XlsxWriter.Build(
            "ميزان المراجعة",
            ["رقم الحساب", "اسم الحساب", "مجموع مدين", "مجموع دائن", "رصيد مدين", "رصيد دائن"],
            _lastRows.Select(r => (IReadOnlyList<XlsxWriter.Cell>)
            [
                XlsxWriter.Cell.Text(r.AccountCode),
                XlsxWriter.Cell.Text(r.AccountNameAr),
                XlsxWriter.Cell.Number(r.TotalDebit),
                XlsxWriter.Cell.Number(r.TotalCredit),
                XlsxWriter.Cell.Number(r.BalanceDebit),
                XlsxWriter.Cell.Number(r.BalanceCredit),
            ]).ToList());

        var path = Path.Combine(Path.GetTempPath(), $"brqosh-trial-{DateTime.Now:yyyyMMddHHmmss}.xlsx");
        File.WriteAllBytes(path, bytes);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        UiFeedback.ShowSuccess("تم تصدير ملف Excel.");
    }

    private async Task ExportPdfAsync()
    {
        if (_lastRows.Count == 0)
        {
            MessageBox.Show("اعرض التقرير أولاً.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (FromDate.SelectedDate is null || ToDate.SelectedDate is null)
            return;

        var from = DateOnly.FromDateTime(FromDate.SelectedDate.Value);
        var to = DateOnly.FromDateTime(ToDate.SelectedDate.Value);
        var username = SecurityContext.ResolveUsername();
        await DocumentPrintHelper.ExportAndDeliverAsync(
            s => s.ExportTrialBalanceAsync(AppServices.CompanyId, from, to, username),
            Window.GetWindow(this),
            new DocumentDeliverOptions(OfferSaveAs: true));
    }

    private async Task RunAsync()
    {
        if (FromDate.SelectedDate is null || ToDate.SelectedDate is null)
            return;

        using var scope = AppServices.CreateInfrastructureScope();
        var query = scope.ServiceProvider.GetRequiredService<ITrialBalanceQuery>();
        _lastRows = await query.ExecuteAsync(
            AppServices.CompanyId,
            DateOnly.FromDateTime(FromDate.SelectedDate.Value),
            DateOnly.FromDateTime(ToDate.SelectedDate.Value));

        ReportGrid.ItemsSource = _lastRows;
        UiTestAutomation.PublishGridCount(ReportGrid, _lastRows.Count);

        var totalDebit = _lastRows.Sum(r => r.TotalDebit);
        var totalCredit = _lastRows.Sum(r => r.TotalCredit);
        var balanced = totalDebit == totalCredit ? "✓ الميزان متوازن" : "✗ الميزان غير متوازن — راجع القيود!";
        TotalsText.Text = $"إجمالي المدين: {totalDebit:N2}    |    إجمالي الدائن: {totalCredit:N2}    |    {balanced}";
        UiTestAutomation.PublishSummary(TotalsText, TotalsText.Text);
    }
}
