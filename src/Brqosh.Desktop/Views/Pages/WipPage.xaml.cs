using System.Windows.Controls;
using Brqosh.Infrastructure.Manufacturing;
using Brqosh.Desktop.Automation;

namespace Brqosh.Desktop.Views.Pages;

public partial class WipPage : UserControl
{
    public WipPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await UiFeedback.RunSafeAsync(ReloadAsync);
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var rows = await new WipQuery(db).ListOpenAsync(AppServices.CompanyId);
        Grid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(Grid, rows.Count);
    }
}
