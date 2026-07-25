using System.Windows;
using System.Windows.Controls;
using Brqosh.Application.Reporting;
using Brqosh.Desktop.Automation;
using Microsoft.Extensions.DependencyInjection;

namespace Brqosh.Desktop.Views.Pages;

public partial class PlatformEventsPage : UserControl
{
    public PlatformEventsPage()
    {
        InitializeComponent();
        var today = DateTime.Today;
        FromDate.SelectedDate = today.AddDays(-7);
        ToDate.SelectedDate = today;
        Loaded += async (_, _) => await UiFeedback.RunSafeAsync(RunAsync);
    }

    private async void OnRun(object sender, RoutedEventArgs e) => await UiFeedback.RunSafeAsync(RunAsync);

    private async Task RunAsync()
    {
        if (FromDate.SelectedDate is null || ToDate.SelectedDate is null)
            return;

        var fromUtc = DateTime.SpecifyKind(FromDate.SelectedDate.Value.Date, DateTimeKind.Local).ToUniversalTime();
        var toUtc = DateTime.SpecifyKind(ToDate.SelectedDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Local).ToUniversalTime();

        using var scope = AppServices.CreateInfrastructureScope();
        var query = scope.ServiceProvider.GetRequiredService<IEventTrackingQuery>();
        var rows = await query.ExecuteAsync(AppServices.CompanyId, fromUtc, toUtc);

        Grid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(Grid, rows.Count);

        var total = rows.Sum(r => r.Count);
        EventsSummaryText.Text =
            $"أنواع أحداث: {rows.Count} · إجمالي الأحداث: {total} · الفترة: {FromDate.SelectedDate:yyyy-MM-dd} → {ToDate.SelectedDate:yyyy-MM-dd}";
    }
}
