using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Treasury;

/// <summary>
/// (CEEF-230 · ط4-٥) <b>عملةُ المشتريات والسداد — وكان جانبُ الموردين بلا عملةٍ إطلاقاً.</b>
///
/// <para><b>ما كان:</b> <c>ط1-٤</c> أعطى جانبَ العملاء عملةً وسعراً وفرقاً محقَّقاً،
/// <b>وأُعلن أنّ جانبَ الموردين بقي</b>. فالمستوردُ لا يستطيع تسجيلَ فاتورةٍ بالدولار
/// إطلاقاً: <b>يُدخلها محوَّلةً بيده</b>، فتضيع القيمةُ الأصلية — <b>ولا يُعرف عند السداد
/// بأيّ سعرٍ نشأت الذمّة</b>، فلا يُحسب فرقُ عملةٍ محقَّق وتبقى الذمّةُ مفتوحةً بفرقٍ
/// لا يُغلق.</para>
///
/// <para><b>والإشارةُ معكوسةٌ عن جانب العملاء</b>: نقدٌ يخرج <b>أقلَّ</b> من الذمّة ربحٌ
/// للمنشأة. <b>ونسخُ منطق العملاء حرفاً يقلب الربحَ خسارةً في كلّ سدادٍ بالعملة</b> —
/// وهو ما يقيسه هذا الملفّ.</para>
/// </summary>
public class PayableRealizedFxTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _supplierId;

    public PayableRealizedFxTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// <b>القياسُ الحاكم: فاتورةٌ بالدولار تُقيَّد بالريال وتحفظ قيمتَها الأصلية.</b>
    ///
    /// <para>ألفُ دولارٍ بسعر 3.75 ⟵ ذمّةٌ 4,312.50 ريالاً بالضريبة، <b>والأصلُ الأجنبيُّ
    /// 1,150 دولاراً محفوظ</b>. وبغير حفظه يُطالِب المورّدُ بألفٍ ومئةٍ وخمسين ولا يجد
    /// النظامُ ما يُقابله.</para>
    /// </summary>
    [Fact]
    public async Task AForeignPurchase_KeepsItsOriginalValueAndPostsInRiyals()
    {
        var invoice = await ForeignPurchaseAsync(1_000m, 3.75m);

        Assert.Equal("USD", invoice.CurrencyCode);
        Assert.Equal(1_150m, invoice.ForeignGrandTotal);
        Assert.Equal(4_312.50m, invoice.Subtotal + invoice.VatAmount);
    }

    /// <summary>
    /// <b>وسدادٌ بسعرٍ أدنى ربحٌ محقَّق — والإشارةُ معكوسةٌ عن جانب العملاء.</b>
    ///
    /// <para>ذمّةٌ 4,312.50 تُسدَّد بـ1,150 دولاراً بسعر 3.70 ⟵ يخرج 4,255. <b>ودفعنا
    /// أقلَّ ممّا كنّا مدينين به بـ57.50 — وهو ربح</b>. <b>ونسخُ منطق العملاء يجعله
    /// خسارة</b>، فتُقفل السنةُ بمصروفٍ لم يقع.</para>
    /// </summary>
    [Fact]
    public async Task PayingAtALowerRate_ClosesThePayableAndBooksAGain()
    {
        var invoice = await ForeignPurchaseAsync(1_000m, 3.75m);

        await PayAsync(invoice, foreignAmount: 1_150m, rate: 3.70m);

        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsPayable));
        Assert.Equal(-4_255m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.CashOnHand));

        // ‏4108 حسابُ إيرادٍ — رصيدُه الدائن يظهر سالباً بمنطق (مدين − دائن).
        Assert.Equal(-57.50m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.RealizedFxGain));
    }

    /// <summary><b>والاتّجاه الآخر: سعرٌ أعلى ⟵ خسارةٌ محقَّقة، والذمّةُ تُغلق كذلك.</b></summary>
    [Fact]
    public async Task PayingAtAHigherRate_BooksALoss()
    {
        var invoice = await ForeignPurchaseAsync(1_000m, 3.75m);

        await PayAsync(invoice, foreignAmount: 1_150m, rate: 3.80m);

        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsPayable));
        Assert.Equal(57.50m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.RealizedFxLoss));
    }

    /// <summary>
    /// <b>وسندٌ بالريال يسلك مسارَه القديم حرفاً — لا حسابَ يُنشأ ولا سطرَ يُضاف.</b>
    ///
    /// <para><b>وهو ما يحمي كلَّ قاعدةٍ قائمة</b>: منشأةٌ لا تستورد لا يتغيّر فيها رقم.</para>
    /// </summary>
    [Fact]
    public async Task ARiyalPayment_AddsNoFxLine()
    {
        var invoice = await ForeignPurchaseAsync(1_000m, 1m, currency: "SAR");

        await PayAsync(invoice, foreignAmount: 1_150m, rate: 1m, currency: "SAR");

        var lines = await _db.JournalEntries.AsNoTracking()
            .Where(e => e.SourceDocument!.StartsWith("PaymentVoucher"))
            .SelectMany(e => e.Lines).ToListAsync();

        Assert.Equal(2, lines.Count);
        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsPayable));
    }

    /// <summary>
    /// <b>ولا يُخلط سندٌ واحدٌ بعملتين.</b>
    ///
    /// <para>تخصيصُ سندٍ دولاريٍّ على فاتورةٍ بالريال يُقارن مبلغين لا يقبل أحدُهما الآخر،
    /// <b>ويُنتج «فرقَ عملة» هو في الحقيقة خطأُ إدخال</b>.</para>
    /// </summary>
    [Fact]
    public async Task AVoucherCannotMixTwoCurrencies()
    {
        var riyalInvoice = await ForeignPurchaseAsync(1_000m, 1m, currency: "SAR");

        var error = await Assert.ThrowsAsync<AccountingException>(
            () => PayAsync(riyalInvoice, foreignAmount: 1_150m, rate: 3.75m));

        Assert.Contains("لا يُخلط", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>ودفترُ الموردين يُحسب بطريقتين ويجب أن يتّفقا — والدرسُ مأخوذٌ لا مُعاد.</b>
    ///
    /// <para>هذا بعينه ما كُشف في <c>ط4-٣</c> على جانب العملاء: <b>إصلاحٌ طُبّق على مسارٍ
    /// من اثنين</b> فتناقض كشفُ الحساب مع اللوحة. <b>فيُقاس هنا من أوّل يوم</b> — لا
    /// يُنتظر أن يظهر عند عميل.</para>
    /// </summary>
    [Fact]
    public async Task TheTwoWaysOfComputingTheSupplierSubsidiary_Agree()
    {
        var invoice = await ForeignPurchaseAsync(1_000m, 3.75m);
        await PayAsync(invoice, foreignAmount: 1_150m, rate: 3.70m);

        var query = new SupplierBalanceQuery(_db);
        var single = (await query.ForSupplierAsync(_companyId, _supplierId)).Balance;
        var all = (await query.AllBalancesAsync(_companyId))[_supplierId];

        Assert.Equal(all, single);

        // ‏**وهويّةُ هـ-٢**: الدفترُ المساعد يساوي حسابَ الموردين في الأستاذ.
        var ledger = -await BalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsPayable);
        Assert.Equal(ledger, single);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<PurchaseInvoice> ForeignPurchaseAsync(
        decimal amount, decimal rate, string currency = "USD")
    {
        await SetupAsync();

        var invoice = PurchaseInvoice.CreateDraft(_companyId, _supplierId,
            new DateOnly(2026, 2, 1), $"INV-{Guid.NewGuid().ToString()[..8]}", true);

        if (currency != "SAR") invoice.SetCurrency(currency, rate);
        invoice.AddLine("مادة مستوردة", 1m, amount);

        await new PurchaseInvoicePostingService(_db).PostAsync(invoice);
        return invoice;
    }

    private async Task PayAsync(PurchaseInvoice invoice, decimal foreignAmount, decimal rate,
        string currency = "USD")
    {
        var voucher = PaymentVoucher.CreateDraft(_companyId, invoice.SupplierId,
            new DateOnly(2026, 3, 1), foreignAmount, ReceiptMethod.Cash, "سداد");

        if (currency != "SAR") voucher.SetCurrency(currency, rate);

        await new PaymentVoucherPostingService(_db).PostAsync(voucher,
            [(PaymentAllocationDocumentType.PurchaseInvoice, invoice.Id,
              invoice.InvoiceNumber!, foreignAmount)]);
    }

    private async Task<decimal> BalanceAsync(string code)
    {
        var trial = await new Brqosh.Infrastructure.Reporting.TrialBalanceQuery(_db)
            .ExecuteAsync(_companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        return Math.Round(trial.Where(l => l.AccountCode == code)
            .Sum(l => l.TotalDebit - l.TotalCredit), 2);
    }

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;

        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة الاستيراد", "310123456700011", BusinessProfile.General);
        _companyId = company.Id;

        var supplier = Supplier.Create(_companyId, "مورّد دوليّ", "312233344400003");
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();
        _supplierId = supplier.Id;
    }
}
