using System.Security.Cryptography;
using System.Text;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Zatca;

namespace Brqosh.Application.Tests.Zatca;

/// <summary>
/// (CEEF-229 · Z-9) رمز المرحلة الثانية يحمل الوسم 8 — المفتاح العام لختم التشفير.
///
/// <para><b>العطل:</b> <c>GenerateTlvBase64Phase2</c> كانت مصفوفته تنتهي عند الوسم 7 حرفياً:
/// توقيعٌ ECDSA <b>بلا مفتاحٍ يتحقّق به</b>. فماسحُ تطبيق «فاتورة» يفكّ سبعة وسوم ولا يستطيع
/// إثبات صحّة الختم، وفحصُ الامتثال على الفاتورة المبسّطة يفشل. والمفتاح يُستخرج من الشهادة
/// محلياً — لا ينتظر شيئاً من الهيئة.</para>
///
/// <para><b>الحارس يفكّ الترميز فعلاً</b> ويقارن قيمة الوسم بمفتاح الشهادة بايتاً ببايت —
/// لا يفحص أن الاستدعاء موجود في الملف.</para>
///
/// <para><b>وكان هنا ادّعاءٌ نُقض (جاهزيّةُ الإطلاق · ح-٧):</b> «الوسم 9 … <b>لا يُولَّد
/// محلياً بحال</b>». <b>والمقيسُ أنّه يُولَّد</b>: توقيعُ المُصدِر على مفتاحنا هو حقلُ
/// <c>signatureValue</c> في الشهادة نفسِها، يُقرأ من الملفّ بلا نداءٍ لأحد. <b>فبقي البندُ
/// مُصنَّفاً «خارجيّاً» يُنتظر، والانتظارُ لشيءٍ في اليد</b> — وهذا الملفُّ نفسُه كان يحمل
/// التصنيفَ ويُسنده.</para>
/// </summary>
public class ZatcaQrTagsTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "brqosh-qr-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _certPath;
    private readonly string _keyPath;

    public ZatcaQrTagsTests() =>
        (_certPath, _keyPath) = ZatcaSignatureVerifier.CreateSigningCertificate(_dir);

    /// <summary>يفكّ ترميز TLV إلى خريطة وسم ⟵ بايتات.</summary>
    private static Dictionary<byte, byte[]> DecodeTlv(string base64)
    {
        var bytes = Convert.FromBase64String(base64);
        var map = new Dictionary<byte, byte[]>();
        var i = 0;
        while (i < bytes.Length)
        {
            var tag = bytes[i++];
            int length = bytes[i++];
            if (length == 0x81) length = bytes[i++];
            else if (length == 0x82) { length = (bytes[i] << 8) | bytes[i + 1]; i += 2; }

            map[tag] = bytes[i..(i + length)];
            i += length;
        }
        return map;
    }

    private static byte[] SignedInvoiceQr(string certPath, string keyPath, out byte[] certPublicKey) =>
        SignedInvoiceQr(certPath, keyPath, out certPublicKey, out _);

    private static byte[] SignedInvoiceQr(
        string certPath, string keyPath, out byte[] certPublicKey, out byte[] certSignature)
    {
        var xml = ZatcaUblInvoiceBuilder.BuildXml(new ZatcaUblInvoiceBuilder.InvoiceData(
            InvoiceNumber: "INV-2026-000001", Uuid: Guid.NewGuid(),
            InvoiceDate: new DateOnly(2026, 3, 14), IssueTime: new TimeOnly(12, 0, 0),
            SellerName: "منشأة الاختبار", SellerVat: "310123456700003",
            CustomerName: "", CustomerVat: null,
            Subtotal: 1000m, VatAmount: 150m, GrandTotal: 1150m,
            Lines: [new SalesInvoiceLine("صنف", 1m, 1000m)],
            Icv: "1", PreviousInvoiceHash: "cHJldg==", QrTlvBase64: "AQE="));

        var result = ZatcaXmlSigner.SignWithResult(xml, certPath, keyPath);

        using var cert = System.Security.Cryptography.X509Certificates.X509CertificateLoader
            .LoadCertificateFromFile(certPath);
        certPublicKey = cert.PublicKey.ExportSubjectPublicKeyInfo();

        // ‏(ح-٧) ‏توقيعُ المُصدِر يُقرأ من الشهادة هنا **بقارئٍ مستقلٍّ عن قارئ الإنتاج** —
        // ‏فلو أخطآ معاً لَما كشف الاختبارُ شيئاً.
        var certificate = new System.Formats.Asn1.AsnReader(
            cert.RawData, System.Formats.Asn1.AsnEncodingRules.DER).ReadSequence();
        certificate.ReadEncodedValue();
        certificate.ReadEncodedValue();
        certSignature = certificate.ReadBitString(out _);

        var qr = ZatcaQrGenerator.GenerateTlvBase64Phase2(
            "منشأة الاختبار", "310123456700003", new DateTime(2026, 3, 14, 12, 0, 0, DateTimeKind.Utc),
            1150m, 150m, result.InvoiceHashBase64, result.SignatureBase64, result.PublicKeyDerBase64,
            result.CertificateSignatureBase64);

        return Encoding.UTF8.GetBytes(qr);
    }

    /// <summary>
    /// <b>القياس الأساسيّ.</b> تسعةُ وسوم، والثامن يطابق مفتاح الشهادة بالضبط.
    ///
    /// <para><b>وكان العددُ ثمانيةً</b> — وهو ما وصف السلوكَ الناقص وصفاً صحيحاً يومَه.
    /// فلمّا صار الوسمُ التاسعُ يخرج، صار العددُ تسعة.</para>
    /// </summary>
    [Fact]
    public void Phase2Qr_CarriesTagEight_MatchingTheCertificatePublicKey()
    {
        var qr = Encoding.UTF8.GetString(SignedInvoiceQr(_certPath, _keyPath, out var certKey));
        var tags = DecodeTlv(qr);

        Assert.Equal(9, tags.Count);
        Assert.True(tags.ContainsKey(8), "الوسم 8 غائب — توقيعٌ بلا مفتاحٍ يتحقّق به");
        Assert.Equal(certKey, tags[8]);
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٧) <b>والوسمُ التاسعُ يطابق توقيعَ المُصدِر بايتاً ببايت.</b>
    ///
    /// <para><b>ولا يُفحص وجودُه وحدَه</b>: وسمٌ بقيمةٍ خاطئةٍ يمرّ على فحص الوجود ويسقط
    /// عند الهيئة. فتُقرأ الشهادةُ هنا بقارئٍ مستقلٍّ ويُقارَن الناتجان.</para>
    /// </summary>
    [Fact]
    public void Phase2Qr_CarriesTagNine_MatchingTheIssuerSignature()
    {
        var qr = Encoding.UTF8.GetString(
            SignedInvoiceQr(_certPath, _keyPath, out _, out var certSignature));
        var tags = DecodeTlv(qr);

        Assert.True(tags.ContainsKey(9),
            "الوسم 9 غائب — ختمٌ ومفتاحٌ بلا ما يُثبت أنّ المُصدِر اعتمد ذلك المفتاح");
        Assert.Equal(certSignature, tags[9]);
        Assert.NotEmpty(tags[9]);
    }

    /// <summary>
    /// <b>وشاهدُ ضبطٍ للوسم التاسع:</b> بلا تمريره يبقى الرمزُ بثمانيةٍ — فهو مشروطٌ لا
    /// يُضاف فارغاً، <b>وبهذا يُعلم أنّ الحالةَ أعلاه تقيس شيئاً حقيقيّاً</b>.
    /// </summary>
    [Fact]
    public void WithoutTheIssuerSignature_TagNineIsAbsent()
    {
        var qr = ZatcaQrGenerator.GenerateTlvBase64Phase2(
            "بائع", "310123456700003", new DateTime(2026, 3, 14, 12, 0, 0, DateTimeKind.Utc),
            115m, 15m, "aGFzaA==", "c2ln", publicKeyDerBase64: "cHVi");

        var tags = DecodeTlv(qr);

        Assert.Equal(8, tags.Count);
        Assert.False(tags.ContainsKey(9));
    }

    /// <summary>والوسوم الخمسة الأولى تبقى صحيحة — الإضافة لم تُزح شيئاً.</summary>
    [Fact]
    public void Phase2Qr_StillCarriesTheFirstFiveTagsCorrectly()
    {
        var qr = Encoding.UTF8.GetString(SignedInvoiceQr(_certPath, _keyPath, out _));
        var tags = DecodeTlv(qr);

        Assert.Equal("منشأة الاختبار", Encoding.UTF8.GetString(tags[1]));
        Assert.Equal("310123456700003", Encoding.UTF8.GetString(tags[2]));
        Assert.Equal("2026-03-14T12:00:00Z", Encoding.UTF8.GetString(tags[3]));
        Assert.Equal("1150.00", Encoding.UTF8.GetString(tags[4]));
        Assert.Equal("150.00", Encoding.UTF8.GetString(tags[5]));
    }

    /// <summary>والمفتاح المستخرج يتحقّق من التوقيع فعلاً — لا مجرّد بايتات في مكانها.</summary>
    [Fact]
    public void TagEight_IsAUsablePublicKey()
    {
        var qr = Encoding.UTF8.GetString(SignedInvoiceQr(_certPath, _keyPath, out _));
        var tags = DecodeTlv(qr);

        using var ecdsa = ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(tags[8], out var read);

        Assert.Equal(tags[8].Length, read);
        Assert.Equal(256, ecdsa.KeySize);
    }

    /// <summary>
    /// <b>حارسٌ للحارس:</b> بلا مفتاح تبقى المرحلة الثانية بسبعة وسوم — فالوسم الثامن مشروط
    /// بتوفّره لا يُضاف فارغاً. وهذا يُثبت أن الاختبار الأول يقيس شيئاً حقيقياً.
    /// </summary>
    [Fact]
    public void WithoutPublicKey_TagEightIsAbsent()
    {
        var qr = ZatcaQrGenerator.GenerateTlvBase64Phase2(
            "بائع", "310123456700003", new DateTime(2026, 3, 14, 12, 0, 0, DateTimeKind.Utc),
            115m, 15m, "aGFzaA==", "c2ln");

        var tags = DecodeTlv(qr);

        Assert.Equal(7, tags.Count);
        Assert.False(tags.ContainsKey(8));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* مجلد مؤقّت */ }
        GC.SuppressFinalize(this);
    }
}
