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
    /// <para><b>وDER يُستبعَد صراحةً</b>: تسلسلُ DER يبدأ بـ<c>0x30</c> ويتجاوز طولُه
    /// الأربعةَ والستّين — <b>فالنفيُ مقيسٌ لا مُفترَض</b>.</para>
    /// </summary>
    [Fact]
    public async Task TheSignatureValue_IsP1363_AsTheDeclaredAlgorithmRequires()
    {
        var signature = await SignedDocumentAsync();

        Assert.Equal(EcdsaSha256, signature.Method);
        Assert.Equal(64, signature.Value.Length);
        Assert.NotEqual(0x30, signature.Value[0]);
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
