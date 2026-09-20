using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Accounting;

/// <summary>
/// <b>السنةُ المقفلةُ صارت مقفلةً فعلاً — وكانت مقفلةً على الورق وحدَه.</b>
///
/// <para><b>العطل:</b> الفترةُ تُنشأ عند أوّل ترحيلٍ فيها لا عند بدء السنة، والإقفالُ السنويُّ
/// كان يُقفل <b>الصفوفَ القائمة</b> وحدَها. فكلُّ شهرٍ بلا حركةٍ ثغرةٌ دائمةٌ في سنةٍ
/// «مقفلة»: يُنشئه <c>EnsurePeriodOpenAsync</c> مفتوحاً ويقبل القيد.</para>
///
/// <para><b>وقيس قبل الإصلاح:</b> إيرادٌ 1000 في 2025-06-15، ثمّ إقفالُ 2025 —
/// الفتراتُ <c>2025-06=Closed · 2025-12=Closed</c> لا غير. ثمّ قيدٌ بتاريخ 2025-03-10
/// <b>قُبِل ورُحِّل</b>: صافي 4101 عن السنة المقفلة <c>-500</c> بدل صفر، و3103 لم يتحرّك،
/// <b>وإعادةُ الإقفال مرفوضةٌ أبداً</b> لأنّ قيدَ الإقفال قائم. أي أنّ دفترَ سنةٍ اعتمدها
/// المراجعُ يتغيّر بعده ولا يُردّ.</para>
///
/// <para><b>ولم يره أحدُ الاختبارات الـ2218</b>: كلُّها تُرحّل قبل أن تُقفل، فكلُّ شهرٍ
/// تُقفله موجودٌ أصلاً. <b>والثغرةُ في الفجوة بين نمطين صحيحين</b> — صفٌّ كسولٌ وحلقةٌ
/// تُقفل الموجود.</para>
///
/// <para><b>والحارسُ دقيقٌ لا شامل:</b> لا يُسأل إلّا حين يغيب الصفّ، فإعادةُ فتح فترةٍ
/// عمداً تبقى نافذة (<see cref="ReopenedPeriod_OfAClosedYear_StillAcceptsPosting"/>)،
/// وسنةٌ مفتوحةٌ تُنشئ شهورَها كما كانت
/// (<see cref="NewMonth_InAnOpenYear_StillCreatesItsPeriod"/>). ولولا هذان الشاهدان
/// لكان الإصلاحُ منعاً للتصحيح المشروع لا حمايةً للدفتر.</para>
/// </summary>
public class YearEndCloseYearLockTests : IDisposable
{
    private readonly BrqoshDbContext _db;

    public YearEndCloseYearLockTests()
    {
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";
    }

    private async Task<(long CompanyId, long CustomerId)> SeedAsync()
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync(
            "شركة تجارية", "310123456700099", BusinessProfile.General);
        var customer = Customer.Create(company.Id, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        return (company.Id, customer.Id);
    }

