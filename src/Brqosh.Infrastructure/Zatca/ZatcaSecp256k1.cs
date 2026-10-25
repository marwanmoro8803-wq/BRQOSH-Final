using System.Security.Cryptography;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities.IO.Pem;
using Org.BouncyCastle.X509;
using OsslPemReader = Org.BouncyCastle.OpenSsl.PemReader;
using PemWriter = Org.BouncyCastle.OpenSsl.PemWriter;

namespace Brqosh.Infrastructure.Zatca;

/// <summary>
/// (موانعُ الإطلاق · الختم) <b>مفتاحُ الختم على المنحنى الذي تُلزم به الهيئةُ: <c>secp256k1</c> — لا <c>nistP256</c>.</b>
///
/// <para><b>ما كان:</b> الخدمةُ تولّد <c>ECCurve.NamedCurves.nistP256</c> وتعلّل ذلك «ZATCA يتطلّب P-256». <b>والدليلُ الأوّليُّ يخالفه:</b>
/// دليلُ الهيئة التقنيّ المنشور (<i>E-invoicing Detailed Technical Guideline</i>) يأمر حرفاً:
/// <c>openssl ecparam -name secp256k1 -genkey -noout -out PrivateKey.pem</c>. وعبارةُ «P-256» في معيار خصائص الأمان تصف <b>طولَ المفتاح</b>
/// (٢٥٦ بتّاً)، لا اسمَ المنحنى. فطلبُ CSID بمفتاح NIST كان سيُرفض، <b>فلا ربطَ للمرحلة الثانية أصلاً</b>.</para>
///
/// <para><b>ولماذا مكتبةٌ خارجيّة:</b> <c>CNG</c> في ويندوز لا يدعم <c>secp256k1</c> (يرمي <c>PlatformNotSupported</c> عند توليد المفتاح أو استيراده)،
/// فلا طريقَ إليه عبر <c>System.Security.Cryptography</c> على المنصّة التي يعمل عليها البرنامج. فالتوليدُ والـCSR والتوقيعُ هنا بـBouncyCastle
/// (رخصةُ MIT)، <b>وبصيغةٍ مطابقةٍ لمخرجات <c>openssl</c></b>: <c>EC PRIVATE KEY</c> بمعاملات المنحنى المسمّى، وتوقيعٌ بصيغة IEEE P1363 (<c>r‖s</c>) كما
/// يُلزم وسمُ <c>ecdsa-sha256</c> (RFC 6931 §2.3.6).</para>
///
/// <para><b>والمفاتيحُ القديمةُ بـP-256</b> (إن وُجدت عند تنصيبٍ قائم) تبقى تُوقِّع بالمسار القديم ولا تُكسر؛ وتجديدُها بمفتاحٍ جديدٍ يكون <c>secp256k1</c>.</para>
/// </summary>
internal static class ZatcaSecp256k1
{
    internal const string CurveOid = "1.3.132.0.10";

    private static readonly X9ECParameters Curve = SecNamedCurves.GetByName("secp256k1");
    private static readonly ECDomainParameters Domain = new(Curve.Curve, Curve.G, Curve.N, Curve.H);

    /// <summary>مفتاحٌ جديدٌ — يُعاد نصُّه بصيغة <c>EC PRIVATE KEY</c> (كمخرجات <c>openssl ecparam -genkey</c>).</summary>
    public static string GenerateKeyPem()
    {
        var generator = new ECKeyPairGenerator();
        generator.Init(new ECKeyGenerationParameters(Domain, new SecureRandom()));
        var pair = generator.GenerateKeyPair();
        var priv = (ECPrivateKeyParameters)pair.Private;
        var pub = (ECPublicKeyParameters)pair.Public;

        var structure = new ECPrivateKeyStructure(
            Domain.N.BitLength, priv.D, new DerBitString(pub.Q.GetEncoded(false)), new X962Parameters(SecObjectIdentifiers.SecP256k1));

        using var writer = new StringWriter();
        new PemWriter(writer).WriteObject(new PemObject("EC PRIVATE KEY", structure.GetEncoded()));
        return writer.ToString();
    }

    /// <summary>هل هذا المفتاحُ على <c>secp256k1</c>؟ — نصٌّ لا يُفهم أو منحنًى آخر ⟵ <c>false</c> فيسلك المسارَ القديم.</summary>
    public static bool IsSecp256k1Key(string keyPem) => TryReadPrivate(keyPem) is not null;

