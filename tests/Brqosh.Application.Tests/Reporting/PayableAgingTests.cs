using Brqosh.Application.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Treasury;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// (CEEF-231 · ط4-٣ب) <b>أعمارُ ديون الموردين — والجانبُ الآخر كان وحدَه مبنياً.</b>
///
/// <para><b>ما كان:</b> بُنيت أعمارُ العملاء في <c>ط4-٣</c> و<b>بقي جانبُ الموردين بلا
/// شيء</b>. فالمالكُ يعرف مَن عليه له ولا يعرف <b>لمن هو عليه ومنذ متى</b>.</para>
///
/// <para><b>وأثرُه ليس أهونَ من جانب العملاء</b>: فاتورةٌ تبلغ التسعين بلا أن يعلم أحدٌ
/// <b>تُوقف التوريدَ وتسحب مهلةَ الائتمان</b>، والمنشأةُ تتوقّف عن العمل — لا لأنّها
/// عاجزة بل <b>لأنّها لم تُبصر</b>.</para>
///
/// <para><b>وهويّةُ هـ-٢٠:</b> مجموعُ خانات المورد = رصيدُه في الدفتر المساعد.</para>
/// </summary>
public class PayableAgingTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _supplierId;
    private int _seq;

    public PayableAgingTests()
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
    /// <b>وهويّةُ هـ-٢٠: المجموعُ يساوي رصيدَ الدفتر المساعد.</b>
    ///
    /// <para><b>وتقريرُ أعمارٍ لا يُطابق كشفَ الحساب لا يُصدَّق في شيء</b> — والمحاسبُ
    /// يُسدّد للموردين أرقاماً لا يعرف من أين جاءت.</para>
    /// </summary>
    [Fact]
    public async Task TheBuckets_SumToTheSubsidiaryLedgerBalance()
    {
        await SetupAsync();
        var asOf = new DateOnly(2026, 6, 30);

        await InvoiceAsync(asOf.AddDays(-70), 5_000m);
        await InvoiceAsync(asOf.AddDays(-5), 3_000m);
        await PayAsync(asOf.AddDays(-2), 2_000m);

        var row = await RowAsync(asOf);
        var balance = (await new SupplierBalanceQuery(_db).AllBalancesAsync(_companyId, asOf))[_supplierId];

        Assert.Equal(balance, row.Total);
    }

    /// <summary>
    /// <b>والسدادُ المخصَّص يُطفئ فاتورتَه لا الأقدم.</b>
    ///
    /// <para>منشأةٌ تُسدّد فاتورةَ الشهر وتترك قديمةً متنازَعاً عليها — <b>وإطفاءُ الأقدم
    /// يُظهر النزاعَ مسدَّداً والجاريةَ متأخّرة</b>، وهو عكسُ الحقيقة تماماً.</para>
    /// </summary>
    [Fact]
    public async Task AnExplicitAllocation_ClearsItsOwnInvoice_NotTheOldest()
    {
        await SetupAsync();
        var asOf = new DateOnly(2026, 6, 30);

        await InvoiceAsync(asOf.AddDays(-120), 1_000m);          // ‏قديمةٌ متنازَعٌ عليها
        var recent = await InvoiceAsync(asOf.AddDays(-5), 2_300m);
        await PayAsync(asOf.AddDays(-1), 2_645m, recent);

        var row = await RowAsync(asOf);

        Assert.Equal(0m, row.Current);
        Assert.Equal(1_150m, row.Over90);
    }

    /// <summary>
    /// <b>والدفعةُ المقدَّمة رصيدٌ مدينٌ يُعرض ولا يُبتلع.</b>
    ///
    /// <para><b>وإخفاؤه يجعل مجموعَ التقرير يخالف كشفَ الحساب</b> — فتنكسر <c>هـ-٢٠</c>.</para>
    /// </summary>
    [Fact]
    public async Task AnOverpayment_ShowsAsAnUnappliedDebit()
    {
        await SetupAsync();
        var asOf = new DateOnly(2026, 6, 30);

        await InvoiceAsync(asOf.AddDays(-10), 1_000m);   // ‏1,150 بالضريبة
        await PayAsync(asOf.AddDays(-1), 1_650m);

        var row = await RowAsync(asOf);

        Assert.Equal(0m, row.Current);
        Assert.Equal(500m, row.UnappliedDebit);
        Assert.Equal(-500m, row.Total);
    }

    /// <summary>
    /// <b>والمرجعُ مؤرَّخٌ كالخانات — لا رصيدَ «الآن» يُقابل خاناتِ الأمس.</b>
    ///
    /// <para><b>وهذا ما أخرجته طفرةٌ نجت في جانب العملاء</b>: مرجعٌ غيرُ مؤرَّخٍ يُخرج
    /// فرقاً يُنسب إلى التقرير <b>وهو في المرجع</b>. فتقريرُ ٣٠ يونيو لا يرى سداداً وقع
    /// في يوليو، <b>والمجموعُ يبقى صادقاً في تاريخه</b>.</para>
    /// </summary>
    [Fact]
    public async Task TheReport_IgnoresWhatHappenedAfterTheAsOfDate()
    {
        await SetupAsync();
        var asOf = new DateOnly(2026, 6, 30);

        await InvoiceAsync(asOf.AddDays(-10), 1_000m);
        await PayAsync(asOf.AddDays(10), 1_150m);   // ‏سدادٌ بعد التاريخ

        var row = await RowAsync(asOf);
        var balance = (await new SupplierBalanceQuery(_db).AllBalancesAsync(_companyId, asOf))[_supplierId];

        Assert.Equal(1_150m, row.Current);
        Assert.Equal(balance, row.Total);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<PayableAgingRow> RowAsync(DateOnly asOf)
    {
        var rows = await new PayableAgingQuery(_db).ExecuteAsync(_companyId, asOf);
        return rows.Single(r => r.SupplierId == _supplierId);
    }

    private async Task<PurchaseInvoice> InvoiceAsync(DateOnly date, decimal amount)
    {
        var invoice = PurchaseInvoice.CreateDraft(_companyId, _supplierId, date,
            $"SUP-{++_seq:000}", vatDeductible: true);
        invoice.AddLine("خدمة", 1m, amount);
        await new PurchaseInvoicePostingService(_db).PostAsync(invoice);
        return invoice;
    }

    private async Task PayAsync(DateOnly date, decimal amount, PurchaseInvoice? allocateTo = null)
    {
        var voucher = PaymentVoucher.CreateDraft(_companyId, _supplierId, date, amount,
            ReceiptMethod.Cash, "سداد");

        var allocations = allocateTo is null
            ? Array.Empty<(PaymentAllocationDocumentType, long, string, decimal)>()
            : [(PaymentAllocationDocumentType.PurchaseInvoice, allocateTo.Id,
                allocateTo.InvoiceNumber!, amount)];

        await new PaymentVoucherPostingService(_db).PostAsync(voucher, allocations);
    }

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;

        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة الأعمار", "310123456700033", BusinessProfile.General);
        _companyId = company.Id;

        var supplier = Supplier.Create(_companyId, "مورّدٌ دائن", "312222222200003");
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();
        _supplierId = supplier.Id;
    }
}
