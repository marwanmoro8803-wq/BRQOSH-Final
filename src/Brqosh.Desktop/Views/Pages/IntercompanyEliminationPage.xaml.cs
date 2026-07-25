using System.Windows;
using System.Windows.Controls;
using Brqosh.Application.Reporting;
using Brqosh.Desktop.Automation;
using Microsoft.Extensions.DependencyInjection;

namespace Brqosh.Desktop.Views.Pages;

public partial class IntercompanyEliminationPage : UserControl
{
    private IReadOnlyList<IntercompanyEliminationDto> _lastRows = [];

    public IntercompanyEliminationPage()
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
        var query = scope.ServiceProvider.GetRequiredService<IIntercompanyEliminationQuery>();
        _lastRows = await query.ExecuteAsync(
            group.Id,
            DateOnly.FromDateTime(FromDate.SelectedDate.Value),
            DateOnly.FromDateTime(ToDate.SelectedDate.Value));

        ReportGrid.ItemsSource = _lastRows;
        UiTestAutomation.PublishGridCount(ReportGrid, _lastRows.Count);

        var totalDebit = _lastRows.Sum(r => r.EliminatedDebit);
        var totalCredit = _lastRows.Sum(r => r.EliminatedCredit);
        EliminationSummaryText.Text =
            $"حسابات IC: {_lastRows.Count} · مدين مُزال: {totalDebit:N2} · دائن مُزال: {totalCredit:N2}";
        TotalsText.Text =
            $"إجمالي الإزالة — مدين: {totalDebit:N2} | دائن: {totalCredit:N2} | {(totalDebit == totalCredit ? "✓ متوازن" : "✗ غير متوازن")}";
        UiTestAutomation.PublishSummary(TotalsText, TotalsText.Text);
    }
}
