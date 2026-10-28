using System.Security.Cryptography;
using Brqosh.Application.Licensing;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Infrastructure.Licensing;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Licensing;

/// <summary>
/// ‏(Phase 7) <b>التفعيلُ دون اتصال</b>: رمزُ طلبٍ مربوطٌ بالجهاز، ورمزُ تفعيلٍ موقَّعٌ من المورّد لا يصلح لجهازٍ آخر ولا يُعاد استعمالُه
/// ‏(ولو استُرجعت القاعدة)، ولا يُقبل محرَّفاً ولا منتهياً ولا مع ساعةٍ أُرجعت.
/// </summary>
[Collection("SequentialEnv")]
public class OfflineActivationIsDeviceBoundAndSingleUseTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);
    private readonly string _root = Path.Combine(Path.GetTempPath(), "brqosh-activation-" + Guid.NewGuid().ToString("N"));
    private readonly ECDsa _vendor = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly string _device = DeviceIdentity.DeviceHash("machine-A", "install-1");
    private readonly BrqoshDbContext _db;

    public OfflineActivationIsDeviceBoundAndSingleUseTests()
    {
        Environment.SetEnvironmentVariable(BrqoshDataRoot.OverrideEnvironmentVariable, _root);
        EmbeddedActivationKeys.PublicKeyPemOverride = _vendor.ExportSubjectPublicKeyInfoPem();
        _db = NewDb();
    }

    public void Dispose()
    {
        EmbeddedActivationKeys.PublicKeyPemOverride = null;
        Environment.SetEnvironmentVariable(BrqoshDataRoot.OverrideEnvironmentVariable, null);
        _db.Dispose();
        _vendor.Dispose();
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private static BrqoshDbContext NewDb()
    {
        var db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        return db;
    }

    private string Code(string? device = null, DateTime? expires = null, bool trial = false, string edition = "professional", int companies = 3) =>
        ActivationCodec.Sign(new ActivationPayload(1, device ?? _device, edition, companies, expires, trial,
            Convert.ToHexString(RandomNumberGenerator.GetBytes(16)), Now.AddDays(-1)), _vendor.ExportPkcs8PrivateKeyPem());

    private OfflineActivationService Service(BrqoshDbContext? db = null, DateTime? at = null) => new(db ?? _db, _device, () => at ?? Now);

    [Fact]
    public void TheRequestCode_CarriesTheDeviceHash_AndCatchesATypo()
    {
        var code = RequestCode.FromDeviceHash(_device);
        Assert.StartsWith("BRQ-", code);
        Assert.True(RequestCode.TryDecode(code, out var decoded));
        Assert.Equal(_device, decoded);
        var typo = code[..^1] + (code[^1] == 'A' ? 'B' : 'A');
        Assert.False(RequestCode.TryDecode(typo, out _));
        Assert.NotEqual(_device, DeviceIdentity.DeviceHash("machine-A", "install-2")); // ‏إعادةُ التركيب = جهازٌ آخر
    }

    [Fact]
    public async Task AValidCode_Activates_RecordsItsNonce_AndGrantsItsEditionAndLimit()
    {
        var result = await Service().ActivateAsync(Code(expires: Now.AddYears(1)));

        Assert.Equal(ActivationVerdict.Valid, result.Verdict);
        var row = await _db.LicenseActivations.SingleAsync();
        Assert.Equal(result.Payload!.Nonce, row.Nonce);
        Assert.Equal("professional", row.Edition);
        var current = OfflineActivationService.Current(_device, Now);
        Assert.Equal(new LicenseEntitlements(LicenseEdition.Professional, 3), current!.Payload!.Entitlements);
    }

    [Fact]
    public async Task TheSameCode_CannotBeReused_EvenAfterTheDatabaseIsRestored()
    {
        var code = Code();
        Assert.Equal(ActivationVerdict.Valid, (await Service().ActivateAsync(code)).Verdict);
        Assert.Equal(ActivationVerdict.AlreadyUsed, (await Service().ActivateAsync(code)).Verdict);

        // ‏قاعدةٌ مُسترجَعةٌ من قبل التفعيل — السجلُّ المحلّيُّ ما زال يعرف الرمز.
        using var restored = NewDb();
        Assert.Equal(ActivationVerdict.AlreadyUsed, (await Service(restored).ActivateAsync(code)).Verdict);

        // ‏وسجلٌّ محلّيٌّ محذوف — القاعدةُ ما زالت تعرفه.
        File.Delete(Path.Combine(_root, "activations.chain"));
        Assert.Equal(ActivationVerdict.AlreadyUsed, (await Service().ActivateAsync(code)).Verdict);
    }

    [Fact]
    public async Task ACodeForAnotherDevice_IsRefused()
    {
        var other = DeviceIdentity.DeviceHash("machine-B", "install-9");
        var result = await Service().ActivateAsync(Code(device: other));
        Assert.Equal(ActivationVerdict.WrongDevice, result.Verdict);
        Assert.Empty(_db.LicenseActivations);
    }

    [Fact]
    public async Task ATamperedOrForeignCode_IsRefused()
    {
        var code = Code(edition: "standard");
        var parts = code.Split('.');
        var upgraded = parts[1][..^2] + (parts[1][^2] == 'A' ? "B" : "A") + parts[1][^1];
        Assert.Equal(ActivationVerdict.BadSignature, (await Service().ActivateAsync($"{parts[0]}.{upgraded}.{parts[2]}")).Verdict);
        Assert.Equal(ActivationVerdict.Malformed, (await Service().ActivateAsync("BRQA1.not-a-code")).Verdict);

        using var stranger = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var forged = ActivationCodec.Sign(new ActivationPayload(1, _device, "professional", 99, null, false, "ff", Now),
            stranger.ExportPkcs8PrivateKeyPem());
        Assert.Equal(ActivationVerdict.BadSignature, (await Service().ActivateAsync(forged)).Verdict);
        Assert.Empty(_db.LicenseActivations);
    }

    [Fact]
    public async Task AnExpiredCode_IsRefused_AndATrialLapsesWhenItsTimeComes()
    {
        Assert.Equal(ActivationVerdict.Expired, (await Service().ActivateAsync(Code(expires: Now.AddDays(-1)))).Verdict);

        Assert.Equal(ActivationVerdict.Valid, (await Service().ActivateAsync(Code(expires: Now.AddDays(7), trial: true))).Verdict);
        Assert.Equal(ActivationVerdict.Valid, OfflineActivationService.Current(_device, Now.AddDays(6))!.Verdict);
        Assert.Equal(ActivationVerdict.Expired, OfflineActivationService.Current(_device, Now.AddDays(8))!.Verdict);
    }

    [Fact]
    public async Task WindingTheClockBack_DoesNotReviveATrial()
    {
        Assert.Equal(ActivationVerdict.Valid, (await Service().ActivateAsync(Code(expires: Now.AddDays(7), trial: true))).Verdict);
        Assert.Equal(ActivationVerdict.Expired, OfflineActivationService.Current(_device, Now.AddDays(10))!.Verdict);

        // ‏إرجاعُ الساعة إلى داخل المدّة يُكشف ولا يُعيد التجربة.
        Assert.Equal(ActivationVerdict.ClockRolledBack, OfflineActivationService.Current(_device, Now.AddDays(2))!.Verdict);
    }

    [Fact]
    public async Task ATamperedLocalLog_StopsActivation()
    {
        Assert.Equal(ActivationVerdict.Valid, (await Service().ActivateAsync(Code())).Verdict);
        var log = Path.Combine(_root, "activations.chain");
        var text = File.ReadAllText(log);
        File.WriteAllText(log, (text[0] == '0' ? '1' : '0') + text[1..]); // ‏nonce الأوّل عُدِّل

        Assert.Equal(ActivationVerdict.LedgerTampered, (await Service().ActivateAsync(Code())).Verdict);
    }

    [Fact]
    public async Task WithoutAnEmbeddedPublicKey_NoCodeIsAccepted()
    {
        var noKey = new OfflineActivationService(_db, _device, () => Now, publicKeyPem: () => "");
        Assert.Equal(ActivationVerdict.NoTrustAnchor, (await noKey.ActivateAsync(Code())).Verdict);
    }
}
