using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Treasury;
using Brqosh.Infrastructure.Zatca;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٤٣ · <c>ui-ux-i18n-printing#0</c>) <b>الإشعارُ المطبوعُ يُسمّي ما
/// يُصحّحه ولماذا، ولمن.</b>
///
/// <para><b>ما كان:</b> الإشعارُ الدائنُ المطبوعُ <b>بلا رقم الفاتورة الأصليّة ولا السبب ولا
/// الرقم الضريبيّ للمشتري ولا عنوانه</b> — وكلُّها في المستند (<c>OriginalInvoiceId</c> ·
/// <c>Reason</c>) وفي الـXML، <b>والفاتورةُ في الملفّ نفسِه تمرّر هويّةَ المشتري</b>. فعميلٌ
/// يتسلّم إشعاراً ورقيّاً <b>لا يعرف أيَّ فاتورةٍ يُصحّح</b>، ولا يخصم به مدخلاتِه، ولا
/// يُطابقه مدقّقٌ بفاتورة.</para>
///
/// <para><b>وعلى الحال نفسِه الإشعارُ المدينُ (للمورّد) ومستندُ الإلغاء</b> — الأخيرُ يحمل
/// المرجعَ في الملاحظات نصّاً، <b>وبلا هويّة المشتري</b>.</para>
/// </summary>
public class PrintedNotesNameWhatTheyCorrectTests : IDisposable
{
    private const string CustomerVat = "311111111100003";
    private const string CustomerAddress = "الرياض — حيّ الملز، طريق العليا ٤٣٢١";
    private const string SupplierVat = "312233344400003";

    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private readonly string _workDirectory;
    private long _companyId;
    private long _customerId;
    private long _supplierId;

    public PrintedNotesNameWhatTheyCorrectTests()
    {
        TestActor.Administrator();
        _workDirectory = Path.Combine(Path.GetTempPath(), "brqosh-h43", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workDirectory);

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
        try { Directory.Delete(_workDirectory, recursive: true); }
        catch (IOException) { /* ‏ملفٌّ مؤقّتٌ ممسوك — لا يعني فشلَ القياس. */ }
        catch (UnauthorizedAccessException) { /* ‏كسابقه. */ }
        GC.SuppressFinalize(this);
    }

    /// <summary><b>القياسُ الحاكم:</b> الإشعارُ الدائنُ يُسمّي فاتورتَه وسببَه.</summary>
    [Fact]
    public async Task ACreditNote_NamesTheInvoiceItCorrects_AndWhy()
    {
        await SetupAsync(zatca: false);
        var invoice = await PostInvoiceAsync();
        var note = await PostCreditNoteAsync(invoice, "إرجاع بضاعةٍ تالفة");

        var options = (await Print().CreditNoteRequestAsync(_companyId, note.Id)).Options;

        Assert.Equal(invoice.InvoiceNumber, options.ReferenceDocumentNumber);
        Assert.Equal("إرجاع بضاعةٍ تالفة", options.ReasonAr);
    }

    /// <summary>
    /// <b>وهويّةُ المشتري كما في الفاتورة</b> — رقمُه الضريبيُّ وعنوانُه. بلاهما لا يخصم
    /// المشتري المسجَّلُ ضريبتَه بالإشعار.
    /// </summary>
    [Fact]
    public async Task ACreditNote_CarriesTheBuyersIdentity()
    {
        await SetupAsync(zatca: false);
        var invoice = await PostInvoiceAsync();
        var note = await PostCreditNoteAsync(invoice, "تسوية سعر");

        var options = (await Print().CreditNoteRequestAsync(_companyId, note.Id)).Options;

        Assert.Equal(CustomerVat, options.PartyVatNumber);
        Assert.Equal(CustomerAddress, options.PartyAddress);
    }

    /// <summary><b>والإشعارُ المدينُ للمورّد كذلك</b> — فاتورتُه وسببُه ورقمُ المورّد الضريبيّ.</summary>
    [Fact]
    public async Task APurchaseDebitNote_NamesItsInvoice_ItsReason_AndTheSupplier()
    {
        await SetupAsync(zatca: false);
        var purchase = PurchaseInvoice.CreateDraft(
            _companyId, _supplierId, new DateOnly(2026, 7, 9), "SUP-9", vatDeductible: true);
        purchase.AddLine("خدمة", 2m, 500m);
        await new PurchaseInvoicePostingService(_db).PostAsync(purchase);

        var note = await new DebitNoteDraftBuilder(_db).BuildAsync(
            _companyId, purchase.Id, new DateOnly(2026, 7, 10), "مرتجعٌ لعيبٍ مصنعيّ",
            [new RequestedReturn(0, 1m)]);
        await new DebitNotePostingService(_db).PostAsync(note);

        var options = (await Print().DebitNoteRequestAsync(_companyId, note.Id)).Options;

        Assert.Equal(purchase.InvoiceNumber, options.ReferenceDocumentNumber);
        Assert.Equal("مرتجعٌ لعيبٍ مصنعيّ", options.ReasonAr);
        Assert.Equal(SupplierVat, options.PartyVatNumber);
    }

