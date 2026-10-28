namespace Brqosh.Infrastructure.Licensing;

/// <summary>
/// خططُ اشتراك برقوش — تجريبيٌّ سبعةَ أيام، وسنويٌّ، ودائمٌ بلا انتهاء.
///
/// <para>القيمةُ الرقميّةُ هي ما يُرمَّز في مفتاح الترخيص الرقميّ (<see cref="NumericLicense"/>):
/// ١ تجريبي، ٢ سنوي، ٣ دائم — فلا تُبدَّل القيمُ بعد صدور مفاتيح.</para>
/// </summary>
public enum LicensePlan
{
    /// <summary>تجريبي — ٧ أيامٍ من تاريخ الإصدار.</summary>
    Trial7 = 1,

    /// <summary>سنوي — ٣٦٥ يوماً من تاريخ الإصدار.</summary>
    Annual = 2,

    /// <summary>دائم — لا ينتهي.</summary>
    Lifetime = 3,
}

/// <summary>مددُ الخطط وأسماؤها وتواريخُ انتهائها.</summary>
public static class LicensePlanInfo
{
    /// <summary>مدةُ الخطة التجريبية — سبعةُ أيام.</summary>
    public const int TrialDays = 7;

    /// <summary>مدةُ الخطة السنوية — ٣٦٥ يوماً.</summary>
    public const int AnnualDays = 365;

    /// <summary>مدةُ الخطة بالأيام، و<c>null</c> للدائم — لا مدةَ له.</summary>
    public static int? DurationDays(LicensePlan plan) => plan switch
    {
        LicensePlan.Trial7 => TrialDays,
        LicensePlan.Annual => AnnualDays,
        LicensePlan.Lifetime => null,
        _ => null,
    };

    /// <summary>
    /// تاريخُ الانتهاء لخطةٍ صدرت في <paramref name="issuedOn"/>.
    /// الدائمُ لا ينتهي: <see cref="DateTime.MaxValue"/>.
    /// </summary>
    public static DateTime ExpiryFor(LicensePlan plan, DateOnly issuedOn) => plan switch
    {
        LicensePlan.Trial7 => issuedOn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(TrialDays),
        LicensePlan.Annual => issuedOn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(AnnualDays),
        LicensePlan.Lifetime => DateTime.MaxValue,
        _ => throw new ArgumentOutOfRangeException(nameof(plan), plan, "خطةُ اشتراكٍ غير معروفة."),
    };

    /// <summary>اسمُ الخطة بالعربية.</summary>
    public static string LabelAr(LicensePlan plan) => plan switch
    {
        LicensePlan.Trial7 => "تجريبي (7 أيام)",
        LicensePlan.Annual => "سنوي",
        LicensePlan.Lifetime => "دائم",
        _ => "غير معروف",
    };

    /// <summary>اسمُ الخطة بالإنجليزية.</summary>
    public static string LabelEn(LicensePlan plan) => plan switch
    {
        LicensePlan.Trial7 => "Trial (7 days)",
        LicensePlan.Annual => "Annual",
        LicensePlan.Lifetime => "Lifetime",
        _ => "Unknown",
    };

    /// <summary>
    /// يقرأ الخطةَ من اسمها المستقرّ ("trial", "annual", "lifetime" بمرادفاتها)
    /// — للإعدادات والسجلّات؛ ويُعيد <c>null</c> لما لا يُعرَف.
    /// </summary>
    public static LicensePlan? FromName(string? name) => name?.Trim().ToLowerInvariant() switch
    {
        "trial" or "trial7" or "trial-7" or "تجريبي" => LicensePlan.Trial7,
        "annual" or "yearly" or "1y" or "سنوي" => LicensePlan.Annual,
        "lifetime" or "permanent" or "perpetual" or "دائم" => LicensePlan.Lifetime,
        _ => null,
    };
}
