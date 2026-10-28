using System.Formats.Asn1;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Brqosh.Domain.Accounting;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Pkcs;

namespace Brqosh.Infrastructure.Zatca;

/// <summary>‏فحصٌ واحدٌ في تقرير الفحص المحلّيّ — اسمُه، ونتيجتُه، وما قيس.</summary>
public sealed record ZatcaPreflightCheck(string Name, bool Passed, string Detail);

/// <summary>‏مستندُ عيّنةٍ مختومٌ محلّيّاً — يُحفظ ليُفحص أيضاً بأداة الهيئة (Fatoora SDK) إن وُجدت.</summary>
public sealed record ZatcaPreflightDocument(string Label, string FileName, byte[] SignedXml, string InvoiceHashBase64);

public sealed record ZatcaPreflightReport(
    ZatcaPortal Portal,
    string EndpointBaseUrl,
    bool OtpPresent,
    string? CsrPem,
    IReadOnlyList<ZatcaPreflightCheck> Checks,
    IReadOnlyList<ZatcaPreflightDocument> Documents)
{
    public bool Ready => Checks.Count > 0 && Checks.All(c => c.Passed);

    /// <summary>‏الخطوةُ التالية: OTP حقيقيّ عند التسجيل — أو تشغيلُ التسجيل إن كان في الإعداد.</summary>
    public string NextStep => !Ready
        ? "أصلِح الفحوصَ الفاشلة أعلاه ثمّ أعِد الفحص."
        : OtpPresent || Portal == ZatcaPortal.Sandbox
            ? "جاهز: شغّل التسجيلَ الفعليّ (POST /zatca-signer/onboard) — يطلب شهادةَ الامتثال ويُجري فحوصَها لدى الهيئة."
            : ZatcaServerOnboarding.AwaitingOtpMessage;
}

/// <summary>
/// ‏(Phase 7 · تجهيزُ تجربة المحاكاة) <b>كلُّ ما يسبق OTP، محلّيّاً وبلا اتصال.</b>
///
/// <para>‏يأخذ إعدادَ التسجيل نفسَه (<see cref="ZatcaOnboardingOptions"/>) ويُجري ما سيُجريه التسجيلُ الحقيقيّ إلى حدّ الشبكة،
/// ‏ثمّ <b>يقيس الناتجَ بقارئٍ مستقلّ</b> لا يثق بالمُنتِج:</para>
/// <list type="bullet">
///   <item>‏الـCSR: يُفكّ بـBouncyCastle، توقيعُه صحيح، المنحنى secp256k1، الموضوعُ C/O/OU/CN، قالبُ الشهادة بحسب البيئة،
///   ‏وسماتُ SAN الخمس (SN بصيغة 1-…|2-…|3-…، UID الرقمُ الضريبيّ، title نوعُ الفواتير، العنوان، النشاط).</item>
///   <item>‏عيّناتُ الامتثال (فاتورة/دائن/مدين لكلّ نوعٍ أعلنه الـCSR): تُختم بشهادةٍ <b>ذاتيّة التوقيع</b> على secp256k1، ثمّ:
///   ‏البصمةُ تُعاد حسابُها من المستند المختوم، التوقيعُ يُتحقَّق منه بالمفتاح العامّ للشهادة، رمزُ QR يُفكّ ويُطابَق وسماً وسماً،
///   ‏وسلسلةُ ICV/PIH متّصلة.</item>
/// </list>
/// <para>‏<b>ولا يُرسل شيئاً ولا يكتب مفتاحاً على القرص</b> — المفتاحُ في الذاكرة ويُرمى. والخطوةُ الوحيدةُ الباقيةُ بعده OTP.</para>
/// </summary>
public static class ZatcaSimulationPreflight
{
    public const string LocalCertificateSubject = "CN=BRQOSH Local Preflight (not issued by ZATCA), O=BRQOSH, C=SA";

    private static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    private static readonly XNamespace Ds = "http://www.w3.org/2000/09/xmldsig#";

