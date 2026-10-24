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

public partial class SupplierStatementPage : UserControl
{
    public record SupplierOption(long Id, string Display);
    public record StatementRow(string Date, string DocumentNumber, string DocumentType,
        decimal Debit, decimal Credit, decimal RunningBalance);

    private long? _lastSupplierId;
    private IReadOnlyList<SupplierStatementLineDto> _lastLines = [];

    public SupplierStatementPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await UiFeedback.RunSafeAsync(LoadSuppliersAsync);
    }

    private async Task LoadSuppliersAsync()
    {
        await using var db = AppServices.CreateDb();
        var suppliers = await db.Suppliers
            .Where(s => s.CompanyId == AppServices.CompanyId && s.IsActive)
            .OrderBy(s => s.NameAr)
            .Select(s => new SupplierOption(s.Id, s.NameAr))
            .ToListAsync();
        SupplierCombo.ItemsSource = suppliers;
        if (suppliers.Count > 0)
        {
            SupplierCombo.SelectedIndex = 0;
            await RunAsync();
        }
    }

    private async void OnSupplierChanged(object sender, SelectionChangedEventArgs e) => await UiFeedback.RunSafeAsync(RunAsync);
    private async void OnRun(object sender, RoutedEventArgs e) => await UiFeedback.RunSafeAsync(RunAsync);

    private async void OnExportPdf(object sender, RoutedEventArgs e) => await UiFeedback.RunSafeAsync(ExportPdfAsync);

    private async Task ExportPdfAsync()
    {
        if (_lastSupplierId is not long supplierId || _lastLines.Count == 0)
        {
            UiDialog.Show("اعرض الكشف أولاً.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var username = SecurityContext.ResolveUsername();
        await DocumentPrintHelper.ExportAndDeliverAsync(
            s => s.ExportSupplierStatementAsync(AppServices.CompanyId, supplierId, username),
            Window.GetWindow(this),
            new DocumentDeliverOptions(OfferSaveAs: true));
    }

    private async Task RunAsync()
    {
        if (SupplierCombo.SelectedValue is not long supplierId)
            return;

        using var scope = AppServices.CreateInfrastructureScope();
        var query = scope.ServiceProvider.GetRequiredService<ISupplierStatementQuery>();
        var result = await query.ExecuteAsync(AppServices.CompanyId, supplierId);
        _lastLines = result.Lines;
        _lastSupplierId = supplierId;

        var displayRows = _lastLines.Select(r => new StatementRow(
            r.Date.ToString("yyyy-MM-dd"), r.DocumentNumber, r.DocumentType,
            r.Debit, r.Credit, r.RunningBalance)).ToList();
        ReportGrid.ItemsSource = displayRows;
        UiTestAutomation.PublishGridCount(ReportGrid, displayRows.Count);
        TotalsText.Text = $"الرصيد المستحق للمورد: {result.ClosingBalance:N2} {CurrencyLabel.Short}";
        UiTestAutomation.PublishSummary(TotalsText, TotalsText.Text);
    }
}
