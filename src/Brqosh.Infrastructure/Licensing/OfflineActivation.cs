using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Brqosh.Application.Licensing;
using Brqosh.Domain.Licensing;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Licensing;

/// <summary>
/// ‏(Phase 7) <b>هويّةُ الجهاز للتفعيل دون اتصال</b>: بصمةُ الجهاز الثابتة + معرّفُ التركيب (يُولَّد مرّةً في جذر البيانات).
/// ‏فإعادةُ التركيب تُولّد هويّةً جديدة — ورمزُ التفعيل القديم لا يطابقها.
/// </summary>
public static class DeviceIdentity
{
    private const string InstallIdFile = "install.id";

    public static string InstallId()
    {
        var path = Path.Combine(BrqoshDataRoot.Path(), InstallIdFile);
        if (File.Exists(path))
        {
            var existing = File.ReadAllText(path).Trim();
            if (Guid.TryParse(existing, out _))
                return existing;
        }
        Directory.CreateDirectory(BrqoshDataRoot.Path());
        var id = Guid.NewGuid().ToString("N");
        File.WriteAllText(path, id);
        return id;
    }

    /// <summary>‏١٢٨ بتّاً من SHA-256 للبصمة ومعرّف التركيب — بست عشرة خانة سداسيّة مضاعفة (٣٢ حرفاً).</summary>
    public static string DeviceHash(string machineFingerprint, string installId) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"BRQOSH-DEVICE-v1|{machineFingerprint}|{installId}")))[..32]
            .ToLowerInvariant();

    public static string CurrentDeviceHash() => DeviceHash(LicenseService.MachineId(), InstallId());
}

/// <summary>‏<b>رمزُ الطلب</b> الذي يُملى للمورّد: بصمةُ الجهاز بترميز Base32 مع خانتَي تحقّقٍ تلتقطان خطأَ النسخ.</summary>
public static class RequestCode
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // ‏بلا I وO و0 و1 — لا تلتبس إملاءً.
    public const string Prefix = "BRQ";

    public static string FromDeviceHash(string deviceHashHex)
    {
        var bytes = Convert.FromHexString(deviceHashHex);
        var body = Base32(bytes) + Base32(SHA256.HashData(bytes)[..1])[..2];
        return Prefix + "-" + string.Join("-", Enumerable.Range(0, (body.Length + 4) / 5)
            .Select(i => body.Substring(i * 5, Math.Min(5, body.Length - i * 5))));
    }

    public static bool TryDecode(string? code, out string deviceHashHex)
    {
        deviceHashHex = "";
        var text = (code ?? "").ToUpperInvariant().Replace("-", "").Replace(" ", "");
        if (!text.StartsWith(Prefix, StringComparison.Ordinal)) return false;
        text = text[Prefix.Length..];
        if (text.Length != 28) return false;
        var bytes = FromBase32(text[..26], 16);
        if (bytes is null || Base32(SHA256.HashData(bytes)[..1])[..2] != text[26..]) return false;
        deviceHashHex = Convert.ToHexString(bytes).ToLowerInvariant();
        return true;
    }

    private static string Base32(byte[] data)
    {
        var sb = new StringBuilder();
        int buffer = 0, bits = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                sb.Append(Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }
        if (bits > 0) sb.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        return sb.ToString();
    }

    private static byte[]? FromBase32(string text, int length)
    {
        var output = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var c in text)
        {
            var v = Alphabet.IndexOf(c);
            if (v < 0) return null;
            buffer = (buffer << 5) | v;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)((buffer >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }
        return output.Count >= length ? output.Take(length).ToArray() : null;
    }
}

