namespace Brqosh.Infrastructure.Licensing;

/// <summary>حالُ الترخيص في هذه اللحظة — ما يقرأه الإقلاعُ والنوافذُ منهما.</summary>
public enum LicenseKind
{
    /// <summary>لا ترخيصَ ولا تجربةَ سليمة.</summary>
    None,

    /// <summary>تجربةٌ جارية — سبعةُ أيامٍ من أوّل تشغيل.</summary>
    Trial,

    /// <summary>ترخيصٌ صالح — رقميٌّ أو موقَّع.</summary>
    Licensed,

    /// <summary>انقضت صلاحيةُ ترخيصٍ أصيلٍ أو التجربة.</summary>
    Expired,
}

/// <summary>وضعُ الترخيص الكامل: نوعُه وخطتُه وتاريخاه وبقيّةُ أيامه.</summary>
public sealed record LicenseStatus(
    LicenseKind Kind,
    LicensePlan? Plan,
    DateTime? IssuedUtc,
    DateTime? ExpiresUtc,
    int? DaysLeft,
    string? Code,
    string? SignedLicenseJson)
{
    /// <summary>التجربةُ الجارية والترخيصُ الصالح — كلاهما يفتح البرنامج للعمل.</summary>
    public bool IsActive => Kind is LicenseKind.Trial or LicenseKind.Licensed;

    /// <summary>اسمُ الخطة بالعربية — وتجربةٌ بلا خطةٍ تسمّى بالخطة التجريبية.</summary>
    public string PlanLabelAr => Plan is LicensePlan plan ? LicensePlanInfo.LabelAr(plan) : "";

    /// <summary>اسمُ الخطة بالإنجليزية.</summary>
    public string PlanLabelEn => Plan is LicensePlan plan ? LicensePlanInfo.LabelEn(plan) : "";
}

/// <summary>
/// <b>حالُ الترخيص واحدٌ مهما كان مصدرُه</b> — مفتاحٌ رقميّ أو ملفٌّ موقَّع أو تجربة.
///
/// <para>يُجمَع القرارُ هنا في مكانٍ واحد: الصالحُ مقدَّمٌ على المنتهي، والمنتهي على
/// التجربة، والتجربةُ على العدم. وبدءُ التجربة يُختم في أوّل قراءةٍ
/// (<see cref="TrialPeriod.EnsureStarted"/>) — فالإقلاعُ الأوّلُ بعد التنصيب يبدأها.</para>
/// </summary>
public static class LicenseStatusResolver
{
    /// <summary>حالُ الترخيص لمنشأةٍ معلومةٍ على الوقت الفعّال — أو على وقتٍ صريحٍ في الاختبار.</summary>
    public static LicenseStatus Current(string companyName, DateTime? nowUtc = null)
    {
        var effectiveNow = nowUtc ?? LicenseClock.EffectiveNowUtc();
        TrialPeriod.EnsureStarted(effectiveNow);

        var machineId = LicenseService.MachineId();
        var stored = LicenseStore.Load();

        LicenseStatus? numeric = null;
        LicenseStatus? signed = null;

        if (stored?.Code is { } code)
            numeric = FromCode(code, companyName, machineId, effectiveNow);

        if (stored?.SignedLicenseJson is { } signedJson)
            signed = FromSignedJson(signedJson, companyName, machineId, effectiveNow);

        // ‏الصالحُ أسبق — رقميٌّ فموقَّع — ثمّ المنتهي الأصيل، ثمّ التجربة.
        if (numeric?.Kind == LicenseKind.Licensed)
            return numeric;
        if (signed?.Kind == LicenseKind.Licensed)
            return signed;
        if (numeric?.Kind == LicenseKind.Expired)
            return numeric;
        if (signed?.Kind == LicenseKind.Expired)
            return signed;

        var trial = TrialPeriod.Status(effectiveNow);
        return trial is { } t
            ? t.Expired
                ? new LicenseStatus(LicenseKind.Expired, LicensePlan.Trial7, t.StartedUtc,
                    t.ExpiresUtc, 0, stored?.Code, stored?.SignedLicenseJson)
                : new LicenseStatus(LicenseKind.Trial, LicensePlan.Trial7, t.StartedUtc,
                    t.ExpiresUtc, t.DaysLeft, stored?.Code, stored?.SignedLicenseJson)
            : new LicenseStatus(LicenseKind.None, null, null, null, null,
                stored?.Code, stored?.SignedLicenseJson);
    }

    /// <summary>يقرأ مفتاحاً رقمياً إلى حال — و<b>لا يخمّن</b>: ما لا يخصّ هذا الجهاز أو هذه المنشأة يُهمَل.</summary>
    internal static LicenseStatus? FromCode(string code, string companyName, string machineId, DateTime effectiveNow)
    {
        var verdict = NumericLicense.Decode(code, machineId, companyName, effectiveNow,
            out var plan, out var expires, out var issuedOn);
        return verdict switch
        {
            NumericLicenseVerdict.Valid => new LicenseStatus(LicenseKind.Licensed, plan,
                issuedOn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), expires,
                DaysLeft(expires, effectiveNow), code, null),
            NumericLicenseVerdict.Expired => new LicenseStatus(LicenseKind.Expired, plan,
                issuedOn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), expires, 0, code, null),
            _ => null,
        };
    }

    /// <summary>يقرأ ملفّاً موقَّعاً إلى حال — والخطةُ تُشتقّ من الفئة والتاريخ.</summary>
    internal static LicenseStatus? FromSignedJson(string signedJson, string companyName,
        string machineId, DateTime effectiveNow)
    {
        if (RsaLicenseValidator.TryValidate(companyName, machineId, signedJson, out var payload,
                alsoAcceptMachineId: LicenseService.LegacyMachineId(), nowUtc: effectiveNow))
        {
            var plan = DerivePlan(payload);
            return new LicenseStatus(LicenseKind.Licensed, plan, null, payload.ExpiresUtc,
                DaysLeft(payload.ExpiresUtc, effectiveNow), null, signedJson);
        }

        return RsaLicenseValidator.IsGenuineButExpired(companyName, machineId, signedJson,
            alsoAcceptMachineId: LicenseService.LegacyMachineId(), nowUtc: effectiveNow)
            && RsaLicenseValidator.TryReadGenuine(companyName, machineId, signedJson,
                out var expiredPayload, alsoAcceptMachineId: LicenseService.LegacyMachineId())
            ? new LicenseStatus(LicenseKind.Expired, DerivePlan(expiredPayload), null,
                expiredPayload.ExpiresUtc, 0, null, signedJson)
            : null;
    }

    private static LicensePlan DerivePlan(LicensePayload payload) =>
        string.Equals(payload.Tier, "lifetime", StringComparison.OrdinalIgnoreCase)
        || payload.ExpiresUtc.Year >= 9000
            ? LicensePlan.Lifetime
            : LicensePlan.Annual;

    /// <summary>بقيّةُ الأيام — سقفٌ إلى الأعلى ولا تقلّ عن صفر؛ والدائمُ بلا عدد.</summary>
    private static int? DaysLeft(DateTime expiresUtc, DateTime effectiveNow)
    {
        if (expiresUtc == DateTime.MaxValue)
            return null;
        var remaining = expiresUtc - effectiveNow;
        if (remaining < TimeSpan.Zero)
            remaining = TimeSpan.Zero;
        return (int)Math.Ceiling(remaining.TotalDays);
    }
}
