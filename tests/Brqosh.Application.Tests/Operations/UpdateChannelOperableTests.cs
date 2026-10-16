using System.Net;
using System.Security.Cryptography;
using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Operations;

namespace Brqosh.Application.Tests.Operations;

/// <summary>
/// (ح-١٤١ · installer-deployment-ops#12) <b>القناةُ تعمل من غير ملفٍّ يُختار كلَّ مرّة.</b>
///
/// <para><b>ما كان:</b> المصدرُ يُختار في كلّ فحص ولا يُحفظ، <b>و<c>StageAsync</c> لا يستدعيها
/// شيء</b> — ولا تبلغ قناةَ https أصلاً: تنسخ الحزمةَ من مجلّدٍ يُمرَّر لها، فعميلٌ قناتُه عنوانُ
/// ويبٍ يرى «يوجد تحديث» ولا سبيلَ له إلى الحزمة.</para>
/// </summary>
public class UpdateChannelOperableTests : IDisposable
{
    private const string Endpoint = "https://updates.example.test/brqosh/manifest.json";

    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "brqosh-operable-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly RSA _updateKey = RSA.Create(2048);

    public UpdateChannelOperableTests()
    {
        Directory.CreateDirectory(_root);
        UpdateCheckService.PublicKeyPemOverride = _updateKey.ExportSubjectPublicKeyInfoPem();
    }

