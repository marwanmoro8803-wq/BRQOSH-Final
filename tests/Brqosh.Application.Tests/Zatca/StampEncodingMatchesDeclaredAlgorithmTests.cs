using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Zatca;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Zatca;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٢٠) <b>ترميزُ الختم يطابق الخوارزميّةَ التي يُعلنها المستندُ
/// نفسُه — وهو الشقُّ الذي <u>يُقاس</u> من بند الختم التشفيريّ.</b>
///
/// <para><b>وما كان قائماً — مقيساً لا مُقدَّراً:</b> تبديلُ المُوقِّع وحدَه إلى DER
/// <b>يُسقط اختبارَين قائمَين</b> (<c>AGeneratedSalesInvoice_CarriesAVerifiableSignature</c>
/// و<c>TamperingAfterSigning_IsDetected</c>) — لأنّ <c>VerifyData(…, SHA256)</c> بلا صيغةٍ
/// مُسمّاة <b>يثبّت P1363 ضمناً</b> بحكم افتراض .NET. <b>فالحمايةُ قائمةٌ عرَضاً.</b></para>
///
/// <para><b>وما لم يكن قائماً — وهو ما تُضيفه هذه البوّابة:</b></para>
/// <list type="bullet">
/// <item>لا شيءَ <b>يذكر المطلوب</b>: الاختباراتُ تقول «مُتحقِّقُنا يقبل توقيعَنا»،
/// <b>ولا تقول «الترميزُ هو ما يُلزم به الوسمُ المُعلَن»</b> — وثباتٌ بالافتراض
/// <b>يسقط بترقية منصّةٍ تُغيّر افتراضَها</b>، بلا سطرٍ يشرح ما انكسر.</item>
/// <item>وتبديلُ <b>الطرفين معاً</b> يُبقي كلَّ اختبارٍ أخضرَ <b>والمستندُ يناقض وسمَه</b>.</item>
/// <item>و<b>طولُ القيمة ونفيُ DER</b> يُقاسان هنا <b>بلا مُتحقِّقٍ أصلاً</b> — فلا
/// يتوقّفان على اتّفاق طرفَين.</item>
/// </list>
///
/// <para><b>والمِقياسُ المستقلّ هو الوسمُ المُعلَن:</b> المستندُ يقول
/// <c>SignatureMethod Algorithm="…xmldsig-more#ecdsa-sha256"</c>، و<c>RFC 6931 §2.3.6</c>
/// يُلزم لهذا الوسم أن تكون القيمةُ <b>ضمَّ <c>r</c> إلى <c>s</c> بطولٍ ثابت</b>
/// (‏IEEE P1363) — <b>لا ترميزَ DER</b>. فهذه دعوى تُثبَت بلا مُصادِق الهيئة.</para>
///
/// <para><b>وحدٌّ مُصرَّحٌ به — وهو لبُّ الأمانة هنا:</b> هذا <b>لا يُثبت قبولَ الهيئة</b>.
/// يُثبت أنّ المستندَ <b>متّسقٌ مع نفسه</b> ومع المعيار الذي يستشهد به. <b>وقبولُ الختم
/// لا يُقطع به إلا بمُصادِق الهيئة</b>، ويبقى بنداً خارجيّاً مُسمًّى.</para>
/// </summary>
public class StampEncodingMatchesDeclaredAlgorithmTests : IDisposable
{
    private const string EcdsaSha256 = "http://www.w3.org/2001/04/xmldsig-more#ecdsa-sha256";
    private static readonly XNamespace Ds = "http://www.w3.org/2000/09/xmldsig#";

    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private readonly string _workDirectory;
    private long _companyId;
    private long _customerId;

