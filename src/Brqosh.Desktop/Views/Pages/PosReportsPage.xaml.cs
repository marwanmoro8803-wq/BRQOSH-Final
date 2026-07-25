using System.Windows.Controls;
using Brqosh.Desktop.Reporting;
using Brqosh.Infrastructure.Security;
using Brqosh.Desktop.Automation;

namespace Brqosh.Desktop.Views.Pages;

public partial class PosReportsPage : UserControl
{
    public PosReportsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await UiFeedback.RunSafeAsync(ReloadAsync);
    }

    private async void OnRefresh(object sender, System.Windows.RoutedEventArgs e)
        => await UiFeedback.RunSafeAsync(ReloadAsync);

    private async void OnExportPdf(object sender, System.Windows.RoutedEventArgs e)
    {
        var username = SecurityContext.ResolveUsername();
        await DocumentPrintHelper.ExportAndDeliverAsync(
            s => s.ExportPosDailyReportAsync(AppServices.CompanyId, username),
            System.Windows.Window.GetWindow(this),
            new DocumentDeliverOptions(OfferSaveAs: true));
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var rows = await new Infrastructure.Retail.PosReportQuery(db).DailySummaryAsync(AppServices.CompanyId);
        Grid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(Grid, rows.Count);
    }
}