    private async Task SellAsync(long companyId, long customerId, DateOnly date, decimal amount)
    {
        var invoice = SalesInvoice.CreateDraft(companyId, customerId, date);
        invoice.AddLine("خدمة", 1m, amount);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);
    }

    private async Task<(long CompanyId, long CustomerId)> ClosedYear2025Async()
    {
        var (companyId, customerId) = await SeedAsync();
        await SellAsync(companyId, customerId, new DateOnly(2025, 6, 15), 1_000m);
        await new YearEndCloseService(_db).CloseYearAsync(companyId, 2025, "admin");
        return (companyId, customerId);
    }

    private Task<bool> PostedOnAsync(long companyId, DateOnly date) =>
        _db.JournalEntries.AnyAsync(e =>
            e.CompanyId == companyId
            && e.EntryDate == date
            && e.Status == JournalEntryStatus.Posted);

    // ── العطلُ نفسُه ────────────────────────────────────────────────────────

    /// <summary><b>القياسُ الأساسيّ:</b> الشهرُ الساكنُ في سنةٍ مقفلةٍ يرفض الترحيل.</summary>
    [Fact]
    public async Task BackdatedPosting_IntoASilentMonthOfAClosedYear_IsRefused()
    {
        var (companyId, customerId) = await ClosedYear2025Async();

        var ex = await Assert.ThrowsAsync<AccountingException>(
            () => SellAsync(companyId, customerId, new DateOnly(2025, 3, 10), 500m));

        Assert.Contains("مقفلة", ex.Message);
        Assert.False(await PostedOnAsync(companyId, new DateOnly(2025, 3, 10)),
            "القيدُ دخل الدفترَ رغم رفضِ الحارس");
    }

    /// <summary>
    /// <b>والسنةُ تُقفل باثنَي عشرَ شهراً</b> — لا بما صادف أن تكون له حركة. وهذا الصفُّ
    /// المقفَلُ هو نفسُه طريقُ التصحيح المشروع: إعادةُ الفتح تطلب صفّاً بمعرّفه.
    /// </summary>
    [Fact]
    public async Task CloseYear_ClosesAllTwelveMonths_NotOnlyTheOnesWithActivity()
    {
        var (companyId, _) = await ClosedYear2025Async();

        var periods = await _db.FiscalPeriods
            .Where(p => p.CompanyId == companyId && p.StartDate.Year == 2025)
            .ToListAsync();

        Assert.Equal(12, periods.Count);
        Assert.All(periods, p => Assert.Equal(FiscalPeriodStatus.Closed, p.Status));
    }

    /// <summary>وسنةٌ ساكنةٌ بلا حركةٍ أصلاً تُقفل باثنَي عشرَ شهراً كذلك — لا بصفرٍ منها.</summary>
    [Fact]
    public async Task CloseYear_OfASilentYear_AlsoClosesAllTwelveMonths()
    {
        var (companyId, _) = await SeedAsync();

        await new YearEndCloseService(_db).CloseYearAsync(companyId, 2024, "admin");

        var periods = await _db.FiscalPeriods
            .Where(p => p.CompanyId == companyId && p.StartDate.Year == 2024)
            .ToListAsync();

        Assert.Equal(12, periods.Count);
        Assert.All(periods, p => Assert.Equal(FiscalPeriodStatus.Closed, p.Status));
    }

    // ── شاهدا الضبط: الحارسُ دقيقٌ لا شامل ──────────────────────────────────

    /// <summary>
    /// <b>شاهدُ ضبطٍ أوّل:</b> إعادةُ فتح فترةٍ من سنةٍ مقفلةٍ تُعيد قبولَ الترحيل. ولولاه
    /// لكان الحارسُ يمنع التصحيحَ الذي وُجدت إعادةُ الفتح من أجله.
    /// </summary>
    [Fact]
    public async Task ReopenedPeriod_OfAClosedYear_StillAcceptsPosting()
    {
        var (companyId, customerId) = await ClosedYear2025Async();

        var march = await _db.FiscalPeriods.SingleAsync(p =>
            p.CompanyId == companyId && p.Code == "2025-03");
        await new FiscalPeriodService(_db).ReopenAsync(companyId, march.Id, "admin");

        await SellAsync(companyId, customerId, new DateOnly(2025, 3, 10), 500m);

        Assert.True(await PostedOnAsync(companyId, new DateOnly(2025, 3, 10)),
            "الترحيلُ في فترةٍ أُعيد فتحُها عمداً رُفض — الحارسُ صار شاملاً");
    }

    /// <summary>
    /// <b>شاهدُ ضبطٍ ثانٍ:</b> شهرٌ جديدٌ في سنةٍ <b>مفتوحة</b> يُنشئ فترتَه ويُرحَّل فيه
    /// كما كان. فالحارسُ لا يُسأل إلّا عن سنةٍ تحمل قيدَ إقفال.
    /// </summary>
    [Fact]
    public async Task NewMonth_InAnOpenYear_StillCreatesItsPeriod()
    {
        var (companyId, customerId) = await ClosedYear2025Async();

        await SellAsync(companyId, customerId, new DateOnly(2026, 3, 10), 500m);

        Assert.True(await PostedOnAsync(companyId, new DateOnly(2026, 3, 10)));
        var march = await _db.FiscalPeriods.SingleAsync(p =>
            p.CompanyId == companyId && p.Code == "2026-03");
        Assert.Equal(FiscalPeriodStatus.Open, march.Status);
    }

    public void Dispose()
    {
        SecurityContext.CurrentRole = null;
        SecurityContext.CurrentUsername = null;
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
