using System.Xml.Linq;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Zatca;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Sales;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٣٥) <b>الإشعارُ الدائنُ كان بلا عملةٍ أصلاً — فمرتجعُ فاتورةٍ
/// دولاريّةٍ يردّ أرقامَها ريالات.</b>
///
/// <para><b>وسطورُ الفاتورة الأجنبيّة مُسعَّرةٌ بعملتها</b>: <c>ForeignSubtotal</c> يُحسب
/// منها <b>والريالُ يُشتقّ بالضرب في السعر</b>. والشاشةُ تنسخ سعرَ السطر إلى إشعارٍ
/// <b>لا حقلَ عملةٍ فيه</b>، فيُقرأ الدولارُ ريالاً.</para>
///
/// <para><b>وقِيس قبل الإصلاح</b> — فاتورةُ <c>USD 1,000</c> بسعر <c>3.75</c>:</para>
///
/// <code>
/// invoiceSarGrand=4312.50 | noteGrand=1150.00 | balanceAfterFullReturn=3162.50
/// </code>
///
/// <para><b>فالعميلُ يردّ البضاعةَ كلَّها ويبقى مديناً بـ٣٬١٦٢٫٥٠</b> — ٧٣٪ من فاتورةٍ
/// لم يبقَ منها شيء. <b>وضريبةُ المخرجات تُعكَس بـ١٥٠ بدل ٥٦٢٫٥٠</b>، فالإقرارُ يُبالغ
/// في مخرجاتِه عن توريدٍ رُدّ بتمامه.</para>
///
/// <para><b>والمرآةُ <c>SalesInvoice</c> حرفاً</b> — وهذا نقلُ نمطٍ قائمٍ لا اختراع،
/// <b>والافتراضُ ريالٌ بسعرِ واحد فلا يتغيّر رقمٌ في إشعارٍ قائم</b>.</para>
/// </summary>
public class CreditNoteCarriesItsInvoiceCurrencyTests : IDisposable
{
    private static readonly XNamespace Cbc =
        "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private readonly string _workDirectory;
    private long _companyId;
    private long _customerId;

    public CreditNoteCarriesItsInvoiceCurrencyTests()
    {
        TestActor.Administrator();
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);

        _workDirectory = Path.Combine(Path.GetTempPath(), "brqosh-h35", Guid.NewGuid().ToString("N"));
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

    // ── المال ───────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ الحاكم — ويُقاس على كشف العميل لا على حقلٍ في صفّ:</b> مرتجعٌ كاملٌ
    /// <b>يُصفّي الرصيد</b>. وكان يتركه ٣٬١٦٢٫٥٠ وقد رُدَّ كلُّ شيء.
    /// </summary>
    [Fact]
    public async Task AFullReturnOfAForeignInvoice_ClearsTheBalance()
    {
        await SetupAsync();
        var invoice = await PostUsdInvoiceAsync(1_000m, 3.75m);

        await PostFullReturnAsync(invoice);

        Assert.Equal(0m, await BalanceAsync());
    }

    /// <summary>
    /// <b>والإشعارُ يحمل عملةَ فاتورته وسعرَها</b> — والريالُ يُشتقّ منهما.
    /// </summary>
    [Fact]
    public async Task TheNote_CarriesTheInvoicesCurrencyAndRate()
    {
        await SetupAsync();
        var invoice = await PostUsdInvoiceAsync(1_000m, 3.75m);

        var note = await PostFullReturnAsync(invoice);

        Assert.Equal("USD", note.CurrencyCode);
        Assert.Equal(3.75m, note.ExchangeRate);
        Assert.Equal(1_000m, note.ForeignSubtotal);
        Assert.Equal(3_750m, note.Subtotal);
        Assert.Equal(562.50m, note.VatAmount);
    }

    /// <summary>
    /// <b>وضريبةُ المخرجات تُعكَس بتمامها</b> — <b>ويُقاس على تقرير الإقرار</b>، فهو
    /// ما يُقدَّم. وكانت تُعكَس بـ١٥٠ من ٥٦٢٫٥٠، <b>فيبقى في الإقرار مخرجاتٌ عن توريدٍ
    /// لم يبقَ منه شيء</b>.
    /// </summary>
    [Fact]
    public async Task TheOutputVat_IsReversedInFull()
    {
        await SetupAsync();
        var invoice = await PostUsdInvoiceAsync(1_000m, 3.75m);

        await PostFullReturnAsync(invoice);

        var vat = await new VatReturnQuery(_db).ExecuteAsync(
            _companyId, new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31));

