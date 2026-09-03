using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Licensing;

namespace Brqosh.Infrastructure.Operations;

/// <summary>بيانُ إصدارٍ مُوقَّع — <b>وما لا يحمل توقيعاً لا يُقرأ</b>.</summary>
public record UpdateManifest(
    string Version,
    string ReleaseDate,
    string PackageFileName,
    string PackageSha256,
    long PackageSizeBytes,
    string? Notes);

/// <summary>البيانُ وتوقيعُه — بنيةُ الملفّ كما يُنشَر.</summary>
public record SignedUpdateManifest(UpdateManifest Manifest, string SignatureBase64);

/// <summary>حالةُ الفحص — <b>ولكلٍّ معنىً لا يُخلط بغيره</b>.</summary>
public enum UpdateStatus
{
    /// <summary>لا بيانَ في المصدر — قناةٌ لم تُهيَّأ بعد.</summary>
    NoChannel = 0,

    /// <summary>المُثبَّتُ أحدثُ أو مساوٍ.</summary>
    UpToDate = 1,

    /// <summary>يوجد أحدث — ولم يُنزَّل بعد.</summary>
    UpdateAvailable = 2,

    /// <summary><b>البيانُ غيرُ موقَّعٍ أو مُلاعَبٌ به — ويُرفض</b>.</summary>
    Untrusted = 3,
}

/// <summary>حصيلةُ الفحص.</summary>
public record UpdateCheckResult(
    UpdateStatus Status,
    string InstalledVersion,
    string? AvailableVersion,
    string? Notes,
    string? Failure);

