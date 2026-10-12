using System.Text.Json;
using Brqosh.Application.Setup;

namespace Brqosh.Infrastructure.Licensing;

/// <summary>
/// ترخيص البرنامج — تحقق RSA دون سر داخل التطبيق.
/// Development: فترة تجريبية 30 يوماً من تاريخ الإعداد إن لم يوجد ترخيص موقّع.
/// </summary>
public static class LicenseService
{
    /// <summary>
    /// (CEEF-237 · ت-٥) <b>هويّةُ الجهاز صارت تصمد لتحديث ويندوز وتبديل الحساب.</b>
    ///
    /// <para><b>ما كان:</b> <c>SHA256(MachineName|UserName|OSVersion)</c>. وثلاثتُها تتغيّر:
    /// <c>OSVersion</c> مع <b>كلّ تحديث ميزاتٍ سنويّ</b>، و<c>UserName</c> حين يفتح موظّفٌ
    /// آخرُ الجهازَ بحسابه، و<c>MachineName</c> حين يُعاد تسميةُ الجهاز.</para>
    ///
    /// <para><b>وأثرُه:</b> ترخيصٌ صحيحٌ يُرفض فجأةً صباحَ يومٍ لم يتغيّر فيه شيءٌ في نظر
    /// العميل — <b>ويتوقّف عن العمل حتى يُصدر المورِّدُ ترخيصاً جديداً</b>. وقد صار هذا
    /// حمّالاً بعد أن أُغلق ت-١ وصار التفعيلُ إلزامياً في الإنتاج.</para>
    ///
    /// <para><b>والبديل:</b> <c>MachineGuid</c> من سجلّ ويندوز — يُكتب عند تنصيب النظام
    /// ويبقى عبر التحديثات وتبديل المستخدمين. وإن تعذّرت قراءتُه (سياسةٌ تمنع، أو نظامٌ
    /// ليس ويندوز) <b>يُرجَع إلى الصيغة القديمة</b> بدل رمي استثناءٍ يُعطّل التفعيل.</para>
    /// </summary>
    public static string MachineId()
    {
        var stable = ReadMachineGuid();
        return stable is null ? LegacyMachineId() : Fingerprint(stable);
    }

    /// <summary>
    /// الصيغةُ القديمة — <b>تبقى مقروءةً لأنّ تراخيصَ صدرت بها</b>، ويقبلها المُتحقّق
    /// إلى جانب الجديدة فلا يتوقّف عميلٌ يعمل عند الترقية.
    /// </summary>
    public static string LegacyMachineId() =>
        Fingerprint($"{Environment.MachineName}|{Environment.UserName}|{Environment.OSVersion}");

    /// <summary>المعرّفاتُ التي يقبلها المُتحقّق — الجديدُ أوّلاً، والقديمُ للانتقال.</summary>
    public static IReadOnlyList<string> AcceptableMachineIds()
    {
        var current = MachineId();
        var legacy = LegacyMachineId();
        return string.Equals(current, legacy, StringComparison.OrdinalIgnoreCase)
            ? [current]
            : [current, legacy];
    }

    private static string Fingerprint(string raw)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash)[..16];
    }

    private static string? ReadMachineGuid()
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Cryptography");
            var value = key?.GetValue("MachineGuid") as string;
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
        catch (Exception ex) when (ex is System.Security.SecurityException
                                      or UnauthorizedAccessException or System.IO.IOException)
        {
            // ‏سياسةٌ تمنع القراءة: لا يُعطَّل التفعيل، يُرجَع إلى الصيغة القديمة.
            return null;
        }
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

    /// <param name="nowUtc">(ح-١٢٧) الوقتُ الفعّال — <see cref="LicenseClock.EffectiveNowUtc()"/> عند الإقلاع؛ والفارغُ ساعةُ الجهاز.</param>
    public static bool ValidateForCompany(string companyName, DateTime? setupCompletedUtc = null, DateTime? nowUtc = null)
    {
        // (CEEF-228) تجاوز الترخيص للتدقيق الآلي - معطّل قسرياً في الإنتاج.
        if (!AppEnvironmentConfig.IsProduction &&
            string.Equals(Environment.GetEnvironmentVariable("BRQOSH_UITEST"), "1", StringComparison.Ordinal))
            return true;

        var machineId = LicenseService.MachineId();
        var stored = Load();

        // ‏(CEEF-237 · ت-٥) ويُقبل المعرّفُ القديم كذلك — **هنا وحدَه**، حيث نتحدّث عن
        // ‏هذا الجهاز بعينه. فترخيصٌ صدر قبل الترقية لا يتوقّف صباحَ التحديث.
        if (stored?.SignedLicenseJson is not null &&
            RsaLicenseValidator.TryValidate(companyName, machineId, stored.SignedLicenseJson, out _,
                alsoAcceptMachineId: LicenseService.LegacyMachineId(), nowUtc: nowUtc))
            return true;

        if (AppEnvironmentConfig.IsProduction)
            return false;

        // فترة تجريبية في التطوير — 30 يوماً من إتمام الإعداد
        if (setupCompletedUtc is DateTime completed)
            return completed.AddDays(30) > DateTime.UtcNow;

        return false;
    }

    /// <summary>
    /// ‏(ح-١٢٠) <b>هل الترخيصُ المحفوظُ أصيلٌ انقضت صلاحيتُه</b> — وهو وحدَه ما يفتح الدفاترَ للقراءة
    /// ‏(<see cref="RsaLicenseValidator.IsGenuineButExpired"/>): المزوَّرُ والمنقولُ من جهازٍ أو منشأةٍ أخرى لا يفتحها.
    /// </summary>
    public static bool IsGenuineButExpiredFor(string companyName, DateTime? nowUtc = null)
    {
        var stored = Load();
        return stored?.SignedLicenseJson is not null
               && RsaLicenseValidator.IsGenuineButExpired(companyName, LicenseService.MachineId(), stored.SignedLicenseJson,
                   alsoAcceptMachineId: LicenseService.LegacyMachineId(), nowUtc: nowUtc);
    }

    /// <summary>(ح-١٢٠) تاريخُ انتهاء الترخيص الصالح المحفوظ — للإنذار قبله؛ و<c>null</c> إن لم يكن صالحاً.</summary>
    public static DateTime? ValidLicenseExpiresUtc(string companyName, DateTime? nowUtc = null)
    {
        var stored = Load();
        return stored?.SignedLicenseJson is not null
               && RsaLicenseValidator.TryValidate(companyName, LicenseService.MachineId(), stored.SignedLicenseJson,
                   out var payload, alsoAcceptMachineId: LicenseService.LegacyMachineId(), nowUtc: nowUtc)
            ? payload!.ExpiresUtc
            : null;
    }

    public static void SaveSignedLicense(string companyName, string signedLicenseJson)
    {
        Save(new LicenseState(companyName, LicenseService.MachineId(), signedLicenseJson));
    }
}
