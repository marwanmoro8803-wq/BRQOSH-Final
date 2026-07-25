using System.Windows;
using System.Windows.Controls;
using Brqosh.Application.Reporting;
using Brqosh.Desktop.Ui;
using Brqosh.Desktop.Automation;
using Microsoft.Extensions.DependencyInjection;

namespace Brqosh.Desktop.Views.Pages;

public partial class DataQualityPage : UserControl
{
    public record Row(string Code, string Severity, string Message);

    public DataQualityPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await UiFeedback.RunSafeAsync(RunAsync);
    }

    private async void OnRun(object sender, RoutedEventArgs e) => await UiFeedback.RunSafeAsync(RunAsync);

    private async Task RunAsync()
    {
        using var scope = AppServices.CreateInfrastructureScope();
        var service = scope.ServiceProvider.GetRequiredService<IDataQualityService>();
        var report = await service.RunAsync(AppServices.CompanyId);

        var rows = report.Findings
            .Select(f => new Row(f.Code, f.Severity, LocaleManager.T(f.MessageAr, f.MessageEn)))
            .ToList();
        Grid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(Grid, rows.Count);

        var errors = report.Findings.Count(f => f.Severity == "Error");
        var warnings = report.Findings.Count(f => f.Severity == "Warning");
        QualitySummaryText.Text =
            $"فحص {report.CheckedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm} · نتائج: {rows.Count} · أخطاء: {errors} · تحذيرات: {warnings}";
    }
}
