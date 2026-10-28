using Brqosh.Application.Licensing;

namespace Brqosh.Infrastructure.Licensing;

/// <summary>
/// ‏(Phase 7 · security-licensing-tenancy#2) <b>قراءةُ الفئة وحدّ المنشآت من الترخيص الموقَّع.</b>
///
/// <para>‏<b>ولماذا مدخلاتٌ جديدةٌ لا حقلُ <c>Tier</c>:</b> أداتا الإصدار تكتبان <c>"standard"</c> في كلّ ترخيصٍ صدر — فقراءتُه فئةً كانت
/// ‏ستُقفل الرواتبَ والمشاريعَ على كلّ عميلٍ قائمٍ صباحَ التحديث. فالفئةُ تُقرأ من <c>edition:</c> والحدُّ من <c>companies:</c>،
/// ‏وترخيصٌ بلا أيٍّ منهما لا يحصر — كمذهب <see cref="LicensedModules"/> نفسِه.</para>
/// </summary>
public static class LicenseEntitlementsReader
{
    public const string EditionPrefix = "edition:";
    public const string CompaniesPrefix = "companies:";

    public static LicenseEntitlements From(IEnumerable<string>? features)
    {
        var edition = LicenseEdition.Unrestricted;
        int? max = null;
        foreach (var feature in features ?? [])
        {
            if (feature is null) continue;
            if (feature.StartsWith(EditionPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var value = feature[EditionPrefix.Length..].Trim();
                // ‏فئةٌ لا يعرفها هذا الإصدار تُقرأ قياسيّةً — لا تصير مفتوحةً هنا.
                edition = value.Equals("professional", StringComparison.OrdinalIgnoreCase)
                    ? LicenseEdition.Professional
                    : LicenseEdition.Standard;
            }
            else if (feature.StartsWith(CompaniesPrefix, StringComparison.OrdinalIgnoreCase))
            {
                // ‏عددٌ تالفٌ أو أقلُّ من واحد يُقرأ واحداً — الحدُّ لا يُفتح بخطأ.
                max = int.TryParse(feature[CompaniesPrefix.Length..].Trim(), out var n) && n >= 1 ? n : 1;
            }
        }
        return new LicenseEntitlements(edition, max);
    }

    /// <summary>‏مدخلاتُ <c>Features</c> للفئة والحدّ — لأداتَي الإصدار.</summary>
    public static string[] ToFeatures(LicenseEdition edition, int? maxCompanies)
    {
        var list = new List<string>();
        if (edition != LicenseEdition.Unrestricted)
            list.Add(EditionPrefix + edition.ToString().ToLowerInvariant());
        if (maxCompanies is int n)
            list.Add(CompaniesPrefix + n);
        return [.. list];
    }
}

/// <summary>‏الترخيصُ المحفوظ على الجهاز: المفتاحُ الرقميُّ الصالح وغيابُ ترخيصٍ أصيل لا يحصران (والإنتاجُ يُوقف غيرَ المرخَّص عند الإقلاع).</summary>
public sealed class StoredLicenseEntitlementsProvider : ILicenseEntitlementsProvider
{
    public LicenseEntitlements Current()
    {
        var stored = LicenseStore.Load();
        if (stored is null)
            return LicenseEntitlements.Unrestricted;
        var machineId = LicenseService.MachineId();

        if (stored.Code is { } code
            && NumericLicense.Decode(code, machineId, stored.CompanyName, DateTime.UtcNow,
                out _, out _, out _) == NumericLicenseVerdict.Valid)
            return LicenseEntitlements.Unrestricted;

        return stored.SignedLicenseJson is not null
               && RsaLicenseValidator.TryReadGenuine(stored.CompanyName, machineId, stored.SignedLicenseJson,
                   out var payload, alsoAcceptMachineId: LicenseService.LegacyMachineId())
            ? LicenseEntitlementsReader.From(payload!.Features)
            : LicenseEntitlements.Unrestricted;
    }
}

/// <summary>
/// ‏<b>بوّابةُ الترخيص في طبقة الخدمات لا الواجهة</b> — تسألها خدماتُ الميزات الاحترافيّة وإنشاءُ المنشآت، فلا يفتحها
/// ‏استدعاءٌ مباشرٌ أو API تجاوز القائمة. ‏<see cref="Use"/> يثبّتها في الاختبارات لهذا المسار وحده.
/// </summary>
public static class LicenseGate
{
    private static readonly AsyncLocal<ILicenseEntitlementsProvider?> Override = new();

    public static ILicenseEntitlementsProvider Default { get; set; } = new StoredLicenseEntitlementsProvider();

    public static LicenseEntitlements Current => (Override.Value ?? Default).Current();

    public static void Ensure(LicensedCapability capability) => Current.EnsureAllows(capability);

    public static IDisposable Use(LicenseEntitlements entitlements)
    {
        var previous = Override.Value;
        Override.Value = new Fixed(entitlements);
        return new Restore(() => Override.Value = previous);
    }

    private sealed class Fixed(LicenseEntitlements value) : ILicenseEntitlementsProvider
    {
        public LicenseEntitlements Current() => value;
    }

    private sealed class Restore(Action undo) : IDisposable
    {
        public void Dispose() => undo();
    }
}

/// <summary>‏مساراتُ API التي تخصّ ميزةً احترافيّة — تُرفض بـ403 <c>licence_tier</c> قبل أن تبلغ الخدمة.</summary>
public static class LicensedApiRoutes
{
    public static LicensedCapability? CapabilityFor(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var p = path.TrimEnd('/');
        if (p.StartsWith("/api/v1/company-groups", StringComparison.OrdinalIgnoreCase))
            return LicensedCapability.Consolidation;
        if (p.EndsWith("/fx-closing-translation", StringComparison.OrdinalIgnoreCase)
            || p.Equals("/api/v1/exchange-rates", StringComparison.OrdinalIgnoreCase)
            || p.EndsWith("/exchange-rates/sync", StringComparison.OrdinalIgnoreCase))
            return LicensedCapability.MultiCurrency;
        if (p.Equals("/api/v1/reports/project-profitability", StringComparison.OrdinalIgnoreCase))
            return LicensedCapability.ProjectsContracts;
        return null;
    }
}
