using System.Xml.Linq;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Zatca;

namespace Brqosh.Application.Tests.Zatca;

/// <summary>
/// (م-٣ · ن-٣٤) <b><c>ds:X509SerialNumber</c> كان يحمل سداسيّاً عشرَ في خانةٍ نوعُها عدد.</b>
///
/// <para>العنصرُ في XMLDSig نوعُه <c>xsd:integer</c>. و<c>X509Certificate2.SerialNumber</c>
/// يعيد نصّاً سداسيّاً عشرَ (<c>"00CA11027979922CCA"</c>)، وكان يُكتب كما هو داخل
/// <c>xades:IssuerSerial</c> في الخصائص الموقَّعة. <b>والمرجعُ عند الهيئة جافا</b>،
/// و<c>X509Certificate.getSerialNumber()</c> فيها تقرأ عددَ DER — فالمكتوبُ والمقروءُ
/// ليسا الشيءَ نفسَه، والفرقُ داخل كتلةٍ <b>مُوقَّعٍ عليها</b>.</para>
///
/// <para><b>ولمَ رقمٌ تسلسليٌّ معلومٌ لا عشوائيّ:</b> <c>CreateSelfSigned</c> يسحب ثمانيةَ
/// بايتاتٍ عشوائيّة، واحتمالُ خلوّ نصِّها من كلّ حرفٍ سداسيٍّ نحو <b>١ من ٢٠٠٠</b>. فتوكيدُ
/// «كلُّها أرقامٌ عشريّة» <b>يمرّ على الكود المعطوب</b> في تلك الحالة — وشهادةٌ جديدةٌ
/// تُولَّد لكلّ مثيلِ اختبار، فالنافذةُ حيّةٌ لا نظريّة. <b>فالمحكُّ يُصاب في كلّ تشغيل.</b></para>
///
/// <para><b>وحدُّ هذا الكناريّ مُعلَن:</b> يُغلق عنصراً واحداً داخل <c>SignedProperties</c>.
/// <b>ولا تحقّقَ مخطّطيّاً (XSD) في المستودع إطلاقاً</b> — فلا يُقال إنّ الفاتورة «صارت
/// تجتاز تحقّقَ الهيئة»، بل إنّ هذه الخانةَ صارت تحمل عدداً.</para>
/// </summary>
public class CertificateSerialIsDecimalTests : IDisposable
{
    private static readonly XNamespace Ds = "http://www.w3.org/2000/09/xmldsig#";

    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "brqosh-serial-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>
    /// <b>القيمتان الذهبيّتان محسوبتان خارج المشروع</b> — لا بتعبيرٍ يُطابق ما في الإنتاج.
    ///
    /// <para>وهذا شرطٌ لا زينة: كناريٌّ يُعيد حسابَ المتوقَّع بـ<c>new BigInteger(cert.SerialNumberBytes…)</c>
    /// — حرفاً بحرفٍ كما في الموقِّع — <b>يُصادق على أيّ خطأٍ في الترميز أو الإشارة</b> لأنّ
    /// الطرفين يخطئان معاً. فالقيمةُ هنا ثابتةٌ نصّاً، مشتقّةٌ من السداسيّ العشرَ وحدَه.</para>
    /// </summary>
    public static TheoryData<string, string> KnownSerials => new()
    {
        // ‏أعلى بتٍّ مضبوط ⟵ تتصدّره حشوةُ DER فيصير النصُّ 18 محرفاً والبايتات 9.
        { "CA11027979922CCA", "14560421791019445450" },

        // ‏وأعلى بتٍّ مطفأ ⟵ بلا حشوة. والحالتان معاً تحرسان الترميزَ والإشارة.
        { "0D11027979922CCA", "941536517851065546" },
    };

    /// <summary>
    /// <b>القياسُ المركزيّ:</b> المكتوبُ في المستند عددٌ عشريٌّ يساوي الرقمَ التسلسليَّ للشهادة.
    ///
    /// <para>والتوكيدُ على <b>المكتوب</b> لا على قيمةٍ يُعيد الاختبارُ اشتقاقَها — فالخانةُ
    /// التي تُقرأ عند الفحص هي التي في المستند.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(KnownSerials))]
    public void TheCertificateSerial_IsWrittenAsADecimalInteger(string hex, string expectedDecimal)
    {
        var (certPath, keyPath) = ZatcaSignatureVerifier.CreateSigningCertificate(
            _dir, Convert.FromHexString(hex));

        var signed = ZatcaXmlSigner.SignWithResult(Invoice(), certPath, keyPath);
        var written = SerialIn(signed.SignedXml);

        Assert.Equal(expectedDecimal, written);
    }

