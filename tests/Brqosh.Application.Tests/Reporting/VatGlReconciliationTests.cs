using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// (CEEF-229 · م2-١٦) <b>مؤشّر مطابقة الإقرار بالأستاذ — غيابُ البيان ليس صفراً.</b>
///
/// <para><b>العطل:</b> <c>GlVatNetPayableBalance</c> كان <c>decimal</c> يسقط إلى صفر حين لا
/// يوجد الحساب أو لا حركة عليه، فيصير <c>GlVariance</c> مساوياً <b>لصافي الإقرار كاملاً
/// دائماً</b>. ولا كاتب لحساب 2107 في المستودع كلّه — لا خدمة تسويةٍ ضريبية تُقفل فيه
/// المدخلات والمخرجات — فالرصيد صفرٌ عند كل منشأة، والمؤشّر يقول أبداً إن الإقرار كلَّه
/// لا يطابق الأستاذ.</para>
///
/// <para><b>ولماذا يستحقّ الإصلاح وهو غير معروض:</b> لأنه في <b>عقد التطبيق</b>. فأيّ شاشةٍ
/// تُضاف غداً ستعرض تحذيراً يصرخ دائماً — <b>وتحذيرٌ يصرخ دائماً أسوأ من غيابه</b>: يتعلّم
/// المستخدم تجاهله فيفوته التحذير الحقيقيّ يوم يقع. الفرق بين «لا أعرف» و«صفر» ليس تفصيلاً
/// في مؤشّرٍ رقابيّ: هو معناه كلُّه.</para>
/// </summary>
public class VatGlReconciliationTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private readonly long _companyId;
    private readonly long _customerId;

    private static readonly DateOnly From = new(2026, 1, 1);
    private static readonly DateOnly To = new(2026, 3, 31);

    public VatGlReconciliationTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _companyId = new DatabaseInitializer(_db)
            .InitializeAsync("منشأة", "310123456700099", BusinessProfile.General)
            .GetAwaiter().GetResult().Id;
        ManualJournals.LiftApprovalThreshold(_db);   // ‏(accounting-core#6) قيودٌ يدويّةٌ كبيرةٌ لسببٍ غيرِ الاعتماد — منشأةٌ بمستخدمٍ واحد

        var customer = Customer.Create(_companyId, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        _db.SaveChanges();
        _customerId = customer.Id;
    }

    private async Task SellAsync(decimal amount)
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 2, 1));
        invoice.AddLine("بضاعة", 1m, amount);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);
    }

    private async Task<VatReturnResult> ReturnAsync() =>
        await new VatReturnQuery(_db).ExecuteAsync(_companyId, From, To);

    /// <summary>
    /// <b>القياس المركزيّ.</b> منشأةٌ لم تُجرِ تسويةً ضريبية — وهي حال <b>كل</b> منشأة اليوم
    /// إذ لا خدمة تسوية أصلاً — لا يُقال لها إن إقرارها كلَّه مخالف. يُقال: لا مطابقة ممكنة.
    /// </summary>
    [Fact]
    public async Task WithNoSettlementPosted_TheVarianceIsUnknown_NotTheWholeReturn()
    {
        await SellAsync(100_000m);

        var result = await ReturnAsync();

        Assert.Equal(15_000m, result.NetVatPayable);
        Assert.False(result.IsGlReconciliationAvailable);
        Assert.Null(result.GlVariance);
    }

    /// <summary>
    /// <b>حارسٌ للحارس — ولولاه لكان «أعِد null دائماً» يُمرّر الاختبار السابق:</b> متى
    /// وُجدت حركةٌ فعلية على الحساب صار الفرق رقماً يُقاس. وهنا التسوية مطابقة تماماً،
    /// فالفرق <b>صفر</b> — وهو المعنى الذي كان الصفرُ الكاذب يسرقه.
    /// </summary>
    [Fact]
    public async Task WithAMatchingSettlementPosted_TheVarianceIsZero()
    {
        await SellAsync(100_000m);

        // تسويةٌ يدوية تنقل صافي الضريبة إلى 2107 — وهو ما ستفعله خدمة التسوية حين تُبنى.
        var output = await _db.Accounts.SingleAsync(a =>
            a.CompanyId == _companyId && a.Code == SaudiChartOfAccounts.SystemCodes.VatOutput);
        var net = await _db.Accounts.SingleAsync(a =>
            a.CompanyId == _companyId && a.Code == SaudiChartOfAccounts.SystemCodes.VatNetPayable);

        var entry = JournalEntry.CreateDraft(_companyId, new DateOnly(2026, 3, 31), "تسوية ضريبية");
        entry.AddLine(output, 15_000m, 0m);
        entry.AddLine(net, 0m, 15_000m);
        await new JournalPostingService(_db).PostAsync(entry);

        var result = await ReturnAsync();

        Assert.True(result.IsGlReconciliationAvailable);
        Assert.Equal(0m, result.GlVariance);
    }

    /// <summary>
    /// وتسويةٌ ناقصة تُظهر الفرق بمقداره — فالمؤشّر يعمل حين يكون له ما يقيسه.
    /// </summary>
    [Fact]
    public async Task WithAPartialSettlement_TheVarianceIsTheShortfall()
    {
        await SellAsync(100_000m);

        var output = await _db.Accounts.SingleAsync(a =>
            a.CompanyId == _companyId && a.Code == SaudiChartOfAccounts.SystemCodes.VatOutput);
        var net = await _db.Accounts.SingleAsync(a =>
            a.CompanyId == _companyId && a.Code == SaudiChartOfAccounts.SystemCodes.VatNetPayable);

        var entry = JournalEntry.CreateDraft(_companyId, new DateOnly(2026, 3, 31), "تسوية جزئية");
        entry.AddLine(output, 12_000m, 0m);
        entry.AddLine(net, 0m, 12_000m);
        await new JournalPostingService(_db).PostAsync(entry);

        var result = await ReturnAsync();

        Assert.Equal(3_000m, result.GlVariance);
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
