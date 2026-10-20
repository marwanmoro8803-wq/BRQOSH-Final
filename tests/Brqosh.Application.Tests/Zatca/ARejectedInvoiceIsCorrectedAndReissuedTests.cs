using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
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
/// (جاهزيّةُ الإطلاق · <c>tax-zatca#8</c>) <b>المرفوضُ يُصحَّح ويُعاد إصداره.</b>
///
/// <para><b>ما كان:</b> مستندٌ «مرفوض» لا مسارَ له. <b>والآن</b>: الفاتورةُ القياسيّةُ المرفوضةُ تُلغى بعكس قيدها <b>بلا إشعارٍ دائنٍ
/// إلكترونيّ</b> (لم تصدر نظاماً)، وتُعاد بياناتُها فتُرحَّل منها فاتورةٌ جديدةٌ من المسار المشترك؛ <b>والرفضُ يُسأل عنه قبل الإلغاء</b>
/// فلا تُلغى فاتورةٌ صحيحة.</para>
/// </summary>
public class ARejectedInvoiceIsCorrectedAndReissuedTests : IDisposable
{
    private static readonly DateOnly Day = new(2026, 3, 10);
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private readonly string _workDirectory;
    private long _companyId;
    private long _customerId;
    private long _productId;

    public ARejectedInvoiceIsCorrectedAndReissuedTests()
    {
        TestActor.Administrator();
        _workDirectory = Path.Combine(Path.GetTempPath(), "brqosh-reissue", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workDirectory);
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>().UseSqlite(_connection).Options);
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        _connection.Dispose();
        try { Directory.Delete(_workDirectory, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        GC.SuppressFinalize(this);
    }

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync("منشأة", "310123456700099", BusinessProfile.General);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();
        ZatcaReadySeller.Complete(company);

        var customer = Customer.Create(_companyId, "عميل منشأة", "311111111100003");
        customer.UpdateRegulatoryIdentity("مؤسسة العميل التجارية",
            Brqosh.Domain.Companies.NationalAddress.Create(buildingNumber: "4321", streetName: "طريق العليا", districtName: "الملز",
                cityName: "الرياض", postalZone: "54321", countrySubentity: "منطقة الرياض", additionalNumber: "8765"));
        _db.Customers.Add(customer);
        var product = Product.Create(_companyId, "S-1", "خدمة", "خدمة", ProductType.Service);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;
        _productId = product.Id;

        var (certPath, keyPath) = ZatcaSignatureVerifier.CreateSigningCertificate(_workDirectory);
        var settings = await _db.ZatcaSettings.SingleAsync(s => s.CompanyId == _companyId);
        settings.Update(isEnabled: true, ZatcaEnvironment.Simulation, "", certPath, keyPath, "");
        await _db.SaveChangesAsync();
    }

