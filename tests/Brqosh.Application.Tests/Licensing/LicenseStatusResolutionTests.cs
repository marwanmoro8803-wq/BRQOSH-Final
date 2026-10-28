using Brqosh.Application.Tests.TestSupport;
using Brqosh.Infrastructure.Licensing;
using Brqosh.Infrastructure.Persistence;

namespace Brqosh.Application.Tests.Licensing;

/// <summary>
/// قرارُ الحال الواحد: مفتاحٌ رقميّ أو ملفٌّ موقَّع أو تجربةٌ — والترتيبُ صالحٌ
/// فمنتهٍ فتجربةٌ فعدم. وقياسٌ أنّ أبواب <see cref="LicenseStore"/> كلَّها تقرأ
/// القرارَ نفسَه (الإقلاعُ والإنذارُ ووضعُ القراءة والوحدات).
/// </summary>
[Collection("SequentialEnv")]
public class LicenseStatusResolutionTests : IDisposable
{
    private const string Company = "منشأة القرار";
    private static readonly string Machine = LicenseService.MachineId();
    private static readonly DateTime Now = new(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc);

    private readonly string _tempRoot;
    private readonly InMemoryTrialStore _trialStore = new();
    private readonly string? _priorUiTest;

    public LicenseStatusResolutionTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "brqosh-license-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        Environment.SetEnvironmentVariable(BrqoshDataRoot.OverrideEnvironmentVariable, _tempRoot);
        TrialPeriod.StoreOverride = _trialStore;
        _priorUiTest = Environment.GetEnvironmentVariable("BRQOSH_UITEST");
        Environment.SetEnvironmentVariable("BRQOSH_UITEST", null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(BrqoshDataRoot.OverrideEnvironmentVariable, null);
        Environment.SetEnvironmentVariable("BRQOSH_UITEST", _priorUiTest);
        TrialPeriod.StoreOverride = null;
        try
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void A_fresh_install_gets_a_seven_day_trial()
    {
        var status = LicenseStatusResolver.Current(Company, Now);

        Assert.Equal(LicenseKind.Trial, status.Kind);
        Assert.Equal(LicensePlan.Trial7, status.Plan);
        Assert.True(status.IsActive);
        Assert.Equal(7, status.DaysLeft);
        Assert.Equal(Now.AddDays(7), status.ExpiresUtc);
        Assert.True(LicenseStore.ValidateForCompany(Company, nowUtc: Now));
    }

    [Fact]
    public void An_expired_trial_is_expired_and_does_not_open_read_only()
    {
        _trialStore.Record = TrialPeriod.Stamp(Now.AddDays(-8));

        var status = LicenseStatusResolver.Current(Company, Now);

        Assert.Equal(LicenseKind.Expired, status.Kind);
        Assert.Equal(LicensePlan.Trial7, status.Plan);
        Assert.False(status.IsActive);
        Assert.False(LicenseStore.IsGenuineButExpiredFor(Company, Now));
        Assert.False(LicenseStore.ValidateForCompany(Company, nowUtc: Now));
    }

    [Fact]
    public void A_valid_numeric_code_licenses_the_company()
    {
        var issued = DateOnly.FromDateTime(Now);
        var code = NumericLicense.Encode(LicensePlan.Annual, issued, Machine, Company);
        LicenseStore.SaveCode(Company, code);

        var status = LicenseStatusResolver.Current(Company, Now);

        Assert.Equal(LicenseKind.Licensed, status.Kind);
        Assert.Equal(LicensePlan.Annual, status.Plan);
        Assert.Equal(365, status.DaysLeft);
        Assert.Equal(code, status.Code);
        Assert.Equal(status.ExpiresUtc, LicenseStore.ValidLicenseExpiresUtc(Company, Now));
        Assert.Null(LicenseStore.LicensedModulesFor(Company));
        Assert.True(LicenseStore.ValidateForCompany(Company, nowUtc: Now));
    }

    [Fact]
    public void An_expired_numeric_code_is_a_genuine_expired_license()
    {
        var issued = DateOnly.FromDateTime(Now.AddDays(-400));
        var code = NumericLicense.Encode(LicensePlan.Annual, issued, Machine, Company);
        LicenseStore.SaveCode(Company, code);

        var status = LicenseStatusResolver.Current(Company, Now);

        Assert.Equal(LicenseKind.Expired, status.Kind);
        Assert.Equal(LicensePlan.Annual, status.Plan);
        Assert.True(LicenseStore.IsGenuineButExpiredFor(Company, Now));
        Assert.Null(LicenseStore.ValidLicenseExpiresUtc(Company, Now));
    }

    [Fact]
    public void A_lifetime_code_has_no_expiry_and_no_days_count()
    {
        var code = NumericLicense.Encode(LicensePlan.Lifetime, DateOnly.FromDateTime(Now), Machine, Company);
        LicenseStore.SaveCode(Company, code);

        var status = LicenseStatusResolver.Current(Company, Now);

        Assert.Equal(LicenseKind.Licensed, status.Kind);
        Assert.Equal(LicensePlan.Lifetime, status.Plan);
        Assert.Null(status.DaysLeft);
        Assert.Equal(DateTime.MaxValue, status.ExpiresUtc);
        Assert.Equal(DateTime.MaxValue, LicenseStore.ValidLicenseExpiresUtc(Company, Now));
    }

    [Fact]
    public void A_code_for_another_machine_or_company_falls_back_to_the_trial()
    {
        var foreign = NumericLicense.Encode(LicensePlan.Annual, DateOnly.FromDateTime(Now), "0123456789ABCDEF", Company);
        LicenseStore.SaveCode(Company, foreign);
        Assert.Equal(LicenseKind.Trial, LicenseStatusResolver.Current(Company, Now).Kind);

        var forOtherCompany = NumericLicense.Encode(LicensePlan.Annual, DateOnly.FromDateTime(Now), Machine, "منشأة أخرى");
        LicenseStore.SaveCode(Company, forOtherCompany);
        Assert.Equal(LicenseKind.Trial, LicenseStatusResolver.Current(Company, Now).Kind);
    }

    [Fact]
    public void A_tampered_code_falls_back_to_the_trial()
    {
        var code = NumericLicense.Encode(LicensePlan.Annual, DateOnly.FromDateTime(Now), Machine, Company);
        var last = code[^1];
        LicenseStore.SaveCode(Company, code[..^1] + (last == '9' ? '0' : (char)(last + 1)));

        Assert.Equal(LicenseKind.Trial, LicenseStatusResolver.Current(Company, Now).Kind);
    }

    [Fact]
    public void A_signed_license_file_still_works_side_by_side()
    {
        using var keys = RsaTestKeys.UseForValidation();
        LicenseStore.SaveSignedLicense(Company, RsaTestKeys.Sign(Company, Machine, days: 30));

        // ‏الملفُّ الموقَّع يُقاس بساعة الجهاز الحقيقية — لا بتاريخ اختبارٍ ثابتٍ سابقٍ له.
        var now = DateTime.UtcNow;
        var status = LicenseStatusResolver.Current(Company, now);

        Assert.Equal(LicenseKind.Licensed, status.Kind);
        Assert.Equal(LicensePlan.Annual, status.Plan);
        Assert.NotNull(LicenseStore.ValidLicenseExpiresUtc(Company, now));
    }

    [Fact]
    public void A_genuine_expired_signed_license_is_expired()
    {
        using var keys = RsaTestKeys.UseForValidation();
        LicenseStore.SaveSignedLicense(Company, RsaTestKeys.Sign(Company, Machine, days: -1));

        var now = DateTime.UtcNow;
        var status = LicenseStatusResolver.Current(Company, now);

        Assert.Equal(LicenseKind.Expired, status.Kind);
        Assert.True(LicenseStore.IsGenuineButExpiredFor(Company, now));
    }

    [Fact]
    public void A_valid_code_beats_an_expired_signed_license()
    {
        using var keys = RsaTestKeys.UseForValidation();
        LicenseStore.SaveSignedLicense(Company, RsaTestKeys.Sign(Company, Machine, days: -1));
        var code = NumericLicense.Encode(LicensePlan.Annual, DateOnly.FromDateTime(Now), Machine, Company);
        LicenseStore.SaveCode(Company, code);

        var status = LicenseStatusResolver.Current(Company, DateTime.UtcNow);

        Assert.Equal(LicenseKind.Licensed, status.Kind);
        Assert.Equal(code, status.Code);
    }

    private sealed class InMemoryTrialStore : TrialPeriod.ITrialStampStore
    {
        public TrialPeriod.TrialStampRecord? Record { get; set; }

        public TrialPeriod.TrialStampRecord? Read() => Record;

        public void Write(TrialPeriod.TrialStampRecord record) => Record = record;
    }
}
