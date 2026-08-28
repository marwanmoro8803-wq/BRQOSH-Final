using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Accounting;

/// <summary>
/// (CEEF-229 · م0-١٣ و م0-١٤) الإقفال السنويّ صار ممكناً.
///
/// <para><b>عطلان متلازمان كانا يجعلان زرّ «إقفال سنوي» لا ينجح أبداً:</b></para>
///
/// <para><b>م0-١٤ —</b> حارس <c>openOutside</c> كان يرفض الإقفال متى وُجد أيّ قيد مرحّل بعد
/// 31 ديسمبر، وهي الحالة الطبيعية لكل منشأة تُقفل ديسمبر في مارس. ورسالته كانت تقترح علاجاً
/// غير موجود: هو يفحص <b>القيود</b> لا الفترات، ولا يُحذف قيدٌ مرحّل ولا يُنقل تاريخه.</para>
///
/// <para><b>م0-١٣ —</b> <c>Account.Deactivate()</c> كانت تضبط <c>DeletedAtUtc</c> أيضاً، أي أن
/// «إيقاف حساب» = «حذفه». فيسقط خلف فلتر الحذف ويرمي <c>AccountAsync</c> استثناءً خاماً، ولا
/// <c>Reactivate</c> أصلاً — فلا مخرج إلا SQL يدويّ على قاعدة الإنتاج.</para>
///
/// <para><b>والترتيب بينهما مُلزِم:</b> حذف الحارس قبل إصلاح الحساب كان <b>ينقل</b> الانهيار إلى
/// <c>AccountAsync</c> بدل إزالته — وهو بعينه ما فعله إصلاحٌ سابق في هذه الحملة حين أضاف
/// <c>IgnoreQueryFilters</c> إلى التقارير ونسي مسار الإقفال.</para>
/// </summary>
public class YearEndCloseBlockersTests : IDisposable
{
    private readonly BrqoshDbContext _db;

    public YearEndCloseBlockersTests()
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

    private async Task<decimal> RetainedEarningsAsync(long companyId)
    {
        var rows = await new TrialBalanceQuery(_db).ExecuteAsync(
            companyId, new DateOnly(2000, 1, 1), new DateOnly(2099, 12, 31));
        var row = rows.FirstOrDefault(r => r.AccountCode == SaudiChartOfAccounts.SystemCodes.RetainedEarnings);
        return row is null ? 0m : row.TotalCredit - row.TotalDebit;
    }

    // ── م0-١٤ · الإقفال ينجح رغم حركة السنة التالية ─────────────────────────

    /// <summary>
    /// <b>السيناريو الواقعيّ الوحيد:</b> منشأة تعمل في 2026 وتُقفل 2025 في مارس.
    /// قبل الإصلاح كان هذا يرتدّ برسالة تقترح علاجاً لا يعمل.
    /// </summary>
    [Fact]
    public async Task CloseYear_Succeeds_EvenWithPostedEntriesInTheFollowingYear()
    {
        var (companyId, customerId) = await SeedAsync();
        await SellAsync(companyId, customerId, new DateOnly(2025, 6, 15), 40_000m);

        // العمل بدأ في السنة التالية — وهو الوضع الطبيعي لا الاستثناء
        await SellAsync(companyId, customerId, new DateOnly(2026, 1, 4), 5_000m);
        await SellAsync(companyId, customerId, new DateOnly(2026, 2, 20), 7_500m);

        var result = await new YearEndCloseService(_db).CloseYearAsync(companyId, 2025, "admin");

        Assert.Equal(2025, result.Year);
        Assert.Equal(40_000m, await RetainedEarningsAsync(companyId));   // إيراد 2025 وحده
    }

    /// <summary>
    /// الحارس الصحيح باقٍ: الترحيل في فترة مقفلة يبقى مرفوضاً. حذفُ حارسٍ زائد لا يعني
    /// فتحَ الباب — ولولا هذا التأكيد لكان الإصلاح إضعافاً لا تصحيحاً.
    /// </summary>
    [Fact]
    public async Task ClosedPeriod_StillRejectsPosting()
    {
        var (companyId, customerId) = await SeedAsync();
        await SellAsync(companyId, customerId, new DateOnly(2025, 6, 15), 10_000m);
        await new YearEndCloseService(_db).CloseYearAsync(companyId, 2025, "admin");

        await Assert.ThrowsAsync<AccountingException>(
            () => new YearEndCloseService(_db).CloseYearAsync(companyId, 2025, "admin"));
    }

    // ── م0-١٣ · حسابٌ موقوف لا يمنع الإقفال ─────────────────────────────────

