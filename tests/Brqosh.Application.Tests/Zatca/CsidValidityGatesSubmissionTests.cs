using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Zatca;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Zatca;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-٥٢ · <c>tax-zatca#10</c>) <b>شهادةُ CSID منتهيةٌ لا تُعدّ «جاهزةً للإرسال» —
/// وكانت تُعدّ.</b>
///
/// <para>‏<c>ZatcaConfigurationValidator</c> يسأل: أموجودٌ ملفُّ الشهادة؟ <b>ولا يسأل: أصالحةٌ هي اليوم؟</b>
/// فشهادةٌ انتهت تمرّ «جاهزة»، والإرسالُ في الإنتاج يمضي إلى الهيئة <b>فيُرفض هناك</b> — ويُسجَّل
/// المستندُ مرفوضاً (<c>tax-zatca#8</c>) كأنّ العيبَ فيه. <b>ولا إنذارَ قبل الانتهاء</b>: أوّلُ ما يُعرف أنّ
/// الشهادةَ انتهت هو أوّلُ فاتورةٍ لا تصل.</para>
/// </summary>
public class CsidValidityGatesSubmissionTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "brqosh-csid-" + Guid.NewGuid().ToString("N"));
    private long _companyId;

    public CsidValidityGatesSubmissionTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    [Fact]
    public async Task AnExpiredCsid_IsNotReadyForSubmission()
    {
        await ConfigureAsync(notBefore: DateTimeOffset.UtcNow.AddYears(-1), notAfter: DateTimeOffset.UtcNow.AddDays(-1));

        var status = await Validate();

        Assert.False(status.ReadyForSubmission, "شهادةٌ منتهيةٌ عُدّت جاهزة — والهيئةُ ترفض كلَّ ما يُرسَل بها.");
        Assert.Contains(status.Warnings, w => w.Contains("انتهت", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ACsidNotYetValid_IsNotReadyForSubmission()
    {
        await ConfigureAsync(notBefore: DateTimeOffset.UtcNow.AddDays(2), notAfter: DateTimeOffset.UtcNow.AddYears(1));

        var status = await Validate();

        Assert.False(status.ReadyForSubmission);
        Assert.Contains(status.Warnings, w => w.Contains("لا تصلح قبل", StringComparison.Ordinal));
    }

    /// <summary>
    /// ‏<b>والإنذارُ قبل الانتهاء</b> — ثلاثون يوماً، <b>والإرسالُ باقٍ</b>: الشهادةُ صالحةٌ اليوم، ومنعُ الإرسال
    /// بسبب الغد يُغلق حالةً مشروعة.
    /// </summary>
    [Fact]
    public async Task ACsidExpiringSoon_WarnsButStaysReady()
    {
        await ConfigureAsync(notBefore: DateTimeOffset.UtcNow.AddYears(-1), notAfter: DateTimeOffset.UtcNow.AddDays(10));

        var status = await Validate();

        Assert.True(status.ReadyForSubmission, string.Join(" | ", status.Warnings));
        Assert.Contains(status.Warnings, w => w.Contains("تنتهي", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnUnreadableCertificate_IsNotReadyForSubmission()
    {
        await ConfigureAsync(notBefore: DateTimeOffset.UtcNow.AddYears(-1), notAfter: DateTimeOffset.UtcNow.AddYears(1));
        var settings = await _db.ZatcaSettings.SingleAsync(s => s.CompanyId == _companyId);
        await File.WriteAllTextAsync(settings.CertificateFilePath, "ليست شهادة");

        var status = await Validate();

        Assert.False(status.ReadyForSubmission);
        Assert.Contains(status.Warnings, w => w.Contains("لا تُقرأ", StringComparison.Ordinal));
    }

    /// <summary>‏<b>وشهادةٌ صالحةٌ لسنةٍ جاهزةٌ بلا إنذار</b> (شاهدُ ضبط) — فالفحصُ لا يرفض كلَّ شهادة.</summary>
    [Fact]
    public async Task AValidCsid_IsReady_WithNoCertificateWarning()
    {
        await ConfigureAsync(notBefore: DateTimeOffset.UtcNow.AddDays(-1), notAfter: DateTimeOffset.UtcNow.AddYears(1));

        var status = await Validate();

        Assert.True(status.ReadyForSubmission, string.Join(" | ", status.Warnings));
        Assert.DoesNotContain(status.Warnings, w => w.Contains("CSID", StringComparison.Ordinal));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private Task<ZatcaConfigurationStatus> Validate() =>
        new ZatcaConfigurationValidator(_db).ValidateAsync(_companyId);

    private async Task ConfigureAsync(DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700003", BusinessProfile.General);
        _companyId = company.Id;
        await ZatcaReadySeller.CompleteAsync(_db, _companyId);

        Directory.CreateDirectory(_dir);
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=BRQOSH CSID Test", ecdsa, HashAlgorithmName.SHA256);
        using var cert = request.CreateSelfSigned(notBefore, notAfter);
        var certPath = Path.Combine(_dir, "csid.crt.pem");
        var keyPath = Path.Combine(_dir, "csid.key.pem");
        await File.WriteAllTextAsync(certPath, cert.ExportCertificatePem());
        await File.WriteAllTextAsync(keyPath, ecdsa.ExportECPrivateKeyPem());

        var settings = await _db.ZatcaSettings.SingleAsync(s => s.CompanyId == _companyId);
        settings.Update(isEnabled: true, ZatcaEnvironment.Production,
            "https://example.invalid/zatca", certPath, keyPath, "token");
        await _db.SaveChangesAsync();
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
        GC.SuppressFinalize(this);
    }
}