    private static ECPrivateKeyParameters? TryReadPrivate(string keyPem)
    {
        try
        {
            using var reader = new StringReader(keyPem);
            var parsed = new OsslPemReader(reader).ReadObject();
            var key = parsed switch
            {
                AsymmetricCipherKeyPair pair => pair.Private as ECPrivateKeyParameters,
                ECPrivateKeyParameters direct => direct,
                _ => null,
            };
            return key is not null && key.Parameters.Curve.Equals(Curve.Curve) && key.Parameters.G.Equals(Curve.G) ? key : null;
        }
        catch (Exception)   // ‏نصٌّ لا يُفهم مفتاحاً على secp256k1 ليس خطأً هنا: يسلك المُنادي المسارَ القديم (P-256) أو يرفض بسببه هو.
        {
            return null;
        }
    }

    private static ECPublicKeyParameters PublicOf(ECPrivateKeyParameters priv) =>
        new(priv.Parameters.G.Multiply(priv.D).Normalize(), Domain);

    /// <summary>
    /// طلبُ شهادةٍ PKCS#10 موقَّعٌ بـ<c>ecdsa-with-SHA256</c> على <c>secp256k1</c>، بالامتدادات التي تقرأها الهيئة. القيمةُ لكلّ امتدادٍ هي <b>DER
    /// قيمتِه</b> كما تُنتجها <c>X509Extension.RawData</c> — فالباني المشتركُ لا يتغيّر.
    /// </summary>
    public static byte[] CreateCsr(string keyPem, IReadOnlyList<(string Oid, string Value)> subject,
        IReadOnlyList<(string Oid, bool Critical, byte[] Value)> extensions)
    {
        var priv = TryReadPrivate(keyPem) ?? throw new InvalidOperationException("المفتاحُ ليس على secp256k1.");

        var oids = subject.Select(s => new DerObjectIdentifier(s.Oid)).ToList();
        var values = subject.Select(s => s.Value).ToList();
        var name = new X509Name(oids, values);

        var generator = new X509ExtensionsGenerator();
        foreach (var (oid, critical, value) in extensions)
            generator.AddExtension(new DerObjectIdentifier(oid), critical, value);

        var attribute = new Org.BouncyCastle.Asn1.Cms.Attribute(
            PkcsObjectIdentifiers.Pkcs9AtExtensionRequest, new DerSet(generator.Generate()));

        var csr = new Pkcs10CertificationRequest(
            new Asn1SignatureFactory("SHA256withECDSA", priv), name, PublicOf(priv), new DerSet(attribute));
        return csr.GetEncoded();
    }

    /// <summary>توقيعُ ECDSA-SHA256 على <paramref name="data"/> بصيغة <c>r‖s</c> (٦٤ بايتاً) — كما يُلزم وسمُ ecdsa-sha256.</summary>
    public static byte[] SignP1363(byte[] data, string keyPem)
    {
        var priv = TryReadPrivate(keyPem) ?? throw new InvalidOperationException("المفتاحُ ليس على secp256k1.");
        var signer = new ECDsaSigner(new HMacDsaKCalculator(new Org.BouncyCastle.Crypto.Digests.Sha256Digest()));
        signer.Init(true, priv);
        var rs = signer.GenerateSignature(SHA256.HashData(data));
        return [.. Pad(rs[0]), .. Pad(rs[1])];
    }

    /// <summary>تحقّقٌ بالمفتاح العامّ (SubjectPublicKeyInfo DER) — لأغراض الاختبار ولفحصٍ ذاتيّ للمُوقِّع.</summary>
    public static bool VerifyP1363(byte[] data, byte[] signature, byte[] subjectPublicKeyInfo)
    {
        if (signature.Length != 64)
            return false;
        var pub = (ECPublicKeyParameters)PublicKeyFactory.CreateKey(subjectPublicKeyInfo);
        var verifier = new ECDsaSigner();
        verifier.Init(false, pub);
        return verifier.VerifySignature(
            SHA256.HashData(data), new BigInteger(1, signature[..32]), new BigInteger(1, signature[32..]));
    }

    /// <summary>المفتاحُ العامّ (SubjectPublicKeyInfo DER) لمفتاحٍ خاصّ.</summary>
    public static byte[] PublicKeyInfo(string keyPem)
    {
        var priv = TryReadPrivate(keyPem) ?? throw new InvalidOperationException("المفتاحُ ليس على secp256k1.");
        return Org.BouncyCastle.X509.SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(PublicOf(priv)).GetEncoded();
    }

    private static byte[] Pad(BigInteger value)
    {
        var raw = value.ToByteArrayUnsigned();
        var padded = new byte[32];
        Buffer.BlockCopy(raw, 0, padded, 32 - raw.Length, raw.Length);
        return padded;
    }
}
