using System.Xml.Linq;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Construction;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Construction;
using Brqosh.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Zatca;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٣٦) <b>إشعارٌ دائنٌ لا يقول ماذا يُلغي.</b>
///
/// <para>مستندُ إلغاء المستخلص كان يخرج <b>بلا <c>BillingReference</c> وبلا سبب</b>:
/// <c>BillingCancel</c> <b>يسقط من مُبدِّل <c>ResolveBillingReferenceAsync</c> إلى
/// <c>(null, null)</c></b>، ونداءُ المولّد وحدَه من بين الثلاثة <b>بلا
/// <c>issueReason</c></b>.</para>
///
/// <para><b>والمرجعُ ليس تزييناً</b>: إشعارٌ دائنٌ بلا إشارةٍ إلى مستنده <b>لا يُغلق
/// توريداً لدى الهيئة</b> — يبقى المستخلصُ قائماً في إقرارها وقد أُلغي في الدفاتر.
/// <b>والسببُ حقلٌ يقرؤه المراجع</b>: توريدٌ رُفع من الإقرار بلا تفسير.</para>
/// </summary>
public class CancellingAProgressBillingSaysWhatItCancelsTests : IDisposable
{
    private static readonly XNamespace Cbc =
        "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public CancellingAProgressBillingSaysWhatItCancelsTests()
    {
        TestActor.Administrator();
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
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// <b>القياسُ الحاكم: المستندُ يُشير إلى المستخلص الذي يُلغيه</b> — برقمه ومعرّفه.
    /// </summary>
    [Fact]
    public async Task TheCancellation_PointsAtTheBillingItCancels()
    {
        var billing = await PostedBillingAsync();
        var original = await GeneratedAsync(billing.Id, ZatcaDocumentType.ProgressBilling);

        await CancelAsync(billing);

        var xml = await XmlAsync(billing.Id, ZatcaDocumentType.BillingCancel);
        var reference = xml.Descendants()
            .Where(e => e.Name.LocalName == "BillingReference")
            .Descendants().FirstOrDefault(e => e.Name.LocalName == "ID")?.Value;

        Assert.Equal(original.InvoiceNumber, reference);
    }

    /// <summary>
    /// <b>وسببُ الإلغاء مكتوب</b> — كما يُكتب للفاتورة والإشعار.
    /// </summary>
    [Fact]
    public async Task TheCancellation_CarriesAWrittenReason()
    {
        var billing = await PostedBillingAsync();

        await CancelAsync(billing);

        var xml = await XmlAsync(billing.Id, ZatcaDocumentType.BillingCancel);
        var reason = xml.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "InstructionNote")?.Value;

        Assert.False(string.IsNullOrWhiteSpace(reason),
            "‏مستندُ إلغاءٍ بلا سبب — توريدٌ يُرفع من الإقرار ولا يُفسَّر.");
        Assert.Contains(billing.BillingNumber!, reason!, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>وشاهدُ ضبط — المستندُ يمرّ مُصادِق المستودع كاملاً</b>، فالإصلاحُ أضاف
    /// ولم يكسر بنيةً.
    /// </summary>
    [Fact]
    public async Task TheCancellation_StillConformsStructurally()
    {
        var billing = await PostedBillingAsync();

        await CancelAsync(billing);

        var xml = await XmlAsync(billing.Id, ZatcaDocumentType.BillingCancel);
        Assert.Empty(UblStructureValidator.Validate(xml));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private Task CancelAsync(ProgressBilling billing) =>
        new ProgressBillingCancellationService(_db)
            .CancelAsync(_companyId, billing.Id, new DateOnly(2026, 3, 20), "admin");

    private async Task<ZatcaEInvoiceDocument> GeneratedAsync(long sourceId, ZatcaDocumentType type)
    {
        var document = await _db.ZatcaEInvoiceDocuments
            .Where(d => d.CompanyId == _companyId
                        && d.SourceDocumentId == sourceId
                        && d.DocumentType == type)
            .OrderByDescending(d => d.Id)
            .FirstOrDefaultAsync();

        Assert.True(document is not null, $"‏لا مستند ZATCA من نوع {type} للمصدر {sourceId}.");
        return document!;
    }

    private async Task<XDocument> XmlAsync(long sourceId, ZatcaDocumentType type)
    {
        var document = await GeneratedAsync(sourceId, type);
        var bytes = await File.ReadAllBytesAsync(document.XmlFilePath);
        return XDocument.Parse(System.Text.Encoding.UTF8.GetString(bytes).TrimStart('﻿'));
    }

    private async Task<ProgressBilling> PostedBillingAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة مقاولات", "300000000000001", BusinessProfile.Construction);
        _companyId = company.Id;

        await ZatcaReadySeller.CompleteAsync(_db, _companyId);
        var settings = await _db.ZatcaSettings.SingleAsync(s => s.CompanyId == _companyId);
        settings.Update(true, ZatcaEnvironment.Simulation, "",
            ZatcaReadySeller.Certificate.CertPath, ZatcaReadySeller.Certificate.KeyPath, "");
        await _db.SaveChangesAsync();

        var customer = Customer.Create(_companyId, "عميل مشروع", "300000000000002").WithStandardIdentity();
        _db.Customers.Add(customer);

        var project = ConstructionProject.Create(_companyId, "PRJ-CANCEL", "برج", customer.Id,
            contractValue: 5_000_000m, retentionPercent: 10m);
        project.Activate();
        await new ConstructionProjectService(_db).CreateAsync(project);
        await _db.SaveChangesAsync();

        var billing = ProgressBilling.CreateDraft(
            _companyId, project.Id, customer.Id, new DateOnly(2026, 3, 1),
            "مستخلص أوّل", 1_000_000m, 10m);

        return await new ProgressBillingPostingService(_db).PostAsync(billing);
    }
}
