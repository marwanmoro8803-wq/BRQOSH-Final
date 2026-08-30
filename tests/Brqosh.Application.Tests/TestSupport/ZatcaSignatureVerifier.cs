using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using System.Xml.Linq;

namespace Brqosh.Application.Tests.TestSupport;

/// <summary>
/// (CEEF-229 · ح-٤) متحقّق توقيع مستقلّ — يُعيد حساب البصمتين ويقارنهما بالمكتوب.
///
/// <para><b>لماذا وُجد:</b> لم يكن في المستودع اختبارٌ واحد يتحقّق من <b>صحّة</b> توقيع.
/// <c>ZatcaGoldenTests</c> يقرأ عناصر <c>cbc:</c>/<c>cac:</c> ولا يمسّ <c>ds:Signature</c>
/// إطلاقاً — فمحوران متعامدان: هو يفحص <b>محتوى</b> الفاتورة، والتوقيع <b>سلامتها</b>.
/// والتحقّق التشفيريّ الوحيد في المستودع كان ملفوفاً بـ<c>catch (CryptographicException) {}</c>.
/// فمرّ عطلان حتميّان — كلٌّ منهما يرفض كل فاتورة — خلف مجموعةٍ خضراء.</para>
///
/// <para><b>ولا يحتاج شيئاً من الهيئة:</b> الشهادة تُولَّد في زمن التشغيل (EC P-256)، والتحقّق
/// حسابيّ بحت. أرخص خطوة تُغيّر الميزان، وكانت غائبة.</para>
///
/// <para><b>ما يفحصه:</b> بصمة المرجع الأول (الفاتورة) بعد تطبيق الاستثناءات الثلاثة · بصمة
/// المرجع الثاني (<c>SignedProperties</c>) في سياقها داخل المستند · وصحّة التوقيع ECDSA على
/// <c>SignedInfo</c> المُقنَّن.</para>
/// </summary>
public static class ZatcaSignatureVerifier
{
    private static readonly XNamespace Ds = "http://www.w3.org/2000/09/xmldsig#";
    private static readonly XNamespace Xades = "http://uri.etsi.org/01903/v1.3.2#";
    private static readonly XNamespace Ext =
        "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";
    private static readonly XNamespace Cac =
        "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    private static readonly XNamespace Cbc =
        "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

    public record VerificationResult(
        bool InvoiceDigestMatches,
        bool SignedPropertiesDigestMatches,
        bool SignatureValid,
        string? Detail = null)
    {
        public bool IsValid => InvoiceDigestMatches && SignedPropertiesDigestMatches && SignatureValid;

        public override string ToString() =>
            $"بصمة الفاتورة: {(InvoiceDigestMatches ? "مطابقة" : "مختلفة")} · " +
            $"بصمة الخصائص الموقّعة: {(SignedPropertiesDigestMatches ? "مطابقة" : "مختلفة")} · " +
            $"التوقيع: {(SignatureValid ? "صحيح" : "باطل")}" +
            (Detail is null ? "" : $" — {Detail}");
    }

