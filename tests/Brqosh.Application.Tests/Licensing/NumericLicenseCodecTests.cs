using System.Numerics;
using Brqosh.Infrastructure.Licensing;

namespace Brqosh.Application.Tests.Licensing;

/// <summary>
/// ترميزُ المفتاح الرقميّ — قياسُ كلِّ حقل: الخطةُ والتاريخُ وبصمةُ الجهاز وختمُ السلامة،
/// والرفضُ لكلّ سببٍ باسمه. والمتّجهاتُ السالبةُ هنا هي نصفُ الفحص: مفتاحٌ يُقبل حيث
/// يجب أن يُرفض عطبٌ أشدُّ من مفتاحٍ لا يعمل.
/// </summary>
public class NumericLicenseCodecTests
{
    private const string Company = "شركة اختبار";
    private static readonly string Machine = LicenseService.MachineId();
    private static readonly DateTime Now = new(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void An_annual_code_round_trips_with_its_expiry()
    {
        var issued = new DateOnly(2026, 1, 10);
        var code = NumericLicense.Encode(LicensePlan.Annual, issued, Machine, Company);

        Assert.Equal(NumericLicenseVerdict.Valid,
            NumericLicense.Decode(code, Machine, Company, Now, out var plan, out var expires, out var issuedOn));

        Assert.Equal(LicensePlan.Annual, plan);
        Assert.Equal(issued, issuedOn);
        Assert.Equal(new DateTime(2027, 1, 10, 0, 0, 0, DateTimeKind.Utc), expires);
    }

    [Fact]
    public void A_trial_code_is_valid_on_day_six_and_expired_on_day_eight()
    {
        var issued = new DateOnly(2026, 1, 1);
        var code = NumericLicense.Encode(LicensePlan.Trial7, issued, Machine, Company);

        Assert.Equal(NumericLicenseVerdict.Valid,
            NumericLicense.Decode(code, Machine, Company, new DateTime(2026, 1, 6, 23, 59, 0, DateTimeKind.Utc),
                out _, out var expires, out _));
        Assert.Equal(new DateTime(2026, 1, 8, 0, 0, 0, DateTimeKind.Utc), expires);

        Assert.Equal(NumericLicenseVerdict.Expired,
            NumericLicense.Decode(code, Machine, Company, new DateTime(2026, 1, 8, 0, 0, 0, DateTimeKind.Utc),
                out _, out _, out _));
    }

    [Fact]
    public void A_lifetime_code_never_expires()
    {
        var code = NumericLicense.Encode(LicensePlan.Lifetime, new DateOnly(2026, 1, 1), Machine, Company);

        Assert.Equal(NumericLicenseVerdict.Valid,
            NumericLicense.Decode(code, Machine, Company, new DateTime(2099, 12, 31, 23, 59, 0, DateTimeKind.Utc),
                out var plan, out var expires, out _));
        Assert.Equal(LicensePlan.Lifetime, plan);
        Assert.Equal(DateTime.MaxValue, expires);
    }

    [Fact]
    public void The_code_binds_to_the_machine_and_the_company()
    {
        var code = NumericLicense.Encode(LicensePlan.Annual, new DateOnly(2026, 1, 1), Machine, Company);

        Assert.Equal(NumericLicenseVerdict.DeviceMismatch,
            NumericLicense.Decode(code, "0123456789ABCDEF", Company, Now, out _, out _, out _));
        Assert.Equal(NumericLicenseVerdict.BadIntegrity,
            NumericLicense.Decode(code, Machine, "منشأة أخرى", Now, out _, out _, out _));
    }

    [Fact]
    public void A_single_changed_digit_breaks_the_integrity()
    {
        var code = NumericLicense.Encode(LicensePlan.Annual, new DateOnly(2026, 1, 1), Machine, Company);
        var last = code[^1];
        var tampered = code[..^1] + (last == '9' ? '0' : (char)(last + 1));

        Assert.Equal(NumericLicenseVerdict.BadIntegrity,
            NumericLicense.Decode(tampered, Machine, Company, Now, out _, out _, out _));
    }

    [Fact]
    public void A_future_issue_date_is_rejected_and_unknown_fields_are_named()
    {
        var future = DateOnly.FromDateTime(Now.AddDays(2));
        var futureCode = NumericLicense.Encode(LicensePlan.Annual, future, Machine, Company);
        Assert.Equal(NumericLicenseVerdict.NotYetValid,
            NumericLicense.Decode(futureCode, Machine, Company, Now, out _, out _, out _));

        var value = UInt128.Parse(NumericLicense.Normalize(futureCode)!);
        var unknownVersion = ((value + ((UInt128)1 << 81)).ToString()).PadLeft(NumericLicense.DigitCount, '0');
        Assert.Equal(NumericLicenseVerdict.UnknownVersion,
            NumericLicense.Decode(unknownVersion, Machine, Company, Now, out _, out _, out _));

        var unknownPlan = ((value - ((UInt128)1 << 80)).ToString()).PadLeft(NumericLicense.DigitCount, '0');
        Assert.Equal(NumericLicenseVerdict.UnknownPlan,
            NumericLicense.Decode(unknownPlan, Machine, Company, Now, out _, out _, out _));
    }

    [Theory]
    [InlineData("1234")]
    [InlineData("")]
    [InlineData("ABCDEFGHIJ123456789012345")]
    public void Malformed_input_is_rejected(string input)
    {
        Assert.Equal(NumericLicenseVerdict.Malformed,
            NumericLicense.Decode(input, Machine, Company, Now, out _, out _, out _));
    }

    [Fact]
    public void Arabic_indic_digits_and_separators_are_normalised()
    {
        var code = NumericLicense.Encode(LicensePlan.Annual, new DateOnly(2026, 1, 1), Machine, Company);
        var arabic = new string(code.Select(ch =>
            ch is >= '0' and <= '9' ? (char)(ch - '0' + '\u0660') : ch).ToArray());
        var spaced = NumericLicense.Format(code).Replace("-", " ");

        Assert.Equal(NumericLicenseVerdict.Valid,
            NumericLicense.Decode(arabic, Machine, Company, Now, out _, out _, out _));
        Assert.Equal(NumericLicenseVerdict.Valid,
            NumericLicense.Decode(spaced, Machine, Company, Now, out _, out _, out _));
    }

    [Fact]
    public void Codes_are_deterministic_per_inputs_and_differ_per_device()
    {
        var a = NumericLicense.Encode(LicensePlan.Annual, new DateOnly(2026, 1, 1), Machine, Company);
        var b = NumericLicense.Encode(LicensePlan.Annual, new DateOnly(2026, 1, 1), Machine, Company);
        var other = NumericLicense.Encode(LicensePlan.Annual, new DateOnly(2026, 1, 1), "0123456789ABCDEF", Company);

        Assert.Equal(a, b);
        Assert.NotEqual(a, other);
        Assert.Equal(25, a.Length);
        Assert.Matches("^\\d{5}-\\d{5}-\\d{5}-\\d{5}-\\d{5}$", NumericLicense.Format(a));
    }

    /// <summary>
    /// متّجهٌ ثابتٌ يقفل التوافقَ بين المنتج وبرنامج «ترخيص برقوش»: المفتاحُ الذي
    /// تُنتجه أداةُ المورِّد لهذه المدخلات يجب أن يبقى هذا الرقمَ بعينه — فأيُّ
    /// انزياحٍ في الترميز أو تغييرٍ في السرّ يظهر هنا فوراً في الطرفين.
    /// </summary>
    [Fact]
    public void The_known_vector_agrees_with_the_issuer_tool()
    {
        var code = NumericLicense.Encode(
            LicensePlan.Annual, new DateOnly(2026, 1, 10), "CF0304A7C4C4ABA1", "شركة برقوش للإنشاءات");

        Assert.Equal("3640592978113060738258138", code);
    }

    [Fact]
    public void Codes_are_not_sequential_between_neighbouring_days_or_plans()
    {
        var today = NumericLicense.Encode(LicensePlan.Annual, new DateOnly(2026, 1, 1), Machine, Company);
        var tomorrow = NumericLicense.Encode(LicensePlan.Annual, new DateOnly(2026, 1, 2), Machine, Company);
        var trial = NumericLicense.Encode(LicensePlan.Trial7, new DateOnly(2026, 1, 1), Machine, Company);

        // ‏لا تسلسلَ ظاهر: الفارقُ العدديُّ بينها كبيرٌ عشوائيٌّ، لا خطوةً واحدة.
        var a = UInt128.Parse(today);
        var b = UInt128.Parse(tomorrow);
        var c = UInt128.Parse(trial);
        Assert.True(a < b ? (b - a) > 1_000_000 : (a - b) > 1_000_000, "الفرق بين يومَين متتاليَين صغيرٌ يشي بتسلسل.");
        Assert.True(a < c ? (c - a) > 1_000_000 : (a - c) > 1_000_000, "الفرق بين خطّتَين صغيرٌ يشي بتسلسل.");
    }
}
