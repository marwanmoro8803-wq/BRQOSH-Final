using System.Globalization;
using System.Text;
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
/// (جاهزيّةُ الإطلاق · ح-٤٢ · <c>tax-zatca#14</c> و<c>ui-ux-i18n-printing#3</c>)
/// <b>المستندُ الأجنبيُّ يُطبع بعملةٍ واحدة — ومعه الريالُ إفصاحاً.</b>
///
/// <para><b>ما كان:</b> كلُّ طباعةٍ تمرّر <b>السطورَ بعملة المستند والإجماليّاتِ بالريال</b>،
/// والكلُّ موسومٌ «ر.س». فاتورةٌ بـUSD 1,000 بسعر 3.75 تُطبع سطراً بـ1,000 تحته
/// «3,750.00 ر.س»، <b>ورأسُ عمود الضريبة «ضريبة 56.25%»</b> (ضريبةٌ بالريال على وعاءٍ
/// بالدولار)، <b>وإجماليُّ السطر دولارٌ مضافٌ إليه ريال</b>، <b>ورمزُ QR بالدولار تحت
/// إجماليٍّ بالريال</b>. <b>وهو المستندُ الذي يُسلَّم للعميل ويُلزم به النظام.</b></para>
///
/// <para><b>وخمسُ طباعاتٍ بالعطل نفسِه</b>: الفاتورتان والإشعاران ومستندُ الإلغاء — والأخيرُ
/// لم يُسمِّه التدقيق، <b>والإشعارُ المدينُ انضمّ بـح-٣٩</b> يومَ صار يحمل عملة.</para>
///
/// <para><b>والقياسُ على ما يُبنى منه المطبوع</b> (<c>…RequestAsync</c> ثمّ <c>Figures</c>)
/// — محتوى PDF لا يُقرأ في الاختبار، <b>والرسمُ يقرأ من <c>Figures</c> وحدَها</b>.</para>
/// </summary>
public class ForeignDocumentsPrintInOneCurrencyTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private readonly string _workDirectory;
    private long _companyId;
    private long _customerId;
    private long _supplierId;

    public ForeignDocumentsPrintInOneCurrencyTests()
    {
        TestActor.Administrator();
        _workDirectory = Path.Combine(Path.GetTempPath(), "brqosh-h42", Guid.NewGuid().ToString("N"));
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

    // ── فاتورةُ المبيعات ────────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ الحاكم:</b> السطورُ والإجماليّاتُ بعملةٍ واحدة — <b>فعمودُ الإجماليّ يجمع
    /// إلى الإجماليّ المطبوع تحته</b>، والرمزُ «USD» لا «ر.س».
    /// </summary>
    [Fact]
    public async Task AForeignInvoice_PrintsLinesAndTotalsInOneCurrency()
    {
        await SetupAsync(zatca: true);
        var invoice = await PostUsdInvoiceAsync();

        var request = await Print().SalesInvoiceRequestAsync(_companyId, invoice.Id);
        var figures = SalesInvoicePdfGenerator.Figures(request);

        Assert.Equal(1_000m, figures.Subtotal);
        Assert.Equal(150m, figures.Vat);
        Assert.Equal(1_150m, figures.Total);
        Assert.Equal(figures.Total, figures.LineTotals.Sum());
        Assert.Equal("USD", figures.CurrencySuffix);
    }

    /// <summary>
    /// <b>ورأسُ عمود الضريبة ١٥٪ — لا «56.25%».</b> كانت النسبةُ ضريبةً بالريال مقسومةً
    /// على وعاءٍ بالدولار، <b>فتُطبع على الفاتورة الضريبية نسبةٌ لا وجود لها</b>.
    /// </summary>
    [Fact]
    public async Task TheVatRate_PrintsAsTheTrueRate()
    {
        await SetupAsync(zatca: true);
        var invoice = await PostUsdInvoiceAsync();

        var figures = SalesInvoicePdfGenerator.Figures(
            await Print().SalesInvoiceRequestAsync(_companyId, invoice.Id));

        Assert.Contains("15%", figures.VatRateLabel, StringComparison.Ordinal);
        Assert.DoesNotContain("56.25", figures.VatColumnHeader, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>والريالُ إفصاحٌ مستقلّ</b> — سعرُ الصرف والأرقامُ الثلاثةُ كما في الدفاتر.
    /// <b>والضريبةُ بالريال إلزاميّةٌ متى خالفت العملةُ الريال</b> (BT-111).
    /// </summary>
    [Fact]
    public async Task TheRiyalDisclosure_CarriesTheLedgerFigures()
    {
        await SetupAsync(zatca: true);
        var invoice = await PostUsdInvoiceAsync();

        var sar = SalesInvoicePdfGenerator.Figures(
            await Print().SalesInvoiceRequestAsync(_companyId, invoice.Id)).Sar;

        Assert.NotNull(sar);
        Assert.Equal("USD", sar!.Code);
        Assert.Equal(3.75m, sar.ExchangeRate);
        Assert.Equal(invoice.Subtotal, sar.SubtotalSar);      // ‏3,750.00
        Assert.Equal(invoice.VatAmount, sar.VatSar);          // ‏562.50
        Assert.Equal(4_312.50m, sar.TotalSar);
    }

    /// <summary>
    /// <b>ورمزُ QR الصادرُ من الهيئة يطابق الإجماليَّ المطبوع</b> — وكان بالدولار تحت
    /// إجماليٍّ بالريال، <b>فيمسحه المشتري فيقرأ رقماً غيرَ الذي في الورقة</b>.
    /// </summary>
    [Fact]
    public async Task TheIssuedQr_MatchesThePrintedTotals()
    {
        await SetupAsync(zatca: true);
        var invoice = await PostUsdInvoiceAsync();

        var request = await Print().SalesInvoiceRequestAsync(_companyId, invoice.Id);
        var tags = DecodeTlv(request.Options.QrTlvBase64!);

        Assert.Equal(Amount(request.Total), Encoding.UTF8.GetString(tags[4]));
        Assert.Equal(Amount(request.Vat), Encoding.UTF8.GetString(tags[5]));
    }

    /// <summary>
    /// <b>ورمزُ الطور الأوّل الاحتياطيُّ كذلك</b> — وكان يُبنى من الريال والرمزُ الصادرُ من
    /// الدولار: <b>اصطلاحان لرمزٍ واحد</b>. وصار على اصطلاح الـXML.
    /// </summary>
    [Fact]
    public async Task TheFallbackQr_MatchesThePrintedTotals()
    {
        await SetupAsync(zatca: false);
        var invoice = await PostUsdInvoiceAsync();

        var request = await Print().SalesInvoiceRequestAsync(_companyId, invoice.Id);
        var tags = DecodeTlv(request.Options.QrTlvBase64!);

        Assert.Equal("1150.00", Encoding.UTF8.GetString(tags[4]));
        Assert.Equal("150.00", Encoding.UTF8.GetString(tags[5]));
        Assert.Equal(Amount(request.Total), Encoding.UTF8.GetString(tags[4]));
    }

    // ── الطباعاتُ الأربعُ الأخرى ───────────────────────────────────────────

    /// <summary><b>والإشعارُ الدائنُ الأجنبيّ</b> (ح-٣٥) يُطبع بعملته.</summary>
    [Fact]
    public async Task AForeignCreditNote_PrintsInItsCurrency()
    {
        await SetupAsync(zatca: true);
        var invoice = await PostUsdInvoiceAsync();

        var note = CreditNote.CreateDraft(_companyId, _customerId, invoice.Id, new DateOnly(2026, 7, 10), "إرجاع");
        note.AddLine("خدمة", 1m, 400m);
        note.SetCurrency(invoice.CurrencyCode, invoice.ExchangeRate);
        await new CreditNotePostingService(_db).PostAsync(note);

        var figures = SalesInvoicePdfGenerator.Figures(await Print().CreditNoteRequestAsync(_companyId, note.Id));

        Assert.Equal(460m, figures.Total);
        Assert.Equal(figures.Total, figures.LineTotals.Sum());
        Assert.Equal("USD", figures.CurrencySuffix);
        Assert.Equal(225m, figures.Sar!.VatSar);              // ‏60 × 3.75
    }

    /// <summary>
    /// <b>وفاتورةُ الشراء الأجنبيّة وإشعارُها المدين</b> — والأخيرُ انضمّ إلى الصنف بـح-٣٩.
    /// </summary>
    [Fact]
    public async Task AForeignPurchaseAndItsDebitNote_PrintInTheirCurrency()
    {
        await SetupAsync(zatca: true);
        var purchase = PurchaseInvoice.CreateDraft(
            _companyId, _supplierId, new DateOnly(2026, 7, 9), "USD-P", vatDeductible: true);
        purchase.AddLine("خدمة", 2m, 1_000m);
        purchase.SetCurrency("USD", 3.75m);
        await new PurchaseInvoicePostingService(_db).PostAsync(purchase);

        var note = await new DebitNoteDraftBuilder(_db).BuildAsync(
            _companyId, purchase.Id, new DateOnly(2026, 7, 10), "مرتجع", [new RequestedReturn(0, 1m)]);
        await new DebitNotePostingService(_db).PostAsync(note);

        var invoiceFigures = SalesInvoicePdfGenerator.Figures(
            await Print().PurchaseInvoiceRequestAsync(_companyId, purchase.Id));
        var noteFigures = SalesInvoicePdfGenerator.Figures(
            await Print().DebitNoteRequestAsync(_companyId, note.Id));

        Assert.Equal(2_300m, invoiceFigures.Total);
        Assert.Equal(invoiceFigures.Total, invoiceFigures.LineTotals.Sum());
        Assert.Equal(7_500m, invoiceFigures.Sar!.SubtotalSar);

        Assert.Equal(1_150m, noteFigures.Total);
        Assert.Equal(noteFigures.Total, noteFigures.LineTotals.Sum());
        Assert.Equal("USD", noteFigures.CurrencySuffix);
        Assert.Equal(3_750m, noteFigures.Sar!.SubtotalSar);
    }

    /// <summary>
    /// <b>ومستندُ الإلغاء — الطباعةُ الخامسةُ التي لم يُسمِّها التدقيق.</b> ح-٣٦ جعل الـXML
    /// يحمل عملةَ ما أُلغي، <b>والمطبوعُ بقي يخلط</b>. ورمزُه رمزُ مستند الإلغاء نفسِه.
    /// </summary>
    [Fact]
    public async Task AForeignInvoiceCancellation_PrintsInTheCancelledCurrency()
    {
        await SetupAsync(zatca: true);
        var invoice = await PostUsdInvoiceAsync();

        await using (var tx = await _db.Database.BeginTransactionAsync())
        {
            await new ZatcaEInvoiceGenerator(_db).TryGenerateCancellationForSalesInvoiceAsync(invoice.Id);
            await tx.CommitAsync();
        }
        var doc = await _db.ZatcaEInvoiceDocuments.SingleAsync(
            d => d.DocumentType == ZatcaDocumentType.InvoiceCancel && d.SourceDocumentId == invoice.Id);
        // ‏(ح-٥٣) **والإرسالُ الذي يليه في المسار الحقيقيّ** (`SalesInvoiceCancellationService`) —
        // ‏في المحاكاة يصير «محاكاةً محلّيّة» فيُطبع برمزه. وكان الاختبارُ يطبع مستندَ تخليصٍ
        // ‏«جاهزاً للاعتماد» برمز، **وهو بعينه ما يمنعه ح-٥٣**: تجاوزَ الإرسالَ فحرس العطل.
        await ZatcaAutoSubmissionHelper.TrySubmitDocumentAsync(_db, _companyId, doc);
        Assert.Equal(ZatcaComplianceStatus.SimulatedLocally, doc.ComplianceStatus);

        var (request, _) = await Print().ZatcaCancellationRequestAsync(_companyId, doc.Id);
        var figures = SalesInvoicePdfGenerator.Figures(request);
        var tags = DecodeTlv(request.Options.QrTlvBase64!);

        Assert.Equal(1_150m, figures.Total);
        Assert.Equal(figures.Total, figures.LineTotals.Sum());
        Assert.Equal("USD", figures.CurrencySuffix);
        Assert.Equal(Amount(figures.Total), Encoding.UTF8.GetString(tags[4]));
    }

    // ── شاهدُ ضبط ─────────────────────────────────────────────────────────

    /// <summary>
    /// <b>وفاتورةُ الريال تُطبع كما كانت حرفاً</b> — «ر.س» ولا إفصاح، والرمزُ بأرقامها.
    /// <b>ولولاه لجاز أن يكون الإصلاحُ غيّر كلَّ مطبوعةٍ قائمة.</b>
    /// </summary>
    [Fact]
    public async Task ARiyalInvoice_PrintsExactlyAsBefore()
    {
        await SetupAsync(zatca: false);
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 9));
        invoice.AddLine("خدمة", 1m, 1_000m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var request = await Print().SalesInvoiceRequestAsync(_companyId, invoice.Id);
        var figures = SalesInvoicePdfGenerator.Figures(request);
        var tags = DecodeTlv(request.Options.QrTlvBase64!);

        Assert.Equal(1_000m, figures.Subtotal);
        Assert.Equal(1_150m, figures.Total);
        Assert.Equal("ر.س", figures.CurrencySuffix);
        Assert.Null(figures.Sar);
        Assert.Equal("1150.00", Encoding.UTF8.GetString(tags[4]));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private DocumentPrintService Print() =>
        new(_db, new AuditService(_db), new ExchangeRateService(_db));

    private static string Amount(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static Dictionary<byte, byte[]> DecodeTlv(string base64)
    {
        var bytes = Convert.FromBase64String(base64);
        var tags = new Dictionary<byte, byte[]>();
        for (var i = 0; i < bytes.Length;)
        {
            var tag = bytes[i];
            var length = bytes[i + 1];
            tags[tag] = bytes[(i + 2)..(i + 2 + length)];
            i += 2 + length;
        }
        return tags;
    }

    private async Task<SalesInvoice> PostUsdInvoiceAsync()
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 9));
        invoice.AddLine("خدمة", 1m, 1_000m);
        invoice.SetCurrency("USD", 3.75m);
        return await new SalesInvoicePostingService(_db).PostAsync(invoice);
    }

    private async Task SetupAsync(bool zatca)
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
        var supplier = Supplier.Create(_companyId, "مورّد", "312233344400003");
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
