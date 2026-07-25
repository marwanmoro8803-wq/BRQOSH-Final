using System.Windows;
using System.Windows.Controls;
using Brqosh.Application.Reporting;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Reporting;
using Brqosh.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Brqosh.Desktop.Views.Pages;

public partial class ConsolidatedTrialBalancePage : UserControl
{
    private IReadOnlyList<ConsolidatedTrialBalanceDto> _lastRows = [];

    public ConsolidatedTrialBalancePage()
    {
        InitializeComponent();
        var today = DateTime.Today;
        FromDate.SelectedDate = new DateTime(today.Year, 1, 1);
        ToDate.SelectedDate = today;
        Loaded += async (_, _) =>
        {
            await LoadGroupsAsync();
            await UiFeedback.RunSafeAsync(RunAsync);
        };
    }

    private async void OnRun(object sender, RoutedEventArgs e) => await UiFeedback.RunSafeAsync(RunAsync);

    private async void OnExportPdf(object sender, RoutedEventArgs e) => await UiFeedback.RunSafeAsync(ExportPdfAsync);

    private async Task ExportPdfAsync()
    {
        if (_lastRows.Count == 0 || GroupCombo.SelectedItem is not ConsolidatedReportPageHelper.GroupItem group)
        {
            UiFeedback.ShowInfo("اعرض التقرير أولاً.");
            return;
        }

        if (FromDate.SelectedDate is null || ToDate.SelectedDate is null)
            return;

        var from = DateOnly.FromDateTime(FromDate.SelectedDate.Value);
        var to = DateOnly.FromDateTime(ToDate.SelectedDate.Value);
        var username = SecurityContext.ResolveUsername();
        await DocumentPrintHelper.ExportAndDeliverAsync(
            s => s.ExportConsolidatedTrialBalanceAsync(AppServices.CompanyId, group.Id, from, to, username),
            Window.GetWindow(this),
            new DocumentDeliverOptions(OfferSaveAs: true));
    }

    private async Task LoadGroupsAsync()
    {
        await using var db = AppServices.CreateDb();
        GroupCombo.ItemsSource = await ConsolidatedReportPageHelper.LoadGroupsAsync(db, AppServices.CompanyId);
        if (GroupCombo.Items.Count > 0)
            GroupCombo.SelectedIndex = 0;
    }

    private async Task RunAsync()
    {
        if (GroupCombo.SelectedItem is not ConsolidatedReportPageHelper.GroupItem group)
            return;
        if (FromDate.SelectedDate is null || ToDate.SelectedDate is null)
            return;

        using var scope = AppServices.CreateInfrastructureScope();
        var query = scope.ServiceProvider.GetRequiredService<IConsolidatedTrialBalanceQuery>();
        _lastRows = await query.ExecuteAsync(
            group.Id,
            DateOnly.FromDateTime(FromDate.SelectedDate.Value),
            DateOnly.FromDateTime(ToDate.SelectedDate.Value));

        ReportGrid.ItemsSource = _lastRows;
        UiTestAutomation.PublishGridCount(ReportGrid, _lastRows.Count);

        var totalDebit = _lastRows.Sum(r => r.TotalDebit);
        var totalCredit = _lastRows.Sum(r => r.TotalCredit);
        var balanced = totalDebit == totalCredit ? "✓ الميزان الموحّد متوازن" : "✗ غير متوازن";
        TotalsText.Text = $"إجمالي المدين: {totalDebit:N2} | إجمالي الدائن: {totalCredit:N2} | {balanced}";
        UiTestAutomation.PublishSummary(TotalsText, TotalsText.Text);
    }
}
