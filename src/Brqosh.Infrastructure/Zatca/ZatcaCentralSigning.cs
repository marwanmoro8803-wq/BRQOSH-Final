using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Zatca;

/// <summary>
/// ‏(ADR 0003 · الخيار 1) <b>الختمُ المركزيّ على الخادم</b>: المحطّةُ تحجز ICV/PIH بقفلها المعتاد داخل معاملة الترحيل،
/// وتبني الـXML غيرَ الموقَّع، ثمّ تطلب ختمَه من الخادم الذي يحمل مفتاحَ CSID — فالسلسلةُ واحدة والمفتاحُ في مكانٍ واحد.
/// </summary>
public interface IZatcaRemoteSigner
{
    Task<ZatcaSignResult> SignAsync(long companyId, byte[] unsignedXml, CancellationToken ct = default);
}

public record ZatcaSignRequest(long CompanyId, string UnsignedXmlBase64);

public record ZatcaSignResponse(
    string SignedXmlBase64, string InvoiceHashBase64, string SignatureBase64,
    string PublicKeyDerBase64, string CertificateSignatureBase64)
{
    public static ZatcaSignResponse From(ZatcaSignResult r) => new(
        Convert.ToBase64String(r.SignedXml), r.InvoiceHashBase64, r.SignatureBase64,
        r.PublicKeyDerBase64, r.CertificateSignatureBase64);

    public ZatcaSignResult ToResult() => new(
        Convert.FromBase64String(SignedXmlBase64), InvoiceHashBase64, SignatureBase64,
        PublicKeyDerBase64, CertificateSignatureBase64);
}

/// <summary>نتيجةُ الطلب على الخادم — رمزُ HTTP وجوابٌ أو سبب.</summary>
public record ZatcaSignOutcome(int StatusCode, ZatcaSignResponse? Response, string? Error);

public static class ZatcaCentralSigning
{
    /// <summary>عنوانُ خادم الختم على المحطّة (مثل https://server:5443). غيابُه = الختمُ محلّيٌّ كما كان.</summary>
    public const string UrlVariable = "BRQOSH_ZATCA_SIGNER_URL";

    /// <summary>سرٌّ مشتركٌ بين المحطّات والخادم. وعلى الخادم: غيابُه يُغلق نقطةَ الختم.</summary>
    public const string TokenVariable = "BRQOSH_ZATCA_SIGNER_TOKEN";

    public const string TokenHeader = "X-Brqosh-Signer-Token";
    public const string Route = "/zatca-signer/sign";

    private static readonly AsyncLocal<IZatcaRemoteSigner?> Override = new();
    private static readonly Lazy<IZatcaRemoteSigner?> FromEnvironment = new(() =>
    {
        var url = Environment.GetEnvironmentVariable(UrlVariable);
        var token = Environment.GetEnvironmentVariable(TokenVariable);
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(token))
            return null;
        return new HttpZatcaRemoteSigner(new HttpClient { BaseAddress = new Uri(url), Timeout = TimeSpan.FromSeconds(20) }, token);
    });

    /// <summary>موقِّعُ الخادم لهذه المحطّة، أو null فيبقى الختمُ محلّياً.</summary>
    public static IZatcaRemoteSigner? Current => Override.Value ?? FromEnvironment.Value;

    /// <summary>للاختبار: موقِّعٌ بديلٌ في هذا السياق غير المتزامن.</summary>
    internal static IDisposable Use(IZatcaRemoteSigner? signer)
    {
        var previous = Override.Value;
        Override.Value = signer;
        return new Restore(() => Override.Value = previous);
    }

    public static string? ServerToken() => Environment.GetEnvironmentVariable(TokenVariable);

    /// <summary>
    /// ‏<b>معالجةُ طلب الختم على الخادم</b>: السرُّ أوّلاً (مقارنةٌ ثابتةُ الزمن، وبلا سرٍّ مُعَدٍّ تُغلق النقطة)، ثمّ الختم.
    /// </summary>
    public static async Task<ZatcaSignOutcome> HandleAsync(
        ZatcaCentralSigner signer, ZatcaSignRequest request, string? presentedToken, string? expectedToken, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(expectedToken))
            return new(503, null, "خادم الختم غير مُعَدّ.");
        if (presentedToken is null
            || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(presentedToken), Encoding.UTF8.GetBytes(expectedToken)))
            return new(401, null, "سرّ خادم الختم غير صحيح.");

        byte[] xml;
        try
        {
            xml = Convert.FromBase64String(request.UnsignedXmlBase64 ?? "");
        }
        catch (FormatException)
        {
            return new(400, null, "المستند ليس Base64.");
        }

        try
        {
            return new(200, ZatcaSignResponse.From(await signer.SignAsync(request.CompanyId, xml, ct)), null);
        }
        catch (ZatcaSigningException ex)
        {
            return new(422, null, ex.Message);
        }
    }

    private sealed class Restore(Action undo) : IDisposable
    {
        public void Dispose() => undo();
    }
}

