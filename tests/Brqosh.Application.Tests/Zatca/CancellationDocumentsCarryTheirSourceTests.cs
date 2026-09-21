using System.Xml.Linq;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
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
/// (جاهزيّةُ الإطلاق · ح-٣٦) <b>مستنداتُ الإلغاء كانت تُسقط ما يحمله مصدرُها:</b> العملةَ
/// والسعرَ وضريبةَ الريال <b>وخصمَ المستند</b>.
///
/// <para><b>وثلاثةُ مسارات إلغاءٍ تُمرّر الريالَ عارياً</b> بينما مسارُ الفاتورة يُمرّر
/// الخمسةَ كاملةً. <b>فإلغاءُ فاتورةٍ بخصمٍ يُنتج سطوراً لا تجمع إلى إجماليّها</b> —
/// ويمسكه <c>UblStructureValidator</c> في المستودع نفسِه — <b>وإلغاءُ فاتورةٍ دولاريّةٍ
/// يُعلَن بالريال</b>.</para>
///
/// <para><b>ورابعٌ في المُبدِّل</b>: <c>BillingCancel</c> كان <b>يسقط إلى
/// <c>(null, null)</c></b> فيخرج مستندُ إلغاء المستخلص <b>بلا <c>BillingReference</c>
/// وبلا سبب</b> — <b>إشعارٌ دائنٌ لا يقول ماذا يُلغي</b>.</para>
/// </summary>
public class CancellationDocumentsCarryTheirSourceTests : IDisposable
{
    private static readonly XNamespace Cbc =
        "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private readonly string _workDirectory;
    private long _companyId;
    private long _customerId;

    public CancellationDocumentsCarryTheirSourceTests()
    {
        TestActor.Administrator();
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);

