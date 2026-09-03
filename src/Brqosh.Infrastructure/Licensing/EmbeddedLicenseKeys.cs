using Brqosh.Application.Setup;

namespace Brqosh.Infrastructure.Licensing;

/// <summary>
/// (CEEF-236 · ت-١/ت-٢) <b>مفتاحُ الثقة العامّ — مُضمَّنٌ في التجميعة فعلاً.</b>
///
/// <para><b>ما كان:</b> الصنفُ اسمُه «مُضمَّن» ووثيقةُ <see cref="RsaLicenseValidator"/> تقول
/// «المفتاح العام مُضمَّن»، <b>ولم يكن يُضمِّن حرفاً</b>: يقرأ متغيّرَ بيئةٍ أو ملفَّ
/// <c>%APPDATA%\BRQOSH\license-public.pem</c>، وإلا أعاد <c>null</c> فيُرفض كلُّ ترخيص.
/// والمُنصِّبُ (<c>installer/brqosh.wxs</c>) لا يشحن الملفَّ، وكتيّبُ التركيب لا يذكره،
/// و<c>.gitignore</c> يتجاهل <c>*.pem</c>. <b>فعلى جهاز عميلٍ نظيفٍ كان ترخيصُ المورِّد
/// الصحيحُ نفسُه يُرفض</b> برسالة «ترخيص غير صالح لهذا الجهاز أو اسم المنشأة» — ويُغلَق
/// التطبيق. ولم يره ١٦٩٤ اختباراً لأنّ كلَّ اختبارات الترخيص تُثبّت
/// <see cref="RsaLicenseValidator.PublicKeyPemOverride"/>، فلم يُعبَر هذا المسارُ قطّ؛
/// والتشغيلاتُ الحيّةُ جرت بـ<c>BRQOSH_UITEST=1</c> الذي يتجاوز الترخيصَ خارجَ الإنتاج.</para>
///
/// <para><b>والشقُّ الثاني:</b> الملفُّ في مجلّدٍ يكتبه المستخدم بلا صلاحيةِ مسؤول، والمتغيّرُ
/// يُضبط بـ<c>setx</c> بنطاق المستخدم. فمن يملك الجهازَ يستبدل مرساةَ الثقة بمفتاحه ويوقّع
/// لنفسه — <b>وقد قِيس</b>: ترخيصُ <c>Enterprise</c> حتى 2125 قُبل من الملفّ ومن المتغيّر.</para>
///
/// <para><b>وما صار:</b> المفتاحُ ثابتٌ مُجمَّع (<see cref="PublicKeyPem"/>). وفي الإنتاج
/// <b>لا يُقرأ غيرُه</b> — على سُنّة <c>AppEnvironmentConfig.ResolveAuthoritative</c>: يُرفع
/// التشدّدُ ولا يُخفَض. وخارجَ الإنتاج يبقى المتغيّرُ ثمّ الملفُّ مقبولَين لمطوّرٍ يجرّب
/// بمفتاحه، ثمّ المُضمَّن. والقرارُ دالةٌ نقيّةٌ (<see cref="Resolve"/>) كلُّ مدخلاتها ظاهرة،
/// فيُقاس بكلّ تركيباته بلا مسّ بيئةِ العملية.</para>
///
/// <para><b>المفتاحُ الخاصُّ المقابل</b> ليس في المستودع ولا في التطبيق؛ يوقّع به المورِّدُ
/// عبر <c>tools/LicenseIssuer sign</c>. <b>وتبديلُ المفتاح العامّ هنا يُبطل كلَّ ترخيصٍ
/// صادرٍ قبله</b> — فهو قرارُ إصدارٍ لا تعديلُ شيفرة.</para>
/// </summary>
internal static class EmbeddedLicenseKeys
{
    /// <summary>متغيّرُ بيئةٍ لمفتاحٍ بديل — <b>يُحترم خارجَ الإنتاج وحدَه</b>.</summary>
    public const string EnvironmentVariable = "BRQOSH_LICENSE_PUBLIC_KEY_PEM";

    /// <summary>ملفُّ مفتاحٍ بديل في جذر البيانات — <b>يُحترم خارجَ الإنتاج وحدَه</b>.</summary>
    public const string DataRootFileName = "license-public.pem";

    /// <summary>
    /// مفتاحُ المورِّد العامّ (RSA-2048 · SubjectPublicKeyInfo) — مرساةُ الثقة الوحيدة في الإنتاج.
    /// وُلِّد بـ<c>tools/LicenseIssuer gen-keys</c> في 2026-09-03؛ نصفُه الخاصُّ محفوظٌ لدى المورِّد.
    /// </summary>
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

    public static string ResolvePublicKeyPem()
    {
        var isProduction = AppEnvironmentConfig.IsProduction;
        return Resolve(
            isProduction,
            RsaLicenseValidator.PublicKeyPemOverride,
            Environment.GetEnvironmentVariable(EnvironmentVariable),
            // ‏في الإنتاج لا يُقرأ الملفُّ أصلاً — لا يُفتح ثمّ يُهمَل.
            isProduction ? null : ReadDataRootFile());
    }

    /// <summary>
    /// قرارُ مرساة الثقة — دالةٌ نقيّة.
    /// <list type="number">
    ///   <item><paramref name="overridePem"/>: تجاوزٌ <c>internal</c> لا يبلغه إلا مشروعُ
    ///     الاختبار (<c>InternalsVisibleTo</c>) — ليس سطحاً يصل إليه مستخدم، فيُحترم في كلّ
    ///     بيئةٍ كي تُقاس سلوكياتُ الإنتاج بمفتاحٍ معلوم.</item>
    ///   <item>خارجَ الإنتاج: المتغيّرُ ثمّ الملفّ.</item>
    ///   <item>وإلا — وفي الإنتاج دائماً — المُضمَّن.</item>
    /// </list>
    /// </summary>
    internal static string Resolve(bool isProduction, string? overridePem, string? envPem, string? filePem)
    {
        if (!string.IsNullOrWhiteSpace(overridePem))
            return overridePem;

        if (!isProduction)
        {
            if (!string.IsNullOrWhiteSpace(envPem))
                return envPem;
            if (!string.IsNullOrWhiteSpace(filePem))
                return filePem;
        }

        return PublicKeyPem;
    }

    private static string? ReadDataRootFile()
    {
        try
        {
            var path = Path.Combine(Persistence.BrqoshDataRoot.Path(), DataRootFileName);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
