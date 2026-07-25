using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Brqosh.Domain.Zatca;

namespace Brqosh.Infrastructure.Zatca;

/// <summary>
/// توقيع XAdES-EPES داخل UBLExtensions — متوافق مع ZATCA Phase 2.
/// المتطلبات الصارمة:
///   1. EC P-256 (secp256r1) حصراً — لا RSA.
///   2. هاش الفاتورة يُحسب من XML النظيف (بدون UBLExtensions) بعد Canonicalization C14N.
///   3. SignedInfo يُوقَّع بعد Canonicalization C14N.
///   4. SignedProperties digest يُحسب من Canonicalized XML للعنصر نفسه.
///   5. IssuerSerial يُستخرج من الشهادة الفعلية.
///   6. CertDigest هو SHA-256 لـ DER bytes الشهادة.
/// </summary>
public static class ZatcaXmlSigner
{
    private static readonly XNamespace ExtNs =
        "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";
    private static readonly XNamespace DsNs = "http://www.w3.org/2000/09/xmldsig#";
    private static readonly XNamespace XadesNs = "http://uri.etsi.org/01903/v1.3.2#";
    private const string C14NAlgorithm = "http://www.w3.org/TR/2001/REC-xml-c14n-20010315";
    private const string EcdsaSha256Algorithm = "http://www.w3.org/2001/04/xmldsig-more#ecdsa-sha256";
    private const string Sha256Algorithm = "http://www.w3.org/2001/04/xmlenc#sha256";

    private static readonly XNamespace Cbc =
        "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private static readonly XNamespace Cac =
        "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";

    public static byte[]? TrySign(byte[] xmlBytes, ZatcaSettings? settings) =>
        TrySignWithResult(xmlBytes, settings)?.SignedXml;

    /// <summary>التوقيع الكامل — يرمي ZatcaSigningException عند أي فشل.</summary>
    public static byte[] Sign(byte[] xmlBytes, string certPath, string keyPath) =>
        SignWithResult(xmlBytes, certPath, keyPath).SignedXml;

    public static ZatcaSignResult SignWithResult(byte[] xmlBytes, string certPath, string keyPath)
    {
        var cert = X509Certificate2.CreateFromPemFile(certPath, keyPath);

        // ZATCA يتطلب EC P-256 حصراً
        var ecdsa = cert.GetECDsaPrivateKey()
            ?? throw new ZatcaSigningException(
                "الشهادة لا تحتوي مفتاح EC — ZATCA يتطلب EC P-256 (secp256r1) حصراً. تحقق من ملف المفتاح الخاص.");

        var signingTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);

        // 1. هاش الفاتورة من XML النظيف (C14N بدون UBLExtensions)
        var invoiceHashB64 = ComputeInvoiceHash(xmlBytes);

        // 2. هاش الشهادة (CertDigest) = SHA-256 لـ DER bytes
        var certDigestB64 = Convert.ToBase64String(SHA256.HashData(cert.RawData));

        // 3. بيانات IssuerSerial من الشهادة الفعلية
        var issuerName = cert.IssuerName.Name ?? "CN=ZATCA";
        var serialNumber = cert.SerialNumber ?? "0";

        // 4. بناء عنصر SignedProperties وحساب digest منه بعد Canonicalization
        var signedPropsElement = BuildSignedPropertiesElement(signingTime, certDigestB64, issuerName, serialNumber);
        var signedPropsDigestB64 = ComputeElementDigest(signedPropsElement);

        // 5. بناء SignedInfo وحساب digest منه بعد Canonicalization للتوقيع
        var signedInfoElement = BuildSignedInfoElement(invoiceHashB64, signedPropsDigestB64);
        var signedInfoCanonical = CanonicalizeSingle(signedInfoElement);

        // 6. التوقيع على Canonicalized SignedInfo
        var signatureBytes = ecdsa.SignData(signedInfoCanonical, HashAlgorithmName.SHA256);
        var signatureB64 = Convert.ToBase64String(signatureBytes);

