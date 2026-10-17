using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Zatca;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Zatca;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١٢١ · ‏`tax-zatca#8`) <b>عطلُ الخادم العابرُ ليس رفضاً — والمعلَّقُ يُعاد إرسالُه دفعةً واحدة.</b>
///
/// <para>‏كان أيُّ ردٍّ غيرِ ناجحٍ يُعلّم المستندَ «مرفوضاً»: ٥٠٣ (الخادم غير متاح) و٤٢٩ (تجاوز الحدّ) و٥٠٠ كلُّها. و«المرفوض»
/// ‏لا يلتقطه إرسالٌ لاحق — <b>ففاتورةٌ سليمةٌ تسقط من الهيئة لأنّ خادمها تعطّل دقيقة</b>. ولا مسارَ لإعادة إرسال المعلَّق
/// ‏إلّا مستنداً مستنداً من الشاشة.</para>
/// </summary>
[Collection("SequentialEnv")]
public class ATransientFailureIsNotARejectionTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "brqosh-zatca-retry-" + Guid.NewGuid().ToString("N"));
    private long _companyId;

    public ATransientFailureIsNotARejectionTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        ZatcaSubmissionService.HandlerOverride = null;
        TestActor.None();
        _db.Dispose();
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, ZatcaSubmissionService.HttpFailureKind.Rejected)]
    [InlineData(HttpStatusCode.Conflict, ZatcaSubmissionService.HttpFailureKind.Rejected)]
    [InlineData(HttpStatusCode.Unauthorized, ZatcaSubmissionService.HttpFailureKind.Configuration)]
    [InlineData(HttpStatusCode.Forbidden, ZatcaSubmissionService.HttpFailureKind.Configuration)]
    [InlineData(HttpStatusCode.RequestTimeout, ZatcaSubmissionService.HttpFailureKind.Transient)]
    [InlineData(HttpStatusCode.TooManyRequests, ZatcaSubmissionService.HttpFailureKind.Transient)]
    [InlineData(HttpStatusCode.InternalServerError, ZatcaSubmissionService.HttpFailureKind.Transient)]
    [InlineData(HttpStatusCode.BadGateway, ZatcaSubmissionService.HttpFailureKind.Transient)]
    [InlineData(HttpStatusCode.ServiceUnavailable, ZatcaSubmissionService.HttpFailureKind.Transient)]
    [InlineData(HttpStatusCode.GatewayTimeout, ZatcaSubmissionService.HttpFailureKind.Transient)]
    public void A_failure_status_is_classified_by_what_it_says_about_the_document(
        HttpStatusCode code, ZatcaSubmissionService.HttpFailureKind kind) =>
        Assert.Equal(kind, ZatcaSubmissionService.ClassifyFailure(code));

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task A_server_or_credential_failure_leaves_the_document_pending(HttpStatusCode code)
    {
        await ConfigureAsync();
        var doc = await PendingAsync(1);
        Respond(code, "{}");

        await Assert.ThrowsAsync<AccountingException>(() => new ZatcaSubmissionService(_db).SubmitAsync(doc.Id));

        var reloaded = await Reload(doc.Id);
        Assert.Equal(ZatcaComplianceStatus.ReadyToReport, reloaded.ComplianceStatus);
        Assert.Contains("autoSubmitFailed", reloaded.ZatcaResponseJson, StringComparison.Ordinal);
    }

    /// <summary>شاهدُ ضبط: ٤٠٠ حكمٌ على المستند — يبقى «مرفوضاً» كما كان.</summary>
    [Fact]
    public async Task A_bad_request_is_still_a_rejection()
    {
        await ConfigureAsync();
        var doc = await PendingAsync(1);
        Respond(HttpStatusCode.BadRequest, "{\"validationResults\":{\"status\":\"ERROR\"}}");

        await Assert.ThrowsAsync<AccountingException>(() => new ZatcaSubmissionService(_db).SubmitAsync(doc.Id));

        Assert.Equal(ZatcaComplianceStatus.Rejected, (await Reload(doc.Id)).ComplianceStatus);
    }

    [Fact]
    public async Task Pending_documents_are_resubmitted_in_chain_order()
    {
        await ConfigureAsync();
        var first = await PendingAsync(1);
        var second = await PendingAsync(2);
        Respond(HttpStatusCode.OK, "{\"reportingStatus\":\"REPORTED\"}");

        var result = await new ZatcaSubmissionService(_db).ResubmitPendingAsync(_companyId);

        Assert.Equal(2, result.Submitted);
        Assert.Equal(0, result.StillPending);
        Assert.Equal(ZatcaComplianceStatus.Reported, (await Reload(first.Id)).ComplianceStatus);
        Assert.Equal(ZatcaComplianceStatus.Reported, (await Reload(second.Id)).ComplianceStatus);
    }

    /// <summary>‏وعطلٌ عابرٌ يوقف الدفعةَ عند أوّل مستند — فالسلسلةُ لا تُرسل لاحقاً قبل سابقه، ولا يضيع شيء.</summary>
    [Fact]
    public async Task A_transient_failure_stops_the_batch_and_keeps_everything_pending()
    {
        await ConfigureAsync();
        await PendingAsync(1);
        await PendingAsync(2);
        Respond(HttpStatusCode.ServiceUnavailable, "{}");

        var result = await new ZatcaSubmissionService(_db).ResubmitPendingAsync(_companyId);

        Assert.Equal(0, result.Submitted);
        Assert.Equal(2, result.StillPending);
        Assert.NotNull(result.LastError);
        Assert.Equal(2, await _db.ZatcaEInvoiceDocuments.CountAsync(d => d.ComplianceStatus == ZatcaComplianceStatus.ReadyToReport));
    }

    /// <summary>
    /// ‏(ح-١٥٦) <b>المؤقّتُ الخلفيُّ يُرسل والجالسُ أمينُ صندوق</b> — ومسارُ الزرّ يرفضه كما كان. الإذنُ مُنح عند ترحيل المستند،
    /// ‏فالمحطّةُ التي يعمل عليها أمينُ الصندوق وحدَه لا تُبقي مبيعاتِها معلّقةً حتى يمرّ محاسب.
    /// </summary>
    [Fact]
    public async Task The_background_resubmission_runs_for_a_cashier_while_the_button_refuses()
    {
        await ConfigureAsync();
        var doc = await PendingAsync(1);
        Respond(HttpStatusCode.OK, "{\"reportingStatus\":\"REPORTED\"}");
        TestActor.Cashier();

        await Assert.ThrowsAsync<AccountingException>(() => new ZatcaSubmissionService(_db).ResubmitPendingAsync(_companyId));
        var result = await new ZatcaSubmissionService(_db).ResubmitPendingInBackgroundAsync(_companyId);

        Assert.Equal(1, result.Submitted);
        Assert.False(result.Skipped);
        Assert.Equal(ZatcaComplianceStatus.Reported, (await Reload(doc.Id)).ComplianceStatus);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private static void Respond(HttpStatusCode code, string body) =>
        ZatcaSubmissionService.HandlerOverride = new FixedResponse(code, body);

    private async Task<ZatcaEInvoiceDocument> Reload(long id)
    {
        _db.ChangeTracker.Clear();
        return await _db.ZatcaEInvoiceDocuments.SingleAsync(d => d.Id == id);
    }

    private async Task<ZatcaEInvoiceDocument> PendingAsync(int icv)
    {
        var xml = Path.Combine(_dir, $"inv-{icv}.xml");
        await File.WriteAllTextAsync(xml, "<Invoice/>");
        var doc = ZatcaEInvoiceDocument.Create(
            _companyId, ZatcaDocumentType.SalesInvoice, sourceDocumentId: icv,
            invoiceNumber: $"INV-{icv}", invoiceUuid: Guid.NewGuid(), icv: icv,
            previousHash: "cHJldmlvdXM=", invoiceHash: $"hash-{icv}",
            mode: ZatcaTransmissionMode.Reporting, qrTlv: "qr", xmlPath: xml, signedXmlPath: null);
        _db.ZatcaEInvoiceDocuments.Add(doc);
        await _db.SaveChangesAsync();
        return doc;
    }

    private async Task ConfigureAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700003", BusinessProfile.General);
        _companyId = company.Id;
        await ZatcaReadySeller.CompleteAsync(_db, _companyId);

        Directory.CreateDirectory(_dir);
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=BRQOSH CSID Test", ecdsa, HashAlgorithmName.SHA256);
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        var certPath = Path.Combine(_dir, "csid.crt.pem");
        var keyPath = Path.Combine(_dir, "csid.key.pem");
        await File.WriteAllTextAsync(certPath, cert.ExportCertificatePem());
        await File.WriteAllTextAsync(keyPath, ecdsa.ExportECPrivateKeyPem());

        var settings = await _db.ZatcaSettings.SingleAsync(s => s.CompanyId == _companyId);
        settings.Update(isEnabled: true, ZatcaEnvironment.Production,
            "https://example.invalid/zatca", certPath, keyPath, "token");
        await _db.SaveChangesAsync();
    }

    private sealed class FixedResponse(HttpStatusCode code, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }
}
