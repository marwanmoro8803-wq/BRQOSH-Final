using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Accounting;

/// <summary>
/// (CEEF-230 · ط4-٧) <b>إعادةُ تقييم الذمم بعملةٍ أجنبية آخرَ الفترة.</b>
///
/// <para><b>ما كان:</b> إعادةُ التقييم تمسّ <b>الأرصدة البنكية وحدَها</b>. فذمّةٌ بالدولار
/// تبقى في الدفاتر <b>بسعر يوم الفاتورة إلى الأبد</b>، وحسابا الفروق غير المحقَّقة
/// (<c>4107</c>/<c>5217</c>) بلا كاتبٍ لجانب الذمم. <b>ومنشأةٌ تُصدّر بالدولار تُقفل
/// سنتَها بذممٍ تخالف قيمتَها الحقيقية.</b></para>
///
/// <para><b>وهو إلزامٌ محاسبيّ</b> — معيارُ المحاسبة الدولي 21: البنودُ النقدية تُترجَم
/// بسعر الإقفال.</para>
/// </summary>
public class MonetaryRevaluationTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _customerId;
    private long _supplierId;

    public MonetaryRevaluationTests()
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
    /// <b>القياسُ الحاكم: ذمّةٌ مدينةٌ بالدولار تُترجَم بسعر الإقفال، والفرقُ ربحٌ غيرُ محقَّق.</b>
    ///
    /// <para>1,150 دولاراً نشأت بسعر 3.75 ⟵ 4,312.50. وسعرُ الإقفال 3.85 ⟵ 4,427.50.
    /// <b>والفرقُ 115 ربحٌ غيرُ محقَّق</b> — وبغيره تُقفل السنةُ بذمّةٍ أقلَّ من قيمتها.</para>
    /// </summary>
    [Fact]
    public async Task AForeignReceivable_IsTranslatedAtTheClosingRate()
    {
        await SetupAsync();
        await SalesInvoiceAsync(1_000m, 3.75m);

        var result = await RevalueAsync(new DateOnly(2026, 12, 31), 3.85m);

        // ‏**والمدى ينتهي بيوم التقييم**: العكسُ في اليوم التالي، ومدى يشمله يُظهر
        // ‏صفراً — **وهو صوابُ التصميم لا خطؤه**. وأوّلُ صياغةٍ لهذا القياس شملته فقرأت
        // ‏الصفرَ عطلاً، **والعطلُ كان في نافذة القياس**.
        var asOf = new DateOnly(2026, 12, 31);

        Assert.Equal(115m, result.ReceivableAdjustment);
        Assert.Equal(115m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.FxAdjustmentReceivable, asOf));
        Assert.Equal(-115m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.UnrealizedFxGain, asOf));
    }

    /// <summary>
    /// <b>والإشارةُ في جانب الموردين معكوسة — وهو مزلقُ النسخ.</b>
    ///
    /// <para>ارتفاعُ السعر يزيد الذمّةَ المدينة <b>ربحاً</b> ويزيد الدائنة <b>خسارة</b>:
    /// صرنا مدينين بأكثر. <b>ونسخُ جانب العملاء يقلب النتيجةَ في كلّ إقفال</b> — فتُقفل
    /// سنةٌ بربحٍ وهي خسارة.</para>
    /// </summary>
    [Fact]
    public async Task AForeignPayable_MovesTheOppositeWay()
    {
        await SetupAsync();
        await PurchaseInvoiceAsync(1_000m, 3.75m);

        var result = await RevalueAsync(new DateOnly(2026, 12, 31), 3.85m);

        var asOf = new DateOnly(2026, 12, 31);

        Assert.Equal(115m, result.PayableAdjustment);
        Assert.Equal(-115m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.FxAdjustmentPayable, asOf));
        Assert.Equal(115m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.UnrealizedFxLoss, asOf));
    }

    /// <summary>
    /// <b>وحسابُ الذمم نفسُه لا يُمسّ — فتصمد هـ-١ وهـ-٢.</b>
    ///
    /// <para><b>وهذا هو سببُ وجود حسابَي التسوية</b>: الدفترُ المساعد يُبنى من المستندات
    /// بأسعار نشأتها، <b>وتقييمُ حساب الذمم يكسر المطابقةَ في كلّ إقفال</b> — فيقضي
    /// المحاسبُ يومَه يبحث عن فرقٍ سببُه النظام.</para>
    /// </summary>
    [Fact]
    public async Task TheControlAccounts_AreNeverTouched()
    {
        await SetupAsync();
        await SalesInvoiceAsync(1_000m, 3.75m);
        await PurchaseInvoiceAsync(500m, 3.75m);

        var arBefore = await BalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsReceivable);
        var apBefore = await BalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsPayable);

        await RevalueAsync(new DateOnly(2026, 12, 31), 3.85m);

        Assert.Equal(arBefore, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsReceivable));
        Assert.Equal(apBefore, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsPayable));

        // ‏**والدفترُ المساعد يساوي حسابَه كما كان** — وهي هـ-١.
        var subsidiary = (await new CustomerBalanceQuery(_db)
            .ForCustomerAsync(_companyId, _customerId)).Balance;
        Assert.Equal(arBefore, subsidiary);
    }

    /// <summary>
    /// <b>ويُعكس القيدُ في اليوم التالي — فلا يتراكم التقييم.</b>
    ///
    /// <para>وبغير العكس يصير الرصيدُ <b>مجموعَ تقديراتِ كلّ فترةٍ مضت</b> لا تقديرَ
    /// اليوم — <b>ويكبر بلا حدّ</b>. فالأثرُ بعد يوم العكس صفر.</para>
    /// </summary>
    [Fact]
    public async Task TheEntry_ReversesTheNextDay()
    {
        await SetupAsync();
        await SalesInvoiceAsync(1_000m, 3.75m);

        var result = await RevalueAsync(new DateOnly(2026, 12, 31), 3.85m);

        Assert.NotNull(result.ReversalEntryId);

        // ‏الرصيدُ حتى نهاية السنة يحمل التقييم…
        Assert.Equal(115m, await BalanceAsync(
            SaudiChartOfAccounts.SystemCodes.FxAdjustmentReceivable, new DateOnly(2026, 12, 31)));

        // ‏…وبعد يوم العكس يعود صفراً.
        Assert.Equal(0m, await BalanceAsync(
            SaudiChartOfAccounts.SystemCodes.FxAdjustmentReceivable, new DateOnly(2027, 12, 31)));
    }

    /// <summary>
    /// <b>وما سُدِّد لا يُعاد تقييمه.</b>
    ///
    /// <para>فالتقييمُ للمفتوح وحدَه — <b>وتقييمُ فاتورةٍ سُدِّدت يُنشئ فرقاً على ذمّةٍ
    /// لم تعد قائمة</b>.</para>
    /// </summary>
    [Fact]
    public async Task ASettledInvoice_IsNotRevalued()
    {
        await SetupAsync();
        var invoice = await SalesInvoiceAsync(1_000m, 3.75m);

        var voucher = ReceiptVoucher.CreateDraft(_companyId, _customerId,
            new DateOnly(2026, 6, 1), 1_150m, ReceiptMethod.Cash, "تحصيل");
        voucher.SetCurrency("USD", 3.75m);
        await new ReceiptVoucherPostingService(_db).PostAsync(voucher,
            [(PaymentAllocationDocumentType.SalesInvoice, invoice.Id, invoice.InvoiceNumber!, 1_150m)]);

        var result = await RevalueAsync(new DateOnly(2026, 12, 31), 3.85m);

        Assert.Equal(0m, result.ReceivableAdjustment);
        Assert.Null(result.JournalEntryId);
    }

    /// <summary>
    /// <b>وعملةٌ مكشوفةٌ بلا سعر إقفال تُرفض ولا تُتجاوَز.</b>
    ///
    /// <para><b>وتجاوزُها يُخرج تقييماً ناقصاً يبدو كاملاً</b> — ولا شيءَ في القيد يدلّ
    /// على ما سقط. <b>ورقمٌ ناقصٌ يُقرأ صحيحاً أسوأُ من رفضٍ يُقرأ.</b></para>
    /// </summary>
    [Fact]
    public async Task AnExposedCurrencyWithoutARate_IsRefusedByName()
    {
        await SetupAsync();
        await SalesInvoiceAsync(1_000m, 3.75m);

        var error = await Assert.ThrowsAsync<AccountingException>(() =>
            new MonetaryRevaluationService(_db).RevalueAsync(
                _companyId, new DateOnly(2026, 12, 31),
                new Dictionary<string, decimal> { ["EUR"] = 4.2m }, "admin"));

        Assert.Contains("USD", error.Message, StringComparison.Ordinal);
    }

    /// <summary><b>ولا يُعاد التقييمُ مرّتين للتاريخ نفسِه</b> — والثانيةُ تُضاعف الأثر.</summary>
    [Fact]
    public async Task RevaluingTheSameDateTwice_IsRefused()
    {
        await SetupAsync();
        await SalesInvoiceAsync(1_000m, 3.75m);

        await RevalueAsync(new DateOnly(2026, 12, 31), 3.85m);

        await Assert.ThrowsAsync<AccountingException>(
            () => RevalueAsync(new DateOnly(2026, 12, 31), 3.85m));

        Assert.Equal(115m, await BalanceAsync(
            SaudiChartOfAccounts.SystemCodes.FxAdjustmentReceivable, new DateOnly(2026, 12, 31)));
    }

    /// <summary><b>ومنشأةٌ بالريال وحده لا يُقيَّد لها شيء.</b></summary>
    [Fact]
    public async Task ARiyalOnlyCompany_PostsNothing()
    {
        await SetupAsync();

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 2, 1));
        invoice.AddLine("خدمة", 1m, 1_000m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var result = await new MonetaryRevaluationService(_db).RevalueAsync(
            _companyId, new DateOnly(2026, 12, 31), new Dictionary<string, decimal>(), "admin");

        Assert.Empty(result.Exposures);
        Assert.Null(result.JournalEntryId);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private Task<MonetaryRevaluationResult> RevalueAsync(DateOnly asOf, decimal usdRate) =>
        new MonetaryRevaluationService(_db).RevalueAsync(
            _companyId, asOf, new Dictionary<string, decimal> { ["USD"] = usdRate }, "admin");

    private async Task<decimal> BalanceAsync(string code, DateOnly? to = null)
    {
        var trial = await new TrialBalanceQuery(_db).ExecuteAsync(
            _companyId, new DateOnly(2026, 1, 1), to ?? new DateOnly(2027, 12, 31));

        return Math.Round(trial.Where(l => l.AccountCode == code)
            .Sum(l => l.TotalDebit - l.TotalCredit), 2);
    }

    private async Task<SalesInvoice> SalesInvoiceAsync(decimal amount, decimal rate)
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 2, 1));
        invoice.SetCurrency("USD", rate);
        invoice.AddLine("تصدير", 1m, amount);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);
        return invoice;
    }

    private async Task<PurchaseInvoice> PurchaseInvoiceAsync(decimal amount, decimal rate)
    {
        var invoice = PurchaseInvoice.CreateDraft(_companyId, _supplierId,
            new DateOnly(2026, 2, 1), $"P-{Guid.NewGuid().ToString()[..8]}", true);
        invoice.SetCurrency("USD", rate);
        invoice.AddLine("استيراد", 1m, amount);
        await new PurchaseInvoicePostingService(_db).PostAsync(invoice);
        return invoice;
    }

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;

        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة التصدير والاستيراد", "310123456700044", BusinessProfile.General);
        _companyId = company.Id;

        var customer = Customer.Create(_companyId, "عميل دوليّ", "311111111100003");
        var supplier = Supplier.Create(_companyId, "مورّد دوليّ", "312233344400003");
        _db.Customers.Add(customer);
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        _customerId = customer.Id;
        _supplierId = supplier.Id;
    }
}
