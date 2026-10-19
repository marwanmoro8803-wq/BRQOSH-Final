using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Zatca;

namespace Brqosh.Application.Tests.Zatca;

/// <summary>
/// (CEEF-237 · ز-١) <b>الـCSR يُفكّ ترميزُه ويُقرأ — لا يُفترض صحيحاً لأنّه بُني بلا استثناء.</b>
///
/// <para><b>ما كان:</b> الـCSR يحمل <c>KeyUsage</c> وحدَه؛ <b>ولا SAN ولا امتدادَ قالبِ شهادة</b>،
/// وهما ما تقرأ منهما الهيئةُ الرقمَ الضريبيَّ ونوعَ الفاتورة ومعرّفَ وحدة الإصدار. وكان
/// <c>serial</c> يُبنى ثمّ يُهمَل — أثرُ نيّةٍ لم تُوصَل.</para>
///
/// <para><b>وأثرُه:</b> طلبُ شهادةِ الامتثال يُرفض عند الهيئة، فلا CSID ولا ربطَ للمرحلة الثانية.
/// <b>ولا يكشفه بناءٌ ولا اختبارٌ يكتفي بأنّ الـCSR ليس فارغاً</b> — يكشفه أوّلُ طلبٍ حقيقيّ،
/// أي عند العميل.</para>
///
/// <para><b>فهذه عضّة:</b> تفكّ ترميزَ الـDER وتؤكّد كلَّ سمةٍ بقيمتها. حذفُ أيّ امتدادٍ
/// من <c>GenerateKeyPairAndCsr</c> يُسقط هذه الاختبارات.</para>
/// </summary>
public class ZatcaCsrExtensionsTests : IDisposable
{
    private readonly string _root;

    public ZatcaCsrExtensionsTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "brqosh-csr-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        Environment.SetEnvironmentVariable("BRQOSH_DATA_ROOT", _root);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("BRQOSH_DATA_ROOT", null);
        try { Directory.Delete(_root, recursive: true); } catch { }
        GC.SuppressFinalize(this);
    }

    private const string Vat = "310123456700003";

    private static CertificateRequest LoadCsr(ZatcaKeyMaterial material) =>
        CertificateRequest.LoadSigningRequest(
            Convert.FromBase64String(material.CsrBase64),
            HashAlgorithmName.SHA256,
            CertificateRequestLoadOptions.UnsafeLoadCertificateExtensions);

    private static X509Extension Extension(CertificateRequest request, string oid) =>
        request.CertificateExtensions.SingleOrDefault(e => e.Oid?.Value == oid)
        ?? throw new Xunit.Sdk.XunitException(
            $"امتدادُ {oid} غائبٌ عن الـCSR — والهيئةُ ترفض الطلبَ بدونه. "
            + $"الموجود: {string.Join(", ", request.CertificateExtensions.Select(e => e.Oid?.Value))}");

    /// <summary>يفكّ SAN من نوع directoryName إلى أزواج (OID, value) بالترتيب.</summary>
    private static List<(string Oid, string Value)> ReadDirectoryName(byte[] rawSan)
    {
        var result = new List<(string, string)>();
        var reader = new AsnReader(rawSan, AsnEncodingRules.DER);
        var generalNames = reader.ReadSequence();
        var directoryName = generalNames.ReadSequence(
            new Asn1Tag(TagClass.ContextSpecific, 4, isConstructed: true));
        var rdnSequence = directoryName.ReadSequence();
        while (rdnSequence.HasData)
        {
            var rdn = rdnSequence.ReadSetOf();
            var attribute = rdn.ReadSequence();
            var oid = attribute.ReadObjectIdentifier();
            var value = attribute.ReadCharacterString(UniversalTagNumber.UTF8String);
            result.Add((oid, value));
        }
        return result;
    }

    [Fact]
    public void Csr_CarriesSubjectAltName_WithVatNumberAndInvoiceTypeAndSerial()
    {
        var material = new ZatcaCsidOnboardingService().GenerateKeyPairAndCsr(
            companyId: 1, organizationName: "شركة برقوش", vatNumber: Vat,
            invoiceType: "1100", registeredAddress: "Riyadh", businessCategory: "Software");

        var attributes = ReadDirectoryName(Extension(LoadCsr(material), "2.5.29.17").RawData);
        var byOid = attributes.ToDictionary(a => a.Oid, a => a.Value);

        Assert.Equal(Vat, byOid["0.9.2342.19200300.100.1.1"]);   // UID — الرقم الضريبي
        Assert.Equal("1100", byOid["2.5.4.12"]);                  // title — نوع الفاتورة
        Assert.Equal("Riyadh", byOid["2.5.4.26"]);                // registeredAddress
        Assert.Equal("Software", byOid["2.5.4.15"]);              // businessCategory

        // ‏SN: معرّفُ وحدة الإصدار — لا يكفي وجودُه، بل شكلُه الذي تقرأه الهيئة.
        var serial = byOid["2.5.4.4"];
        Assert.StartsWith("1-BRQOSH-ERP|2-1.0|3-", serial);
        Assert.True(Guid.TryParse(serial.Split("|3-")[1], out _),
            $"الجزءُ الثالث من SN ليس معرّفاً فريداً: {serial}");
    }

    [Theory]
    [InlineData(ZatcaEnvironment.Production, "ZATCA-Code-Signing")]
    [InlineData(ZatcaEnvironment.Simulation, "PREZATCA-Code-Signing")]
    public void Csr_CarriesCertificateTemplate_MatchingTheEnvironment(
        ZatcaEnvironment environment, string expectedTemplate)
    {
        var material = new ZatcaCsidOnboardingService().GenerateKeyPairAndCsr(
            companyId: 2, organizationName: "شركة برقوش", vatNumber: Vat, environment: environment);

        var raw = Extension(LoadCsr(material), "1.3.6.1.4.1.311.20.2").RawData;
        var template = new AsnReader(raw, AsnEncodingRules.DER)
            .ReadCharacterString(UniversalTagNumber.UTF8String);

        Assert.Equal(expectedTemplate, template);
    }

    [Fact]
    public void Csr_KeepsKeyUsageAndP256_AndIsSelfConsistent()
    {
        var material = new ZatcaCsidOnboardingService().GenerateKeyPairAndCsr(
            companyId: 3, organizationName: "شركة برقوش", vatNumber: Vat);

        var request = LoadCsr(material);
        Assert.Contains(request.CertificateExtensions, e => e.Oid?.Value == "2.5.29.15");

        // ‏المنحنى P-256 شرطٌ في المُوقِّع كما في الهيئة — تبديلُه يكسر التوقيع لا الـCSR.
        Assert.Equal("1.2.840.10045.2.1", request.PublicKey.Oid.Value);
        using var key = ECDsa.Create();
        key.ImportFromPem(Brqosh.Infrastructure.Zatca.ZatcaKeyFile.ReadPem(material.PrivateKeyPath));
        Assert.Equal(256, key.KeySize);
    }

    [Fact]
    public void Csr_RefusesAVatNumberThatIsNotFifteenDigits()
    {
        var service = new ZatcaCsidOnboardingService();
        Assert.Throws<AccountingException>(() =>
            service.GenerateKeyPairAndCsr(4, "شركة", "123"));
        // ‏وخمسةَ عشرَ محرفاً غيرَ رقميّة تمرّ قبل هذا الإصلاح — والهيئةُ ترفضها.
        Assert.Throws<AccountingException>(() =>
            service.GenerateKeyPairAndCsr(4, "شركة", "31012345670000X"));
    }
}
