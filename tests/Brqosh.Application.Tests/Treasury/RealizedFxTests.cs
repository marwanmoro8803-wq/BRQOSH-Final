using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Treasury;

/// <summary>
/// (CEEF-230 · ط1-٤) <b>فرقُ العملة المحقَّق عند السداد.</b>
///
/// <para><b>ما كان:</b> لا فرقَ عملةٍ محقَّقاً في النظام كلِّه — <b>ولا حسابَ له في شجرة
/// الحسابات أصلاً</b> (الموجودان لغير المحقَّق). وسندُ القبض يُقيَّد بمبلغٍ واحد،
/// <b>ولا وسيلةَ للتعبير عن تحصيلٍ بسعرٍ يخالف سعرَ الفاتورة</b>.</para>
///
/// <para><b>والأثرُ:</b> عميلٌ يدفع كلَّ ما عليه بالدولار، والبنكُ يُحوّل بسعرٍ مختلف —
/// <b>فتبقى ذمّتُه مفتوحةً بفرقٍ لا يُغلق أبداً</b>. الدفاترُ تقول إنّه مدين، وهو قد سدّد.</para>
///
/// <para><b>والمحقَّقُ غيرُ غير المحقَّق</b>: هذا ربحٌ وقع بسدادٍ فعليٍّ لا يعود، وذاك تقديرٌ
/// يتغيّر بكلّ إعادة تقييمٍ ويُعكس. <b>وخلطُهما يجعل الأرباحَ تبدو مستقرّةً وهي متقلّبة.</b></para>
/// </summary>
public class RealizedFxTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _customerId;

    public RealizedFxTests()
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
    /// <b>القياسُ المركزيّ: الذمّةُ تُغلق تماماً، والفرقُ يُقيَّد ربحاً.</b>
    ///
    /// <para>فاتورةٌ 1,000 دولارٍ بسعر 3.75 ⟵ ذمّةٌ 4,312.50 ريالاً. ويُحصَّل المبلغُ كاملاً
    /// بسعر 3.80 ⟵ نقدٌ 4,370. <b>والفرقُ 57.50 ربحٌ محقَّق</b> — وبغيره تبقى ذمّةٌ مفتوحةٌ
    /// بـ57.50 لعميلٍ لم يعد مديناً بشيء.</para>
    /// </summary>
    [Fact]
    public async Task CollectingAtAHigherRate_ClosesTheReceivableAndBooksAGain()
    {
        var invoice = await ForeignInvoiceAsync(1000m, 3.75m);
        Assert.Equal(4312.50m, invoice.GrandTotal());

        await CollectAsync(invoice, foreignAmount: 1150m, rate: 3.80m);

        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsReceivable));
        Assert.Equal(4370m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.CashOnHand));

        // ‏4108 حسابُ إيرادٍ — رصيدُه الدائن يظهر سالباً بمنطق (مدين − دائن).
        Assert.Equal(-57.50m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.RealizedFxGain));

        await LedgerIdentities.AssertAllAsync(_db, _companyId);
    }

    /// <summary><b>والاتّجاه الآخر: سعرٌ أدنى ⟵ خسارةٌ محقَّقة، والذمّةُ تُغلق كذلك.</b></summary>
    [Fact]
    public async Task CollectingAtALowerRate_BooksALoss()
    {
        var invoice = await ForeignInvoiceAsync(1000m, 3.75m);

        await CollectAsync(invoice, foreignAmount: 1150m, rate: 3.70m);

        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsReceivable));
        Assert.Equal(57.50m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.RealizedFxLoss));

        await LedgerIdentities.AssertAllAsync(_db, _companyId);
    }

    /// <summary>
    /// <b>وسعرٌ لم يتغيّر لا يُنتج سطرَ فرق.</b>
    ///
    /// <para>وبغير هذا القياس يمرّ ترحيلٌ يُضيف سطرَ صفرٍ إلى كلّ قيد — <b>ويُنتفخ الأستاذُ
    /// بسطورٍ لا تحمل معنى</b>.</para>
    /// </summary>
    [Fact]
    public async Task CollectingAtTheSameRate_AddsNoFxLine()
    {
        var invoice = await ForeignInvoiceAsync(1000m, 3.75m);

        await CollectAsync(invoice, foreignAmount: 1150m, rate: 3.75m);

        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsReceivable));

        var lines = await _db.JournalEntries.AsNoTracking()
            .Where(e => e.SourceDocument!.StartsWith("ReceiptVoucher"))
            .SelectMany(e => e.Lines).ToListAsync();
        Assert.Equal(2, lines.Count);
    }

    /// <summary>
    /// <b>وسندٌ بالريال يسلك مسارَه القديم حرفاً.</b>
    ///
    /// <para>وهو ما يحمي كلَّ قاعدةٍ قائمة: <b>لا حسابَ يُنشأ ولا سطرَ يُضاف</b>.</para>
    /// </summary>
    [Fact]
    public async Task ARiyalVoucher_IsUnchanged()
    {
        await SetupAsync();
        var invoice = SalesInvoice.CreateDraft(_companyId, await CustomerAsync(), new DateOnly(2026, 2, 1));
        invoice.AddLine("خدمة", 1m, 1000m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        await CollectAsync(invoice, foreignAmount: 1150m, rate: 1m, currency: "SAR");

        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsReceivable));

        // ‏**ولا سطرَ فرقٍ في القيد** — والحسابُ نفسُه مبذورٌ للمنشآت الجديدة من القالب،
        // ‏فوجودُه ليس دليلَ استعمال. والدليلُ عددُ السطور.
        var lines = await _db.JournalEntries.AsNoTracking()
            .Where(e => e.SourceDocument!.StartsWith("ReceiptVoucher"))
            .SelectMany(e => e.Lines).ToListAsync();
        Assert.Equal(2, lines.Count);
    }

    /// <summary>
    /// <b>ولا يُخلط سندٌ واحدٌ بعملتين.</b>
    ///
    /// <para>تخصيصُ سندٍ دولاريٍّ على فاتورةٍ بالريال يُقارن مبلغين لا يقبل أحدُهما الآخر،
    /// <b>ويُنتج «فرقَ عملة» هو في الحقيقة خطأُ إدخال</b> — فيُرفض ويُسمّى.</para>
    /// </summary>
    [Fact]
    public async Task MixingCurrenciesInOneVoucher_IsRefused()
    {
        await SetupAsync();
        var riyal = SalesInvoice.CreateDraft(_companyId, await CustomerAsync(), new DateOnly(2026, 2, 1));
        riyal.AddLine("خدمة", 1m, 1000m);
        await new SalesInvoicePostingService(_db).PostAsync(riyal);

        var error = await Assert.ThrowsAsync<AccountingException>(
            () => CollectAsync(riyal, foreignAmount: 1150m, rate: 3.80m, currency: "USD"));

        Assert.Contains("ولا يُخلط سندٌ واحدٌ بعملتين", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>وحسابا فرقِ العملة يُنشآن من القالب إن غابا.</b>
    ///
    /// <para>فقد أُضيفا إلى القالب في هذه الدفعة، <b>وكلُّ قاعدةِ عميلٍ قائمةٍ لا تحملهما</b>
    /// — فأوّلُ تحصيلٍ أجنبيٍّ كان سيسقط بـ«الحساب غير موجود» على قاعدةٍ سليمةٍ تماماً.</para>
    /// </summary>
    [Fact]
    public async Task TheRealizedFxAccounts_AreProvisionedOnDemand()
    {
        var invoice = await ForeignInvoiceAsync(1000m, 3.75m);

        // ‏تُحذف محاكاةً لقاعدةٍ أُنشئت قبل هذه الدفعة.
        var seeded = await _db.Accounts.Where(a => a.CompanyId == _companyId
            && (a.Code == SaudiChartOfAccounts.SystemCodes.RealizedFxGain
                || a.Code == SaudiChartOfAccounts.SystemCodes.RealizedFxLoss)).ToListAsync();
        _db.Accounts.RemoveRange(seeded);
        await _db.SaveChangesAsync();

        await CollectAsync(invoice, foreignAmount: 1150m, rate: 3.80m);

        var created = await _db.Accounts.SingleAsync(a =>
            a.CompanyId == _companyId && a.Code == SaudiChartOfAccounts.SystemCodes.RealizedFxGain);
        Assert.Equal("أرباح فروق عملة محققة", created.NameAr);
        Assert.Equal(AccountType.Revenue, created.Type);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<SalesInvoice> ForeignInvoiceAsync(decimal amount, decimal rate)
    {
        await SetupAsync();
        var customerId = await CustomerAsync();

        var invoice = SalesInvoice.CreateDraft(_companyId, customerId, new DateOnly(2026, 2, 1));
        invoice.SetCurrency("USD", rate);
        invoice.AddLine("خدمة", 1m, amount);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);
        return invoice;
    }

    private async Task CollectAsync(SalesInvoice invoice, decimal foreignAmount, decimal rate,
        string currency = "USD")
    {
        var voucher = ReceiptVoucher.CreateDraft(_companyId, invoice.CustomerId,
            new DateOnly(2026, 3, 1), foreignAmount, ReceiptMethod.Cash, "تحصيل");
        if (currency != "SAR") voucher.SetCurrency(currency, rate);

        await new ReceiptVoucherPostingService(_db).PostAsync(voucher,
            [(PaymentAllocationDocumentType.SalesInvoice, invoice.Id,
              invoice.InvoiceNumber!, foreignAmount)]);
    }

    private async Task<decimal> BalanceAsync(string code)
    {
        var trial = await new Brqosh.Infrastructure.Reporting.TrialBalanceQuery(_db)
            .ExecuteAsync(_companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        return Math.Round(trial.Where(l => l.AccountCode == code)
            .Sum(l => l.TotalDebit - l.TotalCredit), 2);
    }

    private async Task<long> CustomerAsync()
    {
        if (_customerId != 0) return _customerId;
        var customer = Customer.Create(_companyId, "عميل دوليّ", "311111111100003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;
        return _customerId;
    }

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة التصدير", "310123456700066", BusinessProfile.General);
        _companyId = company.Id;
    }
}