    public static ZatcaPreflightReport Run(ZatcaOnboardingOptions options)
    {
        var checks = new List<ZatcaPreflightCheck>();
        var documents = new List<ZatcaPreflightDocument>();
        void Check(string name, bool passed, string detail) => checks.Add(new(name, passed, detail));

        var otpPresent = !string.IsNullOrWhiteSpace(options.Otp);
        try
        {
            options.Validate();
            Check("الإعداد", true, $"البيئة {options.Portal} · الرقم الضريبي {options.Vat} · نوع الفواتير {options.InvoiceType}");
        }
        catch (AccountingException ex)
        {
            Check("الإعداد", false, ex.Message);
            return new ZatcaPreflightReport(options.Portal, options.EffectiveBaseUrl, otpPresent, null, checks, documents);
        }

        var expectedBase = ZatcaPortals.BaseUrl(options.Portal);
        Check("عنوان البيئة", Uri.TryCreate(options.EffectiveBaseUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps,
            options.EffectiveBaseUrl == expectedBase
                ? $"{options.EffectiveBaseUrl} (عنوانُ {options.Portal} المنشور)"
                : $"{options.EffectiveBaseUrl} — متجاوَزٌ بـApiBaseUrl (المنشور: {expectedBase})");

        if (options.Portal != ZatcaPortal.Sandbox)
            Check("OTP", true, otpPresent
                ? "موجودٌ في الإعداد — لم يُستعمل هنا (الفحصُ بلا اتصال)."
                : "غيرُ موجود — وهو الخطوةُ الوحيدةُ الباقية: enter real OTP on registration.");

        // ‏المفتاحُ في الذاكرة فقط.
        var keyPem = ZatcaSecp256k1.GenerateKeyPem();
        Check("مفتاح secp256k1", ZatcaSecp256k1.IsSecp256k1Key(keyPem), "مفتاحٌ مؤقّتٌ في الذاكرة — لا يُكتب على القرص.");

        var (csrDer, csrPem) = ZatcaCsidOnboardingService.BuildCsr(keyPem, options.OrganizationName, options.Vat, options.SolutionName,
            ZatcaPortals.Environment(options.Portal), options.InvoiceType, options.Address, options.BusinessCategory,
            organizationUnit: options.Branch, commonName: $"{options.SolutionName}-{options.Vat}",
            certificateTemplate: ZatcaPortals.CertificateTemplate(options.Portal));
        checks.AddRange(InspectCsr(csrDer, options));

        // ‏عيّناتُ الامتثال — مختومةٌ بشهادةٍ ذاتيّة التوقيع.
        var certPem = ZatcaSecp256k1.CreateSelfSignedCertificatePem(keyPem, LocalCertificateSubject,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30));
        using var cert = X509Certificate2.CreateFromPem(certPem);
        var publicKeyInfo = cert.PublicKey.ExportSubjectPublicKeyInfo();

        var previousHash = ZatcaHashHelper.GenesisPreviousInvoiceHash();
        var icv = 1;
        foreach (var (label, data) in ZatcaServerOnboarding.ComplianceSamples(options))
        {
            var unsigned = ZatcaUblInvoiceBuilder.BuildXml(data with { Icv = icv.ToString(), PreviousInvoiceHash = previousHash });
            var signed = ZatcaXmlSigner.SignPemWithResult(unsigned, certPem, keyPem);
            var timestamp = data.InvoiceDate.ToDateTime(data.IssueTime, DateTimeKind.Utc);
            var qr = ZatcaQrGenerator.GenerateTlvBase64Phase2(options.OrganizationName, options.Vat, timestamp,
                data.GrandTotal, data.VatAmount, signed.InvoiceHashBase64, signed.SignatureBase64,
                signed.PublicKeyDerBase64, signed.CertificateSignatureBase64);
            var final = ZatcaXmlSigner.ReplaceQrInSignedXml(signed.SignedXml, qr);

            var problems = InspectDocument(final, data, options, previousHash, icv, publicKeyInfo);
            Check($"عيّنة: {label}", problems.Count == 0,
                problems.Count == 0 ? $"ICV {icv} · البصمة والتوقيع وQR وPIH سليمة" : string.Join(" · ", problems));

            var fileName = $"{icv:00}-{Regex.Replace(label, "[^A-Za-z0-9]+", "-").Trim('-').ToLowerInvariant()}.xml";
            documents.Add(new ZatcaPreflightDocument(label, fileName, final, signed.InvoiceHashBase64));
            previousHash = signed.InvoiceHashBase64;
            icv++;
        }

        return new ZatcaPreflightReport(options.Portal, options.EffectiveBaseUrl, otpPresent, csrPem, checks, documents);
    }

