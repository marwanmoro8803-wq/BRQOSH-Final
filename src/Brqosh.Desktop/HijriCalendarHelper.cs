using System.Globalization;

namespace Brqosh.Desktop;

/// <summary>تحويل وتقويم هجري للعرض (طلب UX مؤسسةسي).</summary>
public static class HijriCalendarHelper
{
    private static readonly HijriCalendar Hijri = new();
    private static readonly CultureInfo ArSa = CultureInfo.GetCultureInfo("ar-SA");

    public static string FormatHijri(DateTime date) =>
        string.Create(CultureInfo.InvariantCulture,
            $"{Hijri.GetYear(date):0000}/{Hijri.GetMonth(date):00}/{Hijri.GetDayOfMonth(date):00} هـ");

    public static string FormatHijri(DateOnly date) => FormatHijri(date.ToDateTime(TimeOnly.MinValue));

    public static string FormatDual(DateTime date) =>
        $"{date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} م · {FormatHijri(date)}";

    public static string TodayDual() => FormatDual(DateTime.Today);

    public static string MonthNameHijri(DateTime date) =>
        date.ToString("MMMM yyyy", ArSa);
}
