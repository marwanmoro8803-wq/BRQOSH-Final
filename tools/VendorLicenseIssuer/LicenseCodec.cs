using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Brqosh.VendorIssuer;

/// <summary>خططُ الاشتراك — والقيمُ الرقميّةُ هي المرمَّزةُ في المفتاح، فلا تُبدَّل.</summary>
public enum LicensePlan
{
    Trial7 = 1,
    Annual = 2,
    Lifetime = 3,
}

/// <summary>حكمُ فكّ المفتاح — بأسماء المنتج نفسِها وترتيبِه نفسِه.</summary>
public enum NumericLicenseVerdict
{
    Valid = 0,
    Malformed,
    UnknownVersion,
    UnknownPlan,
    DeviceMismatch,
    Expired,
    NotYetValid,
    BadIntegrity,
}

/// <summary>
/// <b>قلبُ الإصدار — نسخةٌ من ترميز المنتج (NumericLicense.cs) تُقاس لا تُفترَض.</b>
///
/// <para>المفتاحُ عددٌ من ٨٣ بتّاً: نسخة (٢) وخطة (٢) ويوم إصدار (١٤) وبصمة جهاز (٢٤)
/// وختمٌ (٤١) من HMAC-SHA256 يغطّي اسمَ المنشأة المُطبَّع. والتطبيقُ يفحصه بلا ملفٍّ
/// ولا خادم. <b>وثمنُ الاستقلال نسخةٌ من القواعد</b> — يدفعها <see cref="SelfTest"/>:
/// لو فارقت نسخةَ المنتج سقط الإصدارُ ولم يُسلَّم مفتاح.</para>
/// </summary>
public static class LicenseCodec
{
    private const int Version = 1;
    private const int Bits = 83;
    private const int PlanShift = 79;
    private const int IssueDayShift = 65;
    private const int DeviceTagShift = 41;
    public const int DigitCount = 25;

    private static readonly DateOnly DayEpoch = new(2025, 1, 1);

    private static readonly uint[] SecretMaskA =
    [
        0x9E3779B9, 0x9F397CBD, 0xA03B7FC1, 0xA13D82C5,
        0xA23F85C9, 0xA34188CD, 0xA4438BD1, 0xA5458ED5,
    ];

    private static readonly uint[] SecretMaskB =
    [
        0xEA7D9304, 0x9E2B73F6, 0xAE9DE975, 0x7B1CDF82,
        0xA9E29D86, 0x95CA0986, 0x9FDA3BAD, 0xCB2C150E,
    ];

