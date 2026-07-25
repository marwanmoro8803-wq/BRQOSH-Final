using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Security;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Automation;

namespace Brqosh.Desktop.Views.Pages;

public partial class ExchangeRatesPage : UserControl
{
    public record Row(
        string FromCurrency,
        string ToCurrency,
        string EffectiveDate,
        string Rate,
        string Source,
        string CreatedAtUtc);

    public ExchangeRatesPage()
    {
        InitializeComponent();
        SyncDatePicker.SelectedDate = DateTime.Today;
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (SyncBtn, PermissionService.CanPostAccounting));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var rows = await db.ExchangeRates.AsNoTracking()
            .Where(r => r.CompanyId == AppServices.CompanyId)
            .OrderByDescending(r => r.EffectiveDate)
            .ThenByDescending(r => r.Id)
            .Take(500)
            .Select(r => new Row(
                r.FromCurrencyCode,
                r.ToCurrencyCode,
                r.EffectiveDate.ToString("yyyy-MM-dd"),
                r.Rate.ToString("N6"),
                r.Source ?? "—",
                r.CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm")))
            .ToListAsync();

        Grid.ItemsSource = rows;

        UiTestAutomation.PublishGridCount(Grid, rows.Count);
        RateSummaryText.Text = rows.Count == 0
            ? "لا توجد أسعار مخزّنة — استخدم «جلب أسعار حية»."
            : $"أسعار مخزّنة: {rows.Count} · آخر تاريخ: {rows[0].EffectiveDate}";
    }

    private async void OnSyncLive(object sender, RoutedEventArgs e)
    {
        if (SyncDatePicker.SelectedDate is null)
        {
            UiFeedback.ShowWarning("اختر تاريخ الجلب.");
            return;
        }

        try
        {
            SyncBtn.IsEnabled = false;
            using var facade = new DesktopAccountingFacade();
            var asOf = DateOnly.FromDateTime(SyncDatePicker.SelectedDate.Value);
            var sync = facade.GetService<FxRateSyncService>();
            var result = await sync.SyncCompanyRatesAsync(AppServices.CompanyId, asOf);
            UiFeedback.ShowInfo(
                $"تم جلب {result.RecordedCount} سعر من {result.ProviderId} بتاريخ {result.AsOfDate:yyyy-MM-dd}.");
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowError(ex.Message);
        }
        finally
        {
            SyncBtn.IsEnabled = true;
        }
    }
}
