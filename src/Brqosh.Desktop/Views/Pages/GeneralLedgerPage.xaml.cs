using System.IO;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Application.Reporting;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Reporting;
using Brqosh.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Brqosh.Desktop.Views.Pages;

public partial class GeneralLedgerPage : UserControl
{
    public record DisplayRow(string AccountCode, string AccountNameAr, DateOnly EntryDate,
        string EntryNumber, string Description, decimal Debit, decimal Credit, decimal RunningBalance);

    private readonly PagingState _paging = new(200);
    private IReadOnlyList<DisplayRow> _allRows = [];
    private IReadOnlyList<DisplayRow> _pageRows = [];

    public GeneralLedgerPage()
    {
        InitializeComponent();
        var today = DateTime.Today;
        FromDate.SelectedDate = new DateTime(today.Year, 1, 1);
        ToDate.SelectedDate = today;
        Loaded += async (_, _) => await UiFeedback.RunSafeAsync(RunAsync);
    }

    private async void OnRun(object sender, RoutedEventArgs e)
    {
        _paging.Reset();
        await UiFeedback.RunSafeAsync(RunAsync);
    }

    private async void OnExportPdf(object sender, RoutedEventArgs e) => await UiFeedback.RunSafeAsync(ExportPdfAsync);

    private void OnPrev(object sender, RoutedEventArgs e)
    {
        _paging.Prev();
        ApplyPage();
    }

    private void OnNext(object sender, RoutedEventArgs e)
    {
        _paging.Next();
        ApplyPage();
    }

    private async Task ExportPdfAsync()
    {
        if (_allRows.Count == 0)
        {
            UiFeedback.ShowInfo("اعرض التقرير أولاً.");
            return;
        }

        if (FromDate.SelectedDate is null || ToDate.SelectedDate is null)
            return;

        var from = DateOnly.FromDateTime(FromDate.SelectedDate.Value);
        var to = DateOnly.FromDateTime(ToDate.SelectedDate.Value);
        var filter = string.IsNullOrWhiteSpace(AccountFilter.Text) ? null : AccountFilter.Text.Trim();
        var username = SecurityContext.ResolveUsername();
        await DocumentPrintHelper.ExportAndDeliverAsync(
            s => s.ExportGeneralLedgerAsync(AppServices.CompanyId, from, to, username, filter),
            Window.GetWindow(this),
            new DocumentDeliverOptions(OfferSaveAs: true));
    }

    private async Task RunAsync()
    {
        if (FromDate.SelectedDate is null || ToDate.SelectedDate is null)
            return;

        var filter = string.IsNullOrWhiteSpace(AccountFilter.Text) ? null : AccountFilter.Text.Trim();

        using var scope = AppServices.CreateInfrastructureScope();
        var query = scope.ServiceProvider.GetRequiredService<IGeneralLedgerQuery>();
        var accounts = await query.ExecuteAsync(
            AppServices.CompanyId,
            DateOnly.FromDateTime(FromDate.SelectedDate.Value),
            DateOnly.FromDateTime(ToDate.SelectedDate.Value),
            filter);

        _allRows = accounts
            .SelectMany(a => a.Lines.Select(l => new DisplayRow(
                a.AccountCode, a.AccountNameAr, l.EntryDate, l.EntryNumber,
                l.Description, l.Debit, l.Credit, l.RunningBalance)))
            .ToList();

        _paging.SetTotal(_allRows.Count);
        GlSummaryText.Text =
            $"حسابات: {accounts.Count:N0} · أسطر: {_allRows.Count:N0} · مدين: {_allRows.Sum(r => r.Debit):N2} · دائن: {_allRows.Sum(r => r.Credit):N2}";
        ApplyPage();
    }

    private void ApplyPage()
    {
        _pageRows = _allRows.Skip(_paging.Skip).Take(_paging.PageSize).ToList();
        ReportGrid.ItemsSource = _pageRows;
        UiTestAutomation.PublishGridCount(ReportGrid, _pageRows.Count);
        CountText.Text = _paging.StatusText("أسطر التقرير");
        PrevBtn.IsEnabled = _paging.CanPrev;
        NextBtn.IsEnabled = _paging.CanNext;
    }
}
