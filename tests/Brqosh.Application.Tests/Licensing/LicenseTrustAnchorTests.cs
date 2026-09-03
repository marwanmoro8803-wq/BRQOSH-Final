using System.Security.Cryptography;
using Brqosh.Application.Setup;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Infrastructure.Licensing;

namespace Brqosh.Application.Tests.Licensing;

/// <summary>
/// (CEEF-236 · ت-١/ت-٢) <b>مرساةُ الثقة: موجودةٌ على جهازٍ نظيف، ولا تُستبدل في الإنتاج.</b>
///
/// <para><b>لماذا لم تكفِ <c>LicensingTests</c>:</b> كلُّها تُثبّت
/// <c>PublicKeyPemOverride</c> عبر <c>RsaTestKeys.UseForValidation()</c> — فتقيس التوقيعَ
/// وتترك <b>من أين يأتي المفتاح</b> بلا قياس. وهناك كان العطل: لا مفتاحَ يُشحَن، فيُرفض ترخيصُ
/// المورِّد الصحيح على كلّ جهاز عميل. هذه الحزمةُ تقيس القرارَ نفسَه (<c>Resolve</c>) بكلّ
/// تركيباته — بلا مسّ جذر البيانات، لأنّ تغييرَ <c>BRQOSH_DATA_ROOT</c> في عمليةٍ تُشغّل
/// اختباراتٍ أخرى بالتوازي يُلوّثها.</para>
/// </summary>
[Collection("SequentialEnv")]
public class LicenseTrustAnchorTests : IDisposable
{
    private readonly string? _priorEnv = Environment.GetEnvironmentVariable(EmbeddedLicenseKeys.EnvironmentVariable);

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(EmbeddedLicenseKeys.EnvironmentVariable, _priorEnv);
        RsaLicenseValidator.PublicKeyPemOverride = null;
        AppEnvironmentConfig.ResetForTests();
    }

    private static (string Public, string Private) FreshKeyPair()
    {
        using var rsa = RSA.Create(2048);
        return (rsa.ExportSubjectPublicKeyInfoPem(), rsa.ExportPkcs8PrivateKeyPem());
    }

    private static string SignWith(string privatePem, string company, string machineId, int days = 30) =>
        RsaLicenseValidator.CreateSignedLicenseJson(
            new LicensePayload(company, machineId, "Enterprise", DateTime.UtcNow.AddDays(days), ["all"]),
            privatePem);

    /// <summary>الثابتُ المُجمَّع مفتاحُ RSA حقيقيٌّ يُستورد — لا نصٌّ يبدو كمفتاح.</summary>
    [Fact]
    public void TheEmbeddedKey_IsARealRsaPublicKey()
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(EmbeddedLicenseKeys.PublicKeyPem);
        Assert.True(rsa.KeySize >= 2048, $"KeySize={rsa.KeySize}");
    }

    /// <summary>
    /// <b>القياسُ الذي كان غائباً:</b> جهازٌ بلا ملفٍّ ولا متغيّرٍ ولا تجاوزٍ — في الإنتاج —
    /// يملك مفتاحاً. كان يُعيد <c>null</c> فيُرفض كلُّ ترخيص.
    /// </summary>
    [Fact]
    public void ACleanProductionMachine_HasATrustAnchor_WithNoFileAndNoVariable()
    {
        var resolved = EmbeddedLicenseKeys.Resolve(isProduction: true, overridePem: null, envPem: null, filePem: null);
        Assert.Equal(EmbeddedLicenseKeys.PublicKeyPem, resolved);
    }

    /// <summary>وفي الإنتاج لا يُخفَض التشدّد: لا الملفُّ ولا المتغيّرُ يُبدّلان المفتاح.</summary>
    [Fact]
    public void Production_IgnoresTheEnvironmentVariable_AndTheDataRootFile()
    {
        var (attacker, _) = FreshKeyPair();

        Assert.Equal(EmbeddedLicenseKeys.PublicKeyPem, EmbeddedLicenseKeys.Resolve(true, null, attacker, null));
        Assert.Equal(EmbeddedLicenseKeys.PublicKeyPem, EmbeddedLicenseKeys.Resolve(true, null, null, attacker));
        Assert.Equal(EmbeddedLicenseKeys.PublicKeyPem, EmbeddedLicenseKeys.Resolve(true, null, attacker, attacker));
    }

    /// <summary>وخارجَ الإنتاج يبقى للمطوّر مفتاحُه: المتغيّرُ أوّلاً ثمّ الملفُّ ثمّ المُضمَّن.</summary>
    [Fact]
    public void OutsideProduction_TheVariableThenTheFile_StillWin()
    {
        var (fromEnv, _) = FreshKeyPair();
        var (fromFile, _) = FreshKeyPair();

        Assert.Equal(fromEnv, EmbeddedLicenseKeys.Resolve(false, null, fromEnv, fromFile));
        Assert.Equal(fromFile, EmbeddedLicenseKeys.Resolve(false, null, null, fromFile));
        Assert.Equal(EmbeddedLicenseKeys.PublicKeyPem, EmbeddedLicenseKeys.Resolve(false, null, null, null));
    }

    /// <summary>
    /// <b>الهجومُ المقيس (ت-٢) من طرفه إلى طرفه:</b> مهاجمٌ يملك الجهازَ يضبط مفتاحَه في
    /// متغيّرِ بيئةٍ بنطاق المستخدم ويوقّع لنفسه ترخيصَ <c>Enterprise</c>. كان يُقبل. في
    /// الإنتاج يُرفض الآن <b>بسبب التوقيع</b> لا بسببٍ آخر.
    /// </summary>
    [Fact]
    public void ASelfSignedLicense_IsRejectedInProduction_EvenWithTheAttackersAnchorInTheEnvironment()
    {
        var (attackerPublic, attackerPrivate) = FreshKeyPair();
        Environment.SetEnvironmentVariable(EmbeddedLicenseKeys.EnvironmentVariable, attackerPublic);
        RsaLicenseValidator.PublicKeyPemOverride = null;
        AppEnvironmentConfig.ForceForTests(AppEnvironment.Production);

        var machine = LicenseService.MachineId();
        var forged = SignWith(attackerPrivate, "منشأة العميل", machine, days: 365 * 99);

        Assert.Equal(LicenseVerdict.BadSignature, RsaLicenseValidator.Validate("منشأة العميل", machine, forged, out var payload));
        Assert.Null(payload);
        Assert.False(RsaLicenseValidator.TryValidate("منشأة العميل", machine, forged, out _));
    }

    /// <summary>
    /// ويُوثَّق الحدُّ صراحةً: <b>خارجَ الإنتاج</b> المتغيّرُ محترم، فالتزويرُ نفسُه يمرّ —
    /// وهذا مقصود (مطوّرٌ يجرّب بمفتاحه). الحارسُ على البيئة، لا على الخوارزمية.
    /// </summary>
    [Fact]
    public void TheSameForgery_PassesOutsideProduction_BecauseTheGuardIsTheEnvironment()
    {
        var (developerPublic, developerPrivate) = FreshKeyPair();
        Environment.SetEnvironmentVariable(EmbeddedLicenseKeys.EnvironmentVariable, developerPublic);
        RsaLicenseValidator.PublicKeyPemOverride = null;
        AppEnvironmentConfig.ForceForTests(AppEnvironment.Development);

        var machine = LicenseService.MachineId();
        var signed = SignWith(developerPrivate, "منشأة التطوير", machine);

        Assert.Equal(LicenseVerdict.Valid, RsaLicenseValidator.Validate("منشأة التطوير", machine, signed, out _));
    }

    /// <summary>كلُّ إخفاقٍ باسمه — فالنافذةُ تقول للعميل ما وقع لا «غير صالح».</summary>
    [Fact]
    public void Verdicts_NameTheActualReason()
    {
        using var scope = RsaTestKeys.UseForValidation();
        var machine = LicenseService.MachineId();
        var good = RsaTestKeys.Sign("شركة", machine);

        Assert.Equal(LicenseVerdict.Valid, RsaLicenseValidator.Validate("شركة", machine, good, out var payload));
        Assert.Equal("شركة", payload!.CompanyName);
        Assert.Equal(LicenseVerdict.CompanyMismatch, RsaLicenseValidator.Validate("شركة أخرى", machine, good, out _));
        Assert.Equal(LicenseVerdict.MachineMismatch, RsaLicenseValidator.Validate("شركة", "OTHERMACHINE0001", good, out _));
        Assert.Equal(LicenseVerdict.Expired, RsaLicenseValidator.Validate("شركة", machine, RsaTestKeys.Sign("شركة", machine, days: -1), out _));
        Assert.Equal(LicenseVerdict.Malformed, RsaLicenseValidator.Validate("شركة", machine, good + "tampered", out _));
        Assert.Equal(LicenseVerdict.Malformed, RsaLicenseValidator.Validate("شركة", machine, "{\"signature\":\"x\"}", out _));
        // (CEEF-236) ‏توقيعٌ ليس نصّاً: `GetString()` يرمي `InvalidOperationException` لا `JsonException`.
        // ‏**ونصٌّ يلصقه المستخدم لا يجوز أن يُسقط النافذةَ بمكدّس** — هو مُشوَّه، وهذا حكمُه.
        Assert.Equal(LicenseVerdict.Malformed, RsaLicenseValidator.Validate("شركة", machine, "{\"payload\":{},\"signature\":12}", out _));
        Assert.Equal(LicenseVerdict.Malformed, RsaLicenseValidator.Validate("شركة", machine, "[]", out _));
        Assert.Equal(LicenseVerdict.Malformed, RsaLicenseValidator.Validate("شركة", machine, "", out _));

        var (_, strangerPrivate) = FreshKeyPair();
        Assert.Equal(LicenseVerdict.BadSignature, RsaLicenseValidator.Validate("شركة", machine, SignWith(strangerPrivate, "شركة", machine), out _));
    }

    /// <summary>مفتاحٌ لا يُستورد عطلُ تركيب — يُسمّى كذلك ولا يُنسَب إلى ترخيص العميل.</summary>
    [Fact]
    public void AnUnusableTrustAnchor_IsReportedAsNoTrustAnchor_NotAsABadLicense()
    {
        RsaLicenseValidator.PublicKeyPemOverride = "this is not a pem";
        var machine = LicenseService.MachineId();
        var (_, somePrivate) = FreshKeyPair();

        Assert.Equal(LicenseVerdict.NoTrustAnchor, RsaLicenseValidator.Validate("شركة", machine, SignWith(somePrivate, "شركة", machine), out _));
        Assert.False(RsaLicenseValidator.HasTrustAnchor());
    }

    /// <summary>
    /// <b>هويّةُ المفتاح مثبَّتةٌ برقمٍ مكتوب، لا محسوبةٍ من الثابت الذي تحرسه.</b>
    ///
    /// <para>كان هذا الاختبار يشتقّ <c>expected</c> من <c>EmbeddedLicenseKeys.PublicKeyPem</c> ثمّ
    /// يقارنه بـ<c>TrustAnchorFingerprint()</c> الذي يقرأ الثابتَ نفسَه — <b>فيصدق مع أيّ مفتاح</b>.
    /// فلو استبدله مطوّرٌ بمفتاحٍ ولّده لتجربةٍ ما، مرّت التسعةُ كلُّها خضراء، <b>وشُحنت نسخةٌ
    /// تردّ كلَّ ترخيصٍ وقّعه المورِّد بـ<c>BadSignature</c></b>.</para>
    ///
    /// <para>والبصمةُ أدناه هي المنشورةُ في <c>docs/OPEN_ITEMS.md §3</c> وتقرير CEEF-236.
    /// <b>وتدويرُ المفتاح قرارُ إصدارٍ يُبطل كلَّ ترخيصٍ سابق</b> — فيلزمه تحديثُ هذا السطر
    /// والوثيقتَين معاً، وهو المقصود: أن يُوقظ الحارسُ من يُبدّله سهواً.</para>
    /// </summary>
    [Fact]
    public void TheEmbeddedAnchor_HasThePublishedFingerprint()
    {
        RsaLicenseValidator.PublicKeyPemOverride = null;
        AppEnvironmentConfig.ForceForTests(AppEnvironment.Production);

        const string published = "9BF0CBE1F306C44C2714DF5DAD8511B710B3BF0F8A1B39B14919B6897C9244C3";

        Assert.True(RsaLicenseValidator.HasTrustAnchor());
        Assert.Equal(published, RsaLicenseValidator.TrustAnchorFingerprint());

        // ‏والحسابُ من الثابت يجب أن يوافق الرقمَ المنشور — فإن اختلفا فالثابتُ بُدِّل.
        using var rsa = RSA.Create();
        rsa.ImportFromPem(EmbeddedLicenseKeys.PublicKeyPem);
        Assert.Equal(published, Convert.ToHexString(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo())));
    }
}
