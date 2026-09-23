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
/// (جاهزيّةُ الإطلاق · ح-٤٤ · <c>ui-ux-i18n-printing#2</c>) <b>الخصمُ يُطبع، وكلُّ عمودٍ
/// يجمع إلى إجماليّه.</b>
///
/// <para><b>ما كان:</b> المطبوعةُ بلا خصمٍ أصلاً — لا خصمُ السطر ولا خصمُ المستند —
/// والإجماليّاتُ بعدهما. <b>فخصمُ عشرةٍ بالمئة على سطرٍ</b> يجعل «الكمّيّة × السعر» غيرَ
/// «الخاضع للضريبة» بلا سبب، <b>وخصمُ المستند</b> يجعل عمودَ «الإجمالي» يزيد على
/// «الإجمالي شامل الضريبة» تحته بقدر الخصم — <b>والخصمُ نفسُه لا يظهر في أيّ موضع</b>.
/// والـXML يحمله (<c>AllowanceCharge</c>) والورقةُ لا.</para>
/// </summary>
public class PrintedDiscountsReconcileTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private readonly string _workDirectory;
    private long _companyId;
    private long _customerId;

    public PrintedDiscountsReconcileTests()
    {
        TestActor.Administrator();
        _workDirectory = Path.Combine(Path.GetTempPath(), "brqosh-h44", Guid.NewGuid().ToString("N"));
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
    /// <b>القياسُ الحاكم:</b> عمودُ «الإجمالي» يجمع إلى «الإجمالي شامل الضريبة» — وكان
    /// يزيد عليه بقدر خصم المستند.
    /// </summary>
    [Fact]
    public async Task TheTotalColumn_SumsToTheTotal_WithADocumentDiscount()
    {
        await SetupAsync();
        var invoice = await PostDiscountedInvoiceAsync();

        var figures = SalesInvoicePdfGenerator.Figures(
            await Print().SalesInvoiceRequestAsync(_companyId, invoice.Id));

        Assert.Equal(invoice.GrandTotal(), figures.Total);
        Assert.Equal(figures.Total, figures.LineTotals.Sum());
        Assert.Equal(figures.Vat, figures.LineVats.Sum());
    }

    /// <summary>
    /// <b>ورأسُ عمود الضريبة ١٥٪ — لا ١٤٫٢٥.</b> كانت النسبةُ ضريبةً بعد خصم المستند
    /// (١٤٢٫٥٠) مقسومةً على وعاءٍ قبله (١٬٠٠٠) — <b>نسبةٌ لا وجود لها على فاتورةٍ ضريبيّة</b>.
    /// </summary>
    [Fact]
    public async Task TheVatRate_IsTheTrueRate_WithADocumentDiscount()
    {
        await SetupAsync();
        var invoice = await PostDiscountedInvoiceAsync();

        var figures = SalesInvoicePdfGenerator.Figures(
            await Print().SalesInvoiceRequestAsync(_companyId, invoice.Id));

        Assert.Contains("15%", figures.VatRateLabel, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>وكلُّ سطرٍ يُعيد نفسَه</b>: <c>الكمّيّة × السعر − الخصم = الخاضع</c>، ومجموعُ
    /// الخاضع == الوعاءُ المطبوع. وكان «الخاضع» يخالف «الكمّيّة × السعر» بلا سببٍ مطبوع.
    /// </summary>
    [Fact]
    public async Task EachLine_ShowsItsDiscount_AndReconciles()
    {
        await SetupAsync();
        var invoice = await PostDiscountedInvoiceAsync();

        var request = await Print().SalesInvoiceRequestAsync(_companyId, invoice.Id);
        var figures = SalesInvoicePdfGenerator.Figures(request);

        for (var i = 0; i < request.Lines.Count; i++)
        {
            var line = request.Lines[i];
            Assert.Equal(line.Qty * line.Price - figures.LineDiscounts[i], figures.LineTaxables[i]);
        }
        Assert.Equal(950m, figures.LineTaxables.Sum());
        Assert.Equal(figures.Subtotal, figures.LineTaxables.Sum());
    }

    /// <summary>
    /// <b>والخصمُ مُفصَحٌ عنه</b>: الإجماليُّ قبله ١٬١٠٠، والخصمُ ١٥٠ (١٠٠ للسطر و٥٠ للمستند)،
    /// <b>ويجمعان إلى الوعاء ٩٥٠</b>. وكان لا يظهر في أيّ موضع.
    /// </summary>
    [Fact]
    public async Task TheDiscount_IsDisclosed_AndReconcilesToTheBase()
    {
        await SetupAsync();
        var invoice = await PostDiscountedInvoiceAsync();

        var figures = SalesInvoicePdfGenerator.Figures(
            await Print().SalesInvoiceRequestAsync(_companyId, invoice.Id));

        Assert.True(figures.HasDiscount);
        Assert.Equal(1_100m, figures.GrossTotal);
        Assert.Equal(150m, figures.DiscountTotal);
        Assert.Equal(50m, figures.DocumentDiscount);
        Assert.Equal(figures.Subtotal, figures.GrossTotal - figures.DiscountTotal);
    }

    /// <summary>
    /// <b>والإشعارُ الدائنُ يحمل حصّتَه من خصم الفاتورة</b> (ح-٦) — وأعمدتُه تجمع كذلك.
    /// </summary>
    [Fact]
    public async Task ACreditNoteWithADocumentDiscountShare_Reconciles()
    {
        await SetupAsync();
        var invoice = await PostDiscountedInvoiceAsync();

        var note = CreditNote.CreateDraft(_companyId, _customerId, invoice.Id, new DateOnly(2026, 7, 10), "إرجاع");
        note.AddLine("صنفٌ آخر", 2m, 50m);
        note.ApplyDocumentDiscount(5m);
        await new CreditNotePostingService(_db).PostAsync(note);

        var figures = SalesInvoicePdfGenerator.Figures(await Print().CreditNoteRequestAsync(_companyId, note.Id));

        Assert.Equal(5m, figures.DocumentDiscount);
        Assert.Equal(figures.Subtotal, figures.LineTaxables.Sum());
        Assert.Equal(figures.Total, figures.LineTotals.Sum());
    }

    /// <summary>
    /// <b>وحصصُ خصم المستند تجمع إليه حرفاً</b> — ثلاثةُ سطورٍ متساويةٍ وخصمُ ١٠:
    /// <c>3.33 × 3 = 9.99</c>، <b>والفلسُ الباقي على أكبر سطر</b> لا يضيع.
    /// </summary>
    [Fact]
    public void TheDocumentDiscountShares_SumToTheDiscount()
    {
        SalesInvoicePdfGenerator.Line L() => new("صنف", 1m, 100m, 100m);

        var shares = SalesInvoicePdfGenerator.AllocateDocumentDiscount([L(), L(), L()], 10m);

        Assert.Equal(10m, shares.Sum());
    }

    /// <summary>
    /// <b>ومستندُ إلغاء فاتورةٍ مخصومةٍ يُطبع مصالَحاً</b> — يحمل خصمَ ما أُلغي.
    /// </summary>
    [Fact]
    public async Task ACancellationOfADiscountedInvoice_Reconciles()
    {
        await SetupAsync(zatca: true);
        var invoice = await PostDiscountedInvoiceAsync();

        await using (var tx = await _db.Database.BeginTransactionAsync())
        {
            await new ZatcaEInvoiceGenerator(_db).TryGenerateCancellationForSalesInvoiceAsync(invoice.Id);
            await tx.CommitAsync();
        }
        var doc = await _db.ZatcaEInvoiceDocuments.SingleAsync(
            d => d.DocumentType == ZatcaDocumentType.InvoiceCancel && d.SourceDocumentId == invoice.Id);

        var (request, _) = await Print().ZatcaCancellationRequestAsync(_companyId, doc.Id);
        var figures = SalesInvoicePdfGenerator.Figures(request);

        Assert.Equal(50m, figures.DocumentDiscount);
        Assert.Equal(figures.Total, figures.LineTotals.Sum());
    }

    /// <summary>
    /// <b>شاهدُ ضبط — فاتورةٌ بلا خصمٍ تُطبع كما كانت</b>: لا عمودَ خصمٍ ولا صفوفَه،
    /// والخاضعُ صافي السطر حرفاً.
    /// </summary>
    [Fact]
    public async Task AnUndiscountedInvoice_PrintsAsBefore()
    {
        await SetupAsync();
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 9));
        invoice.AddLine("صنف", 3m, 100m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var figures = SalesInvoicePdfGenerator.Figures(
            await Print().SalesInvoiceRequestAsync(_companyId, invoice.Id));

        Assert.False(figures.HasDiscount);
        Assert.Equal(300m, Assert.Single(figures.LineTaxables));
        Assert.Contains("15%", figures.VatRateLabel, StringComparison.Ordinal);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private DocumentPrintService Print() =>
        new(_db, new AuditService(_db), new ExchangeRateService(_db));

    /// <summary>
    /// سطران: ١٠ × ١٠٠ بخصم ١٠٪ (صافي ٩٠٠) و٢ × ٥٠ بلا خصم (صافي ١٠٠)؛ وخصمُ مستندٍ ٥٠
    /// ⟵ الوعاء ٩٥٠ والضريبة ١٤٢٫٥٠ والإجمالي ١٬٠٩٢٫٥٠.
    /// </summary>
    private async Task<SalesInvoice> PostDiscountedInvoiceAsync()
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 9));
        invoice.AddLine("صنفٌ مخصوم", 10m, 100m, discountPercent: 10m);
        invoice.AddLine("صنفٌ آخر", 2m, 50m);
        invoice.ApplyDocumentDiscount(50m);
        return await new SalesInvoicePostingService(_db).PostAsync(invoice);
    }

    private async Task SetupAsync(bool zatca = false)
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة الطباعة", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;

        var customer = Customer.Create(_companyId, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;

        if (!zatca)
            return;

        // ‏تهيئةُ الفوترة كما في `CancellationDocumentsPrintTests` — كي يُصدَر مستندُ الإلغاء.
        await new BrqoshSchemaUpdater(_db).ApplyAsync();
        ZatcaReadySeller.Complete(company);
        customer.UpdateRegulatoryIdentity(
            "مؤسسة العميل التجارية",
            Brqosh.Domain.Companies.NationalAddress.Create(
                buildingNumber: "4321", streetName: "طريق العليا", districtName: "الملز",
                cityName: "الرياض", postalZone: "54321", countrySubentity: "منطقة الرياض",
                additionalNumber: "8765"));
        var (certPath, keyPath) = ZatcaSignatureVerifier.CreateSigningCertificate(_workDirectory);
        var settings = await _db.ZatcaSettings.SingleAsync(s => s.CompanyId == _companyId);
        settings.Update(isEnabled: true, ZatcaEnvironment.Simulation, "", certPath, keyPath, "");
        await _db.SaveChangesAsync();
    }
}
