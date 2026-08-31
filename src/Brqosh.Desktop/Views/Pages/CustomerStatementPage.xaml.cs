using System.Windows;
using System.Windows.Controls;
using Brqosh.Application.Reporting;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Reporting;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class CustomerStatementPage : UserControl
{
    public record CustomerOption(long Id, string Display);
    public record StatementRow(string Date, string DocumentNumber, string DocumentType,
        decimal Debit, decimal Credit, decimal RunningBalance);

    private long? _lastCustomerId;
    private IReadOnlyList<CustomerStatementLineDto> _lastLines = [];

    public CustomerStatementPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await UiFeedback.RunSafeAsync(LoadCustomersAsync);
    }

    private async Task LoadCustomersAsync()
    {
        await using var db = AppServices.CreateDb();
        var customers = await db.Customers
            .Where(c => c.CompanyId == AppServices.CompanyId && c.IsActive)
            .OrderBy(c => c.NameAr)
            .Select(c => new CustomerOption(c.Id, c.NameAr))
            .ToListAsync();
        CustomerCombo.ItemsSource = customers;
        if (customers.Count > 0)
        {
            CustomerCombo.SelectedIndex = 0;
            await RunAsync();
        }
    }

    private async void OnCustomerChanged(object sender, SelectionChangedEventArgs e) => await UiFeedback.RunSafeAsync(RunAsync);
    private async void OnRun(object sender, RoutedEventArgs e) => await UiFeedback.RunSafeAsync(RunAsync);

    private async void OnExportPdf(object sender, RoutedEventArgs e) => await UiFeedback.RunSafeAsync(ExportPdfAsync);

    private async Task ExportPdfAsync()
    {
        if (_lastCustomerId is not long customerId || _lastLines.Count == 0)
        {
            UiDialog.Show("اعرض الكشف أولاً.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var username = SecurityContext.ResolveUsername();
        await DocumentPrintHelper.ExportAndDeliverAsync(
            s => s.ExportCustomerStatementAsync(AppServices.CompanyId, customerId, username),
            Window.GetWindow(this),
            new DocumentDeliverOptions(OfferSaveAs: true));
    }

    private async Task RunAsync()
    {
        if (CustomerCombo.SelectedValue is not long customerId)
            return;

        using var scope = AppServices.CreateInfrastructureScope();
        var query = scope.ServiceProvider.GetRequiredService<ICustomerStatementQuery>();
        var result = await query.ExecuteAsync(AppServices.CompanyId, customerId);
        _lastLines = result.Lines;
        _lastCustomerId = customerId;

        var displayRows = _lastLines.Select(r => new StatementRow(
            r.Date.ToString("yyyy-MM-dd"), r.DocumentNumber, r.DocumentType,
            r.Debit, r.Credit, r.RunningBalance)).ToList();
        ReportGrid.ItemsSource = displayRows;
        UiTestAutomation.PublishGridCount(ReportGrid, displayRows.Count);
        TotalsText.Text = $"الرصيد المستحق على العميل: {result.ClosingBalance:N2} ر.س";
        UiTestAutomation.PublishSummary(TotalsText, TotalsText.Text);
    }
}
