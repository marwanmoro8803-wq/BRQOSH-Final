namespace Brqosh.Infrastructure.Licensing;

/// <summary>
/// ‏ <b>وضعُ القراءة فقط — حين انقضى ترخيصٌ أصيل.</b>
///
/// <para>‏المالكُ ملزَمٌ بحفظ دفاتره وإتاحتها، <b>فانتهاءُ الترخيص لا يُغلق الدفاتر</b>: تُقرأ وتُطبع وتُصدَّر، ولا يُضاف إليها
/// ‏ولا يُعدَّل فيها شيءٌ حتى يُجدَّد. والوضعُ يُفرض عند الحفظ (<see cref="Persistence.ReadOnlyBooksInterceptor"/>)، فلا تُحرس
/// ‏كلُّ شاشةٍ وحدَها. <b>وهو لعمليّة التطبيق كلِّها</b>: يُدخَل مرّةً عند الإقلاع ولا يُخرج منه إلّا بإعادة التشغيل بترخيصٍ صالح.</para>
/// </summary>
public static class ReadOnlyBooks
{
    private static volatile bool _active;

    /// <summary>هل الدفاترُ للقراءة فقط في هذه العمليّة.</summary>
    public static bool IsActive => _active;

    /// <summary>يدخل وضعَ القراءة — ولا مخرجَ منه في العمليّة نفسِها.</summary>
    public static void Enter() => _active = true;

    /// <summary>للاختبارات وحدَها — كلُّ اختبارٍ يبدأ خارج الوضع.</summary>
    internal static void ResetForTests() => _active = false;
}

/// <summary>
/// ‏ <b>الوقتُ الذي تُقاس عليه الصلاحية — لا ساعةُ الجهاز وحدَها.</b>
///
/// <para>‏كان <c>RsaLicenseValidator</c> يقيس بـ<c>DateTime.UtcNow</c>، و<see cref="ClockRollbackDetector"/> يُبلِّغ الرجوعَ ولا يُستعمَل
/// ‏في الحكم — <b>فمن أرجع ساعتَه مدّد ترخيصَه بلا حدّ</b>، وت-١٢ معلَّمٌ مغلقاً. والفعّالُ <b>أبعدُ الوقتين</b>: الساعةُ أو أحدثُ وقتٍ رآه
/// ‏التطبيقُ قطّ. <b>ولا يُقفل بذلك أحد</b> (قرارُ ت-١٢): الأصيلُ المنتهي يفتح الدفاترَ للقراءة (ح-١٢٠).</para>
/// </summary>
public static class LicenseClock
{
    public static DateTime EffectiveNowUtc(DateTime nowUtc, DateTime? highWaterUtc) =>
        highWaterUtc is DateTime mark && mark > nowUtc ? mark : nowUtc;

    /// <summary>الوقتُ الفعّالُ على هذا الجهاز — يقرأ العلامةَ ولا يرفعها على ساعةٍ راجعة (<see cref="ClockRollbackDetector.Observe"/>).</summary>
    public static DateTime EffectiveNowUtc() =>
        EffectiveNowUtc(DateTime.UtcNow, ClockRollbackDetector.Observe().HighWaterUtc);
}

/// <summary>
/// ‏(ح-١٢٧) <b>الجلسةُ الطويلةُ لا تتجاوز الانتهاء</b> — وكان الحكمُ عند الإقلاع وحدَه، فتطبيقٌ لا يُغلق يعمل بترخيصٍ انتهى أمس.
/// </summary>
public static class LicenseSessionGuard
{
    /// <summary>هل تدخل الجلسةُ الجاريةُ وضعَ القراءة: ترخيصٌ صالحٌ عند الإقلاع وقد بلغ الوقتُ الفعّالُ انتهاءَه.</summary>
    public static bool ShouldEnterReadOnly(DateTime? licenseExpiresUtc, DateTime effectiveNowUtc, bool alreadyReadOnly) =>
        !alreadyReadOnly && licenseExpiresUtc is DateTime expires && expires < effectiveNowUtc;
}

/// <summary>(ح-١٢٠) <b>إنذارُ انتهاء الترخيص</b> — قبل ثلاثين يوماً، فلا يُفاجأ المالكُ بيومٍ لا يُرحِّل فيه.</summary>
public static class LicenseExpiryNotice
{
    public const int WarningDays = 30;

    /// <summary>الأيّامُ الباقيةُ إن حلّ وقتُ الإنذار (ثلاثون فما دون)، و<c>null</c> قبله.</summary>
    public static int? DaysLeftToWarn(DateTime expiresUtc, DateTime nowUtc)
    {
        var days = Math.Max(0, (int)Math.Floor((expiresUtc - nowUtc).TotalDays));
        return days <= WarningDays ? days : null;
    }
}