    /// <summary>
    /// <b>القياس الأساسيّ.</b> حساب إيراد أُوقف في نوفمبر وعليه حركة 12,000:
    /// الإقفال ينجح ويُرحّل الرصيد إلى الأرباح المبقاة. قبل الإصلاح كان يرمي
    /// <c>Sequence contains no elements</c> بالإنجليزية، ولا مخرج.
    /// </summary>
    [Fact]
    public async Task DeactivatedRevenueAccount_DoesNotBlockYearEndClose()
    {
        var (companyId, customerId) = await SeedAsync();
        await SellAsync(companyId, customerId, new DateOnly(2025, 5, 10), 12_000m);

        // المحاسب يوقف حساب المبيعات بعد أن حُوِّل النشاط إلى حساب آخر
        var revenue = await _db.Accounts.SingleAsync(a =>
            a.CompanyId == companyId && a.Code == SaudiChartOfAccounts.SystemCodes.SalesRevenue);
        typeof(Account).GetProperty(nameof(Account.IsSystem))!
            .SetValue(revenue, false);          // 4101 نظاميّ بالتصميم — نُحاكي حساباً غير نظاميّ
        revenue.Deactivate();
        await _db.SaveChangesAsync();

        Assert.False(revenue.IsActive);
        Assert.Null(revenue.DeletedAtUtc);      // الإيقاف لم يعد حذفاً

        var result = await new YearEndCloseService(_db).CloseYearAsync(companyId, 2025, "admin");

        Assert.Equal(2025, result.Year);
        Assert.Equal(12_000m, await RetainedEarningsAsync(companyId));
    }

    /// <summary>الإيقاف صار قابلاً للتراجع — وغياب ذلك كان يجعله قراراً بلا رجعة.</summary>
    [Fact]
    public async Task DeactivatedAccount_CanBeReactivated()
    {
        var (companyId, _) = await SeedAsync();
        var account = Account.Create("4199", "إيراد تجريبي", "Test", AccountType.Revenue,
            parentId: null, isGroup: false, isSystem: false, companyId: companyId);
        _db.Accounts.Add(account);
        await _db.SaveChangesAsync();

        account.Deactivate();
        await _db.SaveChangesAsync();
        Assert.False(account.IsPostable);

        account.Reactivate();
        await _db.SaveChangesAsync();

        Assert.True(account.IsPostable);
        Assert.Null(account.DeletedAtUtc);
    }

    /// <summary>
    /// الحساب الموقوف يبقى <b>مرئياً</b> — لا يختفي من دليل الحسابات ولا من التقارير.
    /// اختفاؤه كان العَرَض الذي أخفى العطل الحقيقيّ شهوراً.
    /// </summary>
    [Fact]
    public async Task DeactivatedAccount_RemainsVisibleToQueries()
    {
        var (companyId, _) = await SeedAsync();
        var account = Account.Create("4198", "إيراد تجريبي", "Test", AccountType.Revenue,
            parentId: null, isGroup: false, isSystem: false, companyId: companyId);
        _db.Accounts.Add(account);
        await _db.SaveChangesAsync();

        account.Deactivate();
        await _db.SaveChangesAsync();

        var visible = await _db.Accounts.AnyAsync(a => a.CompanyId == companyId && a.Code == "4198");
        Assert.True(visible, "الحساب الموقوف اختفى خلف فلتر الحذف — الإيقاف عاد حذفاً");
    }

    /// <summary>الترحيل التشغيليّ على حساب موقوف يبقى مرفوضاً — الإيقاف يمنع الجديد لا القديم.</summary>
    [Fact]
    public async Task DeactivatedAccount_StillRejectsOperationalPosting()
    {
        var (companyId, _) = await SeedAsync();
        var account = Account.Create("4197", "إيراد تجريبي", "Test", AccountType.Revenue,
            parentId: null, isGroup: false, isSystem: false, companyId: companyId);
        _db.Accounts.Add(account);
        await _db.SaveChangesAsync();
        account.Deactivate();

        var entry = JournalEntry.CreateDraft(companyId, new DateOnly(2026, 1, 1), "قيد يدويّ");

        var ex = Assert.Throws<AccountingException>(() => entry.AddLine(account, 0m, 100m));
        Assert.Contains("موقوف", ex.Message);
    }

    /// <summary>وسطرُ الإقفال يقبله — وهذا هو الفارق كلّه بين إجراءٍ نظاميّ وترحيلٍ تشغيليّ.</summary>
    [Fact]
    public async Task ClosingLine_AcceptsDeactivatedAccount_ButNotGroupAccount()
    {
        var (companyId, _) = await SeedAsync();
        var stopped = Account.Create("4196", "إيراد موقوف", "Test", AccountType.Revenue,
            parentId: null, isGroup: false, isSystem: false, companyId: companyId);
        var group = Account.Create("4195", "مجموعة", "Group", AccountType.Revenue,
            parentId: null, isGroup: true, isSystem: false, companyId: companyId);
        _db.Accounts.AddRange(stopped, group);
        await _db.SaveChangesAsync();
        stopped.Deactivate();

        var entry = JournalEntry.CreateDraft(companyId, new DateOnly(2025, 12, 31), "إقفال");

        entry.AddClosingLine(stopped, 100m, 0m);            // يُقبل
        Assert.Single(entry.Lines);

        var ex = Assert.Throws<AccountingException>(() => entry.AddClosingLine(group, 0m, 100m));
        Assert.Contains("تجميعي", ex.Message);
    }

    public void Dispose()
    {
        SecurityContext.CurrentRole = null;
        SecurityContext.CurrentUsername = null;
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