        _workDirectory = Path.Combine(Path.GetTempPath(), "brqosh-h36", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workDirectory);
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        _connection.Dispose();
        try { Directory.Delete(_workDirectory, recursive: true); } catch { /* مجلّدٌ مؤقّت */ }
        GC.SuppressFinalize(this);
    }

    // ── العملة ──────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ الحاكم:</b> إلغاءُ فاتورةٍ دولاريّةٍ يخرج <b>بالدولار</b> —
    /// والضريبةُ بالريال كما تُلزم الهيئة.
    /// </summary>
    [Fact]
    public async Task CancellingAForeignInvoice_IssuesTheDocumentInItsCurrency()
    {
        await SetupAsync();
        var invoice = await PostInvoiceAsync(1_000m, currency: "USD", rate: 3.75m);

        await CancelInvoiceAsync(invoice);

        var xml = await GeneratedXmlAsync(invoice.Id, ZatcaDocumentType.InvoiceCancel);
        Assert.Equal("USD", xml.Root!.Element(Cbc + "DocumentCurrencyCode")!.Value);
        Assert.Equal("SAR", xml.Root!.Element(Cbc + "TaxCurrencyCode")!.Value);
    }

    /// <summary>
    /// <b>وإلغاءُ إشعارٍ دائنٍ دولاريٍّ كذلك</b> — والمستندُ الناتجُ إشعارٌ مدين.
    /// </summary>
    [Fact]
    public async Task CancellingAForeignCreditNote_IssuesTheDebitNoteInItsCurrency()
    {
        await SetupAsync();
        var invoice = await PostInvoiceAsync(1_000m, currency: "USD", rate: 3.75m);
        var note = await PostCreditNoteAsync(invoice);

        await new CreditNoteCancellationService(_db)
            .CancelAsync(_companyId, note.Id, new DateOnly(2026, 5, 20), "admin");

        var xml = await GeneratedXmlAsync(note.Id, ZatcaDocumentType.DebitNote);
        Assert.Equal("USD", xml.Root!.Element(Cbc + "DocumentCurrencyCode")!.Value);
    }

    // ── اتّساقُ الإجماليّات ─────────────────────────────────────────────────

    /// <summary>
    /// <b>وإلغاءُ فاتورةٍ بخصمِ مستندٍ يخرج مُتّسقاً</b> — <b>ويُقاس بمُصادِق المستودع
    /// نفسِه</b>، وهو يفحص أنّ <c>LineExtensionAmount</c> يساوي مجموعَ الأسطر وأنّ
    /// <c>TaxExclusiveAmount</c> يساوي الفرقَ بعد الخصم.
    ///
    /// <para><b>وكان الخصمُ يسقط</b> — فتخرج سطورٌ لا تجمع إلى إجماليّها.</para>
    /// </summary>
    [Fact]
    public async Task CancellingADiscountedInvoice_ProducesConsistentTotals()
    {
        await SetupAsync();
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 5, 1));
        invoice.AddLine("توريد", 1m, 1_000m);
        invoice.ApplyDocumentDiscount(100m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        await CancelInvoiceAsync(invoice);

        var xml = await GeneratedXmlAsync(invoice.Id, ZatcaDocumentType.InvoiceCancel);
        Assert.Empty(UblStructureValidator.Validate(xml));
    }

    // ── شواهدُ الضبط ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>شاهدُ ضبطٍ أوّل — فاتورةٌ بالريال بلا خصمٍ لم تتغيّر:</b> المستندُ يخرج
    /// ريالاً ويمرّ المُصادِق. <b>ولولاه لجاز أن يكون الإصلاحُ مسّ كلَّ إلغاءٍ قائم.</b>
    /// </summary>
    [Fact]
    public async Task CancellingAPlainSarInvoice_IsUnchanged()
    {
        await SetupAsync();
        var invoice = await PostInvoiceAsync(1_000m);

        await CancelInvoiceAsync(invoice);

        var xml = await GeneratedXmlAsync(invoice.Id, ZatcaDocumentType.InvoiceCancel);
        Assert.Empty(UblStructureValidator.Validate(xml));
        Assert.Equal("SAR", xml.Root!.Element(Cbc + "DocumentCurrencyCode")!.Value);
    }

    /// <summary>
    /// <b>وشاهدُ ضبطٍ ثانٍ — مستندُ الإلغاء يُشير إلى ما يُلغيه:</b> <c>BillingReference</c>
    /// يحمل رقمَ الفاتورة الأصليّة. <b>فمستندٌ لا يقول ماذا يُلغي لا يُغلق شيئاً لدى
    /// الهيئة.</b>
    /// </summary>
    [Fact]
    public async Task TheCancellation_PointsAtWhatItCancels()
    {
        await SetupAsync();
        var invoice = await PostInvoiceAsync(1_000m);

        await CancelInvoiceAsync(invoice);

        var xml = await GeneratedXmlAsync(invoice.Id, ZatcaDocumentType.InvoiceCancel);
        var reference = xml.Descendants()
            .Where(e => e.Name.LocalName == "BillingReference")
            .Descendants().FirstOrDefault(e => e.Name.LocalName == "ID")?.Value;

        Assert.Equal(invoice.InvoiceNumber, reference);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private Task CancelInvoiceAsync(SalesInvoice invoice) =>
        new SalesInvoiceCancellationService(_db)
            .CancelAsync(_companyId, invoice.Id, new DateOnly(2026, 5, 15), "admin");

    private async Task<SalesInvoice> PostInvoiceAsync(
        decimal amount, string? currency = null, decimal rate = 1m)
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 5, 1));
        invoice.AddLine("توريد", 1m, amount);
        if (currency is not null)
            invoice.SetCurrency(currency, rate);
        return await new SalesInvoicePostingService(_db).PostAsync(invoice);
    }

    private async Task<CreditNote> PostCreditNoteAsync(SalesInvoice invoice)
    {
        var note = CreditNote.CreateDraft(_companyId, _customerId, invoice.Id,
            new DateOnly(2026, 5, 10), "مرتجع");

        if (invoice.IsForeignCurrency)
            note.SetCurrency(invoice.CurrencyCode, invoice.ExchangeRate);

        foreach (var line in invoice.Lines)
            note.AddLine(line.Description, line.Quantity, line.UnitPrice, line.ProductId,
                line.VatCategory, line.TaxExemptionReasonCode, line.TaxExemptionReason);

        return await new CreditNotePostingService(_db).PostAsync(note);
    }

    private async Task<XDocument> GeneratedXmlAsync(long sourceId, ZatcaDocumentType type)
    {
        var document = await _db.ZatcaEInvoiceDocuments
            .Where(d => d.CompanyId == _companyId
                        && d.SourceDocumentId == sourceId
                        && d.DocumentType == type)
            .OrderByDescending(d => d.Id)
            .FirstOrDefaultAsync();

        Assert.True(document is not null,
            $"‏الإلغاءُ لم يُنتج مستند ZATCA من نوع {type} — لا شيء يصل الهيئة.");

        var bytes = await File.ReadAllBytesAsync(document!.XmlFilePath);
        return XDocument.Parse(System.Text.Encoding.UTF8.GetString(bytes).TrimStart('﻿'));
    }

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة الإلغاء", "310123456700139", BusinessProfile.Retail);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        company.UpdateRegulatoryIdentity(
            "منشأة الإلغاء للتجارة", "1010123456",
            Brqosh.Domain.Companies.NationalAddress.Create(
                buildingNumber: "1234", streetName: "طريق الملك فهد", districtName: "العليا",
                cityName: "الرياض", postalZone: "12345", countrySubentity: "منطقة الرياض",
                additionalNumber: "5678"));

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
