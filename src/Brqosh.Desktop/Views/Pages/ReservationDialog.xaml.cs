using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Hospitality;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط6-٤) إنشاءُ حجز — <b>ويُحفظ محجوزاً، والإنزالُ زرٌّ صريح</b>.
///
/// <para>فالإنزالُ يفحص التداخلَ ويشغل الغرفةَ ويفتح حسابَ النزيل — <b>وهو حدثٌ ماليّ</b>،
/// لا حفظُ بيانات.</para>
/// </summary>
public partial class ReservationDialog : Window
{
    private sealed record Option(long Id, string Label, decimal Rate = 0m)
    {
        public override string ToString() => Label;
    }

    public ReservationDialog()
    {
        InitializeComponent();
        CheckInBox.SelectedDate = DateTime.Today;
        CheckOutBox.SelectedDate = DateTime.Today.AddDays(1);
        Loaded += async (_, _) => await LoadListsAsync();
    }

    private async Task LoadListsAsync()
    {
        try
        {
            await using var db = AppServices.CreateDb();

            // ‏**والغرفةُ في الصيانة لا تُعرض** — والنطاقُ يرفض الإنزالَ فيها، فعرضُها
            // ‏يُغري بحجزٍ يُرفض عند الباب.
            var rooms = await db.Rooms.AsNoTracking()
                .Where(r => r.CompanyId == AppServices.CompanyId && r.Status != RoomStatus.Maintenance)
                .OrderBy(r => r.RoomNumber)
                .Select(r => new { r.Id, r.RoomNumber, r.NightlyRate })
                .ToListAsync();

            RoomBox.ItemsSource = rooms
                .Select(r => new Option(r.Id, r.RoomNumber, r.NightlyRate))
                .ToList();

            var guests = await db.Customers.AsNoTracking()
                .Where(c => c.CompanyId == AppServices.CompanyId && c.IsActive)
                .OrderBy(c => c.NameAr)
                .Select(c => new { c.Id, c.NameAr })
                .ToListAsync();

            GuestBox.ItemsSource = guests.Select(g => new Option(g.Id, g.NameAr)).ToList();

            if (rooms.Count > 0) RoomBox.SelectedIndex = 0;
            if (guests.Count > 0) GuestBox.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    /// <summary><b>وسعرُ الغرفة يُقترح ولا يُفرض</b> — فالعروضُ والمواسم تُغيّره.</summary>
    private void OnRoomChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RoomBox.SelectedItem is Option room && room.Rate > 0m)
            RateBox.Text = room.Rate.ToString(CultureInfo.InvariantCulture);
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            if (RoomBox.SelectedItem is not Option room)
                throw new AccountingException("اختر الغرفة.");
            if (GuestBox.SelectedItem is not Option guest)
                throw new AccountingException("اختر النزيل.");
            if (CheckInBox.SelectedDate is null || CheckOutBox.SelectedDate is null)
                throw new AccountingException("حدّد الوصول والمغادرة.");

            var reservation = Reservation.Create(AppServices.CompanyId, room.Id, guest.Id,
                NumberBox.Text,
                DateOnly.FromDateTime(CheckInBox.SelectedDate.Value),
                DateOnly.FromDateTime(CheckOutBox.SelectedDate.Value),
                Number(RateBox.Text, "سعر الليلة"));

            await using var db = AppServices.CreateDb();
            db.Reservations.Add(reservation);
            await db.SaveChangesAsync();
            DialogResult = true;
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private static decimal Number(string text, string field) =>
        decimal.TryParse((text ?? "").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new AccountingException($"قيمة «{field}» غير صالحة — ووردت «{text}».");

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
