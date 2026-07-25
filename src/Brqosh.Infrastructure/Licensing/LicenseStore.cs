using System.Text.Json;
using Brqosh.Application.Setup;

namespace Brqosh.Infrastructure.Licensing;

/// <summary>
/// ترخيص البرنامج — تحقق RSA دون سر داخل التطبيق.
/// Development: فترة تجريبية 30 يوماً من تاريخ الإعداد إن لم يوجد ترخيص موقّع.
/// </summary>
public static class LicenseService
{
    public static string MachineId()
    {
        var raw = $"{Environment.MachineName}|{Environment.UserName}|{Environment.OSVersion}";
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash)[..16];
    }

    [Obsolete("استخدم LicenseIssuer لتوقيع التراخيص.")]
    public static string GenerateKey(string companyName, string machineId) =>
        throw new InvalidOperationException("تم إلغاء توليد الترخيص المحلي — استخدم tools/LicenseIssuer.");

    [Obsolete("استخدم RsaLicenseValidator.TryValidate.")]
    public static bool Validate(string companyName, string machineId, string licenseKey) => false;
}

public record LicenseState(string CompanyName, string MachineId, string? SignedLicenseJson);

/// <summary>ملف ترخيص محلي — %APPDATA%\BRQOSH\license.json</summary>
public static class LicenseStore
{
    private static string FilePath => Path.Combine(
        Brqosh.Infrastructure.Persistence.BrqoshDataRoot.Path(), "license.json");

    public static LicenseState? Load()
    {
        if (!File.Exists(FilePath)) return null;
        try
        {
            return JsonSerializer.Deserialize<LicenseState>(File.ReadAllText(FilePath));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static void Save(LicenseState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static bool ValidateForCompany(string companyName, DateTime? setupCompletedUtc = null)
    {
        if (string.Equals(Environment.GetEnvironmentVariable("BRQOSH_UITEST"), "1", StringComparison.Ordinal))
            return true;

        var machineId = LicenseService.MachineId();
        var stored = Load();

        if (stored?.SignedLicenseJson is not null &&
            RsaLicenseValidator.TryValidate(companyName, machineId, stored.SignedLicenseJson, out _))
            return true;

        if (AppEnvironmentConfig.IsProduction)
            return false;

        // فترة تجريبية في التطوير — 30 يوماً من إتمام الإعداد
        if (setupCompletedUtc is DateTime completed)
            return completed.AddDays(30) > DateTime.UtcNow;

        return false;
    }

    public static void SaveSignedLicense(string companyName, string signedLicenseJson)
    {
        Save(new LicenseState(companyName, LicenseService.MachineId(), signedLicenseJson));
    }
}
