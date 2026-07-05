using System.Windows;
using System.Windows.Controls;
using Brqosh.Infrastructure.Reporting;

namespace Brqosh.Desktop.Views.Pages;

public partial class TrialBalancePage : UserControl
{
    public TrialBalancePage()
    {
        InitializeComponent();
        var today = DateTime.Today;
        FromDate.SelectedDate = new DateTime(today.Year, 1, 1);
        ToDate.SelectedDate = today;
        Loaded += async (_, _) => await RunAsync();
    }

    private async void OnRun(object sender, RoutedEventArgs e) => await RunAsync();

    private async Task RunAsync()
    {
        if (FromDate.SelectedDate is null || ToDate.SelectedDate is null)
            return;

        await using var db = AppServices.CreateDb();
        var rows = await new TrialBalanceQuery(db).ExecuteAsync(
            AppServices.CompanyId,
            DateOnly.FromDateTime(FromDate.SelectedDate.Value),
            DateOnly.FromDateTime(ToDate.SelectedDate.Value));

        ReportGrid.ItemsSource = rows;

        var totalDebit = rows.Sum(r => r.TotalDebit);
        var totalCredit = rows.Sum(r => r.TotalCredit);
        var balanced = totalDebit == totalCredit ? "✓ الميزان متوازن" : "✗ الميزان غير متوازن — راجع القيود!";
        TotalsText.Text = $"إجمالي المدين: {totalDebit:N2}    |    إجمالي الدائن: {totalCredit:N2}    |    {balanced}";
    }
}
