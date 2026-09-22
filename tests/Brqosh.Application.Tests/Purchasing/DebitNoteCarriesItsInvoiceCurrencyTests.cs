using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Reporting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Purchasing;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٣٩) <b>الإشعارُ المدينُ كان بلا عملةٍ أصلاً — مرآةُ ح-٣٥ في
/// المشتريات، ولم يُدرجه التدقيق.</b>
///
/// <para><b>وقِيس قبل الإصلاح</b> — شراءٌ بـ<c>USD 1,000</c> بسعر <c>3.75</c>:</para>
///
/// <code>
/// invoiceSarGrand=4312.50 | noteGrand=1150.00 | owedAfterFullReturn=3162.50
/// </code>
///
/// <para><b>فتُعاد البضاعةُ كلُّها وتبقى الدفاترُ تقول إنّا مدينون للمورّد بـ٣٬١٦٢٫٥٠</b>
/// — دفعٌ زائدٌ ينتظر أن يقع. <b>وضريبةُ المدخلات تُعكَس بـ١٥٠ بدل ٥٦٢٫٥٠</b>، فيُستردّ
/// من الهيئة ما لم يُدفع.</para>
///
/// <para><b>وسبقَ العطلُ بانيَ ح-٣٧</b> — الشاشةُ القديمةُ نسخت السعرَ كذلك — <b>لكنّ
/// الباني حمل المنتَجَ والثلاثيَّ الضريبيَّ وأغفل العملة</b>، فلم يُمسكه.</para>
/// </summary>
public class DebitNoteCarriesItsInvoiceCurrencyTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _supplierId;

    public DebitNoteCarriesItsInvoiceCurrencyTests()
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

    // ── المال ───────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ الحاكم — على كشف المورّد:</b> مرتجعٌ كاملٌ <b>يُصفّي المستحقّ</b>.
    /// وكان يتركه ٣٬١٦٢٫٥٠ وقد أُعيد كلُّ شيء.
    /// </summary>
    [Fact]
    public async Task AFullReturnOfAForeignPurchase_ClearsWhatIsOwed()
    {
        await SetupAsync();
        var invoice = await PostUsdPurchaseAsync(1_000m, 3.75m);

        await PostReturnAsync(invoice, quantity: 1m);

        Assert.Equal(0m, await OwedAsync());
    }

    /// <summary><b>والإشعارُ يحمل عملةَ فاتورته وسعرَها</b> — والريالُ يُشتقّ منهما.</summary>
    [Fact]
    public async Task TheNote_CarriesTheInvoicesCurrencyAndRate()
    {
        await SetupAsync();
        var invoice = await PostUsdPurchaseAsync(1_000m, 3.75m);

        var note = await PostReturnAsync(invoice, quantity: 1m);

        Assert.Equal("USD", note.CurrencyCode);
        Assert.Equal(3.75m, note.ExchangeRate);
        Assert.Equal(1_000m, note.ForeignSubtotal);
        Assert.Equal(3_750m, note.Subtotal);
        Assert.Equal(562.50m, note.VatAmount);
    }

    /// <summary>
    /// <b>وضريبةُ المدخلات تُعكَس بتمامها</b> — <b>مقيسةً على تقرير الإقرار</b>.
    /// وكانت تُعكَس بـ١٥٠ من ٥٦٢٫٥٠، <b>فيُستردّ من الهيئة ما لم يُدفع</b>.
    /// </summary>
    [Fact]
    public async Task TheInputVat_IsReversedInFull()
    {
        await SetupAsync();
        var invoice = await PostUsdPurchaseAsync(1_000m, 3.75m);

        await PostReturnAsync(invoice, quantity: 1m);

        var vat = await new VatReturnQuery(_db).ExecuteAsync(
            _companyId, new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31));

        Assert.Equal(562.50m, vat.DebitNotesDeductibleVat);
        Assert.Equal(vat.DeductibleInputVat, vat.DebitNotesDeductibleVat);
    }

    /// <summary>
    /// <b>وحدُّ «متبقّي الفاتورة» يقارن ريالاً بريال.</b> سطرُ خدمةٍ بلا منتَج <b>لا يحرسه
    /// إلّا هذا الحدّ</b> (فحصُ الكمّيّة بالصنف لا يبلغه)، وكان الإشعارُ يُقاس دولاراً
    /// والفاتورةُ ريالاً — <b>فيمرّ إرجاعُ ضعفِ ما اشتُري</b>.
    /// </summary>
    [Fact]
    public async Task ReturningMoreThanWasBought_IsRefused()
    {
        await SetupAsync();
        var invoice = await PostUsdPurchaseAsync(1_000m, 3.75m);

        var ex = await Assert.ThrowsAsync<AccountingException>(
            () => PostReturnAsync(invoice, quantity: 2m));

        Assert.Contains("تتجاوز المتبقي", ex.Message);
        Assert.Equal(4_312.50m, await OwedAsync());
    }

    // ── شواهدُ الضبط ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>شاهدُ ضبطٍ أوّل — الريالُ لم يتغيّر حرفاً:</b> بسعرِ واحدٍ الأجنبيُّ والريالُ
    /// رقمٌ واحد. <b>ولولاه لجاز أن يكون الإصلاحُ مسّ كلَّ إشعارٍ قائم.</b>
    /// </summary>
    [Fact]
    public async Task ASarDebitNote_IsUnchanged()
    {
        await SetupAsync();
        var invoice = PurchaseInvoice.CreateDraft(
            _companyId, _supplierId, new DateOnly(2026, 5, 1), "SAR-1", vatDeductible: true);
        invoice.AddLine("خدمة", 1m, 1_000m);
        await new PurchaseInvoicePostingService(_db).PostAsync(invoice);

        var note = await PostReturnAsync(invoice, quantity: 1m);

        Assert.Equal("SAR", note.CurrencyCode);
        Assert.Equal(1m, note.ExchangeRate);
        Assert.Equal(1_000m, note.Subtotal);
        Assert.Equal(1_000m, note.ForeignSubtotal);
        Assert.Equal(0m, await OwedAsync());
    }

    /// <summary><b>وشاهدُ ضبطٍ ثانٍ — مرتجعٌ جزئيٌّ يُنقص حصّتَه لا أكثر.</b></summary>
    [Fact]
    public async Task APartialReturn_ReducesItsShareOnly()
    {
        await SetupAsync();
        var invoice = await PostUsdPurchaseAsync(1_000m, 3.75m, quantity: 2m);
        Assert.Equal(8_625m, await OwedAsync());

        await PostReturnAsync(invoice, quantity: 1m);

        Assert.Equal(4_312.50m, await OwedAsync());
    }

    /// <summary>
    /// <b>وشاهدُ ضبطٍ ثالث — هويّةُ الموردين (هـ-٢) سليمة:</b> الدفترُ المساعدُ وحسابُ
    /// المراقبة يتحرّكان معاً بالريال.
    /// </summary>
    [Fact]
    public async Task ThePayablesIdentity_Holds()
    {
        await SetupAsync();
        var invoice = await PostUsdPurchaseAsync(1_000m, 3.75m);

        await PostReturnAsync(invoice, quantity: 1m);

        LedgerIdentities.Assert(await LedgerIdentities.PayablesAsync(_db, _companyId));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<decimal> OwedAsync() =>
        (await new SupplierBalanceQuery(_db).ForSupplierAsync(_companyId, _supplierId)).Balance;

    private async Task<PurchaseInvoice> PostUsdPurchaseAsync(
        decimal unitPrice, decimal rate, decimal quantity = 1m)
    {
        var invoice = PurchaseInvoice.CreateDraft(
            _companyId, _supplierId, new DateOnly(2026, 5, 1),
            $"USD-{quantity}", vatDeductible: true);
        invoice.AddLine("خدمة", quantity, unitPrice);
        invoice.SetCurrency("USD", rate);
        return await new PurchaseInvoicePostingService(_db).PostAsync(invoice);
    }

    /// <summary>مرتجعٌ <b>كما يبنيه `DebitNoteDraftBuilder` حرفاً</b> — وهو ما تستدعيه الشاشة.</summary>
    private async Task<DebitNote> PostReturnAsync(PurchaseInvoice invoice, decimal quantity)
    {
        var note = await new DebitNoteDraftBuilder(_db).BuildAsync(
            _companyId, invoice.Id, new DateOnly(2026, 5, 10), "مرتجع",
            [new RequestedReturn(0, quantity)]);
        return await new DebitNotePostingService(_db).PostAsync(note);
    }

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة الاستيراد", "310123456700144", BusinessProfile.General);
        _companyId = company.Id;

        var supplier = Supplier.Create(_companyId, "مورّد دوليّ", "312233344400003");
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();
        _supplierId = supplier.Id;
    }
}
