using System.Security.Cryptography;
using System.Text.Json;
using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Licensing;
using Brqosh.Infrastructure.Operations;

namespace Brqosh.Application.Tests.Operations;

/// <summary>
/// (ح-١٤٠ · installer-deployment-ops#12) <b>القناةُ لها مرساتُها وناشرُها.</b>
///
/// <para><b>ما كان:</b> البيانُ يُتحقَّق منه بمفتاح الترخيص — فمن يملك مفتاحَ الترخيص (وهو
/// يُستعمل كلَّ يومٍ لإصدار التراخيص) يوزّع حزمةً على كلّ عميل؛ <b>والملاحظاتُ خارجَ التوقيع</b>
/// فتُكتب تحت تحديثٍ توقيعُه صحيح؛ <b>ولا أداةَ تُنتج بياناً</b>.</para>
/// </summary>
public class UpdatePublishingTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "brqosh-publish-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly RSA _updateKey = RSA.Create(2048);
    private readonly RSA _licenceKey = RSA.Create(2048);

    public UpdatePublishingTests()
    {
        Directory.CreateDirectory(_root);
        UpdateCheckService.PublicKeyPemOverride = _updateKey.ExportSubjectPublicKeyInfoPem();
        RsaLicenseValidator.PublicKeyPemOverride = _licenceKey.ExportSubjectPublicKeyInfoPem();
    }

    public void Dispose()
    {
        UpdateCheckService.PublicKeyPemOverride = null;
        RsaLicenseValidator.PublicKeyPemOverride = null;
        _updateKey.Dispose();
        _licenceKey.Dispose();
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// <b>القياسُ الحاكم: بيانٌ وقّعه مفتاحُ الترخيص يُرفض.</b>
    /// </summary>
    [Fact]
    public async Task AManifestSignedWithTheLicenceKey_IsUntrusted()
    {
        var path = Write(UpdateCheckService.CreateSignedJson(Manifest("999.0.0"), _licenceKey.ExportPkcs8PrivateKeyPem()));

        var result = await new UpdateCheckService().CheckAsync(path);

        Assert.Equal(UpdateStatus.Untrusted, result.Status);
    }

    /// <summary>
    /// <b>والملاحظاتُ داخلَ التوقيع</b> — نصٌّ يُعرض للمالك لا يُبدَّل تحت توقيعٍ صحيح.
    /// </summary>
    [Fact]
    public async Task NotesChangedAfterSigning_AreUntrusted()
    {
        var json = UpdateCheckService.CreateSignedJson(Manifest("999.0.0"), _updateKey.ExportPkcs8PrivateKeyPem());
        var signed = JsonSerializer.Deserialize<SignedUpdateManifest>(json)!;
        var forged = signed with { Manifest = signed.Manifest with { Notes = "اتّصل بالدعم على 0500000000 وعطّل مضادّ الفيروسات" } };

        var result = await new UpdateCheckService().CheckAsync(Write(JsonSerializer.Serialize(forged)));

        Assert.Equal(UpdateStatus.Untrusted, result.Status);
    }

    /// <summary>
    /// <b>ما ينشره الناشرُ يقبله العميل ويُهيّئه</b> — البصمةُ والحجمُ من الحزمة لا من يد.
    /// </summary>
    [Fact]
    public async Task APublishedChannel_IsAnUpdate_AndItsPackageStages()
    {
        var package = Path.Combine(_root, "BRQOSH-Setup.msi");
        await File.WriteAllBytesAsync(package, RandomNumberGenerator.GetBytes(4096));
        var channel = Path.Combine(_root, "channel");

        var manifestPath = await UpdatePublisher.PublishAsync(package, "999.1.2", new DateOnly(2026, 10, 17),
            "إصلاحاتٌ أمنيّة", _updateKey.ExportPkcs8PrivateKeyPem(), channel);

        var check = await new UpdateCheckService().CheckAsync(manifestPath);
        Assert.Equal(UpdateStatus.UpdateAvailable, check.Status);
        Assert.Equal("999.1.2", check.AvailableVersion);
        Assert.Equal("إصلاحاتٌ أمنيّة", check.Notes);

        var staged = await new UpdateCheckService().StageAsync(manifestPath, channel, Path.Combine(_root, "staging"));
        Assert.Equal(await File.ReadAllBytesAsync(package), await File.ReadAllBytesAsync(staged));
    }

    /// <summary>
    /// <b>ومفتاحٌ لا يطابق المرساةَ يُرفض قبل أن يُكتب شيء</b> — وإلا نُشر بيانٌ يرفضه كلُّ عميل.
    /// </summary>
    [Fact]
    public async Task PublishingWithTheLicenceKey_IsRefused_AndWritesNoManifest()
    {
        var package = Path.Combine(_root, "BRQOSH-Setup.msi");
        await File.WriteAllBytesAsync(package, [1, 2, 3]);
        var channel = Path.Combine(_root, "channel");

        var ex = await Assert.ThrowsAsync<AccountingException>(() => UpdatePublisher.PublishAsync(
            package, "999.0.0", new DateOnly(2026, 10, 17), null, _licenceKey.ExportPkcs8PrivateKeyPem(), channel));

        Assert.Contains("لا يطابق مرساةَ التحديث", ex.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(channel, UpdatePublisher.ManifestFileName)));
    }

    [Theory]
    [InlineData("1.2")]
    [InlineData("v1.2.3")]
    [InlineData("")]
    public async Task AVersionWithoutThreeParts_IsRefused(string version)
    {
        var package = Path.Combine(_root, "BRQOSH-Setup.msi");
        await File.WriteAllBytesAsync(package, [1]);

        await Assert.ThrowsAsync<AccountingException>(() => UpdatePublisher.PublishAsync(
            package, version, new DateOnly(2026, 10, 17), null, _updateKey.ExportPkcs8PrivateKeyPem(), _root));
    }

    /// <summary>
    /// <b>توليدُ المفاتيح لا يستبدل مفتاحاً قائماً، ولا يكتب في جذر البيانات.</b>
    /// </summary>
    [Fact]
    public void GeneratingKeys_NeverOverwrites_AndRefusesTheDataRoot()
    {
        var dir = Path.Combine(_root, "keys");
        var first = UpdatePublisher.GenerateKeys(dir, dataRoot: Path.Combine(_root, "data"));
        var original = File.ReadAllText(first.PrivateKeyPath);

        Assert.Throws<AccountingException>(() => UpdatePublisher.GenerateKeys(dir, dataRoot: Path.Combine(_root, "data")));
        Assert.Equal(original, File.ReadAllText(first.PrivateKeyPath));

        var dataRoot = Path.Combine(_root, "data");
        Assert.Throws<AccountingException>(() => UpdatePublisher.GenerateKeys(dataRoot, dataRoot));
        Assert.False(File.Exists(Path.Combine(dataRoot, UpdatePublisher.PrivateKeyFileName)));
    }

    /// <summary>
    /// <b>المرساةُ المُضمَّنةُ غيرُ مرساة الترخيص، وبصمتُها مثبَّتة</b> — فتبديلُها قرارُ إصدارٍ يُرى.
    /// </summary>
    [Fact]
    public void TheEmbeddedUpdateAnchor_IsItsOwnKey()
    {
        var update = EmbeddedUpdateKeys.Fingerprint(EmbeddedUpdateKeys.PublicKeyPem);

        Assert.Equal("4830E28520DE5F4DB7601D249313E0322A7F99A054F8FC87F6A6A11DF2BF731C", update);
        Assert.NotEqual(EmbeddedUpdateKeys.Fingerprint(EmbeddedLicenseKeys.PublicKeyPem), update);
    }

    /// <summary><b>وفي الإنتاج لا يُقرأ متغيّرُ البيئة</b> — يُرفع التشدّدُ ولا يُخفَض.</summary>
    [Fact]
    public void InProduction_TheEnvironmentCannotReplaceTheAnchor()
    {
        const string attacker = "-----BEGIN PUBLIC KEY-----\nAAAA\n-----END PUBLIC KEY-----";

        Assert.Equal(EmbeddedUpdateKeys.PublicKeyPem, EmbeddedUpdateKeys.Resolve(true, null, attacker));
        Assert.Equal(attacker, EmbeddedUpdateKeys.Resolve(false, null, attacker));
    }

    private static UpdateManifest Manifest(string version) =>
        new(version, "2026-10-17", "BRQOSH-Setup.msi", new string('A', 64), 1024, "إصلاحات");

    private string Write(string json)
    {
        var path = Path.Combine(_root, $"manifest-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }
}
