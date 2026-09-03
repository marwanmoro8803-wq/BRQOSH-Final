using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Hospitality;
using Brqosh.Infrastructure.Hospitality;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط6-٤) <b>الحجوزاتُ وحساباتُ النزلاء — الشاشةُ التي بلا وجودها بقي `ط3-٣ب` بلا باب.</b>
///
/// <para><b>و«تقييدُ الليالي» دفعةٌ يُطلقها المستخدم</b> — ولا مُجدوِلَ في هذا التطبيق.
/// <b>وإيرادُ الغرفة يُستحقّ بمرور الليلة لا بالوصول</b>: نزيلٌ يصل ٢٨ ديسمبر لعشر ليالٍ
/// وقُيّدت العشرُ يومَ وصوله <b>يُدخل سبعَ ليالٍ من إيراد يناير في ديسمبر</b>.</para>
///
/// <para><b>والمغادرةُ تُقيّد ما بقي قبل التصفية</b> — فلا تُصفّى حسابٌ ناقصٌ ليالٍ حتّى
/// لو لم تُشغَّل الدفعة.</para>
/// </summary>
public partial class ReservationsPage : UserControl
{
    public record Row(long Id, string Number, string RoomNumber, string GuestName,
        string CheckIn, string CheckOut, int Nights, decimal Rate, decimal FolioTotal,
        string Status, bool IsBooked, bool IsCheckedIn);

    public ReservationsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (AddBtn, PermissionService.CanPost),
                (CheckInBtn, PermissionService.CanPost),
                (AccrueBtn, PermissionService.CanPost),
                (CheckOutBtn, PermissionService.CanPost),
                (DepositBtn, PermissionService.CanPost));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();

        var reservations = await db.Reservations.AsNoTracking()
            .Where(r => r.CompanyId == AppServices.CompanyId)
            .OrderByDescending(r => r.CheckInDate)
            .ToListAsync();

        var rooms = await db.Rooms.AsNoTracking()
            .Where(r => r.CompanyId == AppServices.CompanyId)
            .ToDictionaryAsync(r => r.Id, r => r.RoomNumber);

        var guests = await db.Customers.AsNoTracking().IgnoreQueryFilters()
            .Where(c => c.CompanyId == AppServices.CompanyId)
            .ToDictionaryAsync(c => c.Id, c => c.NameAr);

        var folios = await db.GuestFolios.AsNoTracking()
            .Where(f => f.CompanyId == AppServices.CompanyId)
            .ToListAsync();

        var totals = folios.GroupBy(f => f.ReservationId)
            .ToDictionary(g => g.Key, g => g.Sum(f => f.Total));

        var rows = reservations.Select(r => new Row(
            r.Id, r.ReservationNumber,
            rooms.TryGetValue(r.RoomId, out var room) ? room : $"#{r.RoomId}",
            guests.TryGetValue(r.GuestCustomerId, out var guest) ? guest : $"#{r.GuestCustomerId}",
            r.CheckInDate.ToString("yyyy-MM-dd"), r.CheckOutDate.ToString("yyyy-MM-dd"),
            r.Nights, r.RatePerNight,
            totals.TryGetValue(r.Id, out var total) ? total : 0m,
            StatusText(r.Status),
            r.Status == ReservationStatus.Booked,
            r.Status == ReservationStatus.CheckedIn)).ToList();

        Grid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(Grid, rows.Count);

        var inHouse = reservations.Count(r => r.Status == ReservationStatus.CheckedIn);
        SummaryText.Text = $"حجوزات: {rows.Count:N0} · نزلاء في الفندق: {inHouse:N0}";
    }

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        if (new ReservationDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnCheckIn(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row) return;
        if (!row.IsBooked)
        {
            UiFeedback.ShowWarning("لا يُنزَل إلا حجزٌ قائم.");
            return;
        }

        try
        {
            using var facade = new DesktopAccountingFacade();
            await facade.GetService<FolioService>().CheckInAsync(AppServices.CompanyId, row.Id);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnAccrue(object sender, RoutedEventArgs e)
    {
        try
        {
            using var facade = new DesktopAccountingFacade();
            var result = await facade.GetService<FolioService>().AccrueNightsAsync(
                AppServices.CompanyId, DateOnly.FromDateTime(DateTime.Today));

            await ReloadAsync();

            SummaryText.Text = result.Refusals.Count == 0
                ? $"قُيّدت {result.NightsPosted:N0} ليلة على {result.FoliosTouched:N0} حساب."
                : $"قُيّدت {result.NightsPosted:N0} ليلة · وتعذّر: " + string.Join(" · ", result.Refusals);
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnCheckOut(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row) return;
        if (!row.IsCheckedIn)
        {
            UiFeedback.ShowWarning("لا يُغادر إلا نزيلٌ نازل.");
            return;
        }

        if (UiDialog.Show("مغادرة النزيل وتصفية حسابه بفاتورة؟", row.Number,
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            await facade.GetService<FolioService>().CheckOutAsync(AppServices.CompanyId, row.Id,
                DateOnly.FromDateTime(DateTime.Today));
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private static string StatusText(ReservationStatus status) => status switch
    {
        ReservationStatus.Booked => "محجوز",
        ReservationStatus.CheckedIn => "نازل",
        ReservationStatus.CheckedOut => "غادر",
        ReservationStatus.Cancelled => "ملغى",
        ReservationStatus.NoShow => "لم يحضر",
        _ => status.ToString(),
    };

    /// <summary>
    /// (CEEF-232 · ح-١) <b>قبضُ عربون — والمالُ يُقيَّد التزاماً لا إيراداً.</b>
    ///
    /// <para><b>وكان لا مسارَ له أصلاً</b>: لا حقلَ عربونٍ على الحجز، ولا خدمة، ولا زرّ —
    /// <b>وحسابُ الدفعات المقدَّمة كاتبُه الوحيد خدمةُ المقاولات</b>. فالعربونُ إمّا
    /// <b>مالٌ في الصندوق بلا قيد</b>، أو يُقبض بسند قبضٍ <b>فيُطفئ ذمّةً لم تنشأ بعد</b>
    /// ويُعترف بإيرادِ إقامةٍ لم تقع.</para>
    /// </summary>
    private async void OnTakeDeposit(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row) return;

        var dialog = new TextInputDialog("قبض عربون", "مبلغ العربون:", "0")
            { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true)
            return;

        if (!NumericInput.TryParse(dialog.Value, out var amount) || amount <= 0)
        {
            UiFeedback.ShowWarning("أدخل مبلغاً موجباً.");
            return;
        }

        try
        {
            using var facade = new DesktopAccountingFacade();
            await facade.GetService<ReservationDepositService>().TakeAsync(
                AppServices.CompanyId, row.Id, amount,
                DateOnly.FromDateTime(DateTime.Today), SecurityContext.ResolveUsername());

            await ReloadAsync();
            UiDialog.Show("قُبض العربون وقُيّد التزاماً.", $"{amount:N2}",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }
}
