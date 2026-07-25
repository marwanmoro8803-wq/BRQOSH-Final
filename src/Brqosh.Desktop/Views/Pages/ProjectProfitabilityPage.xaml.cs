using System.IO;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Application.Reporting;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Ui;
using Brqosh.Infrastructure.Reporting;
using Microsoft.Extensions.DependencyInjection;

namespace Brqosh.Desktop.Views.Pages;

public partial class ProjectProfitabilityPage : UserControl
{
    private IReadOnlyList<ProjectProfitabilityDto> _rows = [];

    public ProjectProfitabilityPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await UiFeedback.RunSafeAsync(ReloadAsync);
    }

    private async void OnRefresh(object sender, RoutedEventArgs e) =>
        await UiFeedback.RunSafeAsync(ReloadAsync);

    private async Task ReloadAsync()
    {
        using var scope = AppServices.CreateInfrastructureScope();
        var query = scope.ServiceProvider.GetRequiredService<IProjectProfitabilityQuery>();
        _rows = await query.ExecuteAsync(AppServices.CompanyId);
        Grid.ItemsSource = _rows;
        UiTestAutomation.PublishGridCount(Grid, _rows.Count);
        CountText.Text = $"المشاريع: {_rows.Count:N0} · {HijriCalendarHelper.TodayDual()}";
        RenderKpis();
    }

    private void RenderKpis()
    {
        KpiGrid.Children.Clear();
        KpiGrid.Children.Add(KpiMetricCardBuilder.Create(this,
            LocaleManager.T("إيراد مقدّر", "Recognized revenue"), "RV",
            _rows.Sum(r => r.RecognizedRevenueEstimate), "#2563EB", KpiValueKind.Currency));
        KpiGrid.Children.Add(KpiMetricCardBuilder.Create(this,
            LocaleManager.T("تكاليف فعلية", "Incurred cost"), "IC",
            _rows.Sum(r => r.IncurredCost), "#DC2626", KpiValueKind.Currency));
        KpiGrid.Children.Add(KpiMetricCardBuilder.Create(this,
            LocaleManager.T("هامش إجمالي", "Gross margin"), "GM",
            _rows.Sum(r => r.GrossMarginEstimate), "#0D9488", KpiValueKind.Currency));
    }

    private void OnExport(object sender, RoutedEventArgs e)
    {
        if (_rows.Count == 0)
        {
            UiFeedback.ShowInfo("لا بيانات للتصدير.");
            return;
        }

        var bytes = ExcelExportHelper.ToCsv(
            ["Code", "Name", "Contract", "EstCost", "Incurred", "Billed", "Revenue", "Retention", "Margin"],
            _rows.Select(r => new object?[]
            {
                r.Code, r.NameAr, r.ContractValue, r.EstimatedTotalCost, r.IncurredCost,
                r.BilledWork, r.RecognizedRevenueEstimate, r.RetentionHeld, r.GrossMarginEstimate,
            }));

        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            $"project-profitability-{DateTime.Now:yyyyMMdd-HHmm}.csv");
        File.WriteAllBytes(path, bytes);
        UiFeedback.ShowInfo($"تم الحفظ: {path}");
    }
}