    public void Dispose()
    {
        UpdateCheckService.PublicKeyPemOverride = null;
        _updateKey.Dispose();
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    // ── القناةُ المحفوظة ───────────────────────────────────────────────────

    [Fact]
    public void TheChannel_IsRemembered_AndHttpIsRefusedWhenSaved()
    {
        Assert.Null(UpdateChannelSettings.Load(_root));

        UpdateChannelSettings.Save("  \\\\server\\updates\\manifest.json  ", _root);
        Assert.Equal("\\\\server\\updates\\manifest.json", UpdateChannelSettings.Load(_root));

        Assert.Throws<AccountingException>(() => UpdateChannelSettings.Save("http://updates.example.test/m.json", _root));
        Assert.Equal("\\\\server\\updates\\manifest.json", UpdateChannelSettings.Load(_root));
    }

    // ── التهيئةُ من القناة نفسِها ──────────────────────────────────────────

    /// <summary><b>قناةُ مجلّد: الحزمةُ بجوار البيان</b> — لا مجلّدَ يُمرَّر بيد.</summary>
    [Fact]
    public async Task AFolderChannel_StagesThePackageBesideItsManifest()
    {
        var package = await PackageAsync(8192);
        var channel = Path.Combine(_root, "channel");
        var manifest = await UpdatePublisher.PublishAsync(package, "999.0.0", new DateOnly(2026, 10, 17), null,
            _updateKey.ExportPkcs8PrivateKeyPem(), channel);

        var staged = await new UpdateCheckService().StageFromChannelAsync(manifest, Path.Combine(_root, "staging"));

        Assert.Equal(await File.ReadAllBytesAsync(package), await File.ReadAllBytesAsync(staged));
    }

    /// <summary>
    /// <b>القياسُ الحاكم: قناةُ https تُنزِّل حزمتَها من جوار البيان وتُقاس ببصمته.</b>
    /// </summary>
    [Fact]
    public async Task AnHttpsChannel_DownloadsThePackage_AndChecksItsHash()
    {
        var bytes = RandomNumberGenerator.GetBytes(5000);
        var handler = new ChannelHandler(Signed(Manifest("999.0.0", bytes)), bytes);

        var staged = await Service(handler).StageFromChannelAsync(Endpoint, Path.Combine(_root, "staging"));

        Assert.Equal(bytes, await File.ReadAllBytesAsync(staged));
        Assert.Equal("https://updates.example.test/brqosh/BRQOSH-Setup.msi", handler.PackageRequest?.ToString());
    }

    /// <summary><b>وحزمةٌ بُدِّلت عند الخادم تُرفض ولا يبقى منها شيء.</b></summary>
    [Fact]
    public async Task AnHttpsPackageThatFailsItsHash_IsRefused_AndNotLeftBehind()
    {
        var signedFor = RandomNumberGenerator.GetBytes(5000);
        var served = RandomNumberGenerator.GetBytes(5000);
        var staging = Path.Combine(_root, "staging");

        await Assert.ThrowsAsync<AccountingException>(() =>
            Service(new ChannelHandler(Signed(Manifest("999.0.0", signedFor)), served))
                .StageFromChannelAsync(Endpoint, staging));

        Assert.Empty(Directory.Exists(staging) ? Directory.GetFiles(staging) : []);
    }

    /// <summary>
    /// <b>وخادمٌ يبثّ أكثرَ من الحجم الموقَّع يُقطع عند الحدّ</b> — لا يُملأ القرصُ قبل قياس البصمة.
    /// </summary>
    [Fact]
    public async Task AnHttpsPackageLargerThanSigned_IsCutOff()
    {
        var signedFor = RandomNumberGenerator.GetBytes(1000);
        var served = RandomNumberGenerator.GetBytes(1_000_000);

        var ex = await Assert.ThrowsAsync<AccountingException>(() =>
            Service(new ChannelHandler(Signed(Manifest("999.0.0", signedFor)), served))
                .StageFromChannelAsync(Endpoint, Path.Combine(_root, "staging")));

        Assert.Contains("الحجم", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>ولا تُهيَّأ حزمةٌ أقدمُ من المُثبَّت</b> — توقيعُها صحيحٌ لأنّه وُقّع يومَ صدورها، وهي إعادة.
    /// </summary>
    [Fact]
    public async Task AnOlderSignedPackage_IsNotStaged()
    {
        var bytes = RandomNumberGenerator.GetBytes(100);

        await Assert.ThrowsAsync<AccountingException>(() =>
            Service(new ChannelHandler(Signed(Manifest("0.0.1", bytes)), bytes))
                .StageFromChannelAsync(Endpoint, Path.Combine(_root, "staging")));
    }

    /// <summary><b>واسمُ الحزمة اسمُ ملفٍّ لا مسار</b> — فلا يُكتب خارجَ مجلّد التهيئة.</summary>
    [Fact]
    public async Task APackageNameWithAPath_IsRefused()
    {
        var bytes = RandomNumberGenerator.GetBytes(100);
        var manifest = Manifest("999.0.0", bytes) with { PackageFileName = "..\\BRQOSH-Setup.msi" };

        await Assert.ThrowsAsync<AccountingException>(() =>
            Service(new ChannelHandler(Signed(manifest), bytes))
                .StageFromChannelAsync(Endpoint, Path.Combine(_root, "staging")));

        Assert.False(File.Exists(Path.Combine(_root, "BRQOSH-Setup.msi")));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<string> PackageAsync(int size)
    {
        var path = Path.Combine(_root, "BRQOSH-Setup.msi");
        await File.WriteAllBytesAsync(path, RandomNumberGenerator.GetBytes(size));
        return path;
    }

    private static UpdateManifest Manifest(string version, byte[] package) =>
        new(version, "2026-10-17", "BRQOSH-Setup.msi",
            Convert.ToHexString(SHA256.HashData(package)), package.Length, "إصلاحات");

    private string Signed(UpdateManifest manifest) =>
        UpdateCheckService.CreateSignedJson(manifest, _updateKey.ExportPkcs8PrivateKeyPem());

    private static UpdateCheckService Service(HttpMessageHandler handler) => new() { Transport = handler };

    /// <summary>خادمُ قناةٍ مُصطنَع: البيانُ على عنوانه والحزمةُ على ما سواه — <b>ولا شبكة</b>.</summary>
    private sealed class ChannelHandler(string manifestJson, byte[] package) : HttpMessageHandler
    {
        public Uri? PackageRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/manifest.json", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(manifestJson),
                });

            PackageRequest = request.RequestUri;
            var content = new ByteArrayContent(package);
            content.Headers.ContentLength = null;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
}