/// <summary>‏حمولةُ رمز التفعيل الموقَّعة — الجهازُ والفئةُ وحدُّ المنشآت والانتهاءُ وnonce فريد. لا سعرَ فيها.</summary>
public sealed record ActivationPayload(
    [property: JsonPropertyName("v")] int Version,
    [property: JsonPropertyName("dev")] string DeviceHash,
    [property: JsonPropertyName("ed")] string Edition,
    [property: JsonPropertyName("co")] int? MaxCompanies,
    [property: JsonPropertyName("exp")] DateTime? ExpiresUtc,
    [property: JsonPropertyName("trial")] bool IsTrial,
    [property: JsonPropertyName("n")] string Nonce,
    [property: JsonPropertyName("iat")] DateTime IssuedUtc)
{
    public LicenseEntitlements Entitlements => new(
        Edition.Equals("professional", StringComparison.OrdinalIgnoreCase) ? LicenseEdition.Professional : LicenseEdition.Standard,
        MaxCompanies);
}

public enum ActivationVerdict
{
    Valid,
    Malformed,
    NoTrustAnchor,
    BadSignature,
    WrongDevice,
    Expired,
    AlreadyUsed,
    ClockRolledBack,
    LedgerTampered,
}

/// <summary>
/// ‏<b>رمزُ التفعيل</b>: <c>BRQA1.&lt;حمولة base64url&gt;.&lt;توقيع ECDSA P-256 base64url&gt;</c>. يوقّعه مولّدُ المورّد بمفتاحه الخاصّ
/// ‏(لا يُشحن مع البرنامج)، ويتحقّق البرنامجُ بالمفتاح العامّ المضمَّن <see cref="EmbeddedActivationKeys"/>.
/// </summary>
public static class ActivationCodec
{
    public const string Prefix = "BRQA1";

    public static string Sign(ActivationPayload payload, string privateKeyPem)
    {
        using var key = ECDsa.Create();
        key.ImportFromPem(privateKeyPem);
        var body = JsonSerializer.SerializeToUtf8Bytes(payload);
        var signature = key.SignData(Encoding.ASCII.GetBytes(Prefix + "." + B64(body)), HashAlgorithmName.SHA256);
        return $"{Prefix}.{B64(body)}.{B64(signature)}";
    }

    public static ActivationVerdict Verify(string? code, string? publicKeyPem, out ActivationPayload? payload)
    {
        payload = null;
        if (string.IsNullOrWhiteSpace(publicKeyPem))
            return ActivationVerdict.NoTrustAnchor;
        var parts = (code ?? "").Trim().Replace("\r", "").Replace("\n", "").Replace(" ", "").Split('.');
        if (parts.Length != 3 || parts[0] != Prefix)
            return ActivationVerdict.Malformed;
        byte[] body, signature;
        try
        {
            body = UnB64(parts[1]);
            signature = UnB64(parts[2]);
        }
        catch (FormatException)
        {
            return ActivationVerdict.Malformed;
        }

        using var key = ECDsa.Create();
        key.ImportFromPem(publicKeyPem);
        if (!key.VerifyData(Encoding.ASCII.GetBytes(Prefix + "." + parts[1]), signature, HashAlgorithmName.SHA256))
            return ActivationVerdict.BadSignature;

        try
        {
            payload = JsonSerializer.Deserialize<ActivationPayload>(body);
        }
        catch (JsonException)
        {
            return ActivationVerdict.Malformed;
        }
        return payload is { Version: 1 } && !string.IsNullOrWhiteSpace(payload.Nonce) && !string.IsNullOrWhiteSpace(payload.DeviceHash)
            ? ActivationVerdict.Valid
            : ActivationVerdict.Malformed;
    }

    public static string CodeSha256(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim()))).ToLowerInvariant();

    private static string B64(byte[] data) => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] UnB64(string text)
    {
        var s = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }
}

/// <summary>
/// ‏<b>المفتاحُ العامّ لرموز التفعيل</b> — يُلصق هنا ما يطبعه <c>tools/ActivationCodeGenerator gen-keys</c> على جهاز المورّد.
/// ‏وفارغاً يُرفض كلُّ رمزٍ بـ<see cref="ActivationVerdict.NoTrustAnchor"/> (لا يقبل البرنامجُ رمزاً لا يعرف موقِّعه).
/// </summary>
public static class EmbeddedActivationKeys
{
    public const string PublicKeyPem = "";

