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
/// إلكترونيّ</b> (لم تصدر نظاماً)، وتُنشأ مسودّةٌ مصحَّحةٌ بسطورها؛ <b>والرفضُ يُسأل عنه قبل الإلغاء</b> فلا تُلغى فاتورةٌ صحيحة.</para>
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

    private async Task<SalesInvoice> PostAsync()
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, Day);
        invoice.AddLine("استشارة", 2m, 750m, _productId);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);
        return invoice;
    }

    private Task<ZatcaEInvoiceDocument> DocumentOfAsync(long invoiceId) =>
        _db.ZatcaEInvoiceDocuments.SingleAsync(d => d.DocumentType == ZatcaDocumentType.SalesInvoice && d.SourceDocumentId == invoiceId);

    [Fact]
    public async Task ARejectedStandardInvoice_IsCancelledWithoutACreditNote_AndACorrectedDraftCarriesItsLines()
    {
        await SetupAsync();
        var invoice = await PostAsync();
        var document = await DocumentOfAsync(invoice.Id);
        Assert.Equal(ZatcaTransmissionMode.Clearance, document.TransmissionMode);
        document.MarkRejected("{\"validationResults\":{\"status\":\"ERROR\"}}");
        await _db.SaveChangesAsync();

        var draftId = await new RejectedInvoiceReissueService(_db)
            .CancelAndCreateCorrectedDraftAsync(_companyId, invoice.Id, Day, "admin");

        Assert.Equal(SalesInvoiceStatus.Cancelled, (await _db.SalesInvoices.SingleAsync(i => i.Id == invoice.Id)).Status);
        Assert.False(await _db.ZatcaEInvoiceDocuments.AnyAsync(d => d.DocumentType == ZatcaDocumentType.InvoiceCancel));

        var draft = await _db.SalesInvoices.SingleAsync(i => i.Id == draftId);
        Assert.Equal(SalesInvoiceStatus.Draft, draft.Status);
        Assert.Equal(_customerId, draft.CustomerId);
        var line = Assert.Single(draft.Lines);
        Assert.Equal(("استشارة", 2m, 750m), (line.Description, line.Quantity, line.UnitPrice));
    }

    [Fact]
    public async Task AnInvoiceThatWasNotRejected_IsNeitherCancelledNorCopied()
    {
        await SetupAsync();
        var invoice = await PostAsync();

        await Assert.ThrowsAsync<AccountingException>(() =>
            new RejectedInvoiceReissueService(_db).CancelAndCreateCorrectedDraftAsync(_companyId, invoice.Id, Day, "admin"));

        Assert.Equal(SalesInvoiceStatus.Posted, (await _db.SalesInvoices.SingleAsync(i => i.Id == invoice.Id)).Status);
        Assert.Equal(1, await _db.SalesInvoices.CountAsync());
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