        // 7. تضمين التوقيع في XML
        var certB64 = Convert.ToBase64String(cert.RawData);
        var signed = EmbedSignature(xmlBytes, invoiceHashB64, signatureB64, certB64,
            signingTime, certDigestB64, issuerName, serialNumber, signedPropsDigestB64);
        return new ZatcaSignResult(signed, invoiceHashB64, signatureB64);
    }

    public static ZatcaSignResult? TrySignWithResult(byte[] xmlBytes, ZatcaSettings? settings)
    {
        if (settings is null) return null;
        if (string.IsNullOrWhiteSpace(settings.CertificateFilePath) ||
            string.IsNullOrWhiteSpace(settings.PrivateKeyFilePath))
            return null;
        if (!File.Exists(settings.CertificateFilePath) || !File.Exists(settings.PrivateKeyFilePath))
            return null;

        try
        {
            return SignWithResult(xmlBytes, settings.CertificateFilePath, settings.PrivateKeyFilePath);
        }
        catch (ZatcaSigningException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ZatcaSigningException($"فشل توقيع الفاتورة: {ex.Message}", ex);
        }
    }

    /// <summary>استبدال QR في XML الموقّع بصيغة Phase 2.</summary>
    public static byte[] ReplaceQrInSignedXml(byte[] signedXml, string qrTlvBase64)
    {
        var doc = XDocument.Load(new MemoryStream(signedXml));
        var qrRef = doc.Descendants(Cac + "AdditionalDocumentReference")
            .FirstOrDefault(e => e.Element(Cbc + "ID")?.Value == "QR")
            ?? throw new ZatcaSigningException("عنصر QR غير موجود في XML الموقّع.");

        var attachment = qrRef.Element(Cac + "Attachment")
            ?? new XElement(Cac + "Attachment");
        attachment.RemoveNodes();
        attachment.Add(new XElement(Cbc + "EmbeddedDocumentBinaryObject",
            new XAttribute("mimeCode", "text/plain"),
            new XAttribute("filename", "qr.txt"),
            qrTlvBase64));
        if (qrRef.Element(Cac + "Attachment") is null)
            qrRef.Add(attachment);

        using var ms = new MemoryStream();
        doc.Save(ms, SaveOptions.DisableFormatting);
        return ms.ToArray();
    }

    /// <summary>
    /// هاش الفاتورة = SHA-256(C14N(Invoice بدون UBLExtensions)).
    /// هذا هو المتطلب الصريح في ZATCA SDK — الهاش يُحسب قبل إضافة التوقيع.
    /// </summary>
    private static string ComputeInvoiceHash(byte[] xmlBytes)
    {
        var xmlDoc = LoadXmlDocument(xmlBytes);

        // إزالة UBLExtensions مؤقتاً إن وُجدت (لضمان الهاش النظيف)
        var nsManager = new XmlNamespaceManager(xmlDoc.NameTable);
        nsManager.AddNamespace("ext", ExtNs.NamespaceName);
        var extNode = xmlDoc.SelectSingleNode("//ext:UBLExtensions", nsManager);
        XmlNode? removedNode = null;
        XmlNode? refNode = null;
        if (extNode is not null)
        {
            refNode = extNode.NextSibling;
            removedNode = extNode.ParentNode!.RemoveChild(extNode);
        }

        var canonical = CanonicalizeDocument(xmlDoc);
        var hash = SHA256.HashData(canonical);

        // إعادة العنصر إن أُزيل
        if (removedNode is not null && xmlDoc.DocumentElement is not null)
        {
            if (refNode is not null)
                xmlDoc.DocumentElement.InsertBefore(removedNode, refNode);
            else
                xmlDoc.DocumentElement.PrependChild(removedNode);
        }

        return Convert.ToBase64String(hash);
    }

    // ─── بناء عناصر XML ───────────────────────────────────────────────────────

    private static XElement BuildSignedPropertiesElement(
        string signingTime, string certDigestB64, string issuerName, string serialNumber)
    {
        return new XElement(XadesNs + "SignedProperties",
            new XAttribute("Id", "xadesSignedProperties"),
            new XElement(XadesNs + "SignedSignatureProperties",
                new XElement(XadesNs + "SigningTime", signingTime),
                new XElement(XadesNs + "SigningCertificate",
                    new XElement(XadesNs + "Cert",
                        new XElement(XadesNs + "CertDigest",
                            new XElement(DsNs + "DigestMethod",
                                new XAttribute("Algorithm", Sha256Algorithm)),
                            new XElement(DsNs + "DigestValue", certDigestB64)),
                        new XElement(XadesNs + "IssuerSerial",
                            new XElement(DsNs + "X509IssuerName", issuerName),
                            new XElement(DsNs + "X509SerialNumber", serialNumber))))));
    }

    private static XElement BuildSignedInfoElement(string invoiceHashB64, string signedPropsDigestB64)
    {
        return new XElement(DsNs + "SignedInfo",
            new XElement(DsNs + "CanonicalizationMethod",
                new XAttribute("Algorithm", C14NAlgorithm)),
            new XElement(DsNs + "SignatureMethod",
                new XAttribute("Algorithm", EcdsaSha256Algorithm)),
            new XElement(DsNs + "Reference",
                new XAttribute("Id", "invoiceSignedData"),
                new XAttribute("URI", ""),
                new XElement(DsNs + "Transforms",
                    new XElement(DsNs + "Transform",
                        new XAttribute("Algorithm", C14NAlgorithm))),
                new XElement(DsNs + "DigestMethod",
                    new XAttribute("Algorithm", Sha256Algorithm)),
                new XElement(DsNs + "DigestValue", invoiceHashB64)),
            new XElement(DsNs + "Reference",
                new XAttribute("Type", "http://uri.etsi.org/01903#SignedProperties"),
                new XAttribute("URI", "#xadesSignedProperties"),
                new XElement(DsNs + "DigestMethod",
                    new XAttribute("Algorithm", Sha256Algorithm)),
                new XElement(DsNs + "DigestValue", signedPropsDigestB64)));
    }

    private static XElement BuildFullSignatureElement(
        string invoiceHashB64, string signatureB64, string certB64,
        string signingTime, string certDigestB64, string issuerName,
        string serialNumber, string signedPropsDigestB64)
    {
        var signedProps = BuildSignedPropertiesElement(signingTime, certDigestB64, issuerName, serialNumber);
        var signedInfo = BuildSignedInfoElement(invoiceHashB64, signedPropsDigestB64);

        return new XElement(DsNs + "Signature",
            new XAttribute("Id", "signature"),
            signedInfo,
            new XElement(DsNs + "SignatureValue", signatureB64),
            new XElement(DsNs + "KeyInfo",
                new XElement(DsNs + "X509Data",
                    new XElement(DsNs + "X509Certificate", certB64))),
            new XElement(XadesNs + "QualifyingProperties",
                new XAttribute(XNamespace.Xmlns + "xades", XadesNs.NamespaceName),
                new XAttribute("Target", "#signature"),
                signedProps));
    }

    // ─── تضمين التوقيع في XML ─────────────────────────────────────────────────

    private static byte[] EmbedSignature(
        byte[] xmlBytes, string invoiceHashB64, string signatureB64, string certB64,
        string signingTime, string certDigestB64, string issuerName,
        string serialNumber, string signedPropsDigestB64)
    {
        var doc = XDocument.Parse(Encoding.UTF8.GetString(xmlBytes), LoadOptions.PreserveWhitespace);
        var invoice = doc.Root ?? throw new ZatcaSigningException("XML بدون جذر Invoice.");

        var signatureElement = BuildFullSignatureElement(
            invoiceHashB64, signatureB64, certB64,
            signingTime, certDigestB64, issuerName, serialNumber, signedPropsDigestB64);

        var extensions = new XElement(ExtNs + "UBLExtensions",
            new XElement(ExtNs + "UBLExtension",
                new XElement(ExtNs + "ExtensionContent",
                    signatureElement)));

        invoice.AddFirst(extensions);

        using var ms = new MemoryStream();
        doc.Save(ms, SaveOptions.DisableFormatting);
        return ms.ToArray();
    }

    // ─── Canonicalization ─────────────────────────────────────────────────────

    /// <summary>Canonicalize مستند XML كامل (C14N بدون تعليقات).</summary>
    private static byte[] CanonicalizeDocument(XmlDocument doc)
    {
        var transform = new XmlDsigC14NTransform();
        transform.LoadInput(doc);
        using var stream = (Stream)transform.GetOutput(typeof(Stream));
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>Canonicalize عنصر XElement منفرد — لحساب digest الـ SignedProperties و SignedInfo.</summary>
    private static byte[] CanonicalizeSingle(XElement element)
    {
        var xmlDoc = new XmlDocument { PreserveWhitespace = true };
        using var reader = element.CreateReader();
        xmlDoc.Load(reader);

        var transform = new XmlDsigC14NTransform();
        transform.LoadInput(xmlDoc);
        using var stream = (Stream)transform.GetOutput(typeof(Stream));
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>حساب SHA-256 digest لعنصر XElement بعد Canonicalization.</summary>
    private static string ComputeElementDigest(XElement element)
    {
        var canonical = CanonicalizeSingle(element);
        return Convert.ToBase64String(SHA256.HashData(canonical));
    }

    private static XmlDocument LoadXmlDocument(byte[] xmlBytes)
    {
        var doc = new XmlDocument { PreserveWhitespace = true };
        using var ms = new MemoryStream(xmlBytes);
        doc.Load(ms);
        return doc;
    }
}

public record ZatcaSignResult(byte[] SignedXml, string InvoiceHashBase64, string SignatureBase64);

/// <summary>استثناء توقيع ZATCA — يُميّز أخطاء التوقيع عن باقي الأخطاء.</summary>
public class ZatcaSigningException(string message, Exception? inner = null)
    : Exception(message, inner);