    /// <summary>‏للاختبارات وحدها.</summary>
    internal static string? PublicKeyPemOverride { get; set; }

    public static string? Current => PublicKeyPemOverride ?? (string.IsNullOrWhiteSpace(PublicKeyPem) ? null : PublicKeyPem);
}

/// <summary>
/// ‏<b>سجلٌّ محلّيٌّ مقاومٌ للعبث بالرموز المستهلكة</b> — سلسلةُ HMAC: كلُّ سطرٍ يحمل بصمةَ ما قبله، فحذفُ سطرٍ أو تعديلُه يكسر السلسلة.
/// ‏يجاور جدولَ <c>license_activations</c>: استرجاعُ نسخةٍ قديمةٍ من القاعدة لا يمحو السجلّ، وحذفُ السجلّ لا يمحو الجدول.
/// </summary>
public static class ConsumedActivationLog
{
    private static string PathFor() => Path.Combine(BrqoshDataRoot.Path(), "activations.chain");

    private static byte[] Key(string deviceHash) => SHA256.HashData(Encoding.UTF8.GetBytes("BRQOSH-ACTIVATION-LOG|" + deviceHash));

    /// <returns>‏الـnonces المستهلكة، أو <c>null</c> إن كُسرت السلسلة.</returns>
    public static IReadOnlySet<string>? Read(string deviceHash)
    {
        var path = PathFor();
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (!File.Exists(path)) return set;
        var previous = "";
        using var hmac = new HMACSHA256(Key(deviceHash));
        foreach (var line in File.ReadAllLines(path).Where(l => l.Length > 0))
        {
            var parts = line.Split('|');
            if (parts.Length != 3) return null;
            var expected = Mac(hmac, previous, parts[0], parts[1]);
            if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(parts[2])))
                return null;
            set.Add(parts[0]);
            previous = parts[2];
        }
        return set;
    }

    public static void Append(string deviceHash, string nonce, DateTime utc)
    {
        var path = PathFor();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var lines = File.Exists(path) ? File.ReadAllLines(path).Where(l => l.Length > 0).ToList() : [];
        var previous = lines.Count == 0 ? "" : lines[^1].Split('|')[^1];
        using var hmac = new HMACSHA256(Key(deviceHash));
        var stamp = utc.ToUniversalTime().ToString("O");
        File.AppendAllText(path, $"{nonce}|{stamp}|{Mac(hmac, previous, nonce, stamp)}{Environment.NewLine}");
    }

    private static string Mac(HMACSHA256 hmac, string previous, string nonce, string stamp) =>
        Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{previous}|{nonce}|{stamp}")));
}

public sealed record ActivationResult(ActivationVerdict Verdict, ActivationPayload? Payload, string MessageAr);

/// <summary>
/// ‏(Phase 7) <b>التفعيلُ دون اتصال</b>: البرنامجُ يعرض <see cref="CurrentRequestCode"/>، والمورّدُ يُصدر رمزَ تفعيلٍ موقَّعاً،
/// ‏فيتحقّق البرنامجُ من التوقيع والجهاز والانتهاء والساعة، ويُسجّل الـnonce مستهلكاً في القاعدة والسجلّ المحلّيّ معاً.
/// </summary>
public sealed class OfflineActivationService(BrqoshDbContext db, string? deviceHash = null, Func<DateTime>? clock = null)
{
    private const string CurrentFile = "activation.code";

    private string Device => deviceHash ?? DeviceIdentity.CurrentDeviceHash();

    public string CurrentRequestCode() => RequestCode.FromDeviceHash(Device);

