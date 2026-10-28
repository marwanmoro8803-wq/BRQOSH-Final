using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Zatca;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Zatca;

/// <summary>
/// ‏(ADR 0003) <b>المحطّةُ بلا مفتاح تختم عبر خادم الختم</b>، والتسلسلُ ICV/PIH يبقى محجوزاً في معاملتها تحت القفل.
/// المفتاحُ هنا شهادةٌ ذاتيّةُ التوقيع للاختبار — وتسجيلُ CSID الحقيقيّ على الخادم ينتظر OTP بوابة فاتورة.
/// </summary>
public class StationsStampThroughTheCentralSignerTests : IDisposable
{
    private const string Token = "station-and-server-shared-secret";
    private readonly BrqoshDbContext _db;
    private readonly string _work = Path.Combine(Path.GetTempPath(), $"brqosh-central-{Guid.NewGuid():N}");

    public StationsStampThroughTheCentralSignerTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Database.CloseConnection();
        _db.Dispose();
        try { Directory.Delete(_work, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task AStationWithoutAKey_IssuesSignedInvoices_ThroughTheServer_AndTheChainHolds()
    {
        var (companyId, customerId) = await SeedAsync();
        using var _ = ZatcaCentralSigning.Use(Station(ServerSigner()));

        for (var i = 0; i < 2; i++)
        {
            var invoice = SalesInvoice.CreateDraft(companyId, customerId, new DateOnly(2026, 7, 9));
            invoice.AddLine($"خدمة {i}", 1m, 100m);
            await new SalesInvoicePostingService(_db).PostAsync(invoice);
        }

        var docs = await _db.ZatcaEInvoiceDocuments.Where(d => d.CompanyId == companyId).OrderBy(d => d.Icv).ToListAsync();
        Assert.Equal([1, 2], docs.Select(d => d.Icv));
        Assert.Equal(docs[0].InvoiceHash, docs[1].PreviousInvoiceHash);
        foreach (var doc in docs)
        {
            Assert.NotNull(doc.SignedXmlFilePath);
            var verified = ZatcaSignatureVerifier.Verify(await File.ReadAllBytesAsync(doc.SignedXmlFilePath!));
            Assert.True(verified.InvoiceDigestMatches && verified.SignedPropertiesDigestMatches && verified.SignatureValid, verified.Detail);
        }
    }

    [Fact]
    public async Task ARefusedStamp_RollsThePostingBack_AndConsumesNoIcv()
    {
        var (companyId, customerId) = await SeedAsync();
        using var _ = ZatcaCentralSigning.Use(Station(ServerSigner(), presentedToken: "wrong"));

        var invoice = SalesInvoice.CreateDraft(companyId, customerId, new DateOnly(2026, 7, 9));
        invoice.AddLine("خدمة", 1m, 100m);
        await Assert.ThrowsAnyAsync<Exception>(() => new SalesInvoicePostingService(_db).PostAsync(invoice));

        _db.ChangeTracker.Clear();
        Assert.False(await _db.ZatcaEInvoiceDocuments.AnyAsync(d => d.CompanyId == companyId));
    }

    [Fact]
    public async Task TheServer_RefusesAWrongOrMissingSecret_AndAnotherSellersDocument()
    {
        var (companyId, _) = await SeedAsync();
        var request = new ZatcaSignRequest(companyId, Convert.ToBase64String("<Invoice/>"u8.ToArray()));

        Assert.Equal(503, (await ZatcaCentralSigning.HandleAsync(ServerSigner(), request, Token, expectedToken: null)).StatusCode);
        Assert.Equal(401, (await ZatcaCentralSigning.HandleAsync(ServerSigner(), request, "wrong", Token)).StatusCode);
        var foreign = await ZatcaCentralSigning.HandleAsync(ServerSigner(), request, Token, Token);
        Assert.Equal(422, foreign.StatusCode);
        Assert.Contains("لبائعٍ غير هذه المنشأة", foreign.Error, StringComparison.Ordinal);
    }

    private ZatcaCentralSigner ServerSigner()
    {
        var (certPath, keyPath) = ZatcaSignatureVerifier.CreateSigningCertificate(Path.Combine(_work, "server"));
        var certPem = File.ReadAllText(certPath);
        var keyPem = File.ReadAllText(keyPath);
        return new ZatcaCentralSigner(_db, (_, xml) => ZatcaXmlSigner.SignPemWithResult(xml, certPem, keyPem));
    }

    /// <summary>عميلُ المحطّة الحقيقيّ، وبروتوكولُ HTTP يُعالَج في العمليّة نفسها بمعالج الخادم.</summary>
    private static HttpZatcaRemoteSigner Station(ZatcaCentralSigner server, string presentedToken = Token) =>
        new(new HttpClient(new InProcessServer(server)) { BaseAddress = new Uri("http://signer.local") }, presentedToken);

    private sealed class InProcessServer(ZatcaCentralSigner server) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal(ZatcaCentralSigning.Route, request.RequestUri!.AbsolutePath);
            var body = await System.Net.Http.Json.HttpContentJsonExtensions.ReadFromJsonAsync<ZatcaSignRequest>(request.Content!, ct);
            var token = request.Headers.TryGetValues(ZatcaCentralSigning.TokenHeader, out var v) ? v.Single() : null;
            var outcome = await ZatcaCentralSigning.HandleAsync(server, body!, token, Token, ct);
            return outcome.Response is { } ok
                ? new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = System.Net.Http.Json.JsonContent.Create(ok) }
                : new HttpResponseMessage((System.Net.HttpStatusCode)outcome.StatusCode) { Content = new StringContent(outcome.Error ?? "") };
        }
    }

    private async Task<(long CompanyId, long CustomerId)> SeedAsync()
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync("منشأة الختم المركزي", "310123456700003", BusinessProfile.Retail);
        company.UpdateRegulatoryIdentity(
            "منشأة الختم المركزي للتجارة", "1010123456",
            Brqosh.Domain.Companies.NationalAddress.Create(
                buildingNumber: "1234", streetName: "طريق الملك فهد", districtName: "العليا",
                cityName: "الرياض", postalZone: "12345", countrySubentity: "منطقة الرياض",
                additionalNumber: "5678"));
        var customer = Customer.Create(company.Id, "عميل نقدي");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        // ‏المحطّةُ مفعَّلةٌ بلا شهادةٍ ولا مفتاح — الختمُ عند الخادم.
        var settings = await _db.ZatcaSettings.SingleAsync(s => s.CompanyId == company.Id);
        settings.Update(isEnabled: true, Brqosh.Domain.Zatca.ZatcaEnvironment.Simulation, "", "", "", "");
        await _db.SaveChangesAsync();
        return (company.Id, customer.Id);
    }
}