    private async Task<SalesInvoice> PostAsync(long? customerId = null)
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, customerId ?? _customerId, Day);
        invoice.AddLine("استشارة", 2m, 750m, _productId, discountPercent: 10m, unitCode: null);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);
        return invoice;
    }

    private Task<ZatcaEInvoiceDocument> DocumentOfAsync(long invoiceId) =>
        _db.ZatcaEInvoiceDocuments.SingleAsync(d => d.DocumentType == ZatcaDocumentType.SalesInvoice && d.SourceDocumentId == invoiceId);

    private async Task<SalesInvoice> RejectAsync(SalesInvoice invoice)
    {
        (await DocumentOfAsync(invoice.Id)).MarkRejected("{\"validationResults\":{\"status\":\"ERROR\"}}");
        await _db.SaveChangesAsync();
        return invoice;
    }

    private RejectedInvoiceReissueService Reissue() => new(_db);

    [Fact]
    public async Task ARejectedStandardInvoice_IsCancelledWithoutACreditNote_AndItsSeedCarriesTheInvoice()
    {
        await SetupAsync();
        var invoice = await RejectAsync(await PostAsync());
        Assert.Equal(ZatcaTransmissionMode.Clearance, (await DocumentOfAsync(invoice.Id)).TransmissionMode);

        var seed = await Reissue().CancelRejectedAsync(_companyId, invoice.Id, Day, "admin");

        Assert.Equal(SalesInvoiceStatus.Cancelled, (await _db.SalesInvoices.SingleAsync(i => i.Id == invoice.Id)).Status);
        Assert.False(await _db.ZatcaEInvoiceDocuments.AnyAsync(d => d.DocumentType == ZatcaDocumentType.InvoiceCancel));

        Assert.Equal((_customerId, "SAR", 1m), (seed.CustomerId, seed.CurrencyCode, seed.ExchangeRate));
        var line = Assert.Single(seed.Lines);
        Assert.Equal(("استشارة", 2m, 750m, 10m, (long?)_productId),
            (line.Description, line.Quantity, line.UnitPrice, line.DiscountPercent, line.ProductId));
    }

    /// <summary>
    /// <b>والمصحَّحةُ تُرحَّل من المسار المشترك</b> — رقمٌ جديدٌ ومستندٌ إلكترونيٌّ جديدٌ لها، والمرفوضُ يبقى كما هو أثراً.
    /// </summary>
    [Fact]
    public async Task TheCorrectedInvoice_PostsThroughTheSharedPath_WithItsOwnNumberAndDocument()
    {
        await SetupAsync();
        var rejected = await RejectAsync(await PostAsync());
        var seed = await Reissue().CancelRejectedAsync(_companyId, rejected.Id, Day, "admin");

        var corrected = SalesInvoice.CreateDraft(_companyId, seed.CustomerId, Day);
        foreach (var l in seed.Lines)
            corrected.AddLine(l.Description, l.Quantity, l.UnitPrice, l.ProductId, l.VatCategory,
                l.TaxExemptionReasonCode, l.TaxExemptionReason, l.BatchNumber, l.DiscountPercent, l.UnitCode);
        await new SalesInvoicePostingService(_db).PostAsync(corrected);

        Assert.NotEqual(rejected.InvoiceNumber, corrected.InvoiceNumber);
        Assert.Equal((rejected.Subtotal, rejected.VatAmount), (corrected.Subtotal, corrected.VatAmount));
        var document = await DocumentOfAsync(corrected.Id);
        Assert.NotEqual(ZatcaComplianceStatus.Rejected, document.ComplianceStatus);
        Assert.Equal(ZatcaComplianceStatus.Rejected, (await DocumentOfAsync(rejected.Id)).ComplianceStatus);
    }

    [Fact]
    public async Task AnInvoiceThatWasNotRejected_IsNotCancelled()
    {
        await SetupAsync();
        var invoice = await PostAsync();

        await Assert.ThrowsAsync<AccountingException>(() => Reissue().CancelRejectedAsync(_companyId, invoice.Id, Day, "admin"));

        Assert.Equal(SalesInvoiceStatus.Posted, (await _db.SalesInvoices.SingleAsync(i => i.Id == invoice.Id)).Status);
    }

    /// <summary>
    /// <b>ومَن أغلق شاشةَ المصحَّحة دون ترحيلٍ يعود إليها</b> — الفاتورةُ ملغاةٌ سلفاً، فتُعاد بياناتُها بلا إلغاءٍ ثانٍ.
    /// </summary>
    [Fact]
    public async Task ARejectedInvoiceAlreadyCancelled_GivesItsSeedAgain_WithoutASecondReversal()
    {
        await SetupAsync();
        var invoice = await RejectAsync(await PostAsync());
        await Reissue().CancelRejectedAsync(_companyId, invoice.Id, Day, "admin");
        var entries = await _db.JournalEntries.CountAsync();

        var again = await Reissue().CancelRejectedAsync(_companyId, invoice.Id, Day, "admin");

        Assert.Single(again.Lines);
        Assert.Equal(entries, await _db.JournalEntries.CountAsync());
    }

    /// <summary>
    /// <b>والمبسّطةُ المرفوضةُ وصلت المشتري</b> — فإلغاؤها إشعارٌ دائنٌ كما كان؛ والاستثناءُ للقياسيّة وحدَها.
    /// </summary>
    [Fact]
    public async Task ARejectedSimplifiedInvoice_IsStillCancelledByACreditNote()
    {
        await SetupAsync();
        var walkIn = Customer.Create(_companyId, "عميل نقدي");
        _db.Customers.Add(walkIn);
        await _db.SaveChangesAsync();
        var invoice = await RejectAsync(await PostAsync(walkIn.Id));
        Assert.Equal(ZatcaTransmissionMode.Reporting, (await DocumentOfAsync(invoice.Id)).TransmissionMode);

        await Reissue().CancelRejectedAsync(_companyId, invoice.Id, Day, "admin");

        Assert.True(await _db.ZatcaEInvoiceDocuments.AnyAsync(d =>
            d.DocumentType == ZatcaDocumentType.InvoiceCancel && d.SourceDocumentId == invoice.Id));
    }

    [Fact]
    public async Task AnAcceptedStandardInvoice_StillGetsItsCreditNoteWhenCancelled()
    {
        await SetupAsync();
        var invoice = await PostAsync();

        await new SalesInvoiceCancellationService(_db).CancelAsync(_companyId, invoice.Id, Day, "admin");

        Assert.True(await _db.ZatcaEInvoiceDocuments.AnyAsync(d =>
            d.DocumentType == ZatcaDocumentType.InvoiceCancel && d.SourceDocumentId == invoice.Id));
    }
}
