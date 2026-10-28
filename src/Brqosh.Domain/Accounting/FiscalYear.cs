namespace Brqosh.Domain.Accounting;

/// <summary>
/// السنةُ الماليةُ بشهر بدايةٍ لكل شركة (الافتراضيُّ يناير = السنةُ الميلادية كما كان).
/// تُسمّى بسنة بدايتها: <c>FY2026</c> — والترقيمُ (JV-/INV-) يبدأ من 1 في كل سنةٍ ماليةٍ بالبادئة نفسِها.
/// </summary>
public static class FiscalYear
{
    public static int NormalizeStartMonth(int startMonth) => startMonth is >= 1 and <= 12 ? startMonth : 1;

    /// <summary>سنةُ بداية السنة المالية التي يقع فيها التاريخ.</summary>
    public static int StartYearFor(DateOnly date, int startMonth)
    {
        startMonth = NormalizeStartMonth(startMonth);
        return date.Month >= startMonth ? date.Year : date.Year - 1;
    }

    public static DateOnly StartOf(int startYear, int startMonth) =>
        new(startYear, NormalizeStartMonth(startMonth), 1);

    public static DateOnly EndOf(int startYear, int startMonth) =>
        StartOf(startYear, startMonth).AddYears(1).AddDays(-1);

    /// <summary>أشهرُ السنة المالية الاثنا عشر بالترتيب (سنة، شهر).</summary>
    public static IEnumerable<(int Year, int Month)> Months(int startYear, int startMonth)
    {
        var start = StartOf(startYear, startMonth);
        for (var i = 0; i < 12; i++)
        {
            var d = start.AddMonths(i);
            yield return (d.Year, d.Month);
        }
    }

    public static string Name(int startYear) => $"FY{startYear}";
}
