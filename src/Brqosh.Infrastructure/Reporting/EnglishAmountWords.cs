namespace Brqosh.Infrastructure.Reporting;

/// <summary>
/// المبلغُ كتابةً بالإنجليزيّة للريال السعوديّ — «Saudi Riyals One Thousand Two Hundred Fifty and Halalas Fifty Only».
/// الصيغةُ المصرفيّةُ المعتادة: الريالاتُ بالحروف ثم الهللاتُ بالحروف، «Only» في الآخر. السقفُ كالعربيّة (٩٩٩٬٩٩٩٬٩٩٩٬٩٩٩٫٩٩).
/// </summary>
public static class EnglishAmountWords
{
    private static readonly string[] Small =
    [
        "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve",
        "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen",
    ];

    private static readonly string[] Tens = ["", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety"];

    public static string ForRiyals(decimal amount)
    {
        if (amount < 0m)
            amount = Math.Abs(amount);
        var rounded = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        if (rounded > 999_999_999_999.99m)
            throw new ArgumentOutOfRangeException(nameof(amount), "المبلغ أكبر من الحدّ المدعوم.");

        var riyals = (long)Math.Truncate(rounded);
        var halalas = (int)Math.Round((rounded - riyals) * 100m, MidpointRounding.AwayFromZero);

        var text = riyals > 0 ? $"Saudi Riyals {Number(riyals)}" : "Saudi Riyals Zero";
        if (halalas > 0)
            text += $" and Halalas {Number(halalas)}";
        return text + " Only";
    }

    public static string Number(long value)
    {
        if (value < 20)
            return Small[value];
        if (value < 100)
            return Tens[value / 10] + (value % 10 == 0 ? "" : " " + Small[value % 10]);
        if (value < 1_000)
            return Small[value / 100] + " Hundred" + (value % 100 == 0 ? "" : " " + Number(value % 100));

        (long Unit, string Name)[] scales = [(1_000_000_000, "Billion"), (1_000_000, "Million"), (1_000, "Thousand")];
        foreach (var (unit, name) in scales)
        {
            if (value >= unit)
                return Number(value / unit) + " " + name + (value % unit == 0 ? "" : " " + Number(value % unit));
        }

        return value.ToString();
    }
}
