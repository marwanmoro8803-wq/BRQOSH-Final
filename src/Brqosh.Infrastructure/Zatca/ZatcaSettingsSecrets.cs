using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Security;

namespace Brqosh.Infrastructure.Zatca;

/// <summary>قراءة/كتابة أسرار ZATCA مع DPAPI عند الحفظ.</summary>
public static class ZatcaSettingsSecrets
{
    public static string GetApiToken(ZatcaSettings settings) =>
        LocalSecretProtector.Unprotect(settings.ApiToken);

    public static string GetCsidSecret(ZatcaSettings settings) =>
        LocalSecretProtector.Unprotect(settings.CsidSecret);

    public static void ApplyProtectedCsid(
        ZatcaSettings settings, string certificatePath, string privateKeyPath,
        string apiToken, string complianceRequestId, string csidSecret)
    {
        settings.ApplyCsid(
            certificatePath,
            privateKeyPath,
            LocalSecretProtector.Protect(apiToken),
            complianceRequestId,
            LocalSecretProtector.Protect(csidSecret));
        // ‏(tax-zatca#16) ‏المفتاحُ كُتب على هذا الجهاز وحدَه — فيُسجَّل أنّه جهازُ الختم.
        settings.RecordSigningMachine(Licensing.LicenseService.MachineId());
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · <c>tax-zatca#16</c>) <b>هل الختمُ مسجَّلٌ على جهازٍ غيرِ هذا؟</b> — فالفواتيرُ الإلكترونيّةُ تُصدَر منه وحدَه،
    /// ومعالجُ الشهادة هنا ينقل الختمَ إلى هذا الجهاز فيتوقّف ذاك.
    /// </summary>
    public static bool IsSignedOnAnotherMachine(ZatcaSettings settings) =>
        settings.SigningMachineId.Length > 0
        && !string.Equals(settings.SigningMachineId, Licensing.LicenseService.MachineId(), StringComparison.Ordinal)
        && !string.Equals(settings.SigningMachineId, Licensing.LicenseService.LegacyMachineId(), StringComparison.Ordinal);

    public static void ApplyProtectedUpdate(
        ZatcaSettings settings, bool isEnabled, ZatcaEnvironment environment, string apiBaseUrl,
        string certificatePath, string privateKeyPath, string apiToken)
    {
        settings.Update(
            isEnabled,
            environment,
            apiBaseUrl,
            certificatePath,
            privateKeyPath,
            LocalSecretProtector.Protect(apiToken));
    }
}
