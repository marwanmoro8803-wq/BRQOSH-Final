using System.Numerics;
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
        var serialNumber = DecimalSerialNumber(cert);

        // (CEEF-229 · Z-2) **يُغرَس الهيكلُ أوّلاً، ثمّ يُقنَّن كلُّ شيءٍ من داخل المستند.**
        //
        // كان الترتيبُ معكوساً: تُحسب البصمتان على عنصرَين **قائمَين بذاتهما** ثمّ يُغرَس
        // الناتج. والتقنينُ الشاملُ يُظهر كلَّ الفضاءات السارية عند القمّة، فالعنصرُ داخل
        // `Invoice` يرث `xmlns:cac` و`xmlns:cbc` و`xmlns:ext` والمنتزعُ لا يرثها — **فالشكلُ
        // الذي وُقّع عليه ليس الشكلَ الذي يقرؤه المتحقّق.**
        //
        // ولم يُكشف لأنّ متحقّقنا كان ينتزع الشجرةَ كذلك، فيتّفق الطرفان **بالبناء لا
        // بالصواب**. وقياسُ العضّة بعد إصلاح المتحقّق وحده:
        // «بصمة الفاتورة: مطابقة · بصمة الخصائص الموقّعة: مختلفة · **التوقيع: باطل**» —
        // فالعطلُ يمسّ المرجعَ الثاني **وقيمةَ التوقيع نفسِها**.
        //
        // ولا دورَ منطقيّاً في الغرس أوّلاً: بصمةُ `SignedProperties` تسكن `SignedInfo`،
        // و`SignedProperties` لا تحوي شيئاً منها. فتُملأ القيمتان في مكانهما بالترتيب.
        var certB64 = Convert.ToBase64String(cert.RawData);
        // (CEEF-229 · Z-NEW) ويُقرأ من البايتات لا من نصٍّ مُفكَّك: `Encoding.UTF8.GetString`
        // على بايتاتٍ تبدأ بعلامة ترتيب البايتات يُنتج نصّاً يبدأ بـU+FEFF فيرمي المحلّل،
        // ومخرَجُ `ZatcaUblInvoiceBuilder` يحمل BOM دائماً. (درسٌ مُرحَّلٌ من `EmbedSignature`
        // المحذوفة — حُذفت لأنّ الغرسَ صار قبل التقنين لا بعده.)
        var doc = XDocument.Load(new MemoryStream(xmlBytes), LoadOptions.PreserveWhitespace);
        var invoice = doc.Root ?? throw new ZatcaSigningException("XML بدون جذر Invoice.");

        invoice.AddFirst(new XElement(ExtNs + "UBLExtensions",
            new XElement(ExtNs + "UBLExtension",
                new XElement(ExtNs + "ExtensionContent",
                    BuildFullSignatureElement(
                        invoiceHashB64, "", certB64, signingTime,
                        certDigestB64, issuerName, serialNumber, "")))));

        // 4. بصمةُ الخصائص الموقّعة — من العنصر المغروس، بفضاءاته الموروثة.
        var signedPropsDigestB64 = Convert.ToBase64String(SHA256.HashData(
            CanonicalizeInDocument(doc, "xades:SignedProperties")));

        var propsReference = doc.Descendants(DsNs + "Reference")
            .FirstOrDefault(r => (string?)r.Attribute("URI") == "#xadesSignedProperties")
            ?? throw new ZatcaSigningException("مرجع الخصائص الموقّعة غائب عن SignedInfo.");
        propsReference.Element(DsNs + "DigestValue")!.Value = signedPropsDigestB64;

        // 5. ثمّ تُقنَّن SignedInfo **بعد امتلائها** — فهي ما يُوقَّع عليه.
        var signedInfoCanonical = CanonicalizeInDocument(doc, "ds:SignedInfo");

        // 6. التوقيع، ثمّ إسكانُ قيمته — وهي خارج SignedInfo فلا تُبطلها.
        var signatureB64 = Convert.ToBase64String(
            ecdsa.SignData(signedInfoCanonical, HashAlgorithmName.SHA256));
        doc.Descendants(DsNs + "SignatureValue").First().Value = signatureB64;

        using var signedStream = new MemoryStream();
        doc.Save(signedStream, SaveOptions.DisableFormatting);
        var signed = signedStream.ToArray();

        // (CEEF-229 · Z-9) المفتاح العام يُستخرج من الشهادة نفسها — لا من مصدرٍ آخر.
        var publicKeyDer = cert.PublicKey.ExportSubjectPublicKeyInfo();

        return new ZatcaSignResult(
            signed, invoiceHashB64, signatureB64, Convert.ToBase64String(publicKeyDer));
    }

    /// <summary>
    /// (CEEF-229 · م2-١٣ب) هل تكفي الإعدادات للتوقيع فعلاً؟
    ///
    /// <para>الشرطُ نفسه الذي يستعمله <see cref="TrySignWithResult"/> ليقرّر، مُستخرَجاً كي
    /// <b>يُسأل قبل استهلاك عدّاد</b>. فالفرقُ بين «لا شهادة» و«وُقّع» يجب أن يُعرف عند
    /// الباب، لا أن يُكتشف من غياب كتلة التوقيع في ملفٍّ صار جزءاً من السلسلة.</para>
    /// </summary>
    public static bool CanSign(
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] ZatcaSettings? settings) =>
        settings is not null
        && !string.IsNullOrWhiteSpace(settings.CertificateFilePath)
        && !string.IsNullOrWhiteSpace(settings.PrivateKeyFilePath)
        && File.Exists(settings.CertificateFilePath)
        && File.Exists(settings.PrivateKeyFilePath);

    public static ZatcaSignResult? TrySignWithResult(byte[] xmlBytes, ZatcaSettings? settings)
    {
        if (!CanSign(settings)) return null;

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
    /// <summary>
    /// (CEEF-229 · Z-1) الهاش يستثني <b>ثلاثة</b> عقد لا واحدة.
    ///
    /// <para>كان يُزيل <c>ext:UBLExtensions</c> وحدها، بينما معيار الهيئة يستثني ثلاثاً:
    /// كتلة الامتدادات، و<c>cac:Signature</c> في جسد الفاتورة، و<b>المرجع الإضافيّ الذي معرّفه
    /// QR</b>.</para>
    ///
    /// <para><b>وغياب الثالث هو ما أبطل التوقيع:</b> <c>ReplaceQrInSignedXml</c> يستبدل رمز
    /// المرحلة الثانية <b>بعد</b> التوقيع — وهو سلوكٌ مشروع في المعيار <b>لأن الوسم مستثنى من
    /// الهاش</b>. لكن الاستثناء لم يكن مطبَّقاً، فالعقدة المُجزَّأة كانت تُغيَّر بعد تجزئتها،
    /// ويصير الملفُّ المحفوظ غيرَ المستند الذي وُقِّع عليه. الاستبعاد يجعل الاستبدال مشروعاً
    /// كما في المعيار، بلا تغيير ترتيب النداءات.</para>
    /// </summary>
    private static string ComputeInvoiceHash(byte[] xmlBytes)
    {
        var xmlDoc = LoadXmlDocument(xmlBytes);
        var nsManager = new XmlNamespaceManager(xmlDoc.NameTable);
        nsManager.AddNamespace("ext", ExtNs.NamespaceName);
        nsManager.AddNamespace("cac", Cac.NamespaceName);
        nsManager.AddNamespace("cbc", Cbc.NamespaceName);

        var removed = new List<(XmlNode Node, XmlNode Parent, XmlNode? Next)>();

        void Detach(XmlNode? node)
        {
            if (node?.ParentNode is null) return;
            removed.Add((node, node.ParentNode, node.NextSibling));
            node.ParentNode.RemoveChild(node);
        }

        Detach(xmlDoc.SelectSingleNode("//ext:UBLExtensions", nsManager));
        Detach(xmlDoc.SelectSingleNode("//cac:Signature", nsManager));
        Detach(xmlDoc.SelectSingleNode(
            "//cac:AdditionalDocumentReference[cbc:ID='QR']", nsManager));

        var hash = SHA256.HashData(CanonicalizeDocument(xmlDoc));

        // تُعاد العقد إلى مواضعها بالترتيب المعكوس حتى لا يتأثّر المستند الأصليّ.
        for (var i = removed.Count - 1; i >= 0; i--)
        {
            var (node, parent, next) = removed[i];
            if (next is not null) parent.InsertBefore(node, next);
            else parent.AppendChild(node);
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
                // (CEEF-229 · Z-1) الاستثناءات الثلاثة تُعلَن صراحةً — وإلا لم يستطع أيّ متحقّق
                // إعادةَ إنتاج الهاش الذي حسبناه، فيُرفض التوقيع مهما صحّت الشهادة.
                new XElement(DsNs + "Transforms",
                    XPathTransform("not(//ancestor-or-self::ext:UBLExtensions)"),
                    XPathTransform("not(//ancestor-or-self::cac:Signature)"),
                    XPathTransform(
                        "not(//ancestor-or-self::cac:AdditionalDocumentReference[cbc:ID='QR'])"),
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

    /// <summary>
    /// تحويل XPath واحد من تحويلات الاستبعاد. فضاءات الأسماء تُعلَن على العنصر نفسه لأن
    /// المتحقّق يقرأ التعبير في سياقه لا في سياق المستند.
    /// </summary>
    private static XElement XPathTransform(string expression) =>
        new(DsNs + "Transform",
            new XAttribute("Algorithm", "http://www.w3.org/TR/1999/REC-xpath-19991116"),
            new XElement(DsNs + "XPath",
                new XAttribute(XNamespace.Xmlns + "ext", ExtNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "cac", Cac.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "cbc", Cbc.NamespaceName),
                expression));

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


    /// <summary>
    /// (CEEF-229 · Z-2) تقنينٌ شاملٌ لعقدةٍ **من داخل مستندها** — لا منتزعةً.
    ///
    /// <para>هذا هو الفرقُ كلُّه: التقنينُ الشامل (inclusive C14N) يُصدِر عند قمّة المجموعة
    /// المُقنَّنة **كلَّ** إعلانات الفضاءات السارية عليها، بما ورثتْه من أجدادها. فعنصرٌ داخل
    /// <c>Invoice</c> يحمل <c>xmlns:cac</c> و<c>xmlns:cbc</c> و<c>xmlns:ext</c> في شكله
    /// المُقنَّن، وانتزاعُه إلى مستندٍ مستقلّ يُسقطها — فتختلف البصمة.</para>
    ///
    /// <para>و<c>ds:Reference</c> هنا بلا <c>Transforms</c>، فالمتحقّقُ المطابقُ يُلغي المرجعَ
    /// داخل المستند ويُقنّنه شاملاً. فهذا بالضبط ما يُحاكى.</para>
    /// </summary>
    private static byte[] CanonicalizeInDocument(XDocument document, string qualifiedName)
    {
        var xmlDoc = new XmlDocument { PreserveWhitespace = true };
        using (var reader = document.CreateReader())
            xmlDoc.Load(reader);

        var ns = new XmlNamespaceManager(xmlDoc.NameTable);
        ns.AddNamespace("ds", DsNs.NamespaceName);
        ns.AddNamespace("xades", XadesNs.NamespaceName);

        var nodes = xmlDoc.SelectNodes(
            $"(//. | //@* | //namespace::*)[ancestor-or-self::{qualifiedName}]", ns);
        if (nodes is null || nodes.Count == 0)
            throw new ZatcaSigningException($"تعذّر عزل «{qualifiedName}» للتقنين داخل المستند.");

        var transform = new XmlDsigC14NTransform();
        transform.LoadInput(nodes);
        using var stream = (Stream)transform.GetOutput(typeof(Stream));
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
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

    /// <summary>
    /// (م-٣ · ن-٣٤) <b>الرقمُ التسلسليُّ عدداً عشريّاً — وكان يُكتب سداسيّاً عشرَ.</b>
    ///
    /// <para><c>ds:X509SerialNumber</c> نوعُه <c>xsd:integer</c> في XMLDSig، أي <b>عددٌ
    /// عشريّ</b>. و<c>X509Certificate2.SerialNumber</c> يعيد نصّاً <b>سداسيّاً عشرَ</b>
    /// (<c>"00B55714FF722C35F3"</c>)، وكان يُكتب كما هو داخل <c>IssuerSerial</c>. والمرجعُ
    /// عند الهيئة جافا، و<c>X509Certificate.getSerialNumber()</c> فيها تقرأ عددَ DER —
    /// <b>فما نكتبه وما تقرؤه ليسا الشيءَ نفسَه</b>.
    /// </para>
    ///
    /// <para><b>والقراءةُ من البايتات لا من النصّ ولا من <c>GetSerialNumber()</c></b>:
    /// الأخيرةُ <b>صغيرةُ النهاية</b> فتعكس العدد. مقيسٌ على .NET 9 في هذا المشروع:
    /// <code>
    /// SerialNumber        = 00B55714FF722C35F3   (18 محرفاً)
    /// SerialNumberBytes   = 00B55714FF722C35F3   (9 بايت)
    /// GetSerialNumber()   = F3352C72FF1457B500   (معكوسة)
    /// </code>
    /// </para>
    ///
    /// <para><b>وحشوةُ DER حاضرةٌ في الطرفين</b> — البايتُ <c>00</c> يتصدّر متى كان أعلى بتٍّ
    /// مضبوطاً، لأنّ عددَ DER <b>مُوقَّع</b> والرقمُ التسلسليُّ موجبٌ بنصّ RFC 5280. ولذلك
    /// <c>isUnsigned: false</c> هي القراءةُ الأمينة — وهي عينُ ما تقرؤه جافا. <b>وقِيس أنّ
    /// الطرقَ الأربع تتطابق</b> (موقَّعة · غيرُ موقَّعة · <c>Parse(hex)</c> · <c>Parse("0"+hex)</c>)
    /// لأنّ الحشوةَ تمنع تصدُّرَ بايتٍ سالب. <b>فادّعاءُ «نصفُ الشهادات تخرج سالبةً» باطلٌ
    /// بالقياس</b> — وقد ورد في تصميمٍ سابقٍ لهذا الإصلاح فسقط عند القياس.
    /// </para>
    ///
    /// <para><b>ولا يُبتلع رقمٌ غائب.</b> كان السطرُ <c>?? "0"</c> يكتب صفراً — <b>وهو رقمٌ
    /// تسلسليٌّ لا وجودَ له</b> يُوقَّع عليه ويُسلَّم. وشهادةٌ بلا رقمٍ لا تصلح للتوقيع أصلاً،
    /// فتُرمى <see cref="ZatcaSigningException"/> ويلتقطها <see cref="TrySignWithResult"/>
    /// فيبقى المستندُ غيرَ موقَّعٍ بدل أن يكون موقَّعاً بكذب. <b>و<c>BigInteger</c> على مدًى
    /// فارغٍ يعيد صفراً كذلك</b> — فالحارسُ يسبقه ولا يعتمد عليه.
    /// </para>
    ///
    /// <para><b>وحدودُ هذا الإصلاح تُقال:</b> يُغلق عنصراً واحداً داخل <c>SignedProperties</c>.
    /// <b>ولا تحقّقَ مخطّطيّاً (XSD) في المستودع إطلاقاً</b> — مقيسٌ ببحثٍ لم يُعِد ملفَّ
    /// <c>.xsd</c> واحداً — فلا يُقال إنّ الفاتورة «صارت تجتاز التحقّق».
    /// </para>
    /// </summary>
    private static string DecimalSerialNumber(X509Certificate2 cert)
    {
        var bytes = cert.SerialNumberBytes.Span;
        if (bytes.IsEmpty)
            throw new ZatcaSigningException(
                "شهادةُ التوقيع بلا رقمٍ تسلسليّ — ولا يُوقَّع مستندٌ ضريبيٌّ برقمٍ مُختلَق.");

        return new BigInteger(bytes, isUnsigned: false, isBigEndian: true)
            .ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}

/// <param name="PublicKeyDerBase64">
/// (CEEF-229 · Z-9) المفتاح العام بترميز DER (SubjectPublicKeyInfo) — الوسم 8 في رمز
/// المرحلة الثانية. كان غائباً، فيخرج الرمز بسبعة وسوم: توقيعٌ بلا مفتاحٍ يتحقّق به،
/// وتطبيق «فاتورة» لا يستطيع إثبات صحّة الختم.
/// </param>
public record ZatcaSignResult(
    byte[] SignedXml, string InvoiceHashBase64, string SignatureBase64, string PublicKeyDerBase64);

/// <summary>استثناء توقيع ZATCA — يُميّز أخطاء التوقيع عن باقي الأخطاء.</summary>
public class ZatcaSigningException(string message, Exception? inner = null)
    : Exception(message, inner);
