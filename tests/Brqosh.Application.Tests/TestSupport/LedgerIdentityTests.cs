using Brqosh.Application.Tests.TestSupport;
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
/// (CEEF-229 · ح-٥) <b>حارسٌ للحارس.</b>
///
/// <para>هذه الاختبارات لا تفحص النظام — تفحص <see cref="LedgerIdentities"/> نفسها. لأن أداة قياس
/// لا تعرف كيف تقول «لا» هي أداةٌ تمرّ على كل شيء، وهذا بالضبط ما أسقط <c>ThemeSwitchingTests</c>
/// و<c>I18nCoverageTests</c> في هذا المستودع من قبل.</para>
///
/// <para>لكل هوية اختباران متلازمان: حالةٌ سليمة <b>يجب أن تمرّ</b>، وحالةٌ مكسورة عمداً
/// <b>يجب أن تسقط</b>. غياب الثاني يجعل الأول بلا معنى.</para>
/// </summary>
public class LedgerIdentityTests : IDisposable
{
    private readonly BrqoshDbContext _db;

    public LedgerIdentityTests()
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
            "شركة الهويات", "310123456700099", BusinessProfile.General);
        var customer = Customer.Create(company.Id, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        return (company.Id, customer.Id);
    }

    private async Task<Account> AccountAsync(long companyId, string code) =>
        await _db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == code);

    // ── الاتجاه الأول: الهوية تصمد على نظام سليم ────────────────────────────

    /// <summary>
    /// بيعٌ آجل عاديّ: الفاتورة تُمدِّن 1104 وتُقيَّد في دفتر العميل معاً — فالطرفان يتطابقان.
    /// لو سقط هذا الاختبار فالهوية نفسها مكسورة، لا النظام.
    /// </summary>
    [Fact]
    public async Task CreditSale_ReceivablesIdentityHolds()
    {
        var (companyId, customerId) = await SeedAsync();

        var invoice = SalesInvoice.CreateDraft(companyId, customerId, new DateOnly(2026, 3, 1));
        invoice.AddLine("خدمة استشارية", 1m, 1000m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var result = await LedgerIdentities.ReceivablesAsync(_db, companyId);

        Assert.True(result.Holds, result.ToString());
        Assert.Equal(1150.00m, result.Ledger);      // 1,000 + ضريبة 150
        Assert.Equal(1150.00m, result.Subsidiary);
    }

    /// <summary>الهويات الثلاث مجتمعةً تصمد على نفس السيناريو السليم.</summary>
    [Fact]
    public async Task CreditSale_AllIdentitiesHold()
    {
        var (companyId, customerId) = await SeedAsync();

        var invoice = SalesInvoice.CreateDraft(companyId, customerId, new DateOnly(2026, 3, 1));
        invoice.AddLine("خدمة", 1m, 500m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        await LedgerIdentities.AssertAllAsync(_db, companyId);   // لا يرمي
    }

    /// <summary>نظام فارغ: الطرفان صفر، والهوية تصمد بلا قسمة ولا استثناء.</summary>
    [Fact]
    public async Task EmptyCompany_AllIdentitiesHold()
    {
        var (companyId, _) = await SeedAsync();

        var results = await LedgerIdentities.MeasureAsync(_db, companyId);

        Assert.All(results, r => Assert.True(r.Holds, r.ToString()));
        Assert.All(results, r => Assert.Equal(0m, r.Ledger));
    }

    // ── الاتجاه الثاني (الأهمّ): الهوية تعضّ على انفصالٍ حقيقيّ ──────────────

    /// <summary>
    /// <b>إثبات العضّ.</b> قيدٌ يدويّ يُمدِّن 1104 بلا أيّ مستند عميل يقابله — وهو بالضبط شكل
    /// كل عطل انفصالٍ وجده تدقيق CEEF-229: الأستاذ يتحرّك والدفتر المساعد لا يعلم.
    /// الهوية <b>يجب</b> أن ترمي، وإلا فهي عمياء.
    /// </summary>
    [Fact]
    public async Task LedgerMovesWithoutSubsidiary_ReceivablesIdentityBites()
    {
        var (companyId, _) = await SeedAsync();

        var entry = JournalEntry.CreateDraft(companyId, new DateOnly(2026, 3, 1), "قيد يدويّ على الذمم");
        entry.AddLine(await AccountAsync(companyId, SaudiChartOfAccounts.SystemCodes.AccountsReceivable),
            7500m, 0m, "مدين ذمم بلا مستند");
        entry.AddLine(await AccountAsync(companyId, SaudiChartOfAccounts.SystemCodes.SalesRevenue),
            0m, 7500m, "إيراد");
        await new JournalPostingService(_db).PostAsync(entry);

        var result = await LedgerIdentities.ReceivablesAsync(_db, companyId);

        Assert.False(result.Holds, "الهوية لم تكشف انفصالاً بـ7,500 — أداة القياس عمياء");
        Assert.Equal(7500.00m, result.Ledger);
        Assert.Equal(0.00m, result.Subsidiary);
        Assert.Equal(-7500.00m, result.Difference);

        var thrown = await Assert.ThrowsAsync<LedgerIdentityException>(
            () => LedgerIdentities.AssertAllAsync(_db, companyId));
        Assert.Contains("1104", thrown.Message);
    }

    /// <summary>إثبات العضّ على جهة الموردين — الاتجاه الدائن.</summary>
    [Fact]
    public async Task LedgerMovesWithoutSubsidiary_PayablesIdentityBites()
    {
        var (companyId, _) = await SeedAsync();

        var entry = JournalEntry.CreateDraft(companyId, new DateOnly(2026, 3, 1), "قيد يدويّ على الدائنين");
        entry.AddLine(await AccountAsync(companyId, SaudiChartOfAccounts.SystemCodes.Inventory),
            4000m, 0m, "مخزون");
        entry.AddLine(await AccountAsync(companyId, SaudiChartOfAccounts.SystemCodes.AccountsPayable),
            0m, 4000m, "دائن بلا مستند");
        await new JournalPostingService(_db).PostAsync(entry);

        var payables = await LedgerIdentities.PayablesAsync(_db, companyId);

        Assert.False(payables.Holds, "الهوية لم تكشف انفصالاً على 2101");
        Assert.Equal(4000.00m, payables.Ledger);
        Assert.Equal(0.00m, payables.Subsidiary);
    }

    /// <summary>
    /// إثبات العضّ على المخزون: قيدٌ يُمدِّن 1107 بلا حركة مخزنية — وهو شكل عطل «إذن الاستلام
    /// بلا قيد» معكوساً، وشكل «مرتجع لا يُرجع بضاعة» تماماً.
    /// </summary>
    [Fact]
    public async Task LedgerMovesWithoutStock_InventoryIdentityBites()
    {
        var (companyId, _) = await SeedAsync();

        var entry = JournalEntry.CreateDraft(companyId, new DateOnly(2026, 3, 1), "قيد يدويّ على المخزون");
        entry.AddLine(await AccountAsync(companyId, SaudiChartOfAccounts.SystemCodes.Inventory),
            12000m, 0m, "مخزون بلا حركة");
        entry.AddLine(await AccountAsync(companyId, SaudiChartOfAccounts.SystemCodes.AccountsPayable),
            0m, 12000m, "دائن");
        await new JournalPostingService(_db).PostAsync(entry);

        var inventory = await LedgerIdentities.InventoryAsync(_db, companyId);

        Assert.False(inventory.Holds, "الهوية لم تكشف مخزوناً في الأستاذ بلا رصيد مقابل");
        Assert.Equal(12000.00m, inventory.Ledger);
        Assert.Equal(0.00m, inventory.Subsidiary);
    }

    /// <summary>
    /// حدّ التسامح مضبوط على الهللة: فرقٌ بـ0.01 يُقبل، وبـ0.02 يُرفض. لولا هذا لكان «التسامح»
    /// باباً خلفياً يبتلع انحرافاً حقيقياً.
    /// </summary>
    [Theory]
    [InlineData(1000.00, 1000.01, true)]
    [InlineData(1000.00, 999.99, true)]
    [InlineData(1000.00, 1000.02, false)]
    [InlineData(1000.00, 999.98, false)]
    public void Tolerance_IsOneHalalaExactly(double subsidiary, double ledger, bool expectedHolds)
    {
        var result = new LedgerIdentities.IdentityResult("اختبار", (decimal)subsidiary, (decimal)ledger);

        Assert.Equal(expectedHolds, result.Holds);
    }

    public void Dispose()
    {
        SecurityContext.CurrentRole = null;
        SecurityContext.CurrentUsername = null;
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