/// <summary>
/// ‏<b>الموقِّعُ على الخادم</b>: يختم بمفتاح CSID المسجَّل على الخادم، ولا يختم إلا مستنداً بائعُه هذه المنشأةُ نفسُها.
/// لا يملك التسلسل — ICV/PIH محجوزان في معاملة المحطّة تحت القفل.
/// </summary>
public sealed class ZatcaCentralSigner(BrqoshDbContext db, Func<ZatcaSettings, byte[], ZatcaSignResult>? sign = null)
{
    private static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";

    public async Task<ZatcaSignResult> SignAsync(long companyId, byte[] unsignedXml, CancellationToken ct = default)
    {
        var vat = await db.Companies.AsNoTracking().Where(c => c.Id == companyId)
                      .Select(c => c.VatRegistrationNumber).SingleOrDefaultAsync(ct)
                  ?? throw new ZatcaSigningException("المنشأة غير موجودة على خادم الختم.");

        string? seller;
        try
        {
            seller = XDocument.Load(new MemoryStream(unsignedXml))
                .Descendants(Cac + "AccountingSupplierParty")
                .Descendants(Cac + "PartyTaxScheme")
                .Select(e => e.Element(Cbc + "CompanyID")?.Value)
                .FirstOrDefault();
        }
        catch (System.Xml.XmlException ex)
        {
            throw new ZatcaSigningException("المستند المطلوب ختمه ليس XML صالحاً.", ex);
        }

        if (!string.Equals(seller?.Trim(), vat, StringComparison.Ordinal))
            throw new ZatcaSigningException("خادم الختم لا يختم مستنداً لبائعٍ غير هذه المنشأة.");

        var settings = await db.ZatcaSettings.AsNoTracking().FirstOrDefaultAsync(s => s.CompanyId == companyId, ct)
                       ?? throw new ZatcaSigningException("لا إعدادات فوترة إلكترونية لهذه المنشأة على خادم الختم.");

        if (sign is not null)
            return sign(settings, unsignedXml);
        return ZatcaXmlSigner.TrySignWithResult(unsignedXml, settings)
               ?? throw new ZatcaSigningException("خادم الختم بلا شهادة CSID — أكمِل تسجيل الشهادة على الخادم.");
    }
}

/// <summary>‏عميلُ المحطّة: يطلب الختمَ من الخادم، وأيُّ رفضٍ يُسقط الترحيلَ كلَّه فلا يُستهلك ICV.</summary>
public sealed class HttpZatcaRemoteSigner(HttpClient http, string token) : IZatcaRemoteSigner
{
    public async Task<ZatcaSignResult> SignAsync(long companyId, byte[] unsignedXml, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, ZatcaCentralSigning.Route)
        {
            Content = JsonContent.Create(new ZatcaSignRequest(companyId, Convert.ToBase64String(unsignedXml))),
        };
        request.Headers.Add(ZatcaCentralSigning.TokenHeader, token);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new ZatcaSigningException($"تعذّر الوصول إلى خادم الختم: {ex.Message}", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw new ZatcaSigningException(
                    $"رفض خادم الختم الطلب ({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync(ct)}");
            var body = await response.Content.ReadFromJsonAsync<ZatcaSignResponse>(ct)
                       ?? throw new ZatcaSigningException("ردّ خادم الختم فارغ.");
            return body.ToResult();
        }
    }
}
