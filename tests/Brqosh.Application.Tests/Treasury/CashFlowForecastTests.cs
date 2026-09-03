using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Treasury;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Treasury;

/// <summary>
/// (CEEF-230 · ط4-٣ج) <b>التدفّقُ المتوقَّع — وأربعةُ أعطالٍ كانت تجعله يُطمئن كذباً.</b>
///
/// <para><b>وكان الكاتبُ مبنيّاً منذ يوليو ومقيساً باختبارٍ واحد</b>: «ثلاثةَ عشرَ
/// أسبوعاً ومجموعان موجبان». <b>وذاك لا يقيس شيئاً ممّا يفعله</b> — فمرّ أربعةُ أعطالٍ
/// تحته سنةً كاملة.</para>
/// </summary>
public class CashFlowForecastTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public CashFlowForecastTests()
    {
        TestActor.Accountant();   // (م2-١) الفاعل مُصرَّح به — لا تجاوز
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    /// <summary>
    /// <b>القياسُ الحاكم: الرصيدُ المتوقَّع يُطبق الافتتاحيَّ والحركة (هـ-٢٢).</b>
    ///
    /// <para><b>وكان لا رصيدَ أصلاً</b>: داخلٌ وخارجٌ لكلّ أسبوعٍ <b>ولا يقول ما الرصيدُ
    /// آخرَه</b>. والمالكُ لا يسأل «كم يدخل» بل <b>«هل أستطيع الدفع»</b>.</para>
    /// </summary>
    [Fact]
    public async Task ProjectedBalance_ExhaustsOpeningPlusFlows()
    {
        await SetupAsync();
        await SaleAsync(1_000m, new DateOnly(2026, 6, 1), termDays: 0);

        var forecast = await new CashFlowForecastService(_db)
            .ForecastAsync(_companyId, new DateOnly(2026, 7, 1));

        var running = forecast.OpeningBalance;
        foreach (var week in forecast.Weeks)
        {
            running += week.ProjectedInflows - week.ProjectedOutflows;
            Assert.Equal(running, week.ProjectedBalance);
        }
    }

    /// <summary>
    /// <b>والافتتاحيُّ هو النقدُ الحقيقيُّ لا صفرٌ.</b>
    ///
    /// <para><b>وطفرةٌ نجت فأخرجت هذا</b>: اختباري كان يقيس <b>الاتّساقَ الداخليّ</b>
    /// — أنّ الرصيدَ يتراكم من الافتتاحيّ المُعلَن — <b>ولا يقيس أنّ المُعلَنَ هو النقدُ
    /// فعلاً</b>. فتصفيرُ الافتتاحيّ يُبقي الاتّساقَ قائماً، <b>والتنبّؤُ كلُّه يصير
    /// خطأً بمقدار ما في الصندوق</b>.</para>
    /// </summary>
    [Fact]
    public async Task OpeningBalance_IsTheRealCash_NotZero()
    {
        await SetupAsync();

        // ‏قبضٌ نقديٌّ يُدخل الصندوقَ مالاً قبل الأفق.
        var receipt = ReceiptVoucher.CreateDraft(_companyId, _customerId,
            new DateOnly(2026, 6, 15), 7_500m, ReceiptMethod.Cash, "دفعة");
        await new ReceiptVoucherPostingService(_db).PostAsync(receipt);

        var forecast = await new CashFlowForecastService(_db)
            .ForecastAsync(_companyId, new DateOnly(2026, 7, 1));

        Assert.Equal(7_500m, forecast.OpeningBalance);
        Assert.Equal(forecast.OpeningBalance
            + forecast.Weeks[0].ProjectedInflows - forecast.Weeks[0].ProjectedOutflows,
            forecast.Weeks[0].ProjectedBalance);
    }

    /// <summary>
    /// <b>وأدنى رصيدٍ وأوّلُ عجزٍ هما ما يُقرَآن أوّلاً.</b>
    ///
    /// <para><b>وطفرةٌ نجت فأخرجت أنّهما بلا قياس</b>: يُحسبان ولا يُتحقّق منهما.
    /// <b>ورقمُ «أدنى رصيد» هو الجوابُ على «هل أعجز»</b> — وتقريرٌ يحسبه خطأً يُطمئن
    /// في الشهر الذي يقع فيه العجز.</para>
    /// </summary>
    [Fact]
    public async Task LowestBalanceAndFirstShortfall_AreReported()
    {
        await SetupAsync();

        var supplier = await _db.Suppliers.SingleAsync(s => s.Id == _supplierId);
        supplier.SetPaymentTerms(0);
        await _db.SaveChangesAsync();

        var purchase = PurchaseInvoice.CreateDraft(
            _companyId, _supplierId, new DateOnly(2026, 7, 1), "INV-BIG-1", true);
        purchase.AddLine("شراء ضخم", 1m, 90_000m);
        await new PurchaseInvoicePostingService(_db).PostAsync(purchase);

        var forecast = await new CashFlowForecastService(_db)
            .ForecastAsync(_companyId, new DateOnly(2026, 7, 1));

        Assert.True(forecast.LowestProjectedBalance < forecast.OpeningBalance,
            "أدنى رصيدٍ لم يُتتبَّع — وهو الجوابُ على «هل أعجز».");
        Assert.NotNull(forecast.FirstShortfallOn);
    }

    /// <summary>
    /// <b>وما بعد الأفق يُعلَن ولا يُحشر في الأسبوع الأخير.</b>
    ///
    /// <para><b>وكان يُحشر</b>: فاتورةٌ تستحقّ بعد سنةٍ تظهر داخلاً في الأسبوع الثالثَ
    /// عشر. <b>وذاك أسوأُ من إسقاطها</b> — يُظهر ذيلَ الأفق مليئاً بمالٍ لا يصل،
    /// <b>فيُبنى عليه التزام</b>.</para>
    /// </summary>
    [Fact]
    public async Task WhatFallsBeyondTheHorizon_IsDeclared_NotCrammedIntoTheLastWeek()
    {
        await SetupAsync();
        // ‏مهلةُ سنةٍ ⇒ الاستحقاقُ بعد الأفق (ثلاثةَ عشرَ أسبوعاً).
        await SaleAsync(5_000m, new DateOnly(2026, 7, 1), termDays: 300);

        var forecast = await new CashFlowForecastService(_db)
            .ForecastAsync(_companyId, new DateOnly(2026, 7, 1));

        Assert.Equal(0m, forecast.Weeks[^1].ProjectedInflows);
        Assert.True(forecast.InflowsBeyondHorizon > 0m,
            "ما بعد الأفق لم يُعلَن — وإسقاطُه صامتاً يُظهر صورةً أفضلَ من الحقيقة.");
    }

    /// <summary>
    /// <b>ومهلةُ السداد لكلّ عميلٍ لا ثابتٌ في الشيفرة.</b>
    ///
    /// <para><b>وكانت ثلاثين يوماً للجميع</b> — فمَن يدفع نقداً ومَن يمهل تسعين سواء،
    /// <b>ويُبنى التنبّؤ على وهمٍ متوسّط</b>.</para>
    /// </summary>
    [Fact]
    public async Task PaymentTerms_ComeFromTheParty_NotAConstant()
    {
        await SetupAsync();
        await SaleAsync(1_000m, new DateOnly(2026, 7, 1), termDays: 0);

        var forecast = await new CashFlowForecastService(_db)
            .ForecastAsync(_companyId, new DateOnly(2026, 7, 1));

        // ‏نقداً ⇒ الأسبوعُ الأوّل. **ولو كان الثابتُ ثلاثين لَوقع في الخامس.**
        Assert.True(forecast.Weeks[0].ProjectedInflows > 0m,
            "البيعُ النقديُّ لم يقع في الأسبوع الأوّل — والمهلةُ تُقرأ من العميل.");
    }

    /// <summary>
    /// <b>والشيكاتُ بتاريخها المكتوب — وكانت مُهمَلةً بالكامل.</b>
    ///
    /// <para><b>وهي أوثقُ ما يُتنبَّأ به</b>: ورقةٌ بتاريخِ استحقاقٍ مكتوبٍ عليها.
    /// <b>فكان التنبّؤ يُبنى على المقدَّر ويترك المؤكَّد.</b></para>
    /// </summary>
    [Fact]
    public async Task PostDatedCheques_AreCounted_ByTheirWrittenDate()
    {
        await SetupAsync();

        var gl = await _db.Accounts.FirstAsync(a => a.CompanyId == _companyId
            && a.Code == Brqosh.Domain.Accounting.SaudiChartOfAccounts.SystemCodes.CashAtBanks);
        var bank = BankAccount.Create(_companyId, "BK-1", "بنك المسبار", "بنك",
            "0001", "SA0000000000000000000001", gl.Id, isDefault: true);
        _db.BankAccounts.Add(bank);
        await _db.SaveChangesAsync();

        var cheque = Cheque.CreateReceived(_companyId, "CHQ-1", new DateOnly(2026, 7, 1),
            3_000m, "شيك مؤجّل", bank.Id, _customerId, dueDate: new DateOnly(2026, 7, 20));
        _db.Cheques.Add(cheque);
        await _db.SaveChangesAsync();

        var forecast = await new CashFlowForecastService(_db)
            .ForecastAsync(_companyId, new DateOnly(2026, 7, 1));

        Assert.True(forecast.Weeks.Take(3).Sum(w => w.ProjectedInflows) >= 3_000m,
            "الشيكُ المؤجَّل لم يدخل التنبّؤ — وهو أوثقُ ما فيه.");
    }

    /// <summary>
    /// <b>ومهلةُ المورّد كذلك من المورّد — لا من ثابت.</b>
    ///
    /// <para><b>وطفرةٌ نجت فأخرجت أنّ جانبَ المدفوعات كان بلا قياس</b>: قِيس جانبُ
    /// المقبوضات وحدَه، <b>والثابتُ في جانب المورّدين يمرّ</b>. والمدفوعاتُ هي التي
    /// تُعجز عن السداد لا المقبوضات.</para>
    /// </summary>
    [Fact]
    public async Task SupplierTerms_ComeFromTheParty_NotAConstant()
    {
        await SetupAsync();

        var supplier = await _db.Suppliers.SingleAsync(s => s.Id == _supplierId);
        supplier.SetPaymentTerms(0);
        await _db.SaveChangesAsync();

        var purchase = PurchaseInvoice.CreateDraft(
            _companyId, _supplierId, new DateOnly(2026, 7, 1), "INV-TERMS-1", true);
        purchase.AddLine("شراء", 1m, 4_000m);
        await new PurchaseInvoicePostingService(_db).PostAsync(purchase);

        var forecast = await new CashFlowForecastService(_db)
            .ForecastAsync(_companyId, new DateOnly(2026, 7, 1));

        // ‏نقداً ⇒ الأسبوعُ الأوّل. **ولو كان الثابتُ ثلاثين لَوقع في الخامس.**
        Assert.True(forecast.Weeks[0].ProjectedOutflows > 0m,
            "الشراءُ النقديُّ لم يقع في الأسبوع الأوّل — والمهلةُ تُقرأ من المورّد.");
        Assert.Equal(0m, forecast.Weeks[4].ProjectedOutflows);
    }

    /// <summary>
    /// <b>والمتأخّرُ يقع في الأسبوع الأوّل لا يُسقَط.</b>
    ///
    /// <para>استحقّ قبل بداية الأفق، <b>وهو مستحقٌّ الآن</b> — وإسقاطُه يُخفي أثقلَ ما
    /// على المنشأة.</para>
    /// </summary>
    [Fact]
    public async Task OverdueAmounts_LandInTheFirstWeek()
    {
        await SetupAsync();
        await SaleAsync(2_000m, new DateOnly(2026, 1, 1), termDays: 0);

        var forecast = await new CashFlowForecastService(_db)
            .ForecastAsync(_companyId, new DateOnly(2026, 7, 1));

        Assert.True(forecast.Weeks[0].ProjectedInflows >= 2_000m,
            "المتأخّرُ لم يقع في الأسبوع الأوّل — وهو مستحقٌّ الآن لا مُسقَط.");
    }

    private long _customerId;
    private long _supplierId;

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;

        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة CF", "310123456700014", BusinessProfile.Retail);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        var customer = Customer.Create(_companyId, "عميل CF");
        var supplier = Supplier.Create(_companyId, "مورد CF");
        _db.Customers.Add(customer);
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;
        _supplierId = supplier.Id;
    }

    private int _seq;

    private async Task SaleAsync(decimal amount, DateOnly on, int termDays)
    {
        var customer = await _db.Customers.SingleAsync(c => c.Id == _customerId);
        customer.SetPaymentTerms(termDays);
        await _db.SaveChangesAsync();

        var sale = SalesInvoice.CreateDraft(_companyId, _customerId, on);
        sale.AddLine($"بيع {++_seq}", 1m, amount);
        await new SalesInvoicePostingService(_db).PostAsync(sale);
    }

    [Fact]
    public async Task Forecast_BuildsThirteenWeeks_FromOpenBalances()
    {
        await SetupAsync();
        var customer = await _db.Customers.SingleAsync(c => c.Id == _customerId);
        var supplier = await _db.Suppliers.SingleAsync(s => s.Id == _supplierId);

        var invoice = SalesInvoice.CreateDraft(_companyId, customer.Id, new DateOnly(2026, 6, 1));
        invoice.AddLine("بيع", 10m, 100m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var purchase = PurchaseInvoice.CreateDraft(
            _companyId, supplier.Id, new DateOnly(2026, 6, 1), "INV-EXT-01", true);
        purchase.AddLine("شراء", 5m, 200m);
        await new PurchaseInvoicePostingService(_db).PostAsync(purchase);

        var forecast = await new CashFlowForecastService(_db)
            .ForecastAsync(_companyId, new DateOnly(2026, 7, 1));

        Assert.Equal(13, forecast.Weeks.Count);
        Assert.True(forecast.TotalProjectedInflows > 0m);
        Assert.True(forecast.TotalProjectedOutflows > 0m);
    }
}
