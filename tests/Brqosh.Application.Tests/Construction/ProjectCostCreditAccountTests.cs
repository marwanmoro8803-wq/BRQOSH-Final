using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Construction;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Construction;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Construction;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٤٥ · <c>sector-modules#9</c>) <b>تكلفةُ المشروع اليدويّة تُقيَّد على الحساب الذي دفع.</b>
///
/// <para><b>ما كان:</b> الدائنُ «الصندوق» في كلّ تكلفة — فتكلفةٌ دُفعت من البنك أو استُحقّت ولم تُدفع تُخرج
/// الصندوقَ سالباً وتترك البنكَ والمستحقّاتِ كما هي، والميزانُ متوازنٌ فلا يشكو أحد.</para>
/// </summary>
public class ProjectCostCreditAccountTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _projectId;

    public ProjectCostCreditAccountTests()
    {
        TestActor.Accountant();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Database.CloseConnection();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Theory]
    [InlineData(SaudiChartOfAccounts.SystemCodes.CashAtBanks)]
    [InlineData(SaudiChartOfAccounts.SystemCodes.AccruedExpenses)]
    public async Task TheChosenAccount_IsCredited_NotPettyCash(string creditCode)
    {
        await SetupAsync();

        await RecordAsync(4_000m, creditCode);

        Assert.Equal(-4_000m, await BalanceAsync(creditCode));
        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.CashOnHand));
        Assert.Equal(4_000m, await BalanceAsync("5105"));
    }

    /// <summary><b>وبلا اختيارٍ يبقى الصندوقُ</b> — سلوكُ المستدعين القائمين لا يتغيّر.</summary>
    [Fact]
    public async Task WithoutAChoice_PettyCashIsStillTheDefault()
    {
        await SetupAsync();

        await RecordAsync(1_000m, null);

        Assert.Equal(-1_000m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.CashOnHand));
    }

    /// <summary>
    /// <b>والموردون والعملاءُ والمخزونُ مرفوضةٌ</b> — أرصدتُها من دفاترها المساعدة، وقيدٌ يدويٌّ عليها
    /// يكسر مطابقتَها؛ <b>ولا يُرحَّل شيءٌ ولا تُسجَّل تكلفة</b>.
    /// </summary>
    [Theory]
    [InlineData(SaudiChartOfAccounts.SystemCodes.AccountsPayable)]
    [InlineData(SaudiChartOfAccounts.SystemCodes.AccountsReceivable)]
    [InlineData(SaudiChartOfAccounts.SystemCodes.Inventory)]
    [InlineData(SaudiChartOfAccounts.SystemCodes.SalesRevenue)]
    public async Task AControlOrPAndLAccount_IsRefused_AndNothingIsRecorded(string creditCode)
    {
        await SetupAsync();

        await Assert.ThrowsAsync<AccountingException>(() => RecordAsync(500m, creditCode));

        Assert.False(await _db.ProjectCostEntries.AnyAsync());
        Assert.False(await _db.JournalEntries.AnyAsync(e => e.SourceDocument!.StartsWith("ProjectCost")));
    }

    /// <summary><b>والحوارُ لا يعرض إلا ما يُقبل</b> — قائمتُه والقبولُ قرارٌ واحد.</summary>
    [Fact]
    public async Task TheDialogList_IsExactlyWhatIsAccepted()
    {
        await SetupAsync();

        var codes = (await new ProjectCostService(_db).CreditableAccountsAsync(_companyId)).Select(a => a.Code).ToList();

        Assert.Contains(SaudiChartOfAccounts.SystemCodes.CashOnHand, codes);
        Assert.Contains(SaudiChartOfAccounts.SystemCodes.CashAtBanks, codes);
        Assert.Contains(SaudiChartOfAccounts.SystemCodes.AccruedExpenses, codes);
        Assert.DoesNotContain(SaudiChartOfAccounts.SystemCodes.AccountsPayable, codes);
        Assert.DoesNotContain(SaudiChartOfAccounts.SystemCodes.Inventory, codes);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة مقاولات", "300000000000145", BusinessProfile.Construction);
        _companyId = company.Id;

        var project = ConstructionProject.Create(_companyId, "PRJ-145", "مشروع الدائن", customerId: null,
            contractValue: 0m, retentionPercent: 0m, estimatedTotalCost: 0m);
        project.Activate();
        await new ConstructionProjectService(_db).CreateAsync(project);
        await _db.SaveChangesAsync();
        _projectId = project.Id;
    }

    private Task<ProjectCostEntry> RecordAsync(decimal amount, string? creditCode) =>
        new ProjectCostService(_db).RecordAsync(_companyId, _projectId, new DateOnly(2026, 5, 10), "أجور معدّات", amount,
            "admin", postJournal: true, creditAccountCode: creditCode);

    private async Task<decimal> BalanceAsync(string accountCode) =>
        (await _db.JournalEntries
            .Where(e => e.CompanyId == _companyId && e.Status != JournalEntryStatus.Draft)
            .SelectMany(e => e.Lines)
            .Where(l => l.AccountCode == accountCode)
            .Select(l => new { l.Debit, l.Credit })
            .ToListAsync())
        .Sum(l => l.Debit - l.Credit);
}