    /// <summary>يولّد شهادة EC P-256 صالحة للتوقيع — بديلُ شهادة الهيئة في الاختبار.</summary>
    public static (string CertPath, string KeyPath) CreateSigningCertificate(string directory)
    {
        Directory.CreateDirectory(directory);
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=BRQOSH Test Signer, O=BRQOSH", ecdsa, HashAlgorithmName.SHA256);
        using var cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));

        var certPath = Path.Combine(directory, "signer.crt.pem");
        var keyPath = Path.Combine(directory, "signer.key.pem");
        File.WriteAllText(certPath, cert.ExportCertificatePem());
        File.WriteAllText(keyPath, ecdsa.ExportECPrivateKeyPem());
        return (certPath, keyPath);
    }

    public static VerificationResult Verify(byte[] signedXml)
    {
        var doc = XDocument.Load(new MemoryStream(signedXml), LoadOptions.PreserveWhitespace);
        var signature = doc.Descendants(Ds + "Signature").FirstOrDefault();
        if (signature is null)
            return new VerificationResult(false, false, false, "لا كتلة توقيع في المستند");

        var signedInfo = signature.Element(Ds + "SignedInfo");
        if (signedInfo is null)
            return new VerificationResult(false, false, false, "لا SignedInfo");

        var references = signedInfo.Elements(Ds + "Reference").ToList();
        if (references.Count < 2)
            return new VerificationResult(false, false, false, $"عدد المراجع {references.Count} — المتوقّع اثنان");

        var invoiceOk = VerifyInvoiceDigest(doc, references[0]);
        var propsOk = VerifySignedPropertiesDigest(signature, references[1]);
        var signatureOk = VerifySignatureValue(signature, signedInfo);

        return new VerificationResult(invoiceOk, propsOk, signatureOk);
    }

    // ── المرجع الأول: الفاتورة بعد الاستثناءات الثلاثة ──────────────────────

    private static bool VerifyInvoiceDigest(XDocument doc, XElement reference)
    {
        var written = reference.Element(Ds + "DigestValue")?.Value;
        if (string.IsNullOrWhiteSpace(written))
            return false;

        var xmlDoc = new XmlDocument { PreserveWhitespace = true };
        using (var reader = doc.CreateReader())
            xmlDoc.Load(reader);

        var ns = new XmlNamespaceManager(xmlDoc.NameTable);
        ns.AddNamespace("ext", Ext.NamespaceName);
        ns.AddNamespace("cac", Cac.NamespaceName);
        ns.AddNamespace("cbc", Cbc.NamespaceName);

        // الاستثناءات الثلاثة — يُعاد تطبيقها هنا مستقلّةً عن المُوقِّع، فالمطابقة دليل.
        Remove(xmlDoc.SelectSingleNode("//ext:UBLExtensions", ns));
        Remove(xmlDoc.SelectSingleNode("//cac:Signature", ns));
        Remove(xmlDoc.SelectSingleNode("//cac:AdditionalDocumentReference[cbc:ID='QR']", ns));

        var transform = new XmlDsigC14NTransform();
        transform.LoadInput(xmlDoc);
        using var stream = (Stream)transform.GetOutput(typeof(Stream));
        using var ms = new MemoryStream();
        stream.CopyTo(ms);

        return Convert.ToBase64String(SHA256.HashData(ms.ToArray())) == written;

        static void Remove(XmlNode? node) => node?.ParentNode?.RemoveChild(node);
    }

    // ── المرجع الثاني: الخصائص الموقّعة في سياقها ───────────────────────────

    private static bool VerifySignedPropertiesDigest(XElement signature, XElement reference)
    {
        var written = reference.Element(Ds + "DigestValue")?.Value;
        if (string.IsNullOrWhiteSpace(written))
            return false;

        var xmlDoc = new XmlDocument { PreserveWhitespace = true };
        using (var reader = signature.CreateReader())
            xmlDoc.Load(reader);

        var ns = new XmlNamespaceManager(xmlDoc.NameTable);
        ns.AddNamespace("xades", Xades.NamespaceName);

        var nodes = xmlDoc.SelectNodes(
            "(//. | //@* | //namespace::*)[ancestor-or-self::xades:SignedProperties]", ns);
        if (nodes is null || nodes.Count == 0)
            return false;

        var transform = new XmlDsigC14NTransform();
        transform.LoadInput(nodes);
        using var stream = (Stream)transform.GetOutput(typeof(Stream));
        using var ms = new MemoryStream();
        stream.CopyTo(ms);

        return Convert.ToBase64String(SHA256.HashData(ms.ToArray())) == written;
    }

    // ── قيمة التوقيع نفسها ──────────────────────────────────────────────────

    private static bool VerifySignatureValue(XElement signature, XElement signedInfo)
    {
        var signatureValue = signature.Element(Ds + "SignatureValue")?.Value;
        var certB64 = signature.Element(Ds + "KeyInfo")?
            .Element(Ds + "X509Data")?.Element(Ds + "X509Certificate")?.Value;

        if (string.IsNullOrWhiteSpace(signatureValue) || string.IsNullOrWhiteSpace(certB64))
            return false;

        var xmlDoc = new XmlDocument { PreserveWhitespace = true };
        using (var reader = signedInfo.CreateReader())
            xmlDoc.Load(reader);

        var transform = new XmlDsigC14NTransform();
        transform.LoadInput(xmlDoc);
        using var stream = (Stream)transform.GetOutput(typeof(Stream));
        using var ms = new MemoryStream();
        stream.CopyTo(ms);

        using var cert = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(certB64));
        using var ecdsa = cert.GetECDsaPublicKey();
        if (ecdsa is null)
            return false;

        return ecdsa.VerifyData(
            ms.ToArray(), Convert.FromBase64String(signatureValue), HashAlgorithmName.SHA256);
    }

    /// <summary>
    /// (CEEF-229 · م2-٢١) اللحظةُ التي يعلنها رمزُ QR (الوسم ٣) — تُقرأ من الرمز نفسه لا
    /// من المُدخَل الذي بُني منه، فيصير المستندُ ورمزُه طرفين مستقلّين قابلين للمقابلة.
    /// </summary>
    public static DateTime ReadQrTimestamp(string qrTlvBase64)
    {
        var bytes = Convert.FromBase64String(qrTlvBase64);
        var i = 0;
        while (i < bytes.Length)
        {
            var tag = bytes[i++];
            int length = bytes[i++];
            if (length == 0x81) length = bytes[i++];
            else if (length == 0x82) { length = (bytes[i] << 8) | bytes[i + 1]; i += 2; }

            if (tag == 3)
                return DateTime.Parse(
                    System.Text.Encoding.UTF8.GetString(bytes[i..(i + length)]),
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AdjustToUniversal
                        | System.Globalization.DateTimeStyles.AssumeUniversal);

            i += length;
        }

        throw new InvalidOperationException("رمز QR بلا وسم زمنيّ (الوسم ٣).");
    }
}
