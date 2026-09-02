using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Treasury;

/// <summary>
/// (CEEF-231 · ط1-٢ج) <b>خصمُ السداد المبكر — والحسابان كانا بلا كاتبٍ في المستودع كلِّه.</b>
///
/// <para><b>وهو ممارسةٌ قائمةٌ لا حالةٌ شاذّة</b>: «٢٪ خلال عشرة أيام، وإلا فثلاثون».
/// فالعميلُ يدفع ٩٨٠ عن فاتورةِ ألف، <b>والذمّةُ تُطفأ كاملةً</b> والعشرون خصمٌ مسموحٌ به
/// على <c>4103</c>.</para>
///
/// <para><b>وبغيره أحدُ أمرين، وكلاهما عطل:</b> إمّا تبقى العشرون ذمّةً مفتوحةً إلى الأبد
/// <b>فتُقرأ ديناً وتُطالَب</b> ويكبر الوهمُ بكلّ فاتورة؛ وإمّا تُشطب <b>بقيدٍ يدويّ</b> —
/// وقيدٌ يدويٌّ <b>يُغيّر الأستاذَ ولا يراه الدفترُ المساعد</b>، فتنكسر <c>هـ-١</c> ويفترق
/// كشفُ العميل عن الميزانية.</para>
///
/// <para><b>والإشارةُ معكوسةٌ بين الجانبين</b>: المسموحُ به مدينٌ يُنقص الإيراد، والمكتسَبُ
/// دائنٌ يُنقص المصروف. <b>ونسخُ جانبٍ إلى آخرَ حرفاً يقلب الكسبَ خسارةً.</b></para>
/// </summary>
public class SettlementDiscountTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _customerId;
    private long _supplierId;
    private int _seq;

    public SettlementDiscountTests()
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

    // ── جانبُ العملاء: خصمٌ مسموحٌ به ────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ الحاكم: نقدٌ أقلُّ من الفاتورة يُطفئ الذمّةَ كاملةً، والفرقُ خصمٌ مسموحٌ به.</b>
    ///
    /// <para><b>وكلا الطرفين يُقاس</b>: الذمّةُ تُغلق <b>و<c>4103</c> يُدين بالفرق</b>.
    /// فإطفاءٌ بلا قيدٍ يُخفي الخصمَ في الإيراد، <b>وقيدٌ بلا إطفاءٍ يترك ذمّةً وهمية</b>.</para>
    /// </summary>
    [Fact]
    public async Task AnEarlyPaymentDiscount_ClearsTheReceivableAndBooksTheDiscount()
    {
        await SetupAsync();
        var invoice = await SalesInvoiceAsync(1_000m);   // ‏1,150 بالضريبة

        await ReceiveAsync(1_127m, discount: 23m, allocateTo: invoice);

        // ‏الذمّةُ صفرٌ — والعميلُ لا يُطالَب بشيء.
        var balance = (await new CustomerBalanceQuery(_db)
            .AllBalancesAsync(_companyId))[_customerId];
        Assert.Equal(0m, balance);

        Assert.Equal(23m, await BalanceAsync("4103"));
    }

    /// <summary>
    /// <b>وهويّةُ هـ-١ تصمد: الدفترُ المساعد = رصيدُ حساب الذمم.</b>
    ///
    /// <para><b>وهذا ما يكسره القيدُ اليدويّ</b>: يُصلح الأستاذَ ويترك كشفَ العميل كما
    /// كان — فيقرأ المحاسبُ رقمين لشيءٍ واحد.</para>
    /// </summary>
    [Fact]
    public async Task TheSubsidiaryLedger_StaysEqualToTheControlAccount()
    {
        await SetupAsync();
        var invoice = await SalesInvoiceAsync(2_000m);   // ‏2,300

        await ReceiveAsync(2_254m, discount: 46m, allocateTo: invoice);

        var subsidiary = (await new CustomerBalanceQuery(_db)
            .AllBalancesAsync(_companyId))[_customerId];

        Assert.Equal(await BalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsReceivable), subsidiary);
    }

    /// <summary>
    /// <b>وسندٌ بلا خصمٍ لا يُنشئ الحسابَ ولا يمسّه.</b>
    ///
    /// <para><b>فقاعدةٌ لا تمنح خصماً لا يظهر فيها حسابٌ بلا حركة</b> — وهو اصطلاحُ هذا
    /// المستودع في كلّ حسابٍ يُنشأ عند الحاجة.</para>
    /// </summary>
    [Fact]
    public async Task AVoucherWithoutADiscount_LeavesTheAccountUntouched()
    {
        await SetupAsync();
        var invoice = await SalesInvoiceAsync(500m);

        await ReceiveAsync(575m, discount: 0m, allocateTo: invoice);

        Assert.Equal(0m, await BalanceAsync("4103"));
    }

    // ── جانبُ الموردين: خصمٌ مكتسَب ─────────────────────────────────────────

    /// <summary>
    /// <b>والمكتسَبُ دائنٌ يُنقص المصروف — لا مدينٌ يزيده.</b>
    ///
    /// <para><b>والإشارةُ معكوسةٌ عن جانب العملاء عمداً</b>: هناك نُعطي فيُنقص الإيراد،
    /// وهنا نكسب فيُنقص المصروف. <b>ونسخُ جانب العملاء حرفاً يقلب الكسبَ خسارةً في كلّ
    /// سداد</b> — وهو الدرسُ نفسُه الذي أخرجه <c>ط4-٥</c> في فروق العملة.</para>
    /// </summary>
    [Fact]
    public async Task AnEarnedDiscount_IsCreditedNotDebited()
    {
        await SetupAsync();
        var invoice = await PurchaseInvoiceAsync(1_000m);   // ‏1,150

        await PayAsync(1_127m, discount: 23m, allocateTo: invoice);

        // ‏دائنٌ ⟵ رصيدٌ سالبٌ بمنطق «مدين − دائن».
        Assert.Equal(-23m, await BalanceAsync("5214"));

        var payable = (await new SupplierBalanceQuery(_db)
            .AllBalancesAsync(_companyId))[_supplierId];
        Assert.Equal(0m, payable);
    }

    // ── الحراسة ─────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>وخصمٌ سالبٌ يُرفض في النطاق.</b>
    /// </summary>
    [Fact]
    public void ANegativeDiscount_IsRefused()
    {
        var voucher = ReceiptVoucher.CreateDraft(1L, 1L, new DateOnly(2026, 3, 1), 100m,
            ReceiptMethod.Cash, "تحصيل");

        Assert.Throws<AccountingException>(() => voucher.GrantSettlementDiscount(-1m));
    }

    /// <summary>
    /// <b>وخصمٌ بلا حسابٍ يُرفض باسمه — ولا يُبتلع بصمت.</b>
    ///
    /// <para><b>وابتلاعُه يُطفئ ذمّةً بلا قيدٍ يقابلها</b> فينكسر توازنُ القيد نفسِه.</para>
    /// </summary>
    [Fact]
    public async Task ADiscountWithoutItsAccount_IsRefusedByName()
    {
        await SetupAsync();
        var voucher = ReceiptVoucher.CreateDraft(_companyId, _customerId,
            new DateOnly(2026, 3, 1), 100m, ReceiptMethod.Cash, "تحصيل");
        voucher.GrantSettlementDiscount(5m);

        var cash = await _db.Accounts.SingleAsync(a =>
            a.CompanyId == _companyId && a.Code == SaudiChartOfAccounts.SystemCodes.CashOnHand);
        var receivable = await _db.Accounts.SingleAsync(a =>
            a.CompanyId == _companyId && a.Code == SaudiChartOfAccounts.SystemCodes.AccountsReceivable);

        var error = Assert.Throws<AccountingException>(() =>
            voucher.Post("RV-X", cash, receivable));

        Assert.Contains("خصم مسموح به", error.Message, StringComparison.Ordinal);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<decimal> BalanceAsync(string code)
    {
        var trial = await new TrialBalanceQuery(_db)
            .ExecuteAsync(_companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        return Math.Round(trial.Where(l => l.AccountCode == code)
            .Sum(l => l.TotalDebit - l.TotalCredit), 2);
    }

    private async Task<SalesInvoice> SalesInvoiceAsync(decimal amount)
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 3, 1));
        invoice.AddLine("خدمة", 1m, amount);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);
        return invoice;
    }

    private async Task<PurchaseInvoice> PurchaseInvoiceAsync(decimal amount)
    {
        var invoice = PurchaseInvoice.CreateDraft(_companyId, _supplierId,
            new DateOnly(2026, 3, 1), $"SUP-{++_seq:000}", vatDeductible: true);
        invoice.AddLine("خدمة", 1m, amount);
        await new PurchaseInvoicePostingService(_db).PostAsync(invoice);
        return invoice;
    }

    private async Task ReceiveAsync(decimal cash, decimal discount, SalesInvoice allocateTo)
    {
        var voucher = ReceiptVoucher.CreateDraft(_companyId, _customerId,
            new DateOnly(2026, 3, 5), cash, ReceiptMethod.Cash, "تحصيل مبكر");

        if (discount > 0m) voucher.GrantSettlementDiscount(discount);

        await new ReceiptVoucherPostingService(_db).PostAsync(voucher,
            [(PaymentAllocationDocumentType.SalesInvoice, allocateTo.Id,
              allocateTo.InvoiceNumber!, cash)]);
    }

    private async Task PayAsync(decimal cash, decimal discount, PurchaseInvoice allocateTo)
    {
        var voucher = PaymentVoucher.CreateDraft(_companyId, _supplierId,
            new DateOnly(2026, 3, 5), cash, ReceiptMethod.Cash, "سداد مبكر");

        if (discount > 0m) voucher.GrantSettlementDiscount(discount);

        await new PaymentVoucherPostingService(_db).PostAsync(voucher,
            [(PaymentAllocationDocumentType.PurchaseInvoice, allocateTo.Id,
              allocateTo.InvoiceNumber!, cash)]);
    }

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;

        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة الخصم", "310123456700055", BusinessProfile.General);
        _companyId = company.Id;

        var customer = Customer.Create(_companyId, "عميلٌ يسدّد مبكراً", "311111111100003");
        var supplier = Supplier.Create(_companyId, "مورّدٌ يمنح خصماً", "312222222200003");
        _db.Customers.Add(customer);
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        _customerId = customer.Id;
        _supplierId = supplier.Id;
    }
}
