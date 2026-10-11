namespace Brqosh.Infrastructure.Licensing;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١٢٠ · ‏`commercial-readiness#6`) <b>وضعُ القراءة فقط — حين انقضى ترخيصٌ أصيل.</b>
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
