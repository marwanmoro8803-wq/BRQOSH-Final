using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Logistics;
using Brqosh.Infrastructure.Logistics;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط3-٤ب) <b>رحلاتُ التوصيل وربحيّةُ المسار — والخاسرُ يظهر أوّلاً.</b>
///
/// <para><b>ما كان:</b> الشحنةُ تحمل أجرتَها، <b>ولا شيءَ يحمل تكلفةَ إيصالها</b>. فشركةٌ
/// تُشغّل اثني عشرَ مساراً <b>لا تعرف أيَّها يخسر</b> — ويغطّي ربحُ التسعةِ خسارةَ الثلاثة،
/// <b>فيُوسَّع في الخاسر لأنّه «كثيرُ الشحنات»</b>.</para>
///
/// <para><b>وجدولُ المسارات مُرتَّبٌ بالهامش صعوداً</b>: الخاسرُ في الأعلى. <b>ورقمٌ لا
/// يُرى أوّلاً لا يُعالَج</b> — ومَن يفتح الشاشةَ يرى أسوأَ مساراته قبل أيّ شيء.</para>
///
/// <para><b>وعمودُ «فرق العهدة» هو حارسُ هـ-٢٠ معروضاً</b>: صفرُه شرطُ التسوية.</para>
/// </summary>
public partial class DeliveryTripsPage : UserControl
{
    public record Row(long Id, string TripNumber, string RouteName, string VehiclePlate,
        string DepartureDate, string StatusText, int Shipments, decimal DriverFloat,
        decimal Expenses, decimal Variance, TripStatus Status);

    public DeliveryTripsPage()
    {
        InitializeComponent();
        var today = DateTime.Today;
        FromBox.SelectedDate = new DateTime(today.Year, today.Month, 1);
        ToBox.SelectedDate = today;

        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (AddBtn, PermissionService.CanPost),
                (FloatBtn, PermissionService.CanPostAccounting),
                (SettleBtn, PermissionService.CanPostAccounting));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        if (FromBox.SelectedDate is null || ToBox.SelectedDate is null) return;

        try
        {
            var from = DateOnly.FromDateTime(FromBox.SelectedDate.Value);
            var to = DateOnly.FromDateTime(ToBox.SelectedDate.Value);

            await using var db = AppServices.CreateDb();

            var trips = await db.DeliveryTrips
                .Where(t => t.CompanyId == AppServices.CompanyId
                            && t.DepartureDate >= from && t.DepartureDate <= to)
                .OrderByDescending(t => t.DepartureDate)
                .ToListAsync();

            var rows = trips.Select(t => new Row(
                t.Id, t.TripNumber, t.RouteName, t.VehiclePlate,
                t.DepartureDate.ToString("yyyy-MM-dd"), StatusText(t.Status),
                t.Shipments.Count, t.DriverFloat, t.TotalExpenses, t.FloatVariance,
                t.Status)).ToList();

            Grid.ItemsSource = rows;
            UiTestAutomation.PublishGridCount(Grid, rows.Count);

            using var facade = new DesktopAccountingFacade();
            var routes = await facade.GetService<DeliveryTripService>()
                .RouteProfitabilityAsync(AppServices.CompanyId, from, to);

            RouteGrid.ItemsSource = routes;

            var losing = routes.Where(r => r.Margin < 0m).ToList();
            SummaryText.Text = routes.Count == 0
                ? "لا رحلاتٍ في هذه المدة — والربحيّةُ تُقاس بالمسار لا بالجملة."
                : $"مسارات: {routes.Count:N0} · الأجور: {routes.Sum(r => r.FreightRevenue):N2} · "
                  + $"المصاريف: {routes.Sum(r => r.TripCost):N2} · "
                  + $"الهامش: {routes.Sum(r => r.Margin):N2}"
                  + (losing.Count > 0
                      ? $" · ⚠️ مساراتٌ خاسرة: {losing.Count:N0} بخسارة {losing.Sum(r => r.Margin):N2}"
                        + " — ويغطّيها ربحُ غيرها فتبدو الشركةُ رابحة."
                      : "");
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        var dialog = new DeliveryTripDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true) await ReloadAsync();
    }

    private async void OnAdvance(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row)
        {
            UiFeedback.ShowWarning("اختر رحلةً أولاً.");
            return;
        }

        try
        {
            var amount = ParseAmount();

            using var facade = new DesktopAccountingFacade();
            await facade.GetService<DeliveryTripService>().AdvanceFloatAsync(
                AppServices.CompanyId, row.Id, amount, DateOnly.FromDateTime(DateTime.Today));

            SummaryText.Text = "سُلِّمت العهدة — وهي أصل في الميزانية لا مصروف حتى تُسوَّى.";
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnSettle(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row)
        {
            UiFeedback.ShowWarning("اختر رحلةً أولاً.");
            return;
        }

        try
        {
            var returned = ParseAmount();

            using var facade = new DesktopAccountingFacade();
            await facade.GetService<DeliveryTripService>().SettleAsync(
                AppServices.CompanyId, row.Id, returned,
                DateOnly.FromDateTime(DateTime.Today));

            SummaryText.Text = "سُوّيت العهدة — ورصيدها صار صفراً، والمصاريف قُيّدت على المسار.";
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    /// <summary>
    /// المبلغُ من شريط الأدوات — <b>ومرئيٌّ قبل الضغط لا مطويٌّ في نافذة</b>.
    /// </summary>
    private decimal ParseAmount()
    {
        if (NumericInput.TryParse(AmountBox.Text, out var value))
            return value;

        throw new Brqosh.Domain.Accounting.AccountingException(
            $"المبلغ غير صالح — ووردَ «{AmountBox.Text}».");
    }

    private static string StatusText(TripStatus status) => status switch
    {
        TripStatus.Planned => LocaleManager.T("مخطَّطة", "Planned"),
        TripStatus.Dispatched => LocaleManager.T("منطلقة", "Dispatched"),
        TripStatus.Completed => LocaleManager.T("منتهية", "Completed"),
        TripStatus.Settled => LocaleManager.T("مُسوّاة", "Settled"),
        _ => LocaleManager.T("ملغاة", "Cancelled"),
    };
}
