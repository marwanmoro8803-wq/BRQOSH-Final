using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Zatca;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.X509;
using OsslPemReader = Org.BouncyCastle.OpenSsl.PemReader;
using OsslPemWriter = Org.BouncyCastle.OpenSsl.PemWriter;

namespace Brqosh.Application.Tests.Zatca;

/// <summary>
/// (موانعُ الإطلاق) <b>مفتاحُ الختم على <c>secp256k1</c> — المنحنى الذي يأمر به دليلُ الهيئة التقنيّ المنشور
/// (<c>openssl ecparam -name secp256k1 -genkey</c>) — لا <c>nistP256</c>.</b> كان البرنامجُ يولّد NIST وطلبُ CSID به يُرفض.
///
/// <para>تُقاس أربعةُ أمور بلا مُصادِق الهيئة: المفتاحُ على المنحنى الصحيح وبصيغة openssl، والـCSR صحيحٌ التوقيعِ وبالامتدادات
/// ومفتاحُه العامُّ هو مفتاحُ الختم، والتوقيعُ صحيحٌ ويفشل عند العبث، وفاتورةٌ كاملةٌ تُوقَّع بشهادةٍ على هذا المنحنى وتُتحقَّق
/// بمُتحقِّقٍ مستقلّ. <b>وهذا لا يُثبت قبولَ الهيئة</b> — يُثبت الاتّساقَ مع المنحنى الذي تطلبه.</para>
/// </summary>
public class TheStampKeyIsOnTheCurveTheAuthorityRequiresTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "brqosh-k1-" + Guid.NewGuid().ToString("N")[..8]);

    public TheStampKeyIsOnTheCurveTheAuthorityRequiresTests()
    {
        Directory.CreateDirectory(_root);
        Environment.SetEnvironmentVariable("BRQOSH_DATA_ROOT", _root);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("BRQOSH_DATA_ROOT", null);
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void TheGeneratedKey_IsSecp256k1_InOpensslFormat()
    {
        var pem = ZatcaSecp256k1.GenerateKeyPem();

        Assert.StartsWith("-----BEGIN EC PRIVATE KEY-----", pem, StringComparison.Ordinal);
        Assert.True(ZatcaSecp256k1.IsSecp256k1Key(pem));

        // معاملاتُ المنحنى المسمّى داخل البنية: [0] OID = 1.3.132.0.10
        var base64 = string.Join("", pem.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("-----", StringComparison.Ordinal)));
        var sequence = (Asn1Sequence)Asn1Object.FromByteArray(Convert.FromBase64String(base64));
        var parameters = sequence.OfType<Asn1TaggedObject>().First(t => t.TagNo == 0);
        Assert.Equal(ZatcaSecp256k1.CurveOid, DerObjectIdentifier.GetInstance(parameters, true).Id);
    }

    [Fact]
    public void ANistKey_IsNotTakenForSecp256k1()
    {
        using var p256 = System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        Assert.False(ZatcaSecp256k1.IsSecp256k1Key(p256.ExportECPrivateKeyPem()));
        Assert.False(ZatcaSecp256k1.IsSecp256k1Key("ليس مفتاحاً"));
    }

    [Fact]
    public void TheOnboardingKey_AndItsCsr_AreOnTheRequiredCurve_AndSelfConsistent()
    {
        var material = new ZatcaCsidOnboardingService().GenerateKeyPairAndCsr(1, "شركة تجريبية", "310123456700003");
        var keyPem = ZatcaKeyFile.ReadPem(material.PrivateKeyPath);
        Assert.True(ZatcaSecp256k1.IsSecp256k1Key(keyPem));

        var csr = new Pkcs10CertificationRequest(Convert.FromBase64String(material.CsrBase64));
        Assert.True(csr.Verify(), "توقيعُ الـCSR لا يطابق مفتاحَه العامّ.");
        Assert.Equal(
            ZatcaSecp256k1.PublicKeyInfo(keyPem),
            Org.BouncyCastle.X509.SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(csr.GetPublicKey()).GetEncoded());

        var subject = csr.GetCertificationRequestInfo().Subject.ToString();
        Assert.Contains("C=SA", subject, StringComparison.Ordinal);
        Assert.Contains("O=شركة تجريبية", subject, StringComparison.Ordinal);

        var attributes = csr.GetCertificationRequestInfo().Attributes!;
        var attribute = Org.BouncyCastle.Asn1.Cms.Attribute.GetInstance(attributes[0]);
        var ext = X509Extensions.GetInstance(attribute.AttrValues[0]);
        foreach (var oid in new[] { "2.5.29.15", "2.5.29.17", "1.3.6.1.4.1.311.20.2" })
            Assert.NotNull(ext.GetExtension(new DerObjectIdentifier(oid)));
    }

    [Fact]
    public void TheSignature_IsP1363_AndVerifies_AndFailsWhenTampered()
    {
        var pem = ZatcaSecp256k1.GenerateKeyPem();
        var data = System.Text.Encoding.UTF8.GetBytes("SignedInfo");
        var sig = ZatcaSecp256k1.SignP1363(data, pem);
        var spki = ZatcaSecp256k1.PublicKeyInfo(pem);

        Assert.Equal(64, sig.Length);
        Assert.True(ZatcaSecp256k1.VerifyP1363(data, sig, spki));
        Assert.False(ZatcaSecp256k1.VerifyP1363(System.Text.Encoding.UTF8.GetBytes("SignedInfp"), sig, spki));
    }

    [Fact]
    public void AFullInvoice_IsSignedWithTheSecp256k1Key_AndVerifiesIndependently()
    {
        // شهادةٌ ذاتيّةُ التوقيع على secp256k1 (تقوم مقامَ شهادة الهيئة) + مفتاحُها.
        var keyPem = ZatcaSecp256k1.GenerateKeyPem();
        using var reader = new StringReader(keyPem);
        var pair = (AsymmetricCipherKeyPair)new OsslPemReader(reader).ReadObject();
        var generator = new X509V3CertificateGenerator();
        generator.SetSerialNumber(BigInteger.ValueOf(DateTime.UtcNow.Ticks));
        generator.SetSubjectDN(new X509Name("CN=BRQOSH k1 Test, O=BRQOSH"));
        generator.SetIssuerDN(new X509Name("CN=BRQOSH k1 Test, O=BRQOSH"));
        generator.SetNotBefore(DateTime.UtcNow.AddDays(-1));
        generator.SetNotAfter(DateTime.UtcNow.AddYears(1));
        generator.SetPublicKey(pair.Public);
        var cert = generator.Generate(new Asn1SignatureFactory("SHA256withECDSA", pair.Private));

        var certPath = Path.Combine(_root, "k1.crt.pem");
        using (var w = new StringWriter())
        {
            new OsslPemWriter(w).WriteObject(cert);
            File.WriteAllText(certPath, w.ToString());
        }

        var keyPath = Path.Combine(_root, "k1.key.pem");
        File.WriteAllText(keyPath, keyPem);

        var xml = ZatcaUblInvoiceBuilder.BuildXml(new ZatcaUblInvoiceBuilder.InvoiceData(
            InvoiceNumber: "INV-2026-000001", Uuid: Guid.NewGuid(),
            InvoiceDate: new DateOnly(2026, 3, 14), IssueTime: new TimeOnly(12, 0, 0),
            SellerName: "منشأة الاختبار", SellerVat: "310123456700003", CustomerName: "", CustomerVat: null,
            Subtotal: 1000m, VatAmount: 150m, GrandTotal: 1150m,
            Lines: [new SalesInvoiceLine("صنف", 1m, 1000m)], Icv: "7",
            PreviousInvoiceHash: "NWZlY2ViNjZmZmM4NmYzOGQ5NTI3ODZjNmQ2OTZjNzljMmRiYzIzOWRkNGU5MWI0NjcyOWQ3M2EyN2ZiNTdlOQ==",
            QrTlvBase64: "AQ5UZXN0AgsxMjM0NTY3ODkwMQ=="));

        var signed = ZatcaXmlSigner.SignWithResult(xml, certPath, keyPath);
        var result = ZatcaSignatureVerifier.Verify(signed.SignedXml);

        Assert.True(result.IsValid, result.ToString());
        Assert.Equal(64, Convert.FromBase64String(signed.SignatureBase64).Length);
    }
}
