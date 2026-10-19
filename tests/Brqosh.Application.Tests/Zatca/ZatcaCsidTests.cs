using System.Security.Cryptography;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Manufacturing;
using Brqosh.Infrastructure.Zatca;

namespace Brqosh.Application.Tests.Zatca;

public class ZatcaCsidTests
{
    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٢٠) <b>وكان هذا الاختبارُ يبتلع فشلَه ويمرّ.</b>
    ///
    /// <para>كان جسمُه كلُّه داخل <c>try</c> ومعه
    /// <c>catch (CryptographicException) { }</c> بتعليق «secp256k1 قد لا يكون متاحاً على
    /// بعض البيئات — تخطّي». <b>فلو عجز توليدُ المفتاح لَمرّ الاختبارُ أخضرَ ولم يؤكّد
    /// شيئاً</b> — وهو عينُ نمط «الابتلاع الصامت» الذي تحرسه
    /// <c>SilentFailureTests</c> في <c>src/</c> <b>ولا تبلغ به الاختبارات</b>.</para>
    ///
    /// <para><b>والمبرِّرُ نفسُه بائت</b>: الخدمةُ <b>لا تستعمل secp256k1</b> أصلاً —
    /// تستعمل <c>nistP256</c> (انظر <see cref="TheStampKeyCurve_IsPinned"/>). فالمصيدةُ
    /// <b>لا يمكن أن تقع للسبب المكتوب فيها</b>، ووقوعُها لأيّ سببٍ آخر يُخفي كسراً حقيقيّاً.</para>
    /// </summary>
    [Fact]
    public void GenerateCsr_CreatesKeyAndCsrFiles()
    {
        var service = new ZatcaCsidOnboardingService();
        var material = service.GenerateKeyPairAndCsr(1, "شركة تجريبية", "310123456700003");

        Assert.True(File.Exists(material.PrivateKeyPath));
        Assert.True(File.Exists(material.CsrPath));
        Assert.False(string.IsNullOrWhiteSpace(material.CsrBase64));
        Assert.Contains("BEGIN CERTIFICATE REQUEST", material.CsrPem);
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٢٠) <b>منحنى مفتاح الختم مُثبَّتٌ صراحةً — لا يتغيّر سهواً.</b>
    ///
    /// <para><b>وهذا الاختبارُ لا يقول إنّ المنحنى صحيح</b> — يقول <b>أيُّ منحنًى يُولَّد
    /// فعلاً</b>، فيصير الاختيارُ مرئيّاً ومقصوداً بدل أن يكون افتراضاً في سطرٍ واحد.</para>
    ///
    /// <para><b>وبندٌ مفتوحٌ مُسمًّى يلزمه تأكيدٌ من المواصفة المنشورة:</b> معيارُ الهيئة
    /// لخصائص الأمان يشترط — بحسب ما هو شائعٌ منذ الطور الثاني — منحنى <c>secp256k1</c>
    /// لزوج مفاتيح الختم، <b>والمولَّدُ هنا <c>nistP256</c></b>
    /// (<c>ZatcaCsidOnboardingService.cs</c>). <b>فإن صحّ ذلك فطلبُ CSID الإنتاجيّ
    /// يُرفض عند الهيئة ولا يقوم الربطُ أصلاً</b> — وهو مانعُ إطلاقٍ لا بندٌ خارجيّ.</para>
    ///
    /// <para><b>ولم يُبدَّل المنحنى هنا</b>: تبديلُه بلا تأكيدٍ يستبدل شكّاً بشكّ، و
    /// <c>secp256k1</c> <b>قد لا تدعمه المنصّةُ أصلاً</b> (‏CNG على Windows) فينكسر توليدُ
    /// الـCSR كلُّه على أجهزةٍ يعمل عليها اليوم. <b>والقرارُ يحتاج مطابقةً بالمواصفة
    /// وتجربةً حيّة — وكلاهما خارج المستودع.</b></para>
    /// </summary>
    [Fact]
    public void TheStampKeyCurve_IsPinned()
    {
        var material = new ZatcaCsidOnboardingService()
            .GenerateKeyPairAndCsr(1, "شركة تجريبية", "310123456700003");

        using var key = ECDsa.Create();
        key.ImportFromPem(ZatcaKeyFile.ReadPem(material.PrivateKeyPath));

        var oid = key.ExportParameters(includePrivateParameters: false).Curve.Oid.Value;

        // ‏1.2.840.10045.3.1.7 = nistP256 (secp256r1) · 1.3.132.0.10 = secp256k1
        Assert.Equal("1.2.840.10045.3.1.7", oid);
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٢٠) <b>مفتاحُ الختم يُكتب <u>نصّاً صريحاً</u> على القرص —
    /// والسجلُّ كان يقول إنّه محميٌّ بـDPAPI.</b>
    ///
    /// <para><b>والواقعُ مقيسٌ هنا</b>: <c>GenerateKeyPairAndCsr</c> يكتب
    /// <c>ExportECPrivateKeyPem()</c> كما هو. و<c>LocalSecretProtector</c> — وهو آلةُ DPAPI
    /// القائمة — <b>يحمي نصَّ اتّصال القاعدة وعلامةَ الساعة، ولا يمسّ هذا الملفّ</b>.</para>
    ///
    /// <para><b>وهذا أخطرُ سرٍّ في النظام</b>: من ملكه أصدر فواتيرَ تنسبها الهيئةُ
    /// إلى المنشأة.</para>
    ///
    /// <para><b>ولم يُشفَّر هنا — والسببُ تعارضٌ حقيقيٌّ لا تهاون:</b> DPAPI بنطاق
    /// <c>CurrentUser</c> يربط الملفَّ <b>بمستخدمِ ويندوز وجهازِه</b>، <b>والنسخةُ
    /// الاحتياطيّةُ تحمل هذا المفتاح منذ ح-٥</b> — فتشفيرُه <b>يُبطل الاستعادةَ على جهازٍ
    /// آخر</b>، وهي عينُ الحالة التي تُطلب فيها النسخةُ الاحتياطيّة. <b>فالمفاضلةُ بين
    /// سرّيّةِ المفتاح وقابليّةِ التعافي قرارُ المالك</b>، لا اختيارُ مُصلِحٍ عابر.</para>
    ///
    /// <para><b>وهذا الاختبارُ يُثبّت الواقعَ ليُرى</b>: يسقط يومَ يُشفَّر — فيُتّخذ القرارُ
    /// عن قصدٍ ويُحدَّث معه كتيّبُ الاستعادة، <b>ولا ينزلق النظامُ إلى حالةٍ لا تُستعاد</b>.</para>
    ///
    /// <para><b>(security-licensing-tenancy#3 · قرارُ المالك 2026-10-18) واتُّخذ القرار:</b> DPAPI <b>للجهاز</b>
    /// (<c>ZatcaKeyFile</c>) — يقرؤه كلُّ مستخدمٍ على هذا الجهاز، ولا يُقرأ منسوخاً على غيره؛ والاستعادةُ على جهازٍ جديدٍ
    /// تعني إعادةَ التسجيل لدى الهيئة، وهي مقبولةٌ ومكتوبةٌ في الكتيّب. فانقلب الاختبارُ يُثبّت الحالةَ الجديدة.</para>
    /// </summary>
    [Fact]
    public void TheStampPrivateKey_IsSealedForTheMachine_AndNeverWrittenAsPem()
    {
        var material = new ZatcaCsidOnboardingService()
            .GenerateKeyPairAndCsr(1, "شركة تجريبية", "310123456700003");

        var onDisk = File.ReadAllText(material.PrivateKeyPath);

        Assert.StartsWith(ZatcaKeyFile.Header, onDisk, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE KEY", onDisk, StringComparison.Ordinal);
        Assert.StartsWith("-----BEGIN EC PRIVATE KEY-----", ZatcaKeyFile.ReadPem(material.PrivateKeyPath).TrimStart(),
            StringComparison.Ordinal);
    }

    /// <summary>(security-licensing-tenancy#3) <b>التركيبةُ القائمةُ لا تبقى على النصّ الصريح</b> — أوّلُ قراءةٍ تشفّره في مكانه.</summary>
    [Fact]
    public void ALegacyPlaintextKey_IsSealedInPlace_OnFirstRead()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pem = ecdsa.ExportECPrivateKeyPem();
        var path = Path.Combine(Path.GetTempPath(), $"brqosh-legacy-{Guid.NewGuid():N}.key");
        File.WriteAllText(path, pem);
        try
        {
            Assert.Equal(pem, ZatcaKeyFile.ReadPem(path));
            Assert.True(ZatcaKeyFile.IsSealed(path));
            Assert.Equal(pem, ZatcaKeyFile.ReadPem(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void GenerateCsr_RejectsInvalidVat()
    {
        var service = new ZatcaCsidOnboardingService();
        Assert.Throws<AccountingException>(() =>
            service.GenerateKeyPairAndCsr(1, "شركة", "123"));
    }

    [Fact]
    public void BuildApiToken_FormatsBasicAuth()
    {
        var token = ZatcaCsidOnboardingService.BuildApiToken("cert123", "secret456");
        Assert.StartsWith("Basic ", token);
    }
}