/// <summary>
/// (CEEF-231 · ط5-٥) <b>التحديثُ عن بُعد — وقناةُ تحديثٍ بلا توقيعٍ بابٌ خلفيّ.</b>
///
/// <para><b>ما كان:</b> الترخيصُ دون اتّصالٍ مبنيٌّ ومختبَر (<c>RsaLicenseValidator</c>)،
/// <b>ولا سبيلَ لمعرفة أنّ إصداراً أحدثَ صدر</b>. فمنشأةٌ تُشغّل نسخةً فيها عطلٌ أُصلح
/// منذ شهور <b>ولا شيءَ يخبرها</b>، والبائعُ يتّصل بعملائه واحداً واحداً.</para>
///
/// <para><b>والقرارُ الحاكم — التوقيعُ شرطٌ لا تحذير.</b> قناةُ تحديثٍ تقبل بياناً غيرَ
/// موقَّعٍ <b>تُسلّم كلَّ حاسوبِ عميلٍ لمن يملك خادمَ التحديث أو الشبكةَ بينهما</b>: يُبدَّل
/// البيانُ فتُنزَّل حزمةٌ أخرى وتُنصَّب على نظامٍ فيه دفاترُ منشأةٍ كاملة. <b>فالبيانُ
/// المرفوضُ يُرفض باسمه</b> — ولا يُقال «تعذّر الفحص»، لأنّ الفرقَ بين انقطاعِ شبكةٍ
/// وتلاعبٍ فرقٌ يجب أن يراه المالك.</para>
///
/// <para><b>والبصمةُ في البيان لا عند الخادم.</b> فخادمٌ مخترَقٌ يخدم أيَّ بايتاتٍ شاء،
/// <b>والتوقيعُ يغطّي البصمةَ</b> — فتُقاس الحزمةُ المُنزَّلة ببصمةٍ وقّعها الناشرُ لا
/// بما يقوله الخادم.</para>
///
/// <para><b>ولا تُنفَّذ الحزمةُ ولا تُنصَّب هنا.</b> تُنزَّل وتُتحقَّق و<b>تُهيَّأ</b>،
/// والمالكُ يُشغّل المثبِّت. <b>وتنصيبٌ آليٌّ من مصدرٍ شبكيّ هو بعينه ما يُبنى التوقيعُ
/// لمنع أثره</b> — ولو صحّ التوقيعُ، فإغلاقُ تطبيقٍ يُمسك دفاترَ منشأةٍ واستبدالُه بلا
/// إذنٍ قرارٌ ليس للبرنامج.</para>
///
/// <para><b>والتراجعُ يُرفض</b>: بيانٌ بإصدارٍ أقدمَ من المُثبَّت <b>هجومُ إعادةٍ</b> —
/// يُعاد به إصدارٌ فيه ثغرةٌ أُصلحت، والتوقيعُ عليه صحيحٌ لأنّه وُقّع يومَ صدوره.</para>
/// </summary>
public sealed class UpdateCheckService
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    /// <summary>
    /// الإصدارُ المُثبَّت — <b>من التجميعة نفسِها لا من ملفّ إعدادات</b>.
    /// </summary>
    /// <remarks>
    /// <b>ورقمٌ في إعداداتٍ يُحرَّر بيدٍ</b>، فيُخبر القناةَ أنّه قديمٌ فيُنزِّل، أو أنّه
    /// حديثٌ فلا يُنزِّل ما يلزمه. <b>والتجميعةُ لا تكذب على نفسها.</b>
    /// </remarks>
    public static string InstalledVersion =>
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3)
        ?? typeof(UpdateCheckService).Assembly.GetName().Version?.ToString(3)
        ?? "0.0.0";

    /// <summary>
    /// يقرأ بيانَ الإصدار من مصدرٍ محلّيٍّ أو شبكيّ (مسار ملفّ أو UNC).
    /// </summary>
    /// <remarks>
    /// <b>والمصدرُ مسارُ ملفٍّ عمداً لا عنوانَ ويب</b>: منشآتٌ كثيرةٌ في هذا السوق تعمل
    /// على شبكةٍ مغلقة، <b>ومشاركةُ شبكةٍ تصلها تحديثُ الوكيل</b>. والتوقيعُ هو ما يحمي
    /// لا القناة — <b>فمصدرٌ يُبدَّل ملفُّه لا يُنتج تحديثاً مقبولاً</b>.
    /// </remarks>
    public async Task<UpdateCheckResult> CheckAsync(string manifestPath, CancellationToken ct = default)
    {
        var installed = InstalledVersion;

        if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
            return new UpdateCheckResult(UpdateStatus.NoChannel, installed, null, null,
                "لا بيانَ إصدارٍ في المصدر — قناةُ التحديث لم تُهيَّأ.");

        SignedUpdateManifest? signed;
        try
        {
            signed = JsonSerializer.Deserialize<SignedUpdateManifest>(
                await File.ReadAllTextAsync(manifestPath, ct));
        }
        catch (JsonException ex)
        {
            // ‏**وبيانٌ لا يُقرأ يُرفض بوصفه غيرَ موثوق** — لا «تعذّر الفحص». فملفٌّ
            // ‏مبتورٌ أو مُبدَّلٌ يظهر هكذا بعينه.
            return new UpdateCheckResult(UpdateStatus.Untrusted, installed, null, null,
                $"بيانُ الإصدار غيرُ مقروء — {ex.Message}");
        }

        if (signed is null || signed.Manifest is null)
            return new UpdateCheckResult(UpdateStatus.Untrusted, installed, null, null,
                "بيانُ الإصدار فارغٌ أو ناقصُ البنية.");

        if (!Verify(signed))
            return new UpdateCheckResult(UpdateStatus.Untrusted, installed, null, null,
                "توقيعُ بيان الإصدار غيرُ صالح — والبيانُ إمّا غيرُ موقَّعٍ وإمّا مُلاعَبٌ به. "
                + "ولا يُنزَّل تحديثٌ لا يُعرف ناشرُه.");

        if (!System.Version.TryParse(signed.Manifest.Version, out var available)
            || !System.Version.TryParse(installed, out var current))
            return new UpdateCheckResult(UpdateStatus.Untrusted, installed, signed.Manifest.Version, null,
                $"رقمُ إصدارٍ غيرُ صالح — «{signed.Manifest.Version}».");

        // ‏**والتراجعُ يُرفض**: بيانٌ بإصدارٍ أقدمَ هجومُ إعادةٍ — يُعاد به إصدارٌ فيه
        // ‏ثغرةٌ أُصلحت، **وتوقيعُه صحيحٌ لأنّه وُقّع يومَ صدوره**.
        if (available <= current)
            return new UpdateCheckResult(UpdateStatus.UpToDate, installed,
                signed.Manifest.Version, signed.Manifest.Notes, null);

        return new UpdateCheckResult(UpdateStatus.UpdateAvailable, installed,
            signed.Manifest.Version, signed.Manifest.Notes, null);
    }

    /// <summary>
    /// يُنزّل الحزمةَ ويتحقّق من بصمتها — <b>ولا يُنصّبها</b>.
    /// </summary>
    /// <remarks>
    /// <b>والبصمةُ من البيان الموقَّع لا من الخادم</b>: خادمٌ مخترَقٌ يخدم أيَّ بايتاتٍ
    /// شاء، والتوقيعُ يغطّي البصمة. <b>وحجمٌ مطابقٌ لا يكفي</b> — القرصُ التالف يكتب
    /// أصفاراً بالطول نفسِه.
    /// </remarks>
    public async Task<string> StageAsync(string manifestPath, string packageSourceDirectory,
        string stagingDirectory, CancellationToken ct = default)
    {
        var signed = JsonSerializer.Deserialize<SignedUpdateManifest>(
            await File.ReadAllTextAsync(manifestPath, ct))
            ?? throw new AccountingException("بيانُ الإصدار فارغ.");

        if (!Verify(signed))
            throw new AccountingException(
                "توقيعُ بيان الإصدار غيرُ صالح — ولا تُنزَّل حزمةٌ لا يُعرف ناشرُها.");

        var source = Path.Combine(packageSourceDirectory, signed.Manifest.PackageFileName);
        if (!File.Exists(source))
            throw new AccountingException($"حزمةُ التحديث غير موجودة: {signed.Manifest.PackageFileName}");

        Directory.CreateDirectory(stagingDirectory);
        var target = Path.Combine(stagingDirectory, signed.Manifest.PackageFileName);

        // ‏**ويُكتب إلى اسمٍ مؤقّتٍ ثمّ يُنقل** — فنقلٌ يُقطع لا يترك حزمةً ناقصةً
        // ‏بالاسم الصحيح، **يُشغّلها المالكُ فتُتلف تثبيتَه**.
        var staging = target + ".partial";
        if (File.Exists(staging)) File.Delete(staging);

        await using (var input = File.OpenRead(source))
        await using (var output = File.Create(staging))
            await input.CopyToAsync(output, ct);

        var actual = await HashAsync(staging, ct);
        if (!string.Equals(actual, signed.Manifest.PackageSha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(staging);
            throw new AccountingException(
                "بصمةُ الحزمة تخالف البيانَ الموقَّع — والحزمةُ إمّا وصلت ناقصةً وإمّا بُدِّلت.");
        }

        if (File.Exists(target)) File.Delete(target);
        File.Move(staging, target);

        return target;
    }

    /// <summary>
    /// يُوقّع بياناً — <b>للناشر لا للعميل</b>.
    /// </summary>
    /// <remarks>
    /// <b>وهو هنا لأنّ التوقيعَ والتحقّق يجب أن يقرآ الحقولَ نفسَها بالترتيب نفسِه</b>.
    /// وفصلُهما في مشروعين يُنتج توقيعاً صحيحاً يُرفض دائماً، <b>ولا يُكتشف إلا عند
    /// أوّل إصدار</b>.
    /// </remarks>
    public static string Sign(UpdateManifest manifest, string privateKeyPem)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);

        return Convert.ToBase64String(rsa.SignData(
            Canonical(manifest), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }

    public static string CreateSignedJson(UpdateManifest manifest, string privateKeyPem) =>
        JsonSerializer.Serialize(
            new SignedUpdateManifest(manifest, Sign(manifest, privateKeyPem)), Json);

    private static bool Verify(SignedUpdateManifest signed)
    {
        var publicKeyPem = EmbeddedLicenseKeys.ResolvePublicKeyPem();

        // ‏**ولا مفتاحَ يعني لا ثقة** — لا «تخطّي التحقّق». وقناةٌ تُصدّق بلا مفتاحٍ
        // ‏تُصدّق كلَّ شيء.
        if (string.IsNullOrWhiteSpace(publicKeyPem)) return false;
        if (string.IsNullOrWhiteSpace(signed.SignatureBase64)) return false;

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);

            return rsa.VerifyData(Canonical(signed.Manifest),
                Convert.FromBase64String(signed.SignatureBase64),
                HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// الصورةُ القانونية للبيان — <b>وهي ما يُوقَّع ويُتحقَّق منه</b>.
    /// </summary>
    /// <remarks>
    /// <b>ولا تُوقَّع صورةُ JSON</b>: تنسيقُها يتغيّر بمسافةٍ أو ترتيبِ حقل، <b>فيُبطل
    /// توقيعاً صحيحاً</b>. والصورةُ هنا حقولٌ بترتيبٍ ثابتٍ وفاصلٍ لا يرد في قيمها.
    /// </remarks>
    private static byte[] Canonical(UpdateManifest m) =>
        Encoding.UTF8.GetBytes(string.Join('\u001F',
            m.Version, m.ReleaseDate, m.PackageFileName,
            m.PackageSha256, m.PackageSizeBytes.ToString()));

    private static async Task<string> HashAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, ct));
    }
}
