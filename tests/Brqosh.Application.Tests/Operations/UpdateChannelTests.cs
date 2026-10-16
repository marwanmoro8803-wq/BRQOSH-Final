using System.Security.Cryptography;
using System.Text.Json;
using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Operations;

namespace Brqosh.Application.Tests.Operations;

/// <summary>
/// (CEEF-231 · ط5-٥) <b>قناةُ التحديث — وقناةٌ بلا توقيعٍ بابٌ خلفيّ.</b>
///
/// <para><b>ما كان:</b> الترخيصُ دون اتّصالٍ مبنيٌّ ومختبَر، <b>ولا سبيلَ لمعرفة أنّ
/// إصداراً أحدثَ صدر</b>. فمنشأةٌ تُشغّل نسخةً فيها عطلٌ أُصلح منذ شهورٍ <b>ولا شيءَ
/// يخبرها</b>.</para>
///
/// <para><b>والقرارُ الحاكم — التوقيعُ شرطٌ لا تحذير.</b> قناةٌ تقبل بياناً غيرَ موقَّعٍ
/// <b>تُسلّم كلَّ حاسوبِ عميلٍ لمن يملك خادمَ التحديث أو الشبكةَ بينهما</b>: يُبدَّل
/// البيانُ فتُنزَّل حزمةٌ أخرى وتُنصَّب على نظامٍ فيه دفاترُ منشأةٍ كاملة.</para>
///
/// <para><b>وهويّةُ ح-٨:</b> ما لا يُثبت توقيعُه لا يُقبَل — <b>ولا يُقال «تعذّر الفحص»</b>،
/// لأنّ الفرقَ بين انقطاعِ شبكةٍ وتلاعبٍ فرقٌ يجب أن يراه المالك.</para>
/// </summary>
public class UpdateChannelTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "brqosh-update-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly RSA _publisher = RSA.Create(2048);

    public UpdateChannelTests()
    {
        Directory.CreateDirectory(_root);

        // ‏مفتاحُ الناشر — يُستبدَل في الاختبارات كما يفعل الترخيص.
        UpdateCheckService.PublicKeyPemOverride = _publisher.ExportSubjectPublicKeyInfoPem();
    }

    public void Dispose()
    {
        UpdateCheckService.PublicKeyPemOverride = null;
        _publisher.Dispose();
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    // ── ح-٨: التوقيعُ شرط ───────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ الحاكم: بيانٌ موقَّعٌ بإصدارٍ أحدثَ يُقرأ «يوجد تحديث».</b>
    /// </summary>
    [Fact]
    public async Task ASignedNewerManifest_ReportsAnUpdate()
    {
        var path = WriteManifest(Version("999.0.0"), signed: true);

        var result = await new UpdateCheckService().CheckAsync(path);

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("999.0.0", result.AvailableVersion);
    }

    /// <summary>
    /// <b>وبيانٌ غيرُ موقَّعٍ يُرفض بوصفه غيرَ موثوق — لا «تعذّر الفحص».</b>
    ///
    /// <para><b>والفرقُ بين انقطاعِ شبكةٍ وتلاعبٍ فرقٌ يجب أن يراه المالك</b>: الأوّلُ
    /// يُعاد، <b>والثاني يُبلَّغ عنه</b>.</para>
    /// </summary>
    [Fact]
    public async Task AnUnsignedManifest_IsRefusedAsUntrusted()
    {
        var path = WriteManifest(Version("999.0.0"), signed: false);

        var result = await new UpdateCheckService().CheckAsync(path);

        Assert.Equal(UpdateStatus.Untrusted, result.Status);
        Assert.Contains("توقيع", result.Failure!, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>وبيانٌ عُبث به بعد توقيعه يُرفض.</b>
    ///
    /// <para><b>وهذا هو الهجومُ الحقيقيّ</b>: يُؤخذ بيانٌ صحيحٌ وتُبدَّل بصمةُ الحزمة فيه،
    /// <b>فتُنزَّل حزمةٌ أخرى ويُقال إنّها الصحيحة</b>.</para>
    /// </summary>
    [Fact]
    public async Task ATamperedManifest_IsRefused()
    {
        var manifest = Version("999.0.0");
        var json = UpdateCheckService.CreateSignedJson(manifest, _publisher.ExportRSAPrivateKeyPem());

        // ‏تُبدَّل البصمةُ ويبقى التوقيعُ كما هو.
        var tampered = json.Replace(manifest.PackageSha256, new string('A', 64));

        var path = Path.Combine(_root, "tampered.json");
        await File.WriteAllTextAsync(path, tampered);

        var result = await new UpdateCheckService().CheckAsync(path);

        Assert.Equal(UpdateStatus.Untrusted, result.Status);
    }

    /// <summary>
    /// <b>وبيانٌ وقّعه غيرُ الناشر يُرفض.</b>
    /// </summary>
    [Fact]
    public async Task AManifestSignedByAnotherKey_IsRefused()
    {
        using var impostor = RSA.Create(2048);

        var json = UpdateCheckService.CreateSignedJson(
            Version("999.0.0"), impostor.ExportRSAPrivateKeyPem());

        var path = Path.Combine(_root, "impostor.json");
        await File.WriteAllTextAsync(path, json);

        var result = await new UpdateCheckService().CheckAsync(path);

        Assert.Equal(UpdateStatus.Untrusted, result.Status);
    }

    /// <summary>
    /// <b>ولا مفتاحَ يعني لا ثقة — لا «تخطّي التحقّق».</b>
    ///
    /// <para><b>وقناةٌ تُصدّق بلا مفتاحٍ تُصدّق كلَّ شيء.</b></para>
    /// </summary>
    [Fact]
    public async Task WithoutAPublisherKey_NothingIsTrusted()
    {
        var path = WriteManifest(Version("999.0.0"), signed: true);
        UpdateCheckService.PublicKeyPemOverride = null;

        var result = await new UpdateCheckService().CheckAsync(path);

        Assert.Equal(UpdateStatus.Untrusted, result.Status);
    }

    // ── التراجعُ يُرفض ──────────────────────────────────────────────────────

    /// <summary>
    /// <b>وبيانٌ بإصدارٍ أقدمَ لا يُعرض تحديثاً — وهو هجومُ إعادة.</b>
    ///
    /// <para><b>وتوقيعُه صحيحٌ لأنّه وُقّع يومَ صدوره</b> — فيُعاد به إصدارٌ فيه ثغرةٌ
    /// أُصلحت.</para>
    /// </summary>
    [Fact]
    public async Task AnOlderSignedManifest_IsNotAnUpdate()
    {
        var path = WriteManifest(Version("0.0.1"), signed: true);

        var result = await new UpdateCheckService().CheckAsync(path);

        Assert.Equal(UpdateStatus.UpToDate, result.Status);
    }

    /// <summary>
    /// <b>ولا قناةَ يُقال «لم تُهيَّأ» — لا «غير موثوق».</b>
    ///
    /// <para><b>وخلطُ الحالتين يُدرَّب المستخدمُ على تجاهل الإنذار</b>: يراه كلَّ يومٍ
    /// على قناةٍ لم تُنشأ، فلا يقرؤه يومَ يقع تلاعبٌ حقيقيّ.</para>
    /// </summary>
    [Fact]
    public async Task AMissingManifest_IsNoChannel_NotUntrusted()
    {
        var result = await new UpdateCheckService()
            .CheckAsync(Path.Combine(_root, "absent.json"));

        Assert.Equal(UpdateStatus.NoChannel, result.Status);
    }

    // ── التهيئة: الحزمةُ تُتحقَّق ببصمتها ──────────────────────────────────

    /// <summary>
    /// <b>والحزمةُ تُتحقَّق ببصمةٍ في البيان الموقَّع — لا بما يقوله الخادم.</b>
    /// </summary>
    [Fact]
    public async Task AStagedPackage_MatchesTheSignedHash()
    {
        var payload = new byte[2048];
        RandomNumberGenerator.Fill(payload);

        var packages = Path.Combine(_root, "packages");
        Directory.CreateDirectory(packages);
        await File.WriteAllBytesAsync(Path.Combine(packages, "BRQOSH-Setup.msi"), payload);

        var manifest = new UpdateManifest("999.0.0", "2026-09-03", "BRQOSH-Setup.msi",
            Convert.ToHexString(SHA256.HashData(payload)), payload.Length, null);

        var path = Path.Combine(_root, "ok.json");
        await File.WriteAllTextAsync(path,
            UpdateCheckService.CreateSignedJson(manifest, _publisher.ExportRSAPrivateKeyPem()));

        var staged = await new UpdateCheckService()
            .StageAsync(path, packages, Path.Combine(_root, "staging"));

        Assert.True(File.Exists(staged));
        Assert.Empty(Directory.GetFiles(Path.Combine(_root, "staging"), "*.partial"));
    }

    /// <summary>
    /// <b>وحزمةٌ لا تُطابق بصمتَها تُرفض ولا تبقى على القرص.</b>
    ///
    /// <para><b>وخادمٌ مخترَقٌ يخدم أيَّ بايتاتٍ شاء</b> — والتوقيعُ يغطّي البصمة.</para>
    /// </summary>
    [Fact]
    public async Task APackageThatFailsItsHash_IsRefusedAndNotLeftBehind()
    {
        var packages = Path.Combine(_root, "bad-packages");
        Directory.CreateDirectory(packages);
        await File.WriteAllBytesAsync(Path.Combine(packages, "BRQOSH-Setup.msi"), new byte[2048]);

        // ‏بصمةٌ لبايتاتٍ أخرى — والحجمُ نفسُه عمداً.
        var manifest = new UpdateManifest("999.0.0", "2026-09-03", "BRQOSH-Setup.msi",
            Convert.ToHexString(SHA256.HashData(new byte[] { 1, 2, 3 })), 2048, null);

        var path = Path.Combine(_root, "bad.json");
        await File.WriteAllTextAsync(path,
            UpdateCheckService.CreateSignedJson(manifest, _publisher.ExportRSAPrivateKeyPem()));

        var staging = Path.Combine(_root, "bad-staging");

        var error = await Assert.ThrowsAsync<AccountingException>(() =>
            new UpdateCheckService().StageAsync(path, packages, staging));

        Assert.Contains("بصمة", error.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(staging));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private static UpdateManifest Version(string version) =>
        new(version, "2026-09-03", "BRQOSH-Setup.msi", new string('B', 64), 1024, "إصلاحات");

    private string WriteManifest(UpdateManifest manifest, bool signed)
    {
        var json = signed
            ? UpdateCheckService.CreateSignedJson(manifest, _publisher.ExportRSAPrivateKeyPem())
            : JsonSerializer.Serialize(new SignedUpdateManifest(manifest, ""));

        var path = Path.Combine(_root, $"manifest-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }
}