    private static byte[] SecretBytes()
    {
        var bytes = new byte[32];
        for (var i = 0; i < 8; i++)
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(i * 4), SecretMaskA[i] ^ SecretMaskB[i]);
        return bytes;
    }

    /// <summary>٢٥ رقماً لاتينيّاً بعد إسقاط الفواصل وتحويل الأرقام العربية.</summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var digits = new char[DigitCount];
        var n = 0;
        foreach (var ch in input)
        {
            if (ch is ' ' or '-' or '\t' or '\r' or '\n' or '\u00A0')
                continue;

            var d = ch switch
            {
                >= '0' and <= '9' => (char)ch,
                >= '\u0660' and <= '\u0669' => (char)('0' + (ch - '\u0660')),
                >= '\u06F0' and <= '\u06F9' => (char)('0' + (ch - '\u06F0')),
                _ => (char)0,
            };
            if (d == 0 || n >= DigitCount)
                return null;
            digits[n++] = d;
        }

        return n == DigitCount ? new string(digits) : null;
    }

    public static string Format(string digits) =>
        $"{digits[..5]}-{digits[5..10]}-{digits[10..15]}-{digits[15..20]}-{digits[20..25]}";

    /// <summary>يبني المفتاح — ويُعيده ٢٥ رقماً بلا فواصل (العرضُ يُنسَّق في الواجهة).</summary>
    public static string Encode(LicensePlan plan, DateOnly issuedOn, string machineId, string companyName)
    {
        if (string.IsNullOrWhiteSpace(machineId))
            throw new ArgumentException("معرّفُ الجهاز مطلوب.", nameof(machineId));
        if (string.IsNullOrWhiteSpace(companyName))
            throw new ArgumentException("اسمُ المنشأة مطلوب.", nameof(companyName));

        var planBits = (int)plan;
        if (planBits is < 1 or > 3)
            throw new ArgumentOutOfRangeException(nameof(plan), plan, "خطةُ اشتراكٍ غير معروفة.");

        var issueDay = issuedOn.DayNumber - DayEpoch.DayNumber;
        if (issueDay is < 0 or > 0x3FFF)
            throw new ArgumentOutOfRangeException(nameof(issuedOn), issuedOn,
                "تاريخُ الإصدار خارجَ نطاق الترميز (2025-01-01 حتى 2069).");

        var deviceTag = DeviceTag(machineId);
        var mac = Mac(planBits, issueDay, deviceTag, NormalizeCompany(companyName));

        UInt128 value = (UInt128)Version << (Bits - 2);
        value |= (UInt128)planBits << PlanShift;
        value |= (UInt128)issueDay << IssueDayShift;
        value |= (UInt128)deviceTag << DeviceTagShift;
        value |= mac;

        return value.ToString().PadLeft(DigitCount, '0');
    }

    /// <summary>يفحص المفتاحَ كما يفحصه المنتج — وللاختبار الذاتيّ هنا.</summary>
    public static NumericLicenseVerdict Decode(
        string code, string machineId, string companyName, DateTime nowUtc,
        out LicensePlan plan, out DateTime expiresUtc, out DateOnly issuedOn)
    {
        plan = default;
        expiresUtc = default;
        issuedOn = default;

        var digits = Normalize(code);
        if (digits is null || !UInt128.TryParse(digits, out var value) || value >= (UInt128)1 << Bits)
            return NumericLicenseVerdict.Malformed;

        var version = (int)((value >> (Bits - 2)) & 0x3);
        if (version != Version)
            return NumericLicenseVerdict.UnknownVersion;

        var planBits = (int)((value >> PlanShift) & 0x3);
        if (planBits is < 1 or > 3)
            return NumericLicenseVerdict.UnknownPlan;
        plan = (LicensePlan)planBits;

        var issueDay = (int)((value >> IssueDayShift) & 0x3FFF);
        var deviceTag = (uint)((value >> DeviceTagShift) & 0xFFFFFF);
        var mac = (ulong)(value & ((1UL << DeviceTagShift) - 1));

        if (deviceTag != DeviceTag(machineId))
            return NumericLicenseVerdict.DeviceMismatch;

        issuedOn = DayEpoch.AddDays(issueDay);
        var today = DateOnly.FromDateTime(nowUtc);
        if (issueDay > today.DayNumber - DayEpoch.DayNumber + 1)
            return NumericLicenseVerdict.NotYetValid;

        if (!MacMatches(planBits, issueDay, deviceTag, NormalizeCompany(companyName), mac))
            return NumericLicenseVerdict.BadIntegrity;

        expiresUtc = ExpiryFor(plan, issuedOn);
        return nowUtc >= expiresUtc ? NumericLicenseVerdict.Expired : NumericLicenseVerdict.Valid;
    }

    /// <summary>فكٌّ بلا فحص جهازٍ ولا منشأة — لقراءة الخطة والتاريخ من مفتاحٍ محفوظ.</summary>
    public static NumericLicenseVerdict Peek(string code, out LicensePlan plan, out DateOnly issuedOn)
    {
        plan = default;
        issuedOn = default;
        var digits = Normalize(code);
        if (digits is null || !UInt128.TryParse(digits, out var value) || value >= (UInt128)1 << Bits)
            return NumericLicenseVerdict.Malformed;

        if ((int)((value >> (Bits - 2)) & 0x3) != Version)
            return NumericLicenseVerdict.UnknownVersion;

        var planBits = (int)((value >> PlanShift) & 0x3);
        if (planBits is < 1 or > 3)
            return NumericLicenseVerdict.UnknownPlan;

        plan = (LicensePlan)planBits;
        issuedOn = DayEpoch.AddDays((int)((value >> IssueDayShift) & 0x3FFF));
        return NumericLicenseVerdict.Valid;
    }

    public static DateTime ExpiryFor(LicensePlan plan, DateOnly issuedOn) => plan switch
    {
        LicensePlan.Trial7 => issuedOn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(7),
        LicensePlan.Annual => issuedOn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(365),
        LicensePlan.Lifetime => DateTime.MaxValue,
        _ => throw new ArgumentOutOfRangeException(nameof(plan), plan, "خطةُ اشتراكٍ غير معروفة."),
    };

    public static string PlanLabelAr(LicensePlan plan) => plan switch
    {
        LicensePlan.Trial7 => "تجريبي — ٧ أيام",
        LicensePlan.Annual => "سنوي — ٣٦٥ يوماً",
        LicensePlan.Lifetime => "دائم — بلا انتهاء",
        _ => "غير معروف",
    };

    public static string PlanName(LicensePlan plan) => plan switch
    {
        LicensePlan.Trial7 => "Trial7",
        LicensePlan.Annual => "Annual",
        LicensePlan.Lifetime => "Lifetime",
        _ => "Unknown",
    };

    /// <summary>بصمةُ الجهاز: ثلاثةُ بايتاتٍ من SHA-256 على المعرّف المطبَّع.</summary>
    private static uint DeviceTag(string machineId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"BRQOSH|device|v1|{machineId.Trim().ToUpperInvariant()}"));
        return ((uint)hash[0] << 16) | ((uint)hash[1] << 8) | hash[2];
    }

    private static ulong Mac(int planBits, int issueDay, uint deviceTag, string normalizedCompany)
    {
        var message = Encoding.UTF8.GetBytes(
            $"BRQOSH|code|v1|{planBits}|{issueDay}|{deviceTag:X6}|{normalizedCompany}");
        var hash = new HMACSHA256(SecretBytes()).ComputeHash(message);
        return (((ulong)hash[0] << 40) | ((ulong)hash[1] << 32) | ((ulong)hash[2] << 24)
                | ((ulong)hash[3] << 16) | ((ulong)hash[4] << 8) | hash[5]) >> 7;
    }

    private static bool MacMatches(int planBits, int issueDay, uint deviceTag,
        string normalizedCompany, ulong expected)
    {
        var actual = Mac(planBits, issueDay, deviceTag, normalizedCompany);
        Span<byte> left = stackalloc byte[8];
        Span<byte> right = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(left, actual);
        BinaryPrimitives.WriteUInt64LittleEndian(right, expected);
        return CryptographicOperations.FixedTimeEquals(left, right);
    }

    /// <summary>طباعةُ اسم المنشأة كما في المنتج: قصٌّ ولمُّ فراغاتٍ وتكبيرٌ لاتينيّ.</summary>
    public static string NormalizeCompany(string companyName) =>
        string.Join(' ', companyName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant();

    /// <summary>معرّفُ الجهاز: ستّ عشريٌّ بستّة عشر محرفاً كما يعرضه المنتج.</summary>
    public static bool LooksLikeMachineId(string value) =>
        value.Length == 16 && value.All(Uri.IsHexDigit);

    /// <summary>محارفُ خفيّةٌ تُسقط الرباطَ بلا أثرٍ مرئيّ — تُنبَّه قبل الإصدار.</summary>
    public static string? DescribeHiddenCharacters(string text)
    {
        var found = new List<string>();
        foreach (var ch in text)
        {
            var suspicious = ch switch
            {
                '\u00A0' => "مسافة غير فاصلة (U+00A0)",
                '\u200B' => "مسافة صفريّة (U+200B)",
                '\u200E' => "علامة اتجاه يسار-يمين (U+200E)",
                '\u200F' => "علامة اتجاه يمين-يسار (U+200F)",
                '\u202A' or '\u202B' or '\u202C' or '\u202D' or '\u202E' => "علامة تضمين اتجاهيّ",
                '\u0640' => "تطويل (U+0640)",
                '\uFEFF' => "علامة ترتيب بايتات (U+FEFF)",
                '\t' => "جدولة (Tab)",
                _ => null,
            };
            if (suspicious is not null && !found.Contains(suspicious))
                found.Add(suspicious);
        }

        if (text.Length != text.Trim().Length)
            found.Insert(0, "مسافات في الطرفين (تُقتطع تلقائياً)");

        return found.Count == 0 ? null : string.Join(" · ", found);
    }

    /// <summary>تاريخٌ منسَّقٌ بثقافةٍ ثابتة — لا تقويمَ هجريّاً على ويندوز عربيّ.</summary>
    public static string FormatDate(DateTime value) => value == DateTime.MaxValue
        ? "بلا انتهاء"
        : value.ToUniversalTime().ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}