    /// <summary>‏يكتب الـCSR والعيّناتِ والتقريرَ في مجلّد — لتُفحص أيضاً بأداة الهيئة دون اتصال إن رغب المشغّل.</summary>
    public static string WriteTo(ZatcaPreflightReport report, string directory)
    {
        Directory.CreateDirectory(directory);
        if (report.CsrPem is not null) File.WriteAllText(Path.Combine(directory, "request.csr"), report.CsrPem);
        foreach (var doc in report.Documents) File.WriteAllBytes(Path.Combine(directory, doc.FileName), doc.SignedXml);
        var path = Path.Combine(directory, "preflight-report.txt");
        File.WriteAllText(path, Format(report), new UTF8Encoding(false));
        return path;
    }

    public static string Format(ZatcaPreflightReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"ZATCA preflight — {report.Portal} — {report.EndpointBaseUrl}");
        foreach (var c in report.Checks)
            sb.AppendLine($"[{(c.Passed ? "OK" : "FAIL")}] {c.Name}: {c.Detail}");
        sb.AppendLine(report.Ready ? "النتيجة: جاهز محلّيّاً." : "النتيجة: غيرُ جاهز.");
        sb.AppendLine("التالي: " + report.NextStep);
        return sb.ToString();
    }

    // ───────────────────────── الـCSR ─────────────────────────

    internal static IReadOnlyList<ZatcaPreflightCheck> InspectCsr(byte[] csrDer, ZatcaOnboardingOptions o)
    {
        var result = new List<ZatcaPreflightCheck>();
        var csr = new Pkcs10CertificationRequest(csrDer);

        result.Add(new("CSR: التوقيع", csr.Verify(), "توقيعُ الطلب بمفتاحه العامّ"));

        var spki = csr.GetCertificationRequestInfo().SubjectPublicKeyInfo;
        var curve = spki.Algorithm.Parameters?.ToAsn1Object() is DerObjectIdentifier oid ? oid.Id : spki.Algorithm.Parameters?.ToString() ?? "?";
        result.Add(new("CSR: المنحنى", curve == ZatcaSecp256k1.CurveOid, $"OID {curve} (المطلوب {ZatcaSecp256k1.CurveOid} secp256k1)"));

        var subject = csr.GetCertificationRequestInfo().Subject;
        string? Rdn(DerObjectIdentifier id) => subject.GetValueList(id).Cast<string>().FirstOrDefault();
        var subjectProblems = new List<string>();
        if (Rdn(X509Name.C) != "SA") subjectProblems.Add($"C={Rdn(X509Name.C)}");
        if (string.IsNullOrWhiteSpace(Rdn(X509Name.O))) subjectProblems.Add("O فارغ");
        if (string.IsNullOrWhiteSpace(Rdn(X509Name.OU))) subjectProblems.Add("OU فارغ");
        if (Rdn(X509Name.CN) is not { } cn || !cn.EndsWith(o.Vat, StringComparison.Ordinal)) subjectProblems.Add($"CN={Rdn(X509Name.CN)}");
        result.Add(new("CSR: الموضوع", subjectProblems.Count == 0,
            subjectProblems.Count == 0 ? $"C=SA, O={Rdn(X509Name.O)}, OU={Rdn(X509Name.OU)}, CN={Rdn(X509Name.CN)}" : string.Join(", ", subjectProblems)));

        X509Extensions? extensions = null;
        foreach (var attr in csr.GetCertificationRequestInfo().Attributes?.OfType<Asn1Encodable>() ?? [])
        {
            var a = AttributePkcs.GetInstance(attr);
            if (a.AttrType.Equals(PkcsObjectIdentifiers.Pkcs9AtExtensionRequest))
                extensions = X509Extensions.GetInstance(a.AttrValues[0]);
        }
        if (extensions is null)
        {
            result.Add(new("CSR: الامتدادات", false, "لا extensionRequest في الطلب"));
            return result;
        }

        var templateExt = extensions.GetExtension(new DerObjectIdentifier("1.3.6.1.4.1.311.20.2"));
        var template = templateExt is null ? null
            : new AsnReader(templateExt.Value.GetOctets(), AsnEncodingRules.DER).ReadCharacterString(UniversalTagNumber.UTF8String);
        var expectedTemplate = ZatcaPortals.CertificateTemplate(o.Portal);
        result.Add(new("CSR: قالب الشهادة", template == expectedTemplate, $"{template ?? "غائب"} (المطلوب لـ{o.Portal}: {expectedTemplate})"));

        var sanExt = extensions.GetExtension(X509Extensions.SubjectAlternativeName);
        var san = sanExt is null ? new Dictionary<string, string>() : ReadDirectoryNameSan(sanExt.Value.GetOctets());
        var sanProblems = new List<string>();
        void Expect(string oidValue, string label, Func<string?, bool> ok)
        {
            san.TryGetValue(oidValue, out var v);
            if (!ok(v)) sanProblems.Add($"{label}={v ?? "غائب"}");
        }
        Expect("2.5.4.4", "SN", v => v is not null && Regex.IsMatch(v, @"^1-[^|]+\|2-[^|]+\|3-[^|]+$"));
        Expect("0.9.2342.19200300.100.1.1", "UID", v => v == o.Vat);
        Expect("2.5.4.12", "title", v => v == o.InvoiceType);
        Expect("2.5.4.26", "registeredAddress", v => !string.IsNullOrWhiteSpace(v));
        Expect("2.5.4.15", "businessCategory", v => !string.IsNullOrWhiteSpace(v));
        result.Add(new("CSR: SAN", sanProblems.Count == 0,
            sanProblems.Count == 0 ? $"SN={san["2.5.4.4"]} · UID={o.Vat} · title={o.InvoiceType}" : string.Join(", ", sanProblems)));
        return result;
    }

    private static Dictionary<string, string> ReadDirectoryNameSan(byte[] der)
    {
        var map = new Dictionary<string, string>();
        var names = new AsnReader(der, AsnEncodingRules.DER).ReadSequence();
        while (names.HasData)
        {
            var tag = names.PeekTag();
            if (tag.TagClass == TagClass.ContextSpecific && tag.TagValue == 4)
            {
                var rdns = names.ReadSequence(tag).ReadSequence();
                while (rdns.HasData)
                {
                    var set = rdns.ReadSetOf();
                    while (set.HasData)
                    {
                        var atv = set.ReadSequence();
                        var oid = atv.ReadObjectIdentifier();
                        map[oid] = atv.ReadCharacterString(UniversalTagNumber.UTF8String);
                    }
                }
            }
            else names.ReadEncodedValue();
        }
        return map;
    }

    // ───────────────────────── المستند ─────────────────────────

    internal static List<string> InspectDocument(byte[] signedXml, ZatcaUblInvoiceBuilder.InvoiceData data,
        ZatcaOnboardingOptions o, string expectedPih, int expectedIcv, byte[] publicKeyInfo)
    {
        var problems = new List<string>();
        var doc = XDocument.Load(new MemoryStream(signedXml), LoadOptions.PreserveWhitespace);
        var root = doc.Root!;

        if ((string?)root.Element(Cbc + "ProfileID") != "reporting:1.0") problems.Add("ProfileID");
        var typeCode = root.Element(Cbc + "InvoiceTypeCode");
        var expectedCode = data.IsDebitNote ? "383" : data.IsCreditNote ? "381" : "388";
        var expectedName = data.IsSimplified ? "02" : "01";
        if ((string?)typeCode != expectedCode || ((string?)typeCode?.Attribute("name"))?.StartsWith(expectedName, StringComparison.Ordinal) != true)
            problems.Add($"InvoiceTypeCode {(string?)typeCode}/{(string?)typeCode?.Attribute("name")}");
        if ((data.IsCreditNote || data.IsDebitNote) && root.Element(Cac + "BillingReference") is null)
            problems.Add("BillingReference غائب للإشعار");

        string? DocRef(string id, bool attachment)
        {
            var r = root.Elements(Cac + "AdditionalDocumentReference").FirstOrDefault(e => (string?)e.Element(Cbc + "ID") == id);
            return attachment
                ? r?.Element(Cac + "Attachment")?.Element(Cbc + "EmbeddedDocumentBinaryObject")?.Value
                : (string?)r?.Element(Cbc + "UUID");
        }
        if (DocRef("ICV", false) != expectedIcv.ToString()) problems.Add($"ICV={DocRef("ICV", false)}");
        if (DocRef("PIH", true) != expectedPih) problems.Add("PIH لا يطابق بصمةَ السابق");
        if (root.Element(Cac + "Signature") is null) problems.Add("cac:Signature غائب");

        // ‏البصمةُ تُعاد من المستند المختوم — يجب أن تساوي ما في SignedInfo وما في الوسم 6.
        var recomputed = ZatcaXmlSigner.ComputeInvoiceHash(signedXml);
        var signedInfo = doc.Descendants(Ds + "SignedInfo").FirstOrDefault();
        var invoiceDigest = signedInfo?.Elements(Ds + "Reference").FirstOrDefault()?.Element(Ds + "DigestValue")?.Value;
        if (invoiceDigest != recomputed) problems.Add("بصمةُ الفاتورة لا تطابق المستند");

        var signatureValue = doc.Descendants(Ds + "SignatureValue").FirstOrDefault()?.Value;
        var signatureOk = signedInfo is not null && signatureValue is not null
            && ZatcaSecp256k1.VerifyP1363(ZatcaXmlSigner.CanonicalizeInDocument(doc, "ds:SignedInfo"),
                Convert.FromBase64String(signatureValue), publicKeyInfo);
        if (!signatureOk) problems.Add("التوقيعُ لا يتحقّق بمفتاح الشهادة");

        var qrBase64 = DocRef("QR", true);
        var tags = qrBase64 is null ? new Dictionary<int, byte[]>() : DecodeTlv(Convert.FromBase64String(qrBase64));
        string Text(int t) => tags.TryGetValue(t, out var v) ? Encoding.UTF8.GetString(v) : "";
        if (Text(1) != o.OrganizationName) problems.Add("QR1 اسم البائع");
        if (Text(2) != o.Vat) problems.Add("QR2 الرقم الضريبي");
        if (!DateTime.TryParseExact(Text(3), "yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal, out _)) problems.Add("QR3 الوقت");
        if (Text(4) != data.GrandTotal.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)) problems.Add("QR4 الإجمالي");
        if (Text(5) != data.VatAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)) problems.Add("QR5 الضريبة");
        if (!tags.TryGetValue(6, out var t6) || Convert.ToBase64String(t6) != recomputed) problems.Add("QR6 البصمة");
        if (!tags.TryGetValue(7, out var t7) || signatureValue is null || Convert.ToBase64String(t7) != signatureValue) problems.Add("QR7 التوقيع");
        if (!tags.TryGetValue(8, out var t8) || !t8.AsSpan().SequenceEqual(publicKeyInfo)) problems.Add("QR8 المفتاح العامّ");
        if (data.IsSimplified && !tags.ContainsKey(9)) problems.Add("QR9 توقيعُ الشهادة (إلزاميٌّ للمبسّطة)");
        return problems;
    }

    internal static Dictionary<int, byte[]> DecodeTlv(byte[] bytes)
    {
        var map = new Dictionary<int, byte[]>();
        var i = 0;
        while (i + 2 <= bytes.Length)
        {
            int tag = bytes[i++];
            int length = bytes[i++];
            if (length > 0x7F)
            {
                // ‏طولٌ بصيغة BER الطويلة (عددُ بايتاته في البتّات الدنيا).
                var n = length & 0x7F;
                length = 0;
                for (var k = 0; k < n && i < bytes.Length; k++) length = (length << 8) | bytes[i++];
            }
            if (i + length > bytes.Length) break;
            map[tag] = bytes[i..(i + length)];
            i += length;
        }
        return map;
    }
}
