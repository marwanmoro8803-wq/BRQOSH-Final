namespace Brqosh.VendorIssuer;

/// <summary>
/// <b>مرساةُ الثقة التي يشحنها المنتج — منسوخةً هنا كي يُقاس التطابق، لا كي يُفترض.</b>
///
/// <para>في الإنتاج لا يقرأ برقوش إلا الثابتَ المُجمَّع في
/// <c>src/Brqosh.Infrastructure/Licensing/EmbeddedLicenseKeys.PublicKeyPem</c> — لا ملفَّ
/// <c>%APPDATA%</c> ولا متغيّرَ بيئة. فالمفتاحُ الخاصُّ الوحيد الذي يُنتج تراخيصَ مقبولةً هو
/// نصفُ هذا المفتاح الآخر، ولا شيءَ سواه.</para>
///
/// <para><b>ولذلك تُنسخ البصمةُ هنا:</b> مُصدِّرٌ يوقّع بمفتاحٍ لا يقابل ما شُحن يُنتج تراخيصَ
/// تُرفض على <b>كلّ</b> جهاز عميل، ورسالةُ الرفض تلوم الترخيصَ لا المفتاح. فالبوّابةُ في
/// <see cref="SelfTest"/> تُسقط الإصدارَ قبل أن يصل إلى عميل.</para>
///
/// <para><b>وتبديلُ المفتاح في المنتج قرارُ إصدارٍ لا تعديلُ شيفرة:</b> يُبطل كلَّ ترخيصٍ
/// صادرٍ قبله. وحين يقع، يُحدَّث هذان الثابتان معاً ويُعاد نشرُ هذه الأداة — فنسخةٌ قديمةٌ
/// منها تظلّ توقّع بالمفتاح القديم بلا شكوى.</para>
/// </summary>
public static class TrustAnchor
{
    /// <summary>بصمةُ SHA-256 على SubjectPublicKeyInfo للمفتاح المُضمَّن في المنتج.</summary>
    public const string ExpectedFingerprint =
        "9BF0CBE1F306C44C2714DF5DAD8511B710B3BF0F8A1B39B14919B6897C9244C3";

    /// <summary>نسخةٌ حرفيّةٌ من <c>EmbeddedLicenseKeys.PublicKeyPem</c> (وُلِّد 2026-09-03).</summary>
    public const string PublicKeyPem = """
        -----BEGIN PUBLIC KEY-----
        MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAyrP4zYGCw3jMt7lhsEjy
        /tSXao66I2WfGsXi6ztvoyGJMvv54i0D40B5bf18D6EXXElHsrbbyhVuoOSVVYC4
        gNqUsWfBWZ603epPwGFDXq4meiwY0bx6mMgfHMYLWk7qmPPvEN4jYMXSJ8cHudf0
        lmRwMJPV8xl8OTWHgPkc4KKNyIJ1GtDq4jtqIpIKBfsPiQe2pwhS0AMisUTGL6IB
        DLS9FhldAo+bRKnNpd8C1elEDjliAxxOyITumJ2jvagFrS7dDWXg6WGKi8IRgunI
        /sO02faH7HbqmX9BQCw0CABrPBYRq1Z3PEhGksKe9jXc2JzUMYvEbz5Cz1r/uK+F
        1QIDAQAB
        -----END PUBLIC KEY-----
        """;
}
