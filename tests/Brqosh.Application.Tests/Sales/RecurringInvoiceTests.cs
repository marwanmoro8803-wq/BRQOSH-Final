using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Sales;

/// <summary>
/// (CEEF-230 · ط1-٥) <b>الفوترةُ الدورية — إيجارٌ أو اشتراكٌ أو عقدُ صيانة.</b>
///
/// <para><b>ما كان:</b> صفرُ شيفرةٍ للفوترة المتكرّرة، و«Recurring» في تصميم الشاشات
/// وعدٌ لم يُنفَّذ. <b>ولا مُجدوِلَ عملٍ في التطبيق إطلاقاً</b> — فبُنيت دفعةً يُطلقها
/// المستخدم، وهو أصدقُ لتطبيقٍ مكتبيّ: خدمةٌ خلفيةٌ في تطبيقٍ مغلقٍ لا تعمل.</para>
/// </summary>
public class RecurringInvoiceTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _customerId;

    public RecurringInvoiceTests()
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

    // ── الجدول: أين يقع الاستحقاق ───────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ الأدقّ: خطةٌ تبدأ في ٣١ يناير لا تنزلق إلى الثامن والعشرين.</b>
    ///
    /// <para>الاشتقاقُ من <b>سابق</b> التكرار يُعطي ٣١ يناير ⟵ ٢٨ فبراير ⟵ <b>٢٨ مارس</b>،
    /// فينزلق يومُ الاستحقاق إلى الأبد ويخالف العقد. <b>والاشتقاقُ من البداية</b> يُعطي
    /// ٣١ مارس — <b>ويعود إلى يومه كلّما اتّسع الشهر</b>.</para>
    /// </summary>
    [Fact]
    public void MonthlyOccurrences_AnchorToTheStartDayNotThePrevious()
    {
        var plan = RecurringInvoicePlan.Create(1L, 1L, "R-1", "إيجار",
            RecurrenceFrequency.Monthly, new DateOnly(2026, 1, 31));

        Assert.Equal(new DateOnly(2026, 1, 31), plan.OccurrenceAt(0));
        Assert.Equal(new DateOnly(2026, 2, 28), plan.OccurrenceAt(1));
        Assert.Equal(new DateOnly(2026, 3, 31), plan.OccurrenceAt(2));
        Assert.Equal(new DateOnly(2026, 4, 30), plan.OccurrenceAt(3));
    }

    /// <summary><b>وكلُّ دوريّةٍ بخطوتها.</b></summary>
    [Theory]
    [InlineData(RecurrenceFrequency.Weekly, "2026-03-15")]
    [InlineData(RecurrenceFrequency.Monthly, "2026-04-08")]
    [InlineData(RecurrenceFrequency.Quarterly, "2026-06-08")]
    [InlineData(RecurrenceFrequency.SemiAnnual, "2026-09-08")]
    [InlineData(RecurrenceFrequency.Annual, "2027-03-08")]
    public void EachFrequency_AdvancesByItsStep(RecurrenceFrequency frequency, string expected)
    {
        var plan = RecurringInvoicePlan.Create(1L, 1L, "R-2", "اشتراك",
            frequency, new DateOnly(2026, 3, 8));

        Assert.Equal(DateOnly.Parse(expected), plan.OccurrenceAt(1));
    }

    /// <summary><b>وخطةٌ لها نهايةٌ تتوقّف عندها.</b></summary>
    [Fact]
    public void APlanPastItsEndDate_HasNoNextOccurrence()
    {
        var plan = RecurringInvoicePlan.Create(1L, 1L, "R-3", "عقد",
            RecurrenceFrequency.Monthly, new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 15));

        plan.MarkGenerated();   // يناير
        Assert.Equal(new DateOnly(2026, 2, 1), plan.NextOccurrence());

        plan.MarkGenerated();   // فبراير
        Assert.Null(plan.NextOccurrence());
    }

    // ── التوليد ─────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>وكلُّ المستحقّات تُولَّد لا آخرُها.</b>
    ///
    /// <para>مَن أغلق البرنامج ثلاثة أشهر له ثلاثُ فواتير — <b>وفاتورةٌ واحدةٌ تُسقط إيرادَ
    /// شهرين بلا أثر</b>، فتخرج الدفاترُ ناقصةً بلا ما يدلّ على النقص.</para>
    /// </summary>
    [Fact]
    public async Task EveryMissedOccurrence_IsGeneratedNotJustTheLast()
    {
        await PlanAsync("R-10", new DateOnly(2026, 1, 1));

        var result = await new RecurringInvoiceService(_db)
            .GenerateDueAsync(_companyId, new DateOnly(2026, 3, 20));

        Assert.True(result.InvoicesCreated == 3, Why(result));

        var invoices = await _db.SalesInvoices.AsNoTracking()
            .Where(i => i.CompanyId == _companyId).OrderBy(i => i.InvoiceDate).ToListAsync();
        Assert.Equal(
            [new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), new DateOnly(2026, 3, 1)],
            invoices.Select(i => i.InvoiceDate));
    }

    /// <summary>
    /// <b>القياسُ الحاكم: تشغيلٌ مرّتين لا يُفوتِر العميلَ مرّتين.</b>
    ///
    /// <para><b>وهو العطلُ الذي يراه العميلُ قبلنا.</b> وحارسُه قيدُ تفرّدٍ في القاعدة على
    /// <c>(الخطة، تاريخ الاستحقاق)</c> — لا فحصٌ في الذاكرة يسبقه تشغيلٌ آخر.</para>
    /// </summary>
    [Fact]
    public async Task RunningTwice_DoesNotBillTwice()
    {
        await PlanAsync("R-11", new DateOnly(2026, 1, 1));
        var service = new RecurringInvoiceService(_db);

        var first = await service.GenerateDueAsync(_companyId, new DateOnly(2026, 2, 10));
        var second = await service.GenerateDueAsync(_companyId, new DateOnly(2026, 2, 10));

        Assert.Equal(2, first.InvoicesCreated);
        Assert.Equal(0, second.InvoicesCreated);
        Assert.Equal(2, await _db.SalesInvoices.CountAsync(i => i.CompanyId == _companyId));
        Assert.Equal(2, await _db.RecurringInvoiceRuns.CountAsync());
    }

    /// <summary><b>وخطةٌ موقوفةٌ لا تُولّد</b> — والاستئنافُ يُعيدها من حيث وقفت.</summary>
    [Fact]
    public async Task APausedPlan_GeneratesNothingUntilResumed()
    {
        var plan = await PlanAsync("R-12", new DateOnly(2026, 1, 1));
        plan.Pause();
        await _db.SaveChangesAsync();

        var service = new RecurringInvoiceService(_db);
        Assert.Equal(0, (await service.GenerateDueAsync(_companyId, new DateOnly(2026, 2, 10))).InvoicesCreated);

        plan.Resume();
        await _db.SaveChangesAsync();
        Assert.Equal(2, (await service.GenerateDueAsync(_companyId, new DateOnly(2026, 2, 10))).InvoicesCreated);
    }

    /// <summary>
    /// <b>وما لم يستحقّ بعد لا يُولَّد.</b>
    ///
    /// <para>وبغير هذا القياس يمرّ توليدٌ يُفوتِر المستقبلَ كلَّه في تشغيلةٍ واحدة —
    /// <b>وحارسٌ يرفض الكلَّ يُقرأ نجاحاً كما يُقرأ حارسٌ يقبل الكلّ</b>.</para>
    /// </summary>
    [Fact]
    public async Task FutureOccurrences_AreNotGenerated()
    {
        await PlanAsync("R-13", new DateOnly(2026, 1, 1));

        var result = await new RecurringInvoiceService(_db)
            .GenerateDueAsync(_companyId, new DateOnly(2026, 1, 15));

        Assert.True(result.InvoicesCreated == 1, Why(result));
    }

    /// <summary>
    /// <b>وخطةٌ تعثر لا تُسقط غيرَها — ويُسمّى ما تعذّر.</b>
    ///
    /// <para>فمعاملةٌ واحدةٌ للدفعة كلِّها تجعل <b>عطلَ خطةٍ يمحو عملَ عشرين</b>. وصمتٌ
    /// عمّا سقط يجعل المالكَ يظنّ أنّ كلَّ عقوده فُوتِرت.</para>
    /// </summary>
    [Fact]
    public async Task AFailingPlan_DoesNotCancelTheOthers()
    {
        await PlanAsync("R-14", new DateOnly(2026, 1, 1));

        var broken = RecurringInvoicePlan.Create(_companyId, _customerId, "R-15", "بلا سطور",
            RecurrenceFrequency.Monthly, new DateOnly(2026, 1, 1));
        _db.RecurringInvoicePlans.Add(broken);
        await _db.SaveChangesAsync();

        var result = await new RecurringInvoiceService(_db)
            .GenerateDueAsync(_companyId, new DateOnly(2026, 1, 15));

        Assert.Equal(1, result.InvoicesCreated);
        Assert.Single(result.Refusals);
        Assert.Contains("R-15", result.Refusals[0], StringComparison.Ordinal);
    }

    /// <summary><b>والفاتورةُ المولَّدة تحمل سطورَ الخطة وخصمَها.</b></summary>
    [Fact]
    public async Task TheGeneratedInvoice_CarriesThePlanLines()
    {
        var customerId = await CustomerAsync();
        var plan = RecurringInvoicePlan.Create(_companyId, customerId, "R-16", "صيانة",
            RecurrenceFrequency.Monthly, new DateOnly(2026, 1, 1));
        plan.AddLine("عقد صيانة", 1m, 1000m, discountPercent: 10m);
        _db.RecurringInvoicePlans.Add(plan);
        await _db.SaveChangesAsync();

        await new RecurringInvoiceService(_db).GenerateDueAsync(_companyId, new DateOnly(2026, 1, 5));

        var invoice = await _db.SalesInvoices.AsNoTracking()
            .SingleAsync(i => i.CompanyId == _companyId);

        Assert.Equal(900m, invoice.Subtotal);
        Assert.Equal(135m, invoice.VatAmount);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    /// <summary>‏رسالةُ الفشل تحمل ما تعذّر — وإلا صار كلُّ سقوطٍ تنقيباً في السجلّ.</summary>
    private static string Why(RecurringGenerationResult r) =>
        $"وُلِّد {r.InvoicesCreated}. وتعذّر:\n  " + string.Join("\n  ", r.Refusals);

    private async Task<RecurringInvoicePlan> PlanAsync(string code, DateOnly start)
    {
        // ‏**والعميلُ يُحضَّر قبل السطر التالي لا داخله**: وسائطُ الاستدعاء تُقيَّم يساراً
        // ‏فيميناً، فـ`_companyId` كان يُقرأ **صفراً** قبل أن يضبطه `await CustomerAsync()`.
        // ‏فتُنشأ خطةٌ لمنشأةٍ لا وجودَ لها، ويُعيد الاستعلامُ فراغاً — **بلا استثناءٍ ولا
        // ‏سببٍ مطبوع**. وكشفه أنّ الحصيلةَ رجعت بصفرِ توليدٍ **وصفرِ تعذُّر**.
        var customerId = await CustomerAsync();

        var plan = RecurringInvoicePlan.Create(_companyId, customerId, code, "إيجار شهريّ",
            RecurrenceFrequency.Monthly, start);
        plan.AddLine("إيجار", 1m, 5000m);
        _db.RecurringInvoicePlans.Add(plan);
        await _db.SaveChangesAsync();
        return plan;
    }

    private async Task<long> CustomerAsync()
    {
        await SetupAsync();
        if (_customerId != 0) return _customerId;

        var customer = Customer.Create(_companyId, "مستأجر", "311111111100003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;
        return _customerId;
    }

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة العقارات", "310123456700066", BusinessProfile.RealEstate);
        _companyId = company.Id;
    }
}