    public StampEncodingMatchesDeclaredAlgorithmTests()
    {
        TestActor.Administrator();
        _workDirectory = Path.Combine(Path.GetTempPath(), "brqosh-h20", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workDirectory);

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        _connection.Dispose();
        try { Directory.Delete(_workDirectory, recursive: true); }
        catch (IOException) { /* ‏ملفٌّ مؤقّتٌ ممسوك — لا يعني فشلَ القياس. */ }
        catch (UnauthorizedAccessException) { /* ‏كسابقه. */ }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// <b>القياسُ الحاكم:</b> الوسمُ المُعلَن هو <c>ecdsa-sha256</c>، <b>والقيمةُ
    /// أربعةٌ وستّون بايتاً</b> — أي <c>r‖s</c> لمنحنًى من ٢٥٦ بت كما يُلزم <c>RFC 6931</c>.
    ///
    /// <para><b>وDER يُستبعَد صراحةً</b> — <b>ببنيته لا بأوّل بايت</b> (<see cref="IsDerEcdsaSignature"/>):
    /// القيمةُ لا تُقرأ تسلسلاً كاملاً من عددين صحيحين. <b>فالنفيُ مقيسٌ لا مُفترَض</b>.</para>
    /// </summary>
    [Fact]
    public async Task TheSignatureValue_IsP1363_AsTheDeclaredAlgorithmRequires()
    {
        var signature = await SignedDocumentAsync();

        Assert.Equal(EcdsaSha256, signature.Method);
        Assert.Equal(64, signature.Value.Length);
        Assert.False(IsDerEcdsaSignature(signature.Value),
            "‏القيمةُ تسلسلُ DER كاملٌ من عددين صحيحين — والوسمُ المُعلَن يُلزم r‖s بطولٍ ثابت.");
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٦٧) <b>توقيعُ DER يُعرَف ببنيته — تسلسلٌ من عددين صحيحين يستغرق القيمةَ كلَّها — لا
    /// بأوّل بايت.</b>
    ///
    /// <para><b>ما كان:</b> <c>Assert.NotEqual(0x30, signature.Value[0])</c>. <b>و<c>r</c> في <c>r‖s</c> عشوائيّ</b>
    /// (ECDSA يختار <c>k</c> عشوائيّاً في كلّ توقيع)، فأوّلُ بايتٍ منه <c>0x30</c> مرّةً في كلّ ٢٥٦ توقيعاً تقريباً — <b>فيسقط
    /// الاختبارُ على توقيع P1363 صحيحٍ تماماً</b>. وقد سقط فعلاً: «Expected: Not 48 · Actual: 48» في بوّابات شجرة وثائق
    /// ح-٦٦ (2026-09-26 21:14)، والحزمةُ نفسُها خضراءُ قبلها وبعدها.</para>
    /// </summary>
    internal static bool IsDerEcdsaSignature(byte[] value)
    {
        try
        {
            var reader = new AsnReader(value, AsnEncodingRules.DER);
            var sequence = reader.ReadSequence();
            sequence.ReadInteger();
            sequence.ReadInteger();
            sequence.ThrowIfNotEmpty();
            reader.ThrowIfNotEmpty();
            return true;
        }
        catch (AsnContentException)
        {
            // ‏ليست بنيةَ DER — وهذا هو الجوابُ المطلوب لا خطأٌ يُبتلع.
            return false;
        }
    }

    /// <summary>
    /// <b>الحالةُ التي سقط عليها الحكمُ القديم — مصنوعةً لا منتظَرة:</b> توقيعُ P1363 <b>صحيحٌ يُتحقَّق منه</b> وأوّلُ بايتٍ
    /// فيه <c>0x30</c>. يُوقَّع حتى يظهر (نحو ٢٥٦ محاولةً في المتوسّط؛ والسقفُ يجعل الإخفاقَ دون ‎e⁻⁷⁸).
    /// </summary>
    [Fact]
    public void AP1363SignatureThatBeginsWith0x30_IsNotMistakenForDer()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var data = new byte[32];
        byte[]? found = null;
        for (var i = 0; i < 20_000 && found is null; i++)
        {
            BitConverter.GetBytes(i).CopyTo(data, 0);
            var candidate = key.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            if (candidate[0] == 0x30)
                found = candidate;
        }

        Assert.NotNull(found);
        Assert.True(key.VerifyData(data, found!, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        Assert.Equal(64, found!.Length);
        Assert.False(IsDerEcdsaSignature(found));
    }

    /// <summary><b>شاهدُ ضبطٍ موجب:</b> توقيعُ DER حقيقيٌّ يُعرَف DER — وإلّا فالحكمُ أعلاه يقول «لا» لكلّ شيء.</summary>
    [Fact]
    public void ARealDerSignature_IsRecognisedAsDer()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var der = key.SignData(new byte[32], HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

        Assert.True(IsDerEcdsaSignature(der));
    }

    /// <summary>
    /// <b>ويُتحقَّق بالصيغة مُسمّاةً لا بالافتراض.</b>
    ///
    /// <para>التحقّقُ هنا يُمرّر <c>IeeeP1363FixedFieldConcatenation</c> <b>صراحةً</b> —
    /// فلو بُدّل المُوقِّعُ إلى DER لَسقط هذا <b>ولو بُدّل المُتحقِّقُ معه</b>. <b>وهذا
    /// ما يُخرج القياسَ من دائرة «كلاهما يستعمل الافتراضَ نفسَه».</b></para>
    /// </summary>
    [Fact]
    public async Task TheSignature_VerifiesUnderTheExplicitlyNamedFormat()
    {
        var signature = await SignedDocumentAsync();

        using var cert = X509CertificateLoader.LoadCertificate(signature.CertificateDer);
        using var ecdsa = cert.GetECDsaPublicKey()!;

        Assert.True(ecdsa.VerifyData(
            signature.SignedInfoCanonical,
            signature.Value,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    }

    /// <summary>
    /// <b>وشاهدُ ضبط — الصيغةُ الأخرى تُرفض.</b>
    ///
    /// <para>لولاه لجاز أن يكون التحقّقُ أعلاه متساهلاً يقبل الصيغتين، <b>فلا يُثبت
    /// شيئاً</b>. فيُعاد التحقّقُ بـ<c>Rfc3279DerSequence</c> ويجب أن <b>يفشل</b>.</para>
    /// </summary>
    [Fact]
    public async Task TheSameSignature_DoesNotVerifyAsDer()
    {
        var signature = await SignedDocumentAsync();

        using var cert = X509CertificateLoader.LoadCertificate(signature.CertificateDer);
        using var ecdsa = cert.GetECDsaPublicKey()!;

        Assert.False(ecdsa.VerifyData(
            signature.SignedInfoCanonical,
            signature.Value,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private sealed record SignatureUnderTest(
        string Method, byte[] Value, byte[] CertificateDer, byte[] SignedInfoCanonical);

    /// <summary>
    /// مستندٌ <b>مُرحَّلٌ حقيقيّ</b> يُقرأ من القرص — لا XML مصنوعٌ في الاختبار.
    /// </summary>
    private async Task<SignatureUnderTest> SignedDocumentAsync()
    {
        await SetupAsync();

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 9));
        invoice.AddLine("خدمة", 1m, 1_000m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var doc = await _db.ZatcaEInvoiceDocuments.SingleAsync(
            d => d.DocumentType == ZatcaDocumentType.SalesInvoice && d.SourceDocumentId == invoice.Id);

        var path = string.IsNullOrWhiteSpace(doc.SignedXmlFilePath) ? doc.XmlFilePath : doc.SignedXmlFilePath!;
        var xml = XDocument.Load(new MemoryStream(File.ReadAllBytes(path)));

        var sig = xml.Descendants(Ds + "Signature").First();
        var method = (string)sig.Descendants(Ds + "SignatureMethod").First().Attribute("Algorithm")!;
        var value = Convert.FromBase64String(sig.Element(Ds + "SignatureValue")!.Value);
        var certB64 = sig.Descendants(Ds + "X509Certificate").First().Value;

        return new SignatureUnderTest(
            method, value, Convert.FromBase64String(certB64), CanonicalSignedInfo(path));
    }

    /// <summary>
    /// <b>يُقنَّن <c>SignedInfo</c> <u>داخل مستنده</u> — لا منتزعاً.</b>
    ///
    /// <para><b>وأوّلُ صياغةٍ لهذا الاختبار كتبته منتزعاً</b> بـ<c>XmlWriter</c> فسقط
    /// التحقّق. <b>والخطأُ كان في القياس لا في المنتَج</b>: العنصرُ يرث إعلاناتِ الفضاءات
    /// من الجذر، فقَنْوَنتُه منتزعاً تُنتج بايتاتٍ غيرَ التي وُقِّع عليها.</para>
    ///
    /// <para><b>وهو الفخُّ نفسُه المسجَّل في <c>ZatcaSignatureVerifier</c> (CEEF-229 · Z-2)</b>
    /// بنصّه: «لو قُنّن منتزعاً لكان التوقيعُ صحيحاً عندنا وباطلاً عند الهيئة». <b>فوقعتُ
    /// فيه وقد كان مكتوباً</b> — وكشفه القياسُ لا القراءة.</para>
    /// </summary>
    private static byte[] CanonicalSignedInfo(string signedXmlPath)
    {
        var xmlDoc = new System.Xml.XmlDocument { PreserveWhitespace = true };
        xmlDoc.Load(signedXmlPath);

        var ns = new System.Xml.XmlNamespaceManager(xmlDoc.NameTable);
        ns.AddNamespace("ds", Ds.NamespaceName);

        var nodes = xmlDoc.SelectNodes(
            "(//. | //@* | //namespace::*)[ancestor-or-self::ds:SignedInfo]", ns);
        Assert.NotNull(nodes);
        Assert.NotEqual(0, nodes!.Count);

        var transform = new System.Security.Cryptography.Xml.XmlDsigC14NTransform();
        transform.LoadInput(nodes);
        using var stream = (Stream)transform.GetOutput(typeof(Stream));
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة الختم", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        ZatcaReadySeller.Complete(company);

        var customer = Customer.Create(_companyId, "عميل", "311111111100003");
        customer.UpdateRegulatoryIdentity(
            "مؤسسة العميل التجارية",
            Brqosh.Domain.Companies.NationalAddress.Create(
                buildingNumber: "4321", streetName: "طريق العليا", districtName: "الملز",
                cityName: "الرياض", postalZone: "54321", countrySubentity: "منطقة الرياض",
                additionalNumber: "8765"));
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;

        var (certPath, keyPath) = ZatcaSignatureVerifier.CreateSigningCertificate(_workDirectory);
        var settings = await _db.ZatcaSettings.SingleAsync(s => s.CompanyId == _companyId);
        settings.Update(isEnabled: true, ZatcaEnvironment.Simulation, "", certPath, keyPath, "");
        await _db.SaveChangesAsync();
    }
}