        Assert.Equal(562.50m, vat.CreditNotesVat);
        Assert.Equal(vat.OutputVat, vat.CreditNotesVat);
    }

    // ── المستندُ الإلكترونيّ ────────────────────────────────────────────────

    /// <summary>
    /// <b>والمستندُ يخرج بعملته</b> — <c>DocumentCurrencyCode</c> دولارٌ والضريبةُ ريال.
    ///
    /// <para><b>ويُقاس على ما أنتجه الترحيلُ نفسُه</b> لا على بناءٍ في الاختبار —
    /// فالمقيسُ هو <c>ZatcaEInvoiceGenerator</c> الذي كان <b>يُمرّر الريالَ ويُسقط
    /// العملةَ والسعرَ وضريبةَ الريال وخصمَ المستند</b>: أربعةً ممّا تُمرّره الفاتورةُ.</para>
    /// </summary>
    [Fact]
    public async Task TheZatcaCreditNote_IsIssuedInTheDocumentCurrency()
    {
        await SetupAsync(enableZatca: true);
        var invoice = await PostUsdInvoiceAsync(1_000m, 3.75m);
        var note = await PostFullReturnAsync(invoice);

        var xml = await GeneratedXmlAsync(note.Id);

        Assert.Empty(UblStructureValidator.Validate(xml));
        Assert.Equal("USD", xml.Root!.Element(Cbc + "DocumentCurrencyCode")!.Value);
        Assert.Equal("SAR", xml.Root!.Element(Cbc + "TaxCurrencyCode")!.Value);
    }

    // ── شواهدُ الضبط ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>شاهدُ ضبطٍ أوّل — والريالُ لم يتغيّر حرفاً:</b> بسعرِ واحدٍ الأجنبيُّ والريالُ
    /// رقمٌ واحد. <b>ولولاه لجاز أن يكون الإصلاحُ مسّ كلَّ إشعارٍ قائم.</b>
    /// </summary>
    [Fact]
    public async Task ASarCreditNote_IsUnchanged()
    {
        await SetupAsync();
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 5, 1));
        invoice.AddLine("توريد", 1m, 1_000m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var note = await PostFullReturnAsync(invoice);

        Assert.Equal("SAR", note.CurrencyCode);
        Assert.Equal(1m, note.ExchangeRate);
        Assert.Equal(1_000m, note.Subtotal);
        Assert.Equal(1_000m, note.ForeignSubtotal);
        Assert.Equal(0m, await BalanceAsync());
    }

    /// <summary>
    /// <b>وشاهدُ ضبطٍ ثانٍ — مرتجعٌ جزئيٌّ يردّ حصّتَه لا أكثر:</b> نصفُ الكمّية
    /// ⟵ نصفُ الرصيد.
    /// </summary>
    [Fact]
    public async Task APartialReturn_GivesBackItsShareOnly()
    {
        await SetupAsync();
        var invoice = await PostUsdInvoiceAsync(1_000m, 3.75m, quantity: 2m);
        Assert.Equal(8_625m, await BalanceAsync());

        var line = invoice.Lines[0];
        var note = CreditNote.CreateDraft(_companyId, _customerId, invoice.Id,
            new DateOnly(2026, 5, 10), "مرتجع جزئيّ");
        note.SetCurrency(invoice.CurrencyCode, invoice.ExchangeRate);
        note.AddLine(line.Description, 1m, line.UnitPrice, line.ProductId,
            line.VatCategory, line.TaxExemptionReasonCode, line.TaxExemptionReason);
        await new CreditNotePostingService(_db).PostAsync(note);

        Assert.Equal(4_312.50m, await BalanceAsync());
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<decimal> BalanceAsync() =>
        (await new CustomerBalanceQuery(_db).ForCustomerAsync(_companyId, _customerId)).Balance;

    private async Task<SalesInvoice> PostUsdInvoiceAsync(
        decimal unitPrice, decimal rate, decimal quantity = 1m)
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 5, 1));
        invoice.AddLine("توريد", quantity, unitPrice);
        invoice.SetCurrency("USD", rate);
        return await new SalesInvoicePostingService(_db).PostAsync(invoice);
    }

    /// <summary>مرتجعٌ كامل — <b>كما تبنيه الشاشةُ: تُورَّث العملةُ ويُنسخ سعرُ السطر</b>.</summary>
    private async Task<CreditNote> PostFullReturnAsync(SalesInvoice invoice)
    {
        var note = CreditNote.CreateDraft(_companyId, _customerId, invoice.Id,
            new DateOnly(2026, 5, 10), "مرتجع كامل");

        if (invoice.IsForeignCurrency)
            note.SetCurrency(invoice.CurrencyCode, invoice.ExchangeRate);

        foreach (var line in invoice.Lines)
            note.AddLine(line.Description, line.Quantity, line.UnitPrice, line.ProductId,
                line.VatCategory, line.TaxExemptionReasonCode, line.TaxExemptionReason);

        return await new CreditNotePostingService(_db).PostAsync(note);
    }

    private async Task<XDocument> GeneratedXmlAsync(long creditNoteId)
    {
        var document = await _db.ZatcaEInvoiceDocuments
            .Where(d => d.CompanyId == _companyId
                        && d.SourceDocumentId == creditNoteId
                        && d.DocumentType == ZatcaDocumentType.CreditNote)
            .OrderByDescending(d => d.Id)
            .FirstOrDefaultAsync();

        Assert.True(document is not null,
            "‏الترحيلُ لم يُنتج مستند ZATCA للإشعار — لا شيء يصل الهيئة.");

        var bytes = await File.ReadAllBytesAsync(document!.XmlFilePath);
        return XDocument.Parse(System.Text.Encoding.UTF8.GetString(bytes).TrimStart('﻿'));
    }

    private async Task SetupAsync(bool enableZatca = false)
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة المرتجعات", "310123456700138", BusinessProfile.Retail);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        company.UpdateRegulatoryIdentity(
            "منشأة المرتجعات للتجارة", "1010123456",
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

        if (!enableZatca) return;

        var (certPath, keyPath) = ZatcaSignatureVerifier.CreateSigningCertificate(_workDirectory);
        var settings = await _db.ZatcaSettings.SingleAsync(s => s.CompanyId == _companyId);
        settings.Update(isEnabled: true, ZatcaEnvironment.Simulation, "", certPath, keyPath, "");
        await _db.SaveChangesAsync();
    }
}
