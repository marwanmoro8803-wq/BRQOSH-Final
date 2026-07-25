using System.Windows;
using System.Windows.Controls;
using Brqosh.Application.Reporting;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Reporting;
using Brqosh.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Brqosh.Desktop.Views.Pages;

public partial class VatReturnPage : UserControl
{
    private VatReturnDto? _lastResult;

    public VatReturnPage()
    {
        InitializeComponent();
        var today = DateTime.Today;
        FromDate.SelectedDate = new DateTime(today.Year, today.Month, 1);
        ToDate.SelectedDate = today;
        Loaded += async (_, _) => await UiFeedback.RunSafeAsync(RunAsync);
    }

    private async void OnRun(object sender, RoutedEventArgs e) => await UiFeedback.RunSafeAsync(RunAsync);

    private async void OnExportPdf(object sender, RoutedEventArgs e) => await UiFeedback.RunSafeAsync(ExportPdfAsync);

    private async Task ExportPdfAsync()
    {
        if (_lastResult is null)
        {
            MessageBox.Show("اعرض الإقرار أولاً.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var username = SecurityContext.ResolveUsername();
        await DocumentPrintHelper.ExportAndDeliverAsync(
            s => s.ExportVatReturnAsync(AppServices.CompanyId, _lastResult.FromDate, _lastResult.ToDate, username),
            Window.GetWindow(this),
            new DocumentDeliverOptions(OfferSaveAs: true));
    }

    private async Task RunAsync()
    {
        if (FromDate.SelectedDate is null || ToDate.SelectedDate is null)
            return;

        using var scope = AppServices.CreateInfrastructureScope();
        var query = scope.ServiceProvider.GetRequiredService<IVatReturnQuery>();
        _lastResult = await query.ExecuteAsync(
            AppServices.CompanyId,
            DateOnly.FromDateTime(FromDate.SelectedDate.Value),
            DateOnly.FromDateTime(ToDate.SelectedDate.Value));

        var r = _lastResult;
        ReportPanel.Children.Clear();
        AddRow("مبيعات خاضعة (وعاء)", r.SalesSubtotal);
        AddRow("ضريبة المخرجات — مبيعات", r.OutputVat);
        AddRow("مستخلصات أعمال (وعاء)", r.ProgressBillingsSubtotal);
        AddRow("ضريبة المخرجات — مستخلصات", r.ProgressBillingsVat);
        AddRow("إشعارات دائن (وعاء)", r.CreditNotesSubtotal);
        AddRow("ضريبة مرتجعة (إشعارات دائن)", r.CreditNotesVat);
        AddRow("صافي ضريبة المخرجات", r.NetOutputVat, bold: true);
        AddRow("مشتريات (وعاء)", r.PurchasesSubtotal);
        AddRow("ضريبة مدخلات قابلة للخصم", r.DeductibleInputVat);
        AddRow("إشعارات مدينة (وعاء)", r.DebitNotesSubtotal);
        AddRow("ضريبة مدخلات مُخفّضة (إشعارات مدينة)", r.DebitNotesDeductibleVat);
        AddRow("صافي ضريبة المدخلات", r.NetDeductibleInputVat, bold: true);
        AddRow("صافي الضريبة المستحقة/المستردة", r.NetVatPayable, bold: true);

        var label = r.NetVatPayable >= 0 ? "مستحقة للهيئة" : "مستردة/مرحّلة";
        TotalsText.Text = $"صافي الضريبة: {r.NetVatPayable:N2} — {label}";
    }

    private void AddRow(string label, decimal value, bool bold = false)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });

        var name = new TextBlock
        {
            Text = label,
            FontSize = 14,
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
        };
        var amount = new TextBlock
        {
            Text = value.ToString("N2"),
            FontSize = 14,
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        Grid.SetColumn(amount, 1);
        grid.Children.Add(name);
        grid.Children.Add(amount);
        ReportPanel.Children.Add(grid);
    }
}
