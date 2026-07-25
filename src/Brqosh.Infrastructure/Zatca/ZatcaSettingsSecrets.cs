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
    }

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
