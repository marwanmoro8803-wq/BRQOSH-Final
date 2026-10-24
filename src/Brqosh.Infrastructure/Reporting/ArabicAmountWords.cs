namespace Brqosh.Infrastructure.Reporting;

/// <summary>
/// المبلغ كتابةً بالعربية (التفقيط) للمستندات المطبوعة: «فقط أربعة آلاف وثلاثمائة وسبعون ريالاً سعودياً لا غير».
///
/// <para>يُطبع تحت المبلغ في السند والفاتورة والمستخلص ليمنع التلاعب بالرقم وحده. المدى: حتى
/// 999 ملياراً — ما فوقه لا يقع في مستند منشأة، ويُرفض صراحةً بدل أن يُطبع نصٌّ مشوَّه.</para>
/// </summary>
public static class ArabicAmountWords
{
    private static readonly string[] Ones =
        ["", "واحد", "اثنان", "ثلاثة", "أربعة", "خمسة", "ستة", "سبعة", "ثمانية", "تسعة", "عشرة"];

    private static readonly string[] Teens =
        ["عشرة", "أحد عشر", "اثنا عشر", "ثلاثة عشر", "أربعة عشر", "خمسة عشر",
         "ستة عشر", "سبعة عشر", "ثمانية عشر", "تسعة عشر"];

    private static readonly string[] FeminineOnes =
        ["", "واحدة", "اثنتان", "ثلاث", "أربع", "خمس", "ست", "سبع", "ثماني", "تسع", "عشر"];

    private static readonly string[] FeminineTeens =
        ["عشر", "إحدى عشرة", "اثنتا عشرة", "ثلاث عشرة", "أربع عشرة", "خمس عشرة",
         "ست عشرة", "سبع عشرة", "ثماني عشرة", "تسع عشرة"];

    private static readonly string[] Tens =
        ["", "", "عشرون", "ثلاثون", "أربعون", "خمسون", "ستون", "سبعون", "ثمانون", "تسعون"];

    private static readonly string[] Hundreds =
        ["", "مائة", "مائتان", "ثلاثمائة", "أربعمائة", "خمسمائة", "ستمائة", "سبعمائة", "ثمانمائة", "تسعمائة"];

    // (مفرد، مثنّى، جمع 3-10، تمييز 11+) لكل رتبة: ألف · مليون · مليار
    private static readonly (string One, string Two, string Few, string Many)[] Scales =
    [
        ("ألف", "ألفان", "آلاف", "ألفاً"),
        ("مليون", "مليونان", "ملايين", "مليوناً"),
        ("مليار", "ملياران", "مليارات", "ملياراً"),
    ];

    private const decimal Max = 999_999_999_999.99m;

    /// <summary>«فقط … ريالاً سعودياً [و… هللة] لا غير» — يُقرَّب المبلغ إلى هللة، وتُهمل إشارته.</summary>
    public static string ForRiyals(decimal amount)
    {
        var abs = Math.Abs(Math.Round(amount, 2, MidpointRounding.AwayFromZero));
        if (abs > Max)
            throw new ArgumentOutOfRangeException(nameof(amount), "المبلغ أكبر من أن يُكتب تفقيطاً.");

        var riyals = (long)Math.Truncate(abs);
        var halalas = (int)Math.Round((abs - riyals) * 100, MidpointRounding.AwayFromZero);

        var parts = new List<string>(2);
        if (riyals > 0 || halalas == 0)
            parts.Add(RiyalPhrase(riyals));
        if (halalas > 0)
            parts.Add(HalalaPhrase(halalas));

        return "فقط " + string.Join(" و", parts) + " لا غير";
    }

    /// <summary>العدد الصحيح كلماتٍ — بلا عملة. <c>0</c> ⟵ «صفر».</summary>
    public static string Number(long value)
    {
        if (value < 0 || value > 999_999_999_999)
            throw new ArgumentOutOfRangeException(nameof(value));
        if (value == 0)
            return "صفر";

        var groups = new int[4];
        for (var i = 0; i < 4; i++)
        {
            groups[i] = (int)(value % 1000);
            value /= 1000;
        }

        var parts = new List<string>(4);
        for (var i = 3; i >= 1; i--)
            if (groups[i] > 0)
                parts.Add(ScaledGroup(groups[i], Scales[i - 1]));
        if (groups[0] > 0)
            parts.Add(Below1000(groups[0]));

        return string.Join(" و", parts);
    }

    private static string RiyalPhrase(long riyals)
    {
        if (riyals == 0) return "صفر ريال سعودي";
        if (riyals == 1) return "ريال سعودي واحد";
        if (riyals == 2) return "ريالان سعوديان";

        var words = Number(riyals);
        var tail = riyals % 100;
        return tail == 0 ? words + " ريال سعودي"
            : tail is >= 3 and <= 10 ? words + " ريالات سعودية"
            : words + " ريالاً سعودياً";
    }

    private static string HalalaPhrase(int halalas)
    {
        // الهللة مؤنّثة: «ثلاث هللات» لا «ثلاثة هللات».
        if (halalas == 1) return "هللة واحدة";
        if (halalas == 2) return "هللتان";
        return Below100(halalas, feminine: true) + (halalas <= 10 ? " هللات" : " هللة");
    }

    private static string ScaledGroup(int value, (string One, string Two, string Few, string Many) scale)
    {
        if (value == 1) return scale.One;
        if (value == 2) return scale.Two;
        // «مائتا ألف» لا «مائتان ألف»: المضافُ يفقد نونَ المثنّى.
        if (value == 200) return "مائتا " + scale.One;

        var tail = value % 100;
        var noun = tail == 0 || tail is 1 or 2 ? scale.One
            : tail is >= 3 and <= 10 ? scale.Few
            : scale.Many;
        return Below1000(value) + " " + noun;
    }

    private static string Below1000(int value)
    {
        var hundreds = value / 100;
        var rest = value % 100;
        var parts = new List<string>(2);

        if (hundreds > 0)
            parts.Add(Hundreds[hundreds]);
        if (rest > 0)
            parts.Add(Below100(rest));

        return string.Join(" و", parts);
    }

    private static string Below100(int value, bool feminine = false)
    {
        var ones = feminine ? FeminineOnes : Ones;
        var teens = feminine ? FeminineTeens : Teens;
        if (value <= 10) return ones[value];
        if (value < 20) return teens[value - 10];

        var unit = value % 10;
        var tens = value / 10;
        return unit == 0 ? Tens[tens] : ones[unit] + " و" + Tens[tens];
    }
}
