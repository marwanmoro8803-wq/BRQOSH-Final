using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// (CEEF-230 · ط4-٣) <b>أعمارُ الديون — التقريرُ الذي يُحيل إليه النظامُ ولا وجودَ له.</b>
///
/// <para><b>ما كان:</b> «كشفُ أعمار الديون» مذكورٌ في نصَّين في المستودع — أحدُهما
/// <b>نصيحةُ المساعد المالي للمالك</b>: «راجعْ كشفَ أعمار الديون من تقارير العملاء».
/// <b>ولا استعلامَ ولا شاشة.</b> فالنظامُ يُرسل مالكَه إلى بابٍ لا وجودَ له.</para>
///
/// <para><b>وهويّةُ هـ-١٤:</b> مجموعُ خانات العميل = رصيدُه في الدفتر المساعد.
/// <b>وتقريرُ أعمارٍ لا يُطابق كشفَ الحساب لا يُصدَّق في شيء</b>.</para>
/// </summary>
public class ReceivableAgingTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _customerId;

    public ReceivableAgingTests()
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
    /// <b>القياسُ الحاكم: كلُّ فاتورةٍ في خانة عمرها.</b>
    ///
    /// <para>وثلاثُ فواتيرَ بأعمار 10 و45 و120 يوماً تقع في ثلاث خاناتٍ مختلفة —
    /// <b>وتقريرٌ يجمعها في واحدةٍ لا يقول شيئاً</b>.</para>
    /// </summary>
    [Fact]
    public async Task EachInvoice_FallsInTheBucketOfItsAge()
    {
        await SetupAsync();
        var asOf = new DateOnly(2026, 6, 30);

        await InvoiceAsync(asOf.AddDays(-10), 1_000m);
        await InvoiceAsync(asOf.AddDays(-45), 2_000m);
        await InvoiceAsync(asOf.AddDays(-120), 4_000m);

        var row = await RowAsync(asOf);

        Assert.Equal(1_150m, row.Current);
        Assert.Equal(2_300m, row.Days31To60);
        Assert.Equal(0m, row.Days61To90);
        Assert.Equal(4_600m, row.Over90);
    }

    /// <summary>
    /// <b>وهويّةُ هـ-١٤: المجموعُ يساوي رصيدَ الدفتر المساعد.</b>
    ///
    /// <para><b>وهي ما يجعل التقريرَ قابلاً للتصديق</b>: رقمٌ في الأعمار لا يُطابق كشفَ
    /// الحساب يجعل المحاسبَ يُطالِب بأرقامٍ لا يعرف مصدرَها.</para>
    /// </summary>
    [Fact]
    public async Task TheBucketsSum_EqualsTheSubsidiaryBalance()
    {
        await SetupAsync();
        var asOf = new DateOnly(2026, 6, 30);

        await InvoiceAsync(asOf.AddDays(-10), 1_000m);
        await InvoiceAsync(asOf.AddDays(-70), 3_000m);
        await ReceiveAsync(asOf.AddDays(-5), 1_500m);

        var row = await RowAsync(asOf);
        var balance = (await new CustomerBalanceQuery(_db)
            .ForCustomerAsync(_companyId, _customerId)).Balance;

        Assert.Equal(balance, row.Total);
    }

    /// <summary>
    /// <b>والسدادُ يُطفئ الأقدمَ فالأقدم.</b>
    ///
    /// <para>فاتورتان: قديمةٌ بـ1,150 وحديثةٌ بـ2,300، وسدادٌ 1,150 <b>يُطفئ القديمة</b>.
    /// <b>وتوزيعُه بالتناسب يُبقي الاثنتين مفتوحتين بجزء</b> — فتبدو ذمّةٌ قديمةٌ مفتوحةً
    /// وهي مسدَّدة، ويُطالَب العميلُ بما دفع.</para>
    /// </summary>
    [Fact]
    public async Task Payments_ExtinguishTheOldestFirst()
    {
        await SetupAsync();
        var asOf = new DateOnly(2026, 6, 30);

        await InvoiceAsync(asOf.AddDays(-100), 1_000m);   // ‏1,150 بالضريبة
        await InvoiceAsync(asOf.AddDays(-10), 2_000m);    // ‏2,300
        await ReceiveAsync(asOf.AddDays(-1), 1_150m);

        var row = await RowAsync(asOf);

        Assert.Equal(0m, row.Over90);
        Assert.Equal(2_300m, row.Current);
    }

    /// <summary>
    /// <b>والتخصيصُ الصريح يسبق الأقدم.</b>
    ///
    /// <para>عميلٌ يدفع فاتورةَ الشهر الجاري ويترك قديمةً متنازَعاً عليها. <b>وإطفاءُ
    /// الأقدم يُظهر النزاعَ مسدَّداً والجاريةَ متأخّرة</b> — وهو عكسُ الحقيقة، ويُخفي
    /// النزاعَ الذي يجب أن يُرى.</para>
    /// </summary>
    [Fact]
    public async Task AnExplicitAllocation_BeatsTheOldestFirstRule()
    {
        await SetupAsync();
        var asOf = new DateOnly(2026, 6, 30);

        await InvoiceAsync(asOf.AddDays(-100), 1_000m);
        var current = await InvoiceAsync(asOf.AddDays(-10), 2_000m);

        await ReceiveAsync(asOf.AddDays(-1), 2_300m, allocateTo: current);

        var row = await RowAsync(asOf);

        Assert.Equal(1_150m, row.Over90);
        Assert.Equal(0m, row.Current);
    }

    /// <summary>
    /// <b>وما فاض عن كلّ الفواتير يُعرض رصيداً دائناً.</b>
    ///
    /// <para>عميلٌ دفع مقدَّماً له رصيدٌ لصالحه. <b>وإخفاؤه يجعل مجموعَ التقرير يخالف
    /// كشفَ الحساب</b> — فتنكسر <c>هـ-١٤</c> ويظهر المقدَّمُ كأنّه لم يُدفع.</para>
    /// </summary>
    [Fact]
    public async Task AnOverpayment_ShowsAsUnappliedCreditAndKeepsTheIdentity()
    {
        await SetupAsync();
        var asOf = new DateOnly(2026, 6, 30);

        await InvoiceAsync(asOf.AddDays(-10), 1_000m);
        await ReceiveAsync(asOf.AddDays(-1), 2_000m);

        var row = await RowAsync(asOf);
        var balance = (await new CustomerBalanceQuery(_db)
            .ForCustomerAsync(_companyId, _customerId)).Balance;

        Assert.Equal(850m, row.UnappliedCredit);
        Assert.Equal(balance, row.Total);
        Assert.True(row.Total < 0m, "المقدَّمُ يترك رصيداً دائناً — والرصيدُ الدائن سالبٌ بمنطق الذمم.");
    }

    /// <summary>
    /// <b>وحدُّ الخانة يومٌ لا يومان.</b>
    ///
    /// <para>ثلاثون يوماً «جارية» وواحدٌ وثلاثون «31–60». <b>وحدٌّ مزاحٌ بيومٍ ينقل ذممَ
    /// آخر الشهر كلَّها إلى الخانة الخطأ</b> — وهو أشيعُ خطأٍ في تقارير الأعمار.</para>
    /// </summary>
    [Theory]
    [InlineData(30, true)]
    [InlineData(31, false)]
    public async Task TheBucketBoundary_IsExactlyThirtyDays(int age, bool expectCurrent)
    {
        await SetupAsync();
        var asOf = new DateOnly(2026, 6, 30);

        await InvoiceAsync(asOf.AddDays(-age), 1_000m);
        var row = await RowAsync(asOf);

        Assert.Equal(expectCurrent ? 1_150m : 0m, row.Current);
        Assert.Equal(expectCurrent ? 0m : 1_150m, row.Days31To60);
    }

    /// <summary>
    /// <b>وما وقع بعد التاريخ لا يدخل التقرير — لا في الخانات ولا في المرجع.</b>
    ///
    /// <para><b>وهذا القياسُ أخرجته طفرةٌ نجت</b>: ترشيحُ المستندات بالتاريخ كان مكتوباً
    /// <b>ونزعُه لا يُغيّر شيئاً</b>، لأنّ المرجعَ الذي تُقابَل به الخاناتُ كان رصيدَ
    /// «الآن» بلا تاريخ. <b>فالحارسُ كان قائماً ولا يحرس</b> — والتقريرُ لشهرٍ مضى
    /// يحمل فواتيرَ الشهر التالي.</para>
    /// </summary>
    [Fact]
    public async Task DocumentsAfterTheAsOfDate_AreExcludedEntirely()
    {
        await SetupAsync();
        var asOf = new DateOnly(2026, 6, 30);

        // ‏**والفاتورةُ القائمةُ قديمةٌ عمداً**: مثالٌ بفاتورةٍ حديثةٍ **لا يُفرّق**، لأنّ
        // ‏الفاتورةَ المستقبليةَ عمرُها سالبٌ فتقع في «الجارية» — فيُنتج التنفيذُ المعطوب
        // ‏المجموعَ نفسَه بالمصادفة. **وهو ما نجَت به الطفرةُ مرّتين.**
        await InvoiceAsync(asOf.AddDays(-120), 1_000m);
        await InvoiceAsync(asOf.AddDays(20), 9_000m);   // ‏بعد التاريخ — لا شأنَ للتقرير به

        var row = await RowAsync(asOf);

        // ‏**والدَّينُ القديم يبقى قديماً**: تنفيذٌ يبتلع المستقبلَ يُطفئه بائتمانٍ موهوم
        // ‏ويُظهر ذمّةً «جارية» مكانَه — **فيُقرأ متعثّرٌ عمرُه أربعةُ أشهرٍ دَيناً طازجاً**.
        Assert.Equal(1_150m, row.Over90);
        Assert.Equal(0m, row.Current);
        Assert.Equal(1_150m, row.Total);

        // ‏**والمرجعُ مؤرَّخٌ كذلك** — فالهويّةُ تصمد بلا أن يُخفي التقريرُ فرقاً.
        var asOfBalance = (await new CustomerBalanceQuery(_db)
            .AllBalancesAsync(_companyId, asOf))[_customerId];
        Assert.Equal(asOfBalance, row.Total);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<ReceivableAgingRow> RowAsync(DateOnly asOf)
    {
        var rows = await new ReceivableAgingQuery(_db).ExecuteAsync(_companyId, asOf);
        return rows.Single(r => r.CustomerId == _customerId);
    }

    private async Task<SalesInvoice> InvoiceAsync(DateOnly date, decimal amount)
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, date);
        invoice.AddLine("خدمة", 1m, amount);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);
        return invoice;
    }

    private async Task ReceiveAsync(DateOnly date, decimal amount, SalesInvoice? allocateTo = null)
    {
        var voucher = ReceiptVoucher.CreateDraft(_companyId, _customerId, date, amount,
            ReceiptMethod.Cash, "تحصيل");

        var allocations = allocateTo is null
            ? Array.Empty<(PaymentAllocationDocumentType, long, string, decimal)>()
            : [(PaymentAllocationDocumentType.SalesInvoice, allocateTo.Id,
                allocateTo.InvoiceNumber!, amount)];

        await new ReceiptVoucherPostingService(_db).PostAsync(voucher, allocations);
    }

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;

        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة الأعمار", "310123456700022", BusinessProfile.General);
        _companyId = company.Id;

        var customer = Customer.Create(_companyId, "عميلٌ مدين", "311111111100003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;
    }
}
