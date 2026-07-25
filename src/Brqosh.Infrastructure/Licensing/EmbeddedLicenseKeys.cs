namespace Brqosh.Infrastructure.Licensing;

/// <summary>
/// المفتاح العام RSA — يُحمَّل من متغير البيئة BRQOSH_LICENSE_PUBLIC_KEY_PEM
/// أو من %APPDATA%\BRQOSH\license-public.pem
/// </summary>
internal static class EmbeddedLicenseKeys
{
    public static string? ResolvePublicKeyPem()
    {
        if (RsaLicenseValidator.PublicKeyPemOverride is not null)
            return RsaLicenseValidator.PublicKeyPemOverride;

        var fromEnv = Environment.GetEnvironmentVariable("BRQOSH_LICENSE_PUBLIC_KEY_PEM");
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv;

        var path = Path.Combine(
            Brqosh.Infrastructure.Persistence.BrqoshDataRoot.Path(), "license-public.pem");
        if (File.Exists(path))
            return File.ReadAllText(path);

        return null;
    }
}
