using System.Security.Cryptography;
using System.Text.Json;
using Brqosh.Domain.Accounting;

namespace Brqosh.Infrastructure.Operations;

/// <summary>
/// (ح-١٤٠ · installer-deployment-ops#12) <b>الناشرُ — نصفُ القناة الذي لم يكن موجوداً.</b>
///
/// <para><b>ما كان:</b> <see cref="UpdateCheckService"/> يقرأ بياناً موقَّعاً ويتحقّق منه
/// ويُهيّئ حزمته، <b>ولا شيءَ في المستودع يُنتج ذلك البيان</b>: لا أداةَ تحسب بصمةَ الحزمة
/// وحجمَها وتوقّعهما. فأوّلُ إصدارٍ كان سيُنشر ببيانٍ مكتوبٍ بيدٍ — وبصمةٌ تُنسخ خطأً
/// تُسقط التهيئةَ عند كلّ عميل، <b>وتوقيعٌ بمفتاحٍ لا يثق به البناءُ يُرفض عند كلّ عميلٍ
/// ولا يعرف الناشرُ</b>.</para>
///
/// <para><b>وما صار:</b> النشرُ يحسب البصمةَ والحجمَ من الحزمة نفسِها، <b>ويرفض مفتاحاً لا
/// يطابق مرساةَ التحديث المُضمَّنة</b> قبل أن يكتب شيئاً، ثمّ يقرأ ما كتب ويتحقّق منه بما
/// يتحقّق به العميل. <b>فبيانٌ يخرج من هنا يُقبَل عند العميل، أو لا يخرج.</b></para>
/// </summary>
public static class UpdatePublisher
{
    /// <summary>اسمُ ملفّ البيان في مجلّد القناة — <b>وهو ما يُعطى للعميل مساراً أو عنواناً</b>.</summary>
    public const string ManifestFileName = "manifest.json";

    /// <summary>اسمُ ملفّ المفتاح الخاصّ — <b>غيرُ اسم مفتاح الترخيص عمداً</b>.</summary>
    public const string PrivateKeyFileName = "update-private.pem";

