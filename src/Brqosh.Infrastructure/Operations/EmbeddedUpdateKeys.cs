using System.Security.Cryptography;
using Brqosh.Application.Setup;

namespace Brqosh.Infrastructure.Operations;

/// <summary>
/// (ح-١٤٠ · installer-deployment-ops#12) <b>مرساةُ ثقة التحديث — غيرُ مرساة الترخيص.</b>
///
/// <para><b>ما كان:</b> <see cref="UpdateCheckService"/> يتحقّق من البيان بمفتاح الترخيص
/// العامّ (<c>EmbeddedLicenseKeys</c>). <b>فمفتاحٌ واحدٌ يوقّع أمرين أثرُهما مختلفٌ جدّاً</b>:
/// ترخيصٌ يفتح منشأةً واحدة، وتحديثٌ يُنزَّل على <b>كلّ</b> جهاز عميل. ومفتاحُ الترخيص
/// يُستعمل كثيراً (كلَّ إصدار ترخيص، على جهاز المورِّد اليوميّ، وبأداةٍ آليّةٍ في رحلة
/// الإنتاج) — <b>فتسرّبُه يجب أن يعني تراخيصَ مزوَّرة، لا حزمةً مزوَّرةً تُوزَّع على كلّ عميل</b>.
/// والفصلُ يُتيح حفظَ مفتاح التحديث بعيداً، يُخرج يومَ الإصدار وحدَه.</para>
///
/// <para><b>وما صار:</b> ثابتٌ مُجمَّعٌ مستقلّ، وفي الإنتاج <b>لا يُقرأ غيرُه</b>؛ وخارجَ
/// الإنتاج متغيّرُ بيئةٍ لمطوّرٍ يجرّب بمفتاحه. <b>وبيانٌ موقَّعٌ بمفتاح الترخيص يُرفض.</b></para>
/// </summary>
internal static class EmbeddedUpdateKeys
{
    /// <summary>متغيّرُ بيئةٍ لمفتاحٍ بديل — <b>يُحترم خارجَ الإنتاج وحدَه</b>.</summary>
    public const string EnvironmentVariable = "BRQOSH_UPDATE_PUBLIC_KEY_PEM";

    /// <summary>
    /// مفتاحُ التحديث العامّ (RSA-2048 · SubjectPublicKeyInfo). وُلِّد بـ<c>tools/UpdatePublisher gen-keys</c>
    /// في 2026-10-17؛ نصفُه الخاصُّ لدى المورِّد (<c>update-private.pem</c>) لا في المستودع.
    /// </summary>
    public const string PublicKeyPem = """
        -----BEGIN PUBLIC KEY-----
        MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEArhR97HF8laOBW/ypJ+jO
        EmAaIO7RJ4SLdaTD/ykAMxEwvxk2p7rV2/XAobHmbUWiNejMbMfPb2G335SCEPsP
        6k8zkZMrMhOCyAFCEP6eG+XY6mtKfWyapkyJMfoiSSA/8sySjMz7Q2zn1LnykXtx
        OZ/63XZFP7ULEn17dDZ142fcC4Q7d2Brqi6BEkNzR7lRpQwYF5fxGlZVi70cNwoY
        1Q0cQCR31P5/howic1nyxyBLJAypxGuWmkQkt4FPJyqjFS0bDNSwDXE2Sx/Qxyy5
        JhaxtlRT0POD2rHmsO1SymrcjwYVtV2dBFBRXzgqzWOePZgKo8mvi88omZndV5fo
        uQIDAQAB
        -----END PUBLIC KEY-----
        """;

    public static string ResolvePublicKeyPem() =>
        Resolve(AppEnvironmentConfig.IsProduction,
            UpdateCheckService.PublicKeyPemOverride,
            Environment.GetEnvironmentVariable(EnvironmentVariable));

    /// <summary>قرارُ المرساة — دالةٌ نقيّة: التجاوزُ الداخليُّ، ثمّ المتغيّرُ خارجَ الإنتاج، ثمّ المُضمَّن.</summary>
    internal static string Resolve(bool isProduction, string? overridePem, string? envPem)
    {
        if (!string.IsNullOrWhiteSpace(overridePem))
            return overridePem;

        if (!isProduction && !string.IsNullOrWhiteSpace(envPem))
            return envPem;

        return PublicKeyPem;
    }

    /// <summary>بصمةُ مفتاحٍ عامّ (SHA-256 على SubjectPublicKeyInfo، ستّ عشريّ).</summary>
    public static string Fingerprint(string publicKeyPem)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(publicKeyPem);
        return Convert.ToHexString(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo()));
    }
}
