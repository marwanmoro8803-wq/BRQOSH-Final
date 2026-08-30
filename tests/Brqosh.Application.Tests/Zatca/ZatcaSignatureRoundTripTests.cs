using System.Xml.Linq;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Zatca;

namespace Brqosh.Application.Tests.Zatca;

/// <summary>
/// (CEEF-229 · ح-٤ و Z-1 و Z-2) التوقيع يُوقَّع ويُتحقَّق منه — بشهادة تُولَّد في زمن التشغيل.
///
/// <para><b>عطلان حتميّان — كلٌّ منهما يرفض كل فاتورة — مرّا خلف مجموعةٍ خضراء:</b></para>
///
/// <para><b>Z-2:</b> بصمة <c>SignedProperties</c> كانت تُحسب على العنصر مُقنَّناً <b>منفرداً</b>
/// (فضاء افتراضيّ <c>etsi</c>)، بينما هو داخل المستند بالبادئة <c>xades:</c> وتحت فضاء
/// <c>xmldsig#</c> الافتراضيّ. بايتاتٌ مختلفة ⟵ بصمتان لا تشتركان في بايت.</para>
///
/// <para><b>Z-1:</b> المرجع الأول كان بتحويل C14N واحد بلا استثناءات، ثم يُستبدل رمز QR
/// <b>بعد</b> التجزئة. والاستبدال مشروع في المعيار <b>لأن الوسم مستثنى</b> — لكن الاستثناء لم
/// يكن مطبَّقاً ولا معلَناً، فالملفُّ المحفوظ لم يعد المستند الذي وُقِّع عليه.</para>
///
/// <para><b>ولماذا لم يكشفهما شيء:</b> <c>ZatcaGoldenTests</c> يقرأ عناصر <c>cbc:</c>/<c>cac:</c>
/// ولا يمسّ <c>ds:Signature</c> إطلاقاً — محوران متعامدان. والتحقّق التشفيريّ الوحيد في المستودع
/// كان ملفوفاً بـ<c>catch (CryptographicException) {}</c>.</para>
///
/// <para><b>حدٌّ مُصرَّح به:</b> معيار الهيئة ينصّ على تقنين C14N 1.1، و.NET لا يوفّره مضمَّناً —
/// فالمستعمَل هنا C14N 1.0 <b>معلَناً كما هو مُطبَّق</b>. الاتساق الداخليّ مُثبَت، ومطابقة
/// الخوارزمية لمواصفة الهيئة تبقى بنداً لبيئتها (م4).</para>
/// </summary>
public class ZatcaSignatureRoundTripTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "brqosh-sign-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _certPath;
    private readonly string _keyPath;

    public ZatcaSignatureRoundTripTests() =>
        (_certPath, _keyPath) = ZatcaSignatureVerifier.CreateSigningCertificate(_dir);

    private static byte[] Invoice(bool isCreditNote = false, string? billingReference = null) =>
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
            IsCreditNote: isCreditNote,
            BillingReferenceInvoiceNumber: billingReference,
            BillingReferenceUuid: billingReference is null ? null : Guid.NewGuid()));

    // ── القياس الأساسيّ ─────────────────────────────────────────────────────

    /// <summary>
    /// <b>ما كان مستحيلاً قبل اليوم:</b> يُوقَّع مستند، ثم تُعاد البصمتان حساباً <b>مستقلاً</b>
    /// وتُقارنان بالمكتوب، ويُتحقَّق من التوقيع نفسه. الثلاثة تنجح.
    /// </summary>
    [Fact]
    public void SignedInvoice_VerifiesCompletely()
    {
        var signed = ZatcaXmlSigner.SignWithResult(Invoice(), _certPath, _keyPath);

        var result = ZatcaSignatureVerifier.Verify(signed.SignedXml);

        Assert.True(result.IsValid, result.ToString());
    }

    /// <summary>
    /// <b>وهذا هو بيت القصيد:</b> بعد استبدال رمز QR بنسخة المرحلة الثانية — وهو ما يفعله
    /// المولّد فعلاً — يبقى التوقيع <b>صحيحاً</b>. لأن الوسم مستثنى من الهاش صراحةً.
    /// قبل الإصلاح كان هذا الاستبدال يُبطل كل فاتورة.
    /// </summary>
    [Fact]
    public void AfterQrReplacement_SignatureRemainsValid()
    {
        var signed = ZatcaXmlSigner.SignWithResult(Invoice(), _certPath, _keyPath);
        var withPhase2Qr = ZatcaXmlSigner.ReplaceQrInSignedXml(
            signed.SignedXml, "AQ5UZXN0AgsxMjM0NTY3ODkwMQMUMjAyNi0wMy0xNFQxMjowMDowMFo=");

        var result = ZatcaSignatureVerifier.Verify(withPhase2Qr);

        Assert.True(result.IsValid, "استبدال QR أبطل التوقيع — الاستثناء غير مطبَّق: " + result);
    }

    /// <summary>الإشعار الدائن يُوقَّع ويُتحقَّق منه كذلك — لا يكسره المرجع الأصليّ.</summary>
    [Fact]
    public void SignedCreditNote_VerifiesCompletely()
    {
        var signed = ZatcaXmlSigner.SignWithResult(
            Invoice(isCreditNote: true, billingReference: "INV-2026-000001"), _certPath, _keyPath);

        Assert.True(ZatcaSignatureVerifier.Verify(signed.SignedXml).IsValid);
    }

    /// <summary>المرجع يُعلن الاستثناءات الثلاثة — وإلا عجز أيّ متحقّق خارجيّ عن إعادة الهاش.</summary>
    [Fact]
    public void FirstReference_DeclaresTheThreeExclusionTransforms()
    {
        XNamespace ds = "http://www.w3.org/2000/09/xmldsig#";
        var signed = ZatcaXmlSigner.SignWithResult(Invoice(), _certPath, _keyPath);
        var doc = XDocument.Load(new MemoryStream(signed.SignedXml));

        var transforms = doc.Descendants(ds + "Reference").First()
            .Element(ds + "Transforms")!.Elements(ds + "Transform").ToList();

        var xpaths = transforms
            .Select(t => t.Element(ds + "XPath")?.Value)
            .Where(v => v is not null)
            .ToList();

        Assert.Equal(4, transforms.Count);          // ثلاثة استثناءات + التقنين
        Assert.Contains(xpaths, v => v!.Contains("UBLExtensions"));
        Assert.Contains(xpaths, v => v!.Contains("cac:Signature"));
        Assert.Contains(xpaths, v => v!.Contains("cbc:ID='QR'"));
    }

    // ── حارسٌ للحارس: المتحقّق يعرف كيف يقول «لا» ───────────────────────────

    /// <summary>
    /// <b>أهمّ اختبار في الملف.</b> متحقّقٌ لا يرفض شيئاً يمرّ على كل شيء. يُعبَث بمبلغٍ في
    /// الفاتورة بعد التوقيع — وهو <b>ليس</b> ضمن المستثنيات — فيجب أن تنكسر بصمة المرجع الأول.
    /// </summary>
    [Fact]
    public void TamperingWithAnAmount_BreaksTheInvoiceDigest()
    {
        XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
        var signed = ZatcaXmlSigner.SignWithResult(Invoice(), _certPath, _keyPath);

        var doc = XDocument.Load(new MemoryStream(signed.SignedXml), LoadOptions.PreserveWhitespace);
        doc.Descendants(cbc + "PayableAmount").First().Value = "9999.00";
        using var ms = new MemoryStream();
        doc.Save(ms, SaveOptions.DisableFormatting);

        var result = ZatcaSignatureVerifier.Verify(ms.ToArray());

        Assert.False(result.InvoiceDigestMatches, "العبث بالمبلغ لم يكسر بصمة الفاتورة");
    }

    /// <summary>والعبث بوقت التوقيع يكسر بصمة الخصائص الموقّعة — المرجع الثاني.</summary>
    [Fact]
    public void TamperingWithSigningTime_BreaksSignedPropertiesDigest()
    {
        XNamespace xades = "http://uri.etsi.org/01903/v1.3.2#";
        var signed = ZatcaXmlSigner.SignWithResult(Invoice(), _certPath, _keyPath);

        var doc = XDocument.Load(new MemoryStream(signed.SignedXml), LoadOptions.PreserveWhitespace);
        doc.Descendants(xades + "SigningTime").First().Value = "2020-01-01T00:00:00Z";
        using var ms = new MemoryStream();
        doc.Save(ms, SaveOptions.DisableFormatting);

        var result = ZatcaSignatureVerifier.Verify(ms.ToArray());

        Assert.False(result.SignedPropertiesDigestMatches, "العبث بوقت التوقيع لم يكسر بصمة الخصائص");
    }

    /// <summary>وتبديل قيمة التوقيع نفسها يُبطله — الطرف الثالث من التحقق.</summary>
    [Fact]
    public void TamperingWithSignatureValue_InvalidatesTheSignature()
    {
        XNamespace ds = "http://www.w3.org/2000/09/xmldsig#";
        var signed = ZatcaXmlSigner.SignWithResult(Invoice(), _certPath, _keyPath);

        var doc = XDocument.Load(new MemoryStream(signed.SignedXml), LoadOptions.PreserveWhitespace);
        var value = doc.Descendants(ds + "SignatureValue").First();
        var bytes = Convert.FromBase64String(value.Value);
        bytes[^1] ^= 0xFF;
        value.Value = Convert.ToBase64String(bytes);
        using var ms = new MemoryStream();
        doc.Save(ms, SaveOptions.DisableFormatting);

        Assert.False(ZatcaSignatureVerifier.Verify(ms.ToArray()).SignatureValid);
    }

    /// <summary>مستندٌ بلا توقيع يُرفض صراحةً لا يُقبل صامتاً.</summary>
    [Fact]
    public void UnsignedDocument_IsReportedAsInvalid()
    {
        var result = ZatcaSignatureVerifier.Verify(Invoice());

        Assert.False(result.IsValid);
        Assert.Contains("لا كتلة توقيع", result.Detail ?? "");
    }

    /// <summary>
    /// (CEEF-229 · Z-2) <b>حارسٌ يمنع الطرفين من الاتّفاق على الخطأ مرّةً أخرى.</b>
    ///
    /// <para>العطلُ الأصليّ لم يكن أنّ التوقيع فشل — بل أنّه <b>نجح عندنا</b>: المُوقِّع يُقنّن
    /// <c>SignedProperties</c> منتزعةً، والمتحقّقُ ينتزعها كذلك، فيتّفقان بالبناء وتشهد الأداةُ
    /// لتوقيعٍ ترفضه الهيئة. <b>فاختبارٌ يسأل «هل يقول المتحقّق نعم؟» لا يمسك عودةَ العطل</b> —
    /// لأنّ الجوابَ كان «نعم» طوالَ الوقت.</para>
    ///
    /// <para>فيُقاس هنا شيءٌ لا يعتمد على أيٍّ منهما: <b>يُحسب الشكلان المُقنَّنان معاً</b> —
    /// الشكلُ داخل المستند والشكلُ منتزعاً — ويُثبَت أنّهما <b>مختلفان</b>، وأنّ <b>المكتوب هو
    /// الأوّل لا الثاني</b>. فلو رجع أحدُ الطرفين إلى الانتزاع سقط هذا الاختبار بعينه.</para>
    /// </summary>
    [Fact]
    public void TheWrittenDigest_IsTheInDocumentForm_NotTheDetachedOne()
    {
        var signed = ZatcaXmlSigner.SignWithResult(Invoice(), _certPath, _keyPath);
        var doc = XDocument.Load(new MemoryStream(signed.SignedXml), LoadOptions.PreserveWhitespace);

        XNamespace ds = "http://www.w3.org/2000/09/xmldsig#";
        XNamespace xades = "http://uri.etsi.org/01903/v1.3.2#";

        var written = doc.Descendants(ds + "Reference")
            .Single(r => (string?)r.Attribute("URI") == "#xadesSignedProperties")
            .Element(ds + "DigestValue")!.Value;

        var signedProperties = doc.Descendants(xades + "SignedProperties").Single();

        var inDocument = CanonicalDigest(signedProperties.Document!, keepContext: true);
        var detached = CanonicalDigest(new XDocument(new XElement(signedProperties)), keepContext: false);

        // ‏لو تطابقا لصار الاختبارُ بلا معنى — والفرقُ هو إعلاناتُ الجذر الموروثة.
        Assert.True(inDocument != detached,
            "الشكلان المُقنَّنان متطابقان — فالفحص لا يقيس شيئاً. راجع تهيئة الفاتورة: "
            + "يجب أن يُعلن جذرُها فضاءاتٍ يرثها SignedProperties.");

        Assert.Equal(inDocument, written);
        Assert.NotEqual(detached, written);
    }

    /// <summary>بصمةُ <c>SignedProperties</c> من مستندٍ مُعطى — بسياقه أو بدونه.</summary>
    private static string CanonicalDigest(XDocument document, bool keepContext)
    {
        var xmlDoc = new System.Xml.XmlDocument { PreserveWhitespace = true };
        using (var reader = document.CreateReader())
            xmlDoc.Load(reader);

        var ns = new System.Xml.XmlNamespaceManager(xmlDoc.NameTable);
        ns.AddNamespace("xades", "http://uri.etsi.org/01903/v1.3.2#");

        var nodes = xmlDoc.SelectNodes(
            "(//. | //@* | //namespace::*)[ancestor-or-self::xades:SignedProperties]", ns)!;

        var transform = new System.Security.Cryptography.Xml.XmlDsigC14NTransform();
        transform.LoadInput(nodes);
        using var stream = (Stream)transform.GetOutput(typeof(Stream));
        using var ms = new MemoryStream();
        stream.CopyTo(ms);

        _ = keepContext;
        return Convert.ToBase64String(
            System.Security.Cryptography.SHA256.HashData(ms.ToArray()));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* مجلد مؤقّت */ }
        GC.SuppressFinalize(this);
    }
}
