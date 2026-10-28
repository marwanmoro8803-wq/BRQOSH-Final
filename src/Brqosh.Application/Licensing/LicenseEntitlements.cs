using Brqosh.Domain.Accounting;

namespace Brqosh.Application.Licensing;

/// <summary>‏فئةُ الترخيص (Phase 7 · security-licensing-tenancy#2).</summary>
public enum LicenseEdition
{
    /// <summary>‏ترخيصٌ لا يُسمّي فئتَه (كلُّ ما صدر قبل هذا) أو لا ترخيص في التطوير — لا حصر، وهو سلوكُ الأمس بعينه.</summary>
    Unrestricted = 0,
    Standard = 1,
    Professional = 2,
}

/// <summary>‏ما تفتحه الفئةُ الاحترافيّة وحدها — من مسودة الباقات (تعدّدُ العملات والقوائمُ الموحّدة، والرواتب وGOSI، والمشاريعُ والعقود).</summary>
public enum LicensedCapability
{
    MultiCurrency,
    Consolidation,
    Payroll,
    ProjectsContracts,
}

/// <summary>
/// ‏<b>ما يمنحه الترخيصُ الموقَّع</b>: الفئةُ وحدُّ المنشآت — من مدخلَي <c>edition:&lt;standard|professional&gt;</c> و<c>companies:&lt;N&gt;</c>
/// ‏في <c>Features</c> تحت التوقيع. <c>MaxCompanies = null</c> لا حدّ.
/// </summary>
public sealed record LicenseEntitlements(LicenseEdition Edition, int? MaxCompanies)
{
    public static LicenseEntitlements Unrestricted { get; } = new(LicenseEdition.Unrestricted, null);

    public bool Allows(LicensedCapability capability) => Edition != LicenseEdition.Standard;

    public void EnsureAllows(LicensedCapability capability)
    {
        if (!Allows(capability))
            throw new AccountingException(
                $"هذه الميزة ({CapabilityNameAr(capability)}) متاحةٌ في الفئة الاحترافيّة (Professional) فقط — ترخيصُك قياسيّ (Standard).");
    }

    public void EnsureCanAddCompany(int existingCompanies)
    {
        if (MaxCompanies is int max && existingCompanies >= max)
            throw new AccountingException(
                $"بلغتَ حدَّ المنشآت في ترخيصك ({max}) — لإضافة منشأةٍ يلزم ترخيصٌ بعددٍ أكبر.");
    }

    public static string CapabilityNameAr(LicensedCapability capability) => capability switch
    {
        LicensedCapability.MultiCurrency => "تعدّد العملات",
        LicensedCapability.Consolidation => "القوائم الموحّدة",
        LicensedCapability.Payroll => "الرواتب والتأمينات",
        LicensedCapability.ProjectsContracts => "المشاريع والعقود",
        _ => capability.ToString(),
    };
}

/// <summary>‏مصدرُ ما يمنحه الترخيص — المحفوظُ على الجهاز في التشغيل، وثابتٌ في الاختبارات.</summary>
public interface ILicenseEntitlementsProvider
{
    LicenseEntitlements Current();
}