    /// <summary>
    /// يُولّد زوجَ مفاتيح التحديث — <b>ولا يستبدل ملفّاً قائماً أبداً، ولا يكتب في جذر البيانات</b>.
    /// </summary>
    /// <remarks>
    /// على سُنّة <c>tools/LicenseIssuer gen-keys</c>: <b>مفتاحُ توقيعٍ يُتلَف لا يُستعاد</b>،
    /// وكلُّ عميلٍ يثق بنصفه العامّ يحتاج بناءً جديداً ليثق بغيره. ولا <c>--force</c>.
    /// </remarks>
    public static UpdateKeyPair GenerateKeys(string directory, string dataRoot)
    {
        var resolved = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        if (string.Equals(resolved, Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataRoot)),
                StringComparison.OrdinalIgnoreCase))
            throw new AccountingException(
                $"مرفوض: {resolved} هو جذرُ بيانات المنتج — ومفتاحُ توقيعٍ لا يُحفظ حيث يعمل المنتج.");

        using var rsa = RSA.Create(2048);
        var fingerprint = EmbeddedUpdateKeys.Fingerprint(rsa.ExportSubjectPublicKeyInfoPem());
        var privatePath = Path.Combine(resolved, PrivateKeyFileName);
        var publicPath = Path.Combine(resolved, $"update-public-{fingerprint[..16]}.pem");

        foreach (var existing in new[] { privatePath, publicPath })
            if (File.Exists(existing))
                throw new AccountingException(
                    $"مرفوض: {existing} موجودٌ سلفاً — ولن يُستبدل. مفتاحُ توقيعٍ يُتلَف لا يُستعاد؛ "
                    + "انقل القديمَ بيدك أو مرّر مجلّداً آخر.");

        Directory.CreateDirectory(resolved);
        File.WriteAllText(privatePath, rsa.ExportPkcs8PrivateKeyPem());
        File.WriteAllText(publicPath, rsa.ExportSubjectPublicKeyInfoPem());

        // ‏قراءةٌ بعد الكتابة: كتابةٌ ناقصةٌ تُكتشف الآن لا يومَ أوّل إصدار.
        using var reread = RSA.Create();
        reread.ImportFromPem(File.ReadAllText(privatePath));
        if (EmbeddedUpdateKeys.Fingerprint(reread.ExportSubjectPublicKeyInfoPem()) != fingerprint)
            throw new AccountingException($"الزوجُ كُتب ولا يُقرأ كما كُتب — احذف {privatePath} وأعد المحاولة.");

        return new UpdateKeyPair(privatePath, publicPath, fingerprint);
    }

    /// <summary>
    /// ينشر حزمةً: يحسب بصمتَها وحجمَها، يوقّع البيان، ينسخ الحزمةَ إلى مجلّد القناة
    /// ويكتب <see cref="ManifestFileName"/> — <b>ثمّ يتحقّق منه بما يتحقّق به العميل</b>.
    /// </summary>
    public static async Task<string> PublishAsync(
        string packagePath,
        string version,
        DateOnly releaseDate,
        string? notes,
        string privateKeyPem,
        string channelDirectory,
        CancellationToken ct = default)
    {
        if (!System.Version.TryParse(version, out var parsed) || parsed.Build < 0)
            throw new AccountingException($"رقمُ إصدارٍ غيرُ صالح — «{version}» (الصيغة: 1.2.3).");

        if (!File.Exists(packagePath))
            throw new AccountingException($"الحزمةُ غير موجودة: {packagePath}");

        // ‏**والمفتاحُ يُقاس قبل أن يُكتب شيء**: بيانٌ بمفتاحٍ آخرَ يُرفض عند كلّ عميل،
        // ‏ويُنشر صامتاً فلا يعرف الناشرُ إلا من شكاوى العملاء.
        string signingFingerprint;
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(privateKeyPem);
            signingFingerprint = EmbeddedUpdateKeys.Fingerprint(rsa.ExportSubjectPublicKeyInfoPem());
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            throw new AccountingException($"المفتاحُ الخاصّ لا يُقرأ — {ex.Message}");
        }

        var anchorFingerprint = EmbeddedUpdateKeys.Fingerprint(EmbeddedUpdateKeys.ResolvePublicKeyPem());
        if (signingFingerprint != anchorFingerprint)
            throw new AccountingException(
                $"مرفوض: المفتاحُ الموقِّع ({signingFingerprint[..16]}) لا يطابق مرساةَ التحديث في هذا البناء "
                + $"({anchorFingerprint[..16]}) — وكلُّ عميلٍ سيرفض البيان. "
                + "ومفتاحُ الترخيص ليس مفتاحَ التحديث.");

        Directory.CreateDirectory(channelDirectory);
        var fileName = Path.GetFileName(packagePath);
        var published = Path.Combine(channelDirectory, fileName);
        if (!string.Equals(Path.GetFullPath(published), Path.GetFullPath(packagePath),
                StringComparison.OrdinalIgnoreCase))
            File.Copy(packagePath, published, overwrite: true);

        string sha;
        await using (var stream = File.OpenRead(published))
            sha = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct));

        var manifest = new UpdateManifest(
            parsed.ToString(3),
            releaseDate.ToString("yyyy-MM-dd"),
            fileName,
            sha,
            new FileInfo(published).Length,
            string.IsNullOrWhiteSpace(notes) ? null : notes.Trim());

        // ‏**ويُكتب إلى اسمٍ مؤقّتٍ ثمّ يُنقل** — فعميلٌ يقرأ في لحظة الكتابة لا يرى نصفَ بيان.
        var manifestPath = Path.Combine(channelDirectory, ManifestFileName);
        var partial = manifestPath + ".partial";
        await File.WriteAllTextAsync(partial, UpdateCheckService.CreateSignedJson(manifest, privateKeyPem), ct);
        File.Move(partial, manifestPath, overwrite: true);

        var reread = JsonSerializer.Deserialize<SignedUpdateManifest>(await File.ReadAllTextAsync(manifestPath, ct));
        if (reread is null || !UpdateCheckService.Verify(reread))
            throw new AccountingException("البيانُ كُتب ولا يجتاز تحقّقَ العميل — لا يُنشر.");

        return manifestPath;
    }
}

/// <summary>مسارا الزوج المولَّد وبصمةُ نصفه العامّ.</summary>
public sealed record UpdateKeyPair(string PrivateKeyPath, string PublicKeyPath, string Fingerprint);