    /// <summary>
    /// <b>ومستندُ الإلغاء يُسمّي ما أُلغي ويحمل هويّةَ المشتري</b> — إشعارٌ ضريبيٌّ كغيره.
    /// </summary>
    [Fact]
    public async Task AnInvoiceCancellation_NamesTheCancelledInvoice_AndTheBuyer()
    {
        await SetupAsync(zatca: true);
        var invoice = await PostInvoiceAsync();

        await using (var tx = await _db.Database.BeginTransactionAsync())
        {
            await new ZatcaEInvoiceGenerator(_db).TryGenerateCancellationForSalesInvoiceAsync(invoice.Id);
            await tx.CommitAsync();
        }
        var doc = await _db.ZatcaEInvoiceDocuments.SingleAsync(
            d => d.DocumentType == ZatcaDocumentType.InvoiceCancel && d.SourceDocumentId == invoice.Id);

        var (request, _) = await Print().ZatcaCancellationRequestAsync(_companyId, doc.Id);

        Assert.Equal(invoice.InvoiceNumber, request.Options.ReferenceDocumentNumber);
        Assert.Equal(CustomerVat, request.Options.PartyVatNumber);
        Assert.Equal(CustomerAddress, request.Options.PartyAddress);
    }

    /// <summary>
    /// <b>شاهدُ ضبط — الفاتورةُ نفسُها لا مرجعَ لها ولا سبب</b>: الحقلان للإشعارات وحدَها،
    /// <b>فلا يظهر في فاتورةٍ صفُّ «المستند الأصليّ» فارغاً أو مخترَعاً</b>.
    /// </summary>
    [Fact]
    public async Task AnInvoice_HasNoReferenceAndNoReason()
    {
        await SetupAsync(zatca: false);
        var invoice = await PostInvoiceAsync();

        var options = (await Print().SalesInvoiceRequestAsync(_companyId, invoice.Id)).Options;

        Assert.Null(options.ReferenceDocumentNumber);
        Assert.Null(options.ReasonAr);
        Assert.Equal(CustomerVat, options.PartyVatNumber);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private DocumentPrintService Print() =>
        new(_db, new AuditService(_db), new ExchangeRateService(_db));

    private async Task<SalesInvoice> PostInvoiceAsync()
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 9));
        invoice.AddLine("خدمة", 1m, 1_000m);
        return await new SalesInvoicePostingService(_db).PostAsync(invoice);
    }

    private async Task<CreditNote> PostCreditNoteAsync(SalesInvoice invoice, string reason)
    {
        var note = CreditNote.CreateDraft(_companyId, _customerId, invoice.Id, new DateOnly(2026, 7, 10), reason);
        note.AddLine("خدمة", 1m, 400m);
        return await new CreditNotePostingService(_db).PostAsync(note);
    }

    private async Task SetupAsync(bool zatca)
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة الطباعة", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        ZatcaReadySeller.Complete(company);

        var customer = Customer.Create(_companyId, "عميل", CustomerVat, address: CustomerAddress);
        customer.UpdateRegulatoryIdentity(
            "مؤسسة العميل التجارية",
            Brqosh.Domain.Companies.NationalAddress.Create(
                buildingNumber: "4321", streetName: "طريق العليا", districtName: "الملز",
                cityName: "الرياض", postalZone: "54321", countrySubentity: "منطقة الرياض",
                additionalNumber: "8765"));
        var supplier = Supplier.Create(_companyId, "مورّد", SupplierVat);
        _db.Customers.Add(customer);
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;
        _supplierId = supplier.Id;

        if (!zatca)
            return;

        var (certPath, keyPath) = ZatcaSignatureVerifier.CreateSigningCertificate(_workDirectory);
        var settings = await _db.ZatcaSettings.SingleAsync(s => s.CompanyId == _companyId);
        settings.Update(isEnabled: true, ZatcaEnvironment.Simulation, "", certPath, keyPath, "");
        await _db.SaveChangesAsync();
    }
}