    public async Task<ActivationResult> ActivateAsync(string code, CancellationToken ct = default)
    {
        var now = clock?.Invoke() ?? DateTime.UtcNow;
        if (ClockRollbackDetector.Observe(now).RolledBack)
            return Refuse(ActivationVerdict.ClockRolledBack);

        var verdict = Check(code, Device, now, out var payload);
        if (verdict != ActivationVerdict.Valid)
            return Refuse(verdict, payload);

        var consumed = ConsumedActivationLog.Read(Device);
        if (consumed is null)
            return Refuse(ActivationVerdict.LedgerTampered, payload);
        if (consumed.Contains(payload!.Nonce) || await db.LicenseActivations.AnyAsync(a => a.Nonce == payload.Nonce, ct))
            return Refuse(ActivationVerdict.AlreadyUsed, payload);

        db.LicenseActivations.Add(LicenseActivation.Record(payload.Nonce, payload.DeviceHash, payload.Edition,
            payload.MaxCompanies, payload.ExpiresUtc, payload.IsTrial, ActivationCodec.CodeSha256(code),
            payload.IssuedUtc, now));
        await db.SaveChangesAsync(ct);
        ConsumedActivationLog.Append(Device, payload.Nonce, now);
        File.WriteAllText(Path.Combine(BrqoshDataRoot.Path(), CurrentFile), code.Trim());

        return new ActivationResult(ActivationVerdict.Valid, payload, "تمّ التفعيل.");
    }

    /// <summary>‏التفعيلُ القائم — يُعاد التحقّقُ منه عند كلّ قراءة (التوقيع والجهاز والانتهاء والساعة).</summary>
    public static ActivationResult? Current(string? deviceHash = null, DateTime? nowUtc = null)
    {
        var path = Path.Combine(BrqoshDataRoot.Path(), CurrentFile);
        if (!File.Exists(path)) return null;
        var now = nowUtc ?? DateTime.UtcNow;
        if (ClockRollbackDetector.Observe(now).RolledBack)
            return Refuse(ActivationVerdict.ClockRolledBack);
        var verdict = Check(File.ReadAllText(path), deviceHash ?? DeviceIdentity.CurrentDeviceHash(), now, out var payload);
        return verdict == ActivationVerdict.Valid
            ? new ActivationResult(verdict, payload, payload!.IsTrial ? "نسخةٌ تجريبيّة مفعّلة." : "مفعّل.")
            : Refuse(verdict, payload);
    }

    private static ActivationVerdict Check(string code, string device, DateTime now, out ActivationPayload? payload)
    {
        var verdict = ActivationCodec.Verify(code, EmbeddedActivationKeys.Current, out payload);
        if (verdict != ActivationVerdict.Valid) return verdict;
        if (!string.Equals(payload!.DeviceHash, device, StringComparison.OrdinalIgnoreCase))
            return ActivationVerdict.WrongDevice;
        if (payload.ExpiresUtc is { } expires && now >= expires)
            return ActivationVerdict.Expired;
        return ActivationVerdict.Valid;
    }

    private static ActivationResult Refuse(ActivationVerdict verdict, ActivationPayload? payload = null) =>
        new(verdict, payload, verdict switch
        {
            ActivationVerdict.Malformed => "رمزُ التفعيل غيرُ مكتمل أو محرَّف — انسخه كاملاً.",
            ActivationVerdict.NoTrustAnchor => "لا مفتاحَ تحقّقٍ مضمَّنٌ في هذا الإصدار — عطلُ بناءٍ لا عطلُ رمز.",
            ActivationVerdict.BadSignature => "رمزُ التفعيل لم يصدر من المورّد أو عُدِّل.",
            ActivationVerdict.WrongDevice => "رمزُ التفعيل صدر لجهازٍ آخر — أرسل رمزَ الطلب الظاهر على هذا الجهاز.",
            ActivationVerdict.Expired => "انتهت مدّةُ هذا الترخيص.",
            ActivationVerdict.AlreadyUsed => "استُعمل رمزُ التفعيل هذا من قبل — لكلّ تفعيلٍ رمزٌ جديد.",
            ActivationVerdict.ClockRolledBack => "ساعةُ الجهاز أُرجعت إلى الوراء — صحّح التاريخ ثمّ أعد المحاولة.",
            ActivationVerdict.LedgerTampered => "سجلُّ التفعيل المحلّيّ عُبث به — تواصل مع المورّد.",
            _ => "تعذّر التفعيل.",
        });
}
