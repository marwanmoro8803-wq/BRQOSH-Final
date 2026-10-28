using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace Brqosh.Infrastructure.Licensing;

/// <summary>حكمُ فحص مفتاحٍ رقميّ — سببٌ مسمّىً لا «غير صالح».</summary>
public enum NumericLicenseVerdict
{
    Valid = 0,

    /// <summary>ليس ٢٥ رقماً أو فيه ما ليس رقماً.</summary>
    Malformed,

    /// <summary>نسخةُ ترميزٍ لا يعرفها هذا البناء.</summary>
    UnknownVersion,

    /// <summary>بتّا الخطة لا يقابلان خطةً معروفة.</summary>
    UnknownPlan,

    /// <summary>المفتاحُ صادرٌ لجهازٍ آخر.</summary>
    DeviceMismatch,

    /// <summary>انقضت صلاحيتُه.</summary>
    Expired,

    /// <summary>تاريخُ إصداره في المستقبل.</summary>
    NotYetValid,

    /// <summary>سلامتُه مكسورة أو صادرٌ لمنشأةٍ أخرى — لا يُفرَّق بينهما عمداً.</summary>
    BadIntegrity,
}

/// <summary>
/// <b>مفتاحُ الترخيص الرقميّ — ٢٥ رقماً يحمل كلَّ ما يحتاجه الفحص بلا ملفٍّ ولا خادم.</b>
///
/// <para>المفتاحُ عددٌ من ٨٣ بتّاً مكتوبٌ بعشرٍ: ٢٥ رقماً في خمس مجموعات. يحمل
/// نسخةَ الترميز والخطةَ (تجريبي ٧ أيام / سنوي / دائم) وتاريخَ الإصدار (أياماً منذ
/// 2025-01-01) وبصمةً للجهاز، ثمّ <b>ختمَ سلامةٍ ٤١ بتّاً</b> (‏HMAC-SHA256) يغطّي
/// ما سبق مع اسم المنشأة المُطبَّع.</para>
///
/// <para>فيُرفض على جهازٍ آخر (<see cref="NumericLicenseVerdict.DeviceMismatch"/>)،
/// ويُرفض إن عُبث برقمٍ واحد أو صدر لمنشأةٍ أخرى، ولا يُمكِن تمديدُه بتحريرِ الرقم.
/// والرقمُ عشوائيُّ المظهر فلا يُخمَّن ولا يتسلسل.</para>
///
/// <para><b>حدٌّ يُقال صراحةً:</b> الختمُ سرٌّ مشتركٌ مُجمَّعٌ في التطبيق، فمن يملك
/// البراعةَ في هندسة التجميعة عكسيّاً يستطيع صنعَ مفاتيح. تُخزَّن البايتاتُ هنا
/// مجزّأةً ومقنّعةً — <b>تعميةٌ لا أمان</b>: تصرفُ العينَ العجلى ولا تصدّ خبيراً.
/// والمنعُ الصارمُ لإعادة الاستعمال يستلزم خدمةَ تفعيلٍ متصلة، وهذا لا يشترطه
/// الفحصُ هنا.</para>
/// </summary>
public static class NumericLicense
{
    private const int Version = 1;

    /// <summary>بدايةُ عدّاد الأيام — أيامُ الإصدار تُقاس منها.</summary>
    private static readonly DateOnly DayEpoch = new(2025, 1, 1);

    /// <summary>طولُ المفتاح المعياريّ بالأرقام.</summary>
    public const int DigitCount = 25;

    private const int Bits = 83;

    /// <summary>بتّا الخطة (٢ بتّ) — بعد بتّي النسخة.</summary>
    private const int PlanShift = 79;

    private const int IssueDayShift = 65;

    private const int DeviceTagShift = 41;

    // ── السرّ المشترك (٣٢ بايتاً) — مجزّأٌ ومقنّعٌ؛ التعميةُ لا الأمان، كما وُثّق أعلاه. ──
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

    /// <summary>بايتاتُ السرّ — تُبنى من الشطرَين ولا يُكتب السرُّ حرفاً واحداً.</summary>
    internal static byte[] SecretBytes()
    {
        var bytes = new byte[32];
        for (var i = 0; i < 8; i++)
        {
            var word = SecretMaskA[i] ^ SecretMaskB[i];
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(i * 4), word);
        }
        return bytes;
    }

    /// <summary>
    /// يطبّع المُدخَل إلى ٢٥ رقماً لاتينيّاً: يُسقط الفواصل والمسافات ويحوّل
    /// الأرقامَ العربية (٠-٩) والمشرقية (۰-۹). ويعيد <c>null</c> لما ليس مفتاحاً.
    /// </summary>
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

    /// <summary>يُنسِّق ٢٥ رقماً في خمس مجموعاتٍ للعرض.</summary>
    public static string Format(string digits) =>
        $"{digits[..5]}-{digits[5..10]}-{digits[10..15]}-{digits[15..20]}-{digits[20..25]}";

    /// <summary>يبني مفتاحاً لخطةٍ صدرت في يومٍ محدّد لجهازٍ ومنشأةٍ محدّدَين.</summary>
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

    /// <summary>
    /// يفحص المفتاحَ على جهازٍ ومنشأةٍ ووقتٍ معلومَين. والترتيبُ سببُ الرفض الأوّل:
    /// الشكل ← النسخة ← الخطة ← الجهاز ← المستقبل ← الصلاحية ← السلامة.
    /// </summary>
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

        expiresUtc = LicensePlanInfo.ExpiryFor(plan, issuedOn);
        return nowUtc >= expiresUtc ? NumericLicenseVerdict.Expired : NumericLicenseVerdict.Valid;
    }

    /// <summary>فكٌّ بلا فحص صلاحيةٍ ولا منشأةٍ ولا جهاز — لاستخراج الخطة والتاريخ للعرض.</summary>
    public static NumericLicenseVerdict Peek(string code, out LicensePlan plan, out DateOnly issuedOn)
    {
        plan = default;
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
        issuedOn = DayEpoch.AddDays((int)((value >> IssueDayShift) & 0x3FFF));
        return NumericLicenseVerdict.Valid;
    }

    /// <summary>بصمةُ الجهاز: ثلاثةُ بايتاتٍ من SHA-256 على المعرّف المطبَّع.</summary>
    private static uint DeviceTag(string machineId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"BRQOSH|device|v1|{machineId.Trim().ToUpperInvariant()}"));
        return ((uint)hash[0] << 16) | ((uint)hash[1] << 8) | hash[2];
    }

    /// <summary>الختم: أعلى ٤١ بتّاً من HMAC-SHA256 يغطّي الحقولَ واسمَ المنشأة.</summary>
    private static ulong Mac(int planBits, int issueDay, uint deviceTag, string normalizedCompany)
    {
        var message = Encoding.UTF8.GetBytes(
            $"BRQOSH|code|v1|{planBits}|{issueDay}|{deviceTag:X6}|{normalizedCompany}");
        var hash = new HMACSHA256(SecretBytes()).ComputeHash(message);
        // ‏أعلى ٤١ بتّاً من بايتات الختم الستة الأولى — ليستقرّ المقدارُ في حقل ٤١ بتّ.
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

    /// <summary>طباعةُ اسم المنشأة: قصٌّ ثمّ لمُّ الفراغات الداخلية ثمّ تكبيرٌ لاتينيّ.</summary>
    internal static string NormalizeCompany(string companyName) =>
        string.Join(' ', companyName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant();
}
