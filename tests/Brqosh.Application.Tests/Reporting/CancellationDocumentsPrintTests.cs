using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Treasury;
using Brqosh.Infrastructure.Zatca;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٩) <b>مستندُ الإلغاء يُطبع بنفسه — لا بمستنده المصدر.</b>
///
/// <para>ثلاثةُ أنواعٍ تُصدَر عند الإلغاء (<c>InvoiceCancel</c> · <c>BillingCancel</c> ·
/// <c>DebitNote</c>) <b>لم يكن لها فرعٌ في مُوزِّع الطباعة</b>، فيرمي «طباعة PDF غير
/// مدعومة» — <b>ومستندٌ ضريبيٌّ لا يُطبع نصفُ مستند</b>.</para>
///
/// <para><b>والرميةُ كانت أصدقَ من البديل</b>: إسنادُ الطباعة إلى المصدر يطبع
/// <b>الفاتورةَ الملغاة بدل إشعار إلغائها</b> — وهو داءُ ح-١٥ بعينه. <b>فالقياسُ هنا
/// يحرس هذا تحديداً</b>: أنّ المطبوع يحمل <b>رقمَ مستند الإلغاء</b> لا رقمَ المصدر.</para>
/// </summary>
public class CancellationDocumentsPrintTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private readonly string _workDirectory;
    private long _companyId;
    private long _customerId;

    public CancellationDocumentsPrintTests()
    {
        TestActor.Administrator();
        _workDirectory = Path.Combine(Path.GetTempPath(), "brqosh-h19", Guid.NewGuid().ToString("N"));
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

    /// <summary>
    /// <b>القياسُ الحاكم:</b> إشعارُ إلغاء فاتورةٍ <b>يُطبع</b>، <b>وباسمه هو</b> —
    /// واسمُ ملفّه يحمل رقمَ مستند الإلغاء لا رقمَ الفاتورة الملغاة.
    /// </summary>
    [Fact]
    public async Task AnInvoiceCancellation_PrintsAsItself_NotAsTheCancelledInvoice()
    {
        await SetupAsync();
        var invoice = await PostInvoiceAsync();

        await using (var tx = await _db.Database.BeginTransactionAsync())
        {
            await new ZatcaEInvoiceGenerator(_db).TryGenerateCancellationForSalesInvoiceAsync(invoice.Id);
            await tx.CommitAsync();
        }

        var doc = await _db.ZatcaEInvoiceDocuments.SingleAsync(
            d => d.DocumentType == ZatcaDocumentType.InvoiceCancel && d.SourceDocumentId == invoice.Id);

        var result = await Print().ExportZatcaDocumentAsync(_companyId, doc.Id, "tester");

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(result.PdfBytes, 0, 4));

        // ‏**وهذا هو الحارس**: لو أُسندت الطباعةُ إلى المصدر لَحمل الاسمُ رقمَ الفاتورة.
        Assert.Contains(doc.InvoiceNumber, result.SuggestedFileName, StringComparison.Ordinal);
        Assert.DoesNotContain("brqosh-inv-", result.SuggestedFileName, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>وإشعارُ إلغاء إشعارٍ دائن يُطبع «إشعار مدين» — لا «إشعار مدين مشتريات».</b>
    ///
    /// <para><b>والفرقُ ليس لفظاً</b>: نوعُ المشتريات يُعنون الطرفَ بـ«المورد»،
    /// <b>وطرفُ هذا عميل</b> — فنسبةُ المستند الضريبيّ إلى الطرف الخطأ.</para>
    /// </summary>
    [Fact]
    public async Task ACreditNoteCancellation_PrintsAsASalesDebitNote()
    {
        await SetupAsync();
        var invoice = await PostInvoiceAsync();

        var note = CreditNote.CreateDraft(
            _companyId, _customerId, invoice.Id, new DateOnly(2026, 7, 10), "إرجاع");
        note.AddLine("خدمة", 1m, 400m);
        await new CreditNotePostingService(_db).PostAsync(note);

        await using (var tx = await _db.Database.BeginTransactionAsync())
        {
            await new ZatcaEInvoiceGenerator(_db).TryGenerateCancellationForCreditNoteAsync(note.Id);
            await tx.CommitAsync();
        }

        var doc = await _db.ZatcaEInvoiceDocuments.SingleAsync(
            d => d.DocumentType == ZatcaDocumentType.DebitNote && d.SourceDocumentId == note.Id);

        var result = await Print().ExportZatcaDocumentAsync(_companyId, doc.Id, "tester");

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(result.PdfBytes, 0, 4));
        Assert.Contains(doc.InvoiceNumber, result.SuggestedFileName, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>وشاهدُ ضبط — النوعُ الذي ليس إلغاءً يُرفض بصراحة:</b> فالدالّةُ لا تُستعمل
    /// لِما ليس لها، <b>ولا تُنتج ورقةً عن مستندٍ آخر</b>.
    /// </summary>
    [Fact]
    public async Task ANonCancellationType_IsRefused_NotPrintedAsSomethingElse()
    {
        await SetupAsync();
        var invoice = await PostInvoiceAsync();

        var doc = await _db.ZatcaEInvoiceDocuments.SingleAsync(
            d => d.DocumentType == ZatcaDocumentType.SalesInvoice && d.SourceDocumentId == invoice.Id);

        var error = await Assert.ThrowsAsync<AccountingException>(
            () => Print().ExportZatcaCancellationAsync(_companyId, doc.Id, "tester"));

        Assert.Contains("ليس مستندَ إلغاء", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>وشاهدُ ضبطٍ ثانٍ — الفاتورةُ العاديّةُ ما زالت تُطبع كما كانت.</b>
    /// <b>فلم يُكسر بابٌ سليمٌ لأجل بابٍ أُصلح.</b>
    /// </summary>
    [Fact]
    public async Task APlainSalesInvoice_StillPrintsThroughTheSameDispatcher()
    {
        await SetupAsync();
        var invoice = await PostInvoiceAsync();

        var doc = await _db.ZatcaEInvoiceDocuments.SingleAsync(
            d => d.DocumentType == ZatcaDocumentType.SalesInvoice && d.SourceDocumentId == invoice.Id);

        var result = await Print().ExportZatcaDocumentAsync(_companyId, doc.Id, "tester");

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(result.PdfBytes, 0, 4));
        Assert.StartsWith("brqosh-inv-", result.SuggestedFileName, StringComparison.Ordinal);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private DocumentPrintService Print() =>
        new(_db, new AuditService(_db), new ExchangeRateService(_db));

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة الطباعة", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        ZatcaReadySeller.Complete(company);

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

    private async Task<SalesInvoice> PostInvoiceAsync()
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 9));
        invoice.AddLine("خدمة", 1m, 1_000m);
        return await new SalesInvoicePostingService(_db).PostAsync(invoice);
    }
}
