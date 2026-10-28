using System.Globalization;

namespace Brqosh.Infrastructure.Licensing;

/// <summary>
/// <b>إشعاراتُ المدة ونوع الاشتراك</b> — إنذارٌ قبل الانتهاء بأيّامٍ تليق بالخطة،
/// والرسالةُ تسمّي الخطةَ وبقيّةَ الأيام وتاريخَ الانتهاء، كما طلب المالك.
///
/// <para>النافذةُ قبل الانتهاء: يومان للتجريبية (٧ أيام)، وثلاثون للسنوية. والدائمُ
/// لا يُزعَج. والرسائلُ هنا مزدوجةُ اللغة فتُمرَّر إلى
/// <c>LocaleManager.T</c> عند العرض — فلا تدخل سجلَّ الترجمة.</para>
/// </summary>
public static class LicenseNotices
{
    /// <summary>أيامُ الإنذار لكلّ خطة — و<c>null</c> لخطةٍ لا تنتهي.</summary>
    public static int? WarningDays(LicensePlan plan) => plan switch
    {
        LicensePlan.Trial7 => 2,
        LicensePlan.Annual => 30,
        LicensePlan.Lifetime => null,
        _ => null,
    };

    /// <summary>هل آنَ الإنذار: اشتراكٌ نشطٌ محدودٌ وبقيّتُه دون نافذة الإنذار.</summary>
    public static bool ShouldWarn(LicenseStatus status, DateTime nowUtc)
    {
        if (!status.IsActive || status.Plan is not LicensePlan plan || status.ExpiresUtc is not DateTime expires)
            return false;
        if (expires == DateTime.MaxValue)
            return false;

        var remaining = expires - nowUtc;
        if (remaining < TimeSpan.Zero)
            remaining = TimeSpan.Zero;
        var days = (int)Math.Ceiling(remaining.TotalDays);
        return WarningDays(plan) is int window && days <= window;
    }

    /// <summary>رسالةُ الإشعار بالعربية — و<c>null</c> إن لم يأنْ إنذار.</summary>
    public static string? StartupMessageAr(LicenseStatus status, DateTime nowUtc)
    {
        if (!ShouldWarn(status, nowUtc))
            return null;

        var days = status.DaysLeft ?? 0;
        var remains = DaysPhraseAr(days);
        var label = status.PlanLabelAr;
        var date = FormatDate(status.ExpiresUtc);

        return status.Plan switch
        {
            LicensePlan.Trial7 =>
                $"نسختك التجريبية («{label}») تنتهي {remains} — فعِّل ترخيصاً سنوياً أو دائماً لمواصلة العمل.",
            LicensePlan.Annual =>
                $"اشتراكك السنوي («{label}») ينتهي {remains} ({date}) — جدِّده قبل ذلك؛ وبعد انتهائه تُفتح الدفاتر للقراءة فقط.",
            _ => null,
        };
    }

    /// <summary>رسالةُ الإشعار بالإنجليزية — و<c>null</c> إن لم يأنْ إنذار.</summary>
    public static string? StartupMessageEn(LicenseStatus status, DateTime nowUtc)
    {
        if (!ShouldWarn(status, nowUtc))
            return null;

        var days = status.DaysLeft ?? 0;
        var remains = days switch { 0 => "today", 1 => "in 1 day", _ => $"in {days} days" };
        var label = status.PlanLabelEn;
        var date = FormatDate(status.ExpiresUtc);

        return status.Plan switch
        {
            LicensePlan.Trial7 =>
                $"Your trial ({label}) ends {remains} — activate an annual or lifetime licence to keep working.",
            LicensePlan.Annual =>
                $"Your annual subscription ({label}) ends {remains} ({date}) — renew before then; after it expires the books open read-only.",
            _ => null,
        };
    }

    private static string DaysPhraseAr(int days) => days switch
    {
        0 => "اليوم",
        1 => "بعد يومٍ واحد",
        2 => "بعد يومَين",
        >= 3 and <= 10 => $"بعد {days} أيام",
        _ => $"بعد {days} يوماً",
    };

    private static string FormatDate(DateTime? expiresUtc) =>
        expiresUtc is DateTime d
            ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : "";
}
