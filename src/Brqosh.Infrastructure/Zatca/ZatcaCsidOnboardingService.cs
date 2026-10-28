using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Zatca;

namespace Brqosh.Infrastructure.Zatca;

public record ZatcaCsidResult(
    string RequestId,
    string BinarySecurityToken,
    string Secret,
    string CertificatePemPath,
    string PrivateKeyPemPath);

/// <summary>
/// تسجيل CSID لدى ZATCA — توليد مفتاح/CSR ثم طلب شهادة الامتثال والإنتاج.
/// مرجع: بوابة المطورين https://zatca.gov.sa
/// </summary>
public class ZatcaCsidOnboardingService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(90) };

    /// <summary>للاختبارات وحدَها — معالجٌ يردّ بدل الهيئة. <c>internal</c>: لا يبلغه مستخدم.</summary>
    internal static HttpMessageHandler? HandlerOverride { get; set; }

    private static HttpClient Client =>
        HandlerOverride is null ? Http : new HttpClient(HandlerOverride, disposeHandler: false) { Timeout = Http.Timeout };

    /// <summary>
    /// <b>واجهاتُ الهيئة كلُّها تشترط <c>Accept-Version</c> — والتسجيلُ كان بلاها.</b>
    ///
    /// <para>أُضيفت الترويسةُ إلى الإرسال في CEEF-206 بعد أن رُفض الطلبُ رغم صحّة المسار والحمولة، <b>وبقي طلبا الشهادة
    /// (الامتثال والإنتاج) بلاها</b> — فالتسجيلُ الحيُّ يُرفض قبل أن يبلغ الإرسال. الترويسةُ تُبنى هنا مرّةً للطلبين.</para>
    /// </summary>
    private static HttpRequestMessage JsonPost(string url, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Accept-Version", "V2");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    /// <summary>OID قالبِ الشهادة لدى مايكروسوفت — تشترطه الهيئة في الـCSR.</summary>
    private const string CertificateTemplateOid = "1.3.6.1.4.1.311.20.2";

    // أسماءُ القوالب كما تقبلها الهيئة لكلّ بيئة.
    private const string TemplateProduction = "ZATCA-Code-Signing";
    private const string TemplateSimulation = "PREZATCA-Code-Signing";

    /// <summary>
    /// <b>‏CSR بامتدادَي الهيئة الإلزاميّين — وبدونهما يُرفض الطلبُ من أوّل خطوة.</b>
    ///
    /// <para>ما كان: الـCSR يحمل <c>KeyUsage</c> وحدَه. <b>ولا اسمَ بديلاً (SAN) ولا امتدادَ قالبِ
    /// شهادة</b> — وهما ما تقرأ منهما الهيئةُ الرقمَ الضريبيَّ ونوعَ الفاتورة وهويّةَ وحدة
    /// الإصدار. وكان سطرُ <c>serial</c> يُبنى ثمّ <b>يُهمَل</b>، وهو أثرُ نيّةٍ لم تُوصَل.</para>
    ///
    /// <para>وأثرُه: طلبُ شهادة الامتثال يُرفض عند الهيئة، <b>فلا CSID ولا ربطَ للمرحلة الثانية</b> —
    /// أي أنّ عميلاً في موجةِ ربطٍ إلزاميّ لا يستطيع إصدارَ فاتورةٍ نظاميّةٍ بهذا البناء.</para>
    ///
    /// <para><b>ولا يكشفه بناءٌ ولا اختبارُ وحدةٍ لا يفكّ ترميزَ الـCSR</b> — يكشفه أوّلُ طلبٍ حقيقيّ.
    /// فالاختبارُ المرافق يفكّ ترميزَ الـDER ويؤكّد وجودَ الامتدادَين وقيمَهما.</para>
    /// </summary>
    public ZatcaKeyMaterial GenerateKeyPairAndCsr(
        long companyId, string organizationName, string vatNumber, string solutionName = "BRQOSH-ERP",
        ZatcaEnvironment environment = ZatcaEnvironment.Simulation,
        string invoiceType = "1100", string registeredAddress = "Riyadh", string businessCategory = "Software",
        bool forRenewal = false)
    {
        if (string.IsNullOrWhiteSpace(vatNumber) || vatNumber.Length != 15)
            throw new AccountingException("الرقم الضريبي (15 رقماً) مطلوب لتوليد CSR.");
        if (!vatNumber.All(char.IsDigit))
            throw new AccountingException("الرقم الضريبي يجب أن يكون أرقاماً فقط.");

        var dir = GetCertDirectory(companyId);
        Directory.CreateDirectory(dir);

        // ‏(موانعُ الإطلاق) ‏**المنحنى secp256k1** — دليلُ الهيئة التقنيّ يأمر `openssl ecparam -name secp256k1`، وCNG في ويندوز لا يدعمه
        // ‏فالتوليدُ والـCSR بـBouncyCastle (`ZatcaSecp256k1`). كان `nistP256` بتعليلٍ لا يسنده نصٌّ منشور، وطلبُ CSID به يُرفض.
        var keyPem = ZatcaSecp256k1.GenerateKeyPem();
        // ‏(ح-١٣٧) مفتاحُ التجديد يُكتب جانباً: المفتاحُ الحاليُّ يوقّع الفواتيرَ حتى تقبل الهيئةُ الجديد.
        var privateKeyPath = Path.Combine(dir, forRenewal ? RenewingKeyFile : "private.key");
        // ‏(security-licensing-tenancy#3) ‏مشفَّراً للجهاز — لا يمسّ القرصَ PEM صريحٌ قطّ.
        ZatcaKeyFile.Write(privateKeyPath, keyPem);

        // ‏معرّفُ وحدة الإصدار (EGS) كما تتوقّعه الهيئة: 1-اسم الحلّ|2-إصدارُه|3-معرّفٌ فريد.
        var serial = $"1-{SanitizeDn(solutionName)}|2-1.0|3-{Guid.NewGuid()}";

        // ‏ترتيبُ RDN في ASN.1 من الأعلى: C ثمّ O ثمّ OU ثمّ CN (وهو ما يُقرأ نصّاً CN=…,OU=…,O=…,C=SA).
        var organization = SanitizeDn(organizationName);
        (string Oid, string Value)[] subject =
        [
            ("2.5.4.6", "SA"),
            ("2.5.4.10", organization),
            ("2.5.4.11", SanitizeDn(registeredAddress)),
            ("2.5.4.3", organization),
        ];

        // KeyUsage: digitalSignature
        var extensions = new List<(string Oid, bool Critical, byte[] Value)>
        {
            ("2.5.29.15", false, new byte[] { 0x03, 0x02, 0x07, 0x80 }),
        };
        var template = BuildCertificateTemplateExtension(environment);
        extensions.Add((template.Oid!.Value!, template.Critical, template.RawData));
        var san = BuildZatcaSubjectAltName(serial, vatNumber, invoiceType, registeredAddress, businessCategory);
        extensions.Add((san.Oid!.Value!, san.Critical, san.RawData));

        var csrDer = ZatcaSecp256k1.CreateCsr(keyPem, subject, extensions);
        var csrPem = PemEncode("CERTIFICATE REQUEST", csrDer);
        var csrPath = Path.Combine(dir, forRenewal ? "request.renewing.csr" : "request.csr");
        File.WriteAllText(csrPath, csrPem);

        return new ZatcaKeyMaterial(
            privateKeyPath,
            csrPath,
            Convert.ToBase64String(csrDer),
            csrPem);
    }

    /// <summary>امتدادُ قالبِ الشهادة (‏1.3.6.1.4.1.311.20.2‎) — قيمتُه UTF8String باسم القالب.</summary>
    internal static X509Extension BuildCertificateTemplateExtension(ZatcaEnvironment environment)
    {
        var name = environment == ZatcaEnvironment.Production ? TemplateProduction : TemplateSimulation;
        var writer = new System.Formats.Asn1.AsnWriter(System.Formats.Asn1.AsnEncodingRules.DER);
        writer.WriteCharacterString(System.Formats.Asn1.UniversalTagNumber.UTF8String, name);
        return new X509Extension(new Oid(CertificateTemplateOid), writer.Encode(), critical: false);
    }

    /// <summary>
    /// ‏SAN من نوع <c>directoryName</c> يحمل خمسَ سماتٍ تقرأها الهيئة.
    /// ‏<c>GeneralName ::= CHOICE { … directoryName [4] Name … }</c>، و<c>Name</c> اختيارٌ
    /// ‏فالوسمُ <c>[4]</c> صريحٌ يلفّ <c>RDNSequence</c>.
    /// </summary>
    internal static X509Extension BuildZatcaSubjectAltName(
        string serialNumber, string vatNumber, string invoiceType,
        string registeredAddress, string businessCategory)
    {
        // (OID, value) — الترتيبُ كما في عيّنات الهيئة.
        var attributes = new (string Oid, string Value)[]
        {
            ("2.5.4.4", serialNumber),                        // SN
            ("0.9.2342.19200300.100.1.1", vatNumber),         // UID
            ("2.5.4.12", invoiceType),                        // title
            ("2.5.4.26", registeredAddress),                  // registeredAddress
            ("2.5.4.15", businessCategory),                   // businessCategory
        };

        var writer = new System.Formats.Asn1.AsnWriter(System.Formats.Asn1.AsnEncodingRules.DER);
        var directoryNameTag = new System.Formats.Asn1.Asn1Tag(
            System.Formats.Asn1.TagClass.ContextSpecific, 4, isConstructed: true);

        using (writer.PushSequence())                 // GeneralNames ::= SEQUENCE OF GeneralName
        using (writer.PushSequence(directoryNameTag)) // [4] EXPLICIT — لأنّ Name اختيار
        using (writer.PushSequence())                 // RDNSequence
        {
            foreach (var (oid, value) in attributes)
            {
                using (writer.PushSetOf())            // RelativeDistinguishedName
                using (writer.PushSequence())         // AttributeTypeAndValue
                {
                    writer.WriteObjectIdentifier(oid);
                    writer.WriteCharacterString(System.Formats.Asn1.UniversalTagNumber.UTF8String, value);
                }
            }
        }

        return new X509Extension(new Oid("2.5.29.17"), writer.Encode(), critical: false);
    }

    public async Task<ZatcaCsidResult> RequestComplianceCsidAsync(
        string apiBaseUrl, string otp, string csrBase64, long companyId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(otp))
            throw new AccountingException("رمز OTP من بوابة فاتورة مطلوب.");
        if (string.IsNullOrWhiteSpace(csrBase64))
            throw new AccountingException("CSR غير موجود — ولّد المفتاح أولاً.");

        using var request = JsonPost(apiBaseUrl.TrimEnd('/') + "/compliance", new { csr = csrBase64 });
        request.Headers.Add("OTP", otp.Trim());

        return await ParseCsidResponseAsync(await Client.SendAsync(request, ct), companyId, ct);
    }

    public async Task<ZatcaCsidResult> RequestProductionCsidAsync(
        string apiBaseUrl, string complianceRequestId, string binarySecurityToken, string secret,
        long companyId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(complianceRequestId))
            throw new AccountingException("معرّف طلب الامتثال مطلوب.");

        using var request = JsonPost(apiBaseUrl.TrimEnd('/') + "/production/csids",
            new { compliance_request_id = complianceRequestId });
        request.Headers.Authorization = BasicAuth(binarySecurityToken, secret);

        return await ParseCsidResponseAsync(await Client.SendAsync(request, ct), companyId, ct);
    }

    private const string RenewingKeyFile = "private.renewing.key";

    /// <summary>
    /// <b>تجديدُ شهادة الإنتاج قبل انتهائها — دون إعادة التسجيل من أوّله.</b>
    ///
    /// <para>‏<c>PATCH /production/csids</c> بـCSR جديد ورمزِ OTP، ويُصادَق بالشهادة الحاليّة. <b>والمفتاحُ الجديدُ لا يحلّ
    /// محلَّ القديم إلّا بعد قبول الهيئة</b>: يُولَّد بـ<c>forRenewal</c> في ملفٍّ جانبيّ، فإن رُفض التجديدُ أو انقطع بقيت
    /// الشهادةُ القائمةُ ومفتاحُها يوقّعان كما كانا.</para>
    /// </summary>
    public async Task<ZatcaCsidResult> RenewProductionCsidAsync(
        string apiBaseUrl, string otp, string csrBase64, string currentApiToken,
        long companyId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(otp))
            throw new AccountingException("رمز OTP من بوابة فاتورة مطلوب.");
        if (string.IsNullOrWhiteSpace(csrBase64))
            throw new AccountingException("CSR التجديد غير موجود — ولّده أولاً.");
        if (string.IsNullOrWhiteSpace(currentApiToken))
            throw new AccountingException("لا شهادةَ إنتاجٍ قائمةٌ تُجدَّد — سجّل الجهازَ أوّلاً.");
        if (!File.Exists(Path.Combine(GetCertDirectory(companyId), RenewingKeyFile)))
            throw new AccountingException("مفتاح التجديد غير موجود — ولّد CSR التجديد أولاً.");

        using var request = JsonPost(apiBaseUrl.TrimEnd('/') + "/production/csids", new { csr = csrBase64 });
        request.Method = HttpMethod.Patch;
        request.Headers.Add("OTP", otp.Trim());
        request.Headers.TryAddWithoutValidation("Authorization", currentApiToken.Trim());

        return await ParseCsidResponseAsync(await Client.SendAsync(request, ct), companyId, ct, renewing: true);
    }

    private async Task<ZatcaCsidResult> ParseCsidResponseAsync(
        HttpResponseMessage response, long companyId, CancellationToken ct, bool renewing = false)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new AccountingException($"رفضت الهيئة الطلب ({(int)response.StatusCode}): {Truncate(body, 400)}");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
            throw new AccountingException("أخطاء من الهيئة: " + errors.ToString());

        var requestId = root.TryGetProperty("requestID", out var rid)
            ? rid.ValueKind == JsonValueKind.Number ? rid.GetInt64().ToString() : rid.GetString() ?? ""
            : "";

        if (!root.TryGetProperty("binarySecurityToken", out var tokenEl))
            throw new AccountingException("استجابة غير متوقعة — لا توجد شهادة.");

        var token = tokenEl.GetString() ?? throw new AccountingException("شهادة CSID فارغة.");
        var secret = root.TryGetProperty("secret", out var sec) ? sec.GetString() ?? "" : "";

        var dir = GetCertDirectory(companyId);
        Directory.CreateDirectory(dir);

        var certDer = Convert.FromBase64String(token);
        var certPem = PemEncode("CERTIFICATE", certDer);
        var certPath = Path.Combine(dir, "csid.crt.pem");
        var privateKeyPath = Path.Combine(dir, "private.key");

        // ‏التجديدُ المقبول وحدَه يُبدّل الزوج: المفتاحُ الجانبيُّ يحلّ محلَّ القديم مع شهادته.
        if (renewing)
            File.Move(Path.Combine(dir, RenewingKeyFile), privateKeyPath, overwrite: true);
        File.WriteAllText(certPath, certPem);

        if (!File.Exists(privateKeyPath))
            throw new AccountingException("ملف المفتاح الخاص غير موجود — أعد توليد CSR.");

        return new ZatcaCsidResult(requestId, token, secret, certPath, privateKeyPath);
    }

    public static string BuildApiToken(string binarySecurityToken, string secret) =>
        $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{binarySecurityToken}:{secret}"))}";

    private static AuthenticationHeaderValue BasicAuth(string user, string password) =>
        new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}")));

    /// <summary>
    /// <b>جذرُ البيانات واحد — وكان هذا الموضعُ يبنيه بيده.</b>
    ///
    /// <para>كان يركّب <c>%APPDATA%\BRQOSH</c> حرفيّاً، <b>فلا يرى <c>BRQOSH_DATA_ROOT</c></b> —
    /// بينما أرشيفُ فواتير XML يستعمل <see cref="Persistence.BrqoshDataRoot"/>. <b>فمفتاحُ
    /// التوقيع وأرشيفُه يسكنان جذرين مختلفين متى وُجِّه الجذر</b>، وذلك ما وُجد
    /// <c>BrqoshDataRoot</c> لمنعه أصلاً (CEEF-203: تشغيلةُ اختبارٍ واحدةٌ كتبت في بيانات
    /// المستخدم الحقيقيّة).</para>
    ///
    /// <para><b>والقيمةُ لا تتغيّر حين لا يُوجَّه الجذر</b> — الافتراضُ هو
    /// <c>%APPDATA%\BRQOSH</c> نفسُه — <b>فتركيباتُ العملاء القائمة تجد مفاتيحَها في مكانها</b>.
    /// وإنّما يتغيّر المُوجَّه: اختباراً كان يكتب مفاتيحَ حقيقيّةً في ملفّ المستخدم، ونسخاً
    /// احتياطيّاً صار يلتقط الشجرةَ كلَّها بجذرٍ واحد.</para>
    /// </summary>
    private static string GetCertDirectory(long companyId) =>
        Path.Combine(Persistence.BrqoshDataRoot.Path(),
            "zatca", companyId.ToString(), "csid");

    private static string SanitizeDn(string value) =>
        (value ?? "").Replace(",", " ").Replace("=", " ").Trim();

    private static string PemEncode(string label, byte[] der)
    {
        var b64 = Convert.ToBase64String(der);
        var sb = new StringBuilder();
        sb.AppendLine($"-----BEGIN {label}-----");
        for (var i = 0; i < b64.Length; i += 64)
            sb.AppendLine(b64.Substring(i, Math.Min(64, b64.Length - i)));
        sb.AppendLine($"-----END {label}-----");
        return sb.ToString();
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}

public record ZatcaKeyMaterial(
    string PrivateKeyPath,
    string CsrPath,
    string CsrBase64,
    string CsrPem);