    /// <summary>
    /// <b>والشكلُ يُحرس مستقلاً عن القيمة</b> — فقيمةٌ صحيحةٌ بشكلٍ خاطئ لا تُقرأ عدداً.
    /// وهذا التوكيدُ وحدَه هو ما كان يسقط على الكود المعطوب: <c>"00CA11027979922CCA"</c>
    /// فيه <c>C</c> و<c>A</c>.
    /// </summary>
    [Theory]
    [MemberData(nameof(KnownSerials))]
    public void TheWrittenSerial_ContainsNothingButAsciiDigits(string hex, string expectedDecimal)
    {
        _ = expectedDecimal;

        var (certPath, keyPath) = ZatcaSignatureVerifier.CreateSigningCertificate(
            _dir, Convert.FromHexString(hex));

        var written = SerialIn(ZatcaXmlSigner.SignWithResult(Invoice(), certPath, keyPath).SignedXml);

        Assert.NotEmpty(written);
        Assert.All(written, c => Assert.True(
            char.IsAsciiDigit(c),
            $"‏المكتوبُ «{written}» فيه «{c}» — وخانةُ xsd:integer لا تقبل غيرَ الأرقام العشريّة."));
    }

    /// <summary>
    /// <b>شاهدُ ضبطٍ موجب — وهو ألزمُ من القياس:</b> التوقيعُ ما زال صحيحاً بعد التغيير.
    ///
    /// <para>فالرقمُ داخل <c>SignedProperties</c>، وتغييرُ نصِّه <b>يغيّر بايتاتِ الكتلة
    /// وبصمتَها وقيمةَ التوقيع</b>. ولو حُسبت البصمةُ قبل الغرس لانكسر التوقيعُ كلُّه —
    /// وهو عطلٌ سبق أن وقع في هذا الملفّ بعينه (‏CEEF-229 · Z-2).</para>
    /// </summary>
    [Fact]
    public void ChangingTheSerialFormat_DoesNotBreakTheSignature()
    {
        var (certPath, keyPath) = ZatcaSignatureVerifier.CreateSigningCertificate(
            _dir, Convert.FromHexString("CA11027979922CCA"));

        var signed = ZatcaXmlSigner.SignWithResult(Invoice(), certPath, keyPath);
        var result = ZatcaSignatureVerifier.Verify(signed.SignedXml);

        Assert.True(result.IsValid, result.Detail ?? "‏التوقيعُ لم يعد صحيحاً بعد تغيير صيغة الرقم التسلسليّ.");
    }

    // ── مساعدات ──────────────────────────────────────────────────────────────

    private static string SerialIn(byte[] signedXml)
    {
        var doc = XDocument.Load(new MemoryStream(signedXml), LoadOptions.PreserveWhitespace);
        var element = doc.Descendants(Ds + "X509SerialNumber").SingleOrDefault();

        Assert.NotNull(element);
        return element!.Value.Trim();
    }

    private static byte[] Invoice() =>
        ZatcaUblInvoiceBuilder.BuildXml(new ZatcaUblInvoiceBuilder.InvoiceData(
            InvoiceNumber: "INV-2026-000001",
            Uuid: Guid.NewGuid(),
            InvoiceDate: new DateOnly(2026, 3, 14), IssueTime: new TimeOnly(12, 0, 0),
            SellerName: "منشأة الاختبار",
            SellerVat: "310123456700003",
            CustomerName: "",
            CustomerVat: null,
            Subtotal: 1000m,
            VatAmount: 150m,
            GrandTotal: 1150m,
            Lines: [new SalesInvoiceLine("صنف", 1m, 1000m)],
            Icv: "7",
            PreviousInvoiceHash: "NWZlY2ViNjZmZmM4NmYzOGQ5NTI3ODZjNmQ2OTZjNzljMmRiYzIzOWRkNGU5MWI0NjcyOWQ3M2EyN2ZiNTdlOQ==",
            QrTlvBase64: "AQ5UZXN0AgsxMjM0NTY3ODkwMQ==",
            IsCreditNote: false,
            BillingReferenceInvoiceNumber: null,
            BillingReferenceUuid: null));

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch (IOException) { /* ‏تنظيفٌ لا يُسقط اختباراً */ }
        GC.SuppressFinalize(this);
    }
}
