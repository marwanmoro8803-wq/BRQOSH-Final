using System.Windows;
using System.Windows.Controls;
using Brqosh.Domain.Accounting;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class FiscalPeriodsPage : UserControl
{
    public record PeriodRow(long Id, string Code, DateOnly StartDate, DateOnly EndDate,
        string StatusAr, int EntryCount, string ClosedAt);

    public FiscalPeriodsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var periods = await db.FiscalPeriods
            .Where(p => p.CompanyId == AppServices.CompanyId)
            .OrderByDescending(p => p.Code)
            .ToListAsync();

        var rows = new List<PeriodRow>();
        foreach (var period in periods)
        {
            var count = await db.JournalEntries.CountAsync(e =>
                e.CompanyId == AppServices.CompanyId &&
                e.EntryDate >= period.StartDate && e.EntryDate <= period.EndDate &&
                e.Status == JournalEntryStatus.Posted);

            rows.Add(new PeriodRow(
                period.Id, period.Code, period.StartDate, period.EndDate,
                period.Status == FiscalPeriodStatus.Closed ? "مقفلة 🔒" : "مفتوحة",
                count,
                period.ClosedAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "—"));
        }

        PeriodsGrid.ItemsSource = rows;
    }

    private async void OnClose(object sender, RoutedEventArgs e) => await Transition(p => p.Close());

    private async void OnReopen(object sender, RoutedEventArgs e) => await Transition(p => p.Reopen());

    private async Task Transition(Action<FiscalPeriod> action)
    {
        if (PeriodsGrid.SelectedItem is not PeriodRow row)
        {
            MessageBox.Show("اختر فترة من الجدول أولاً.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            await using var db = AppServices.CreateDb();
            var period = await db.FiscalPeriods.SingleAsync(p => p.Id == row.Id);
            action(period);
            await db.SaveChangesAsync();
            await ReloadAsync();
        }
        catch (AccountingException ex)
        {
            MessageBox.Show(ex.Message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
