using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Accounting;

/// <summary>
/// ‏(Phase 7) <b>حساباتٌ ناقصةٌ في الدليل</b>: تكاليفُ التمويل، وضريبةُ الاستقطاع المستحقّة، ومجمّعُ إهلاكٍ لكلّ فئة أصول —
/// في المنشأة الجديدة، وتُضاف للمنشأة القائمة عند الإقلاع مرّةً واحدة.
/// </summary>
public class TheChartHasFinanceCostsWithholdingAndPerClassDepreciationTests : IDisposable
{
    private readonly BrqoshDbContext _db;

    public TheChartHasFinanceCostsWithholdingAndPerClassDepreciationTests()
    {
        TestActor.Administrator();
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
    [InlineData("5221", AccountType.Expense, "52")]
    [InlineData("2118", AccountType.Liability, "21")]
    [InlineData("12071", AccountType.Asset, "12")]
    [InlineData("12076", AccountType.Asset, "12")]
    public async Task ANewCompany_HasTheAccount_UnderItsParent(string code, AccountType type, string parent)
    {
        var company = await InitAsync();

        var account = await _db.Accounts.SingleAsync(a => a.CompanyId == company && a.Code == code);
        var parentAccount = await _db.Accounts.SingleAsync(a => a.CompanyId == company && a.Code == parent);
        Assert.Equal(type, account.Type);
        Assert.Equal(parentAccount.Id, account.ParentId);
        Assert.True(account.IsSystem);
    }

    [Fact]
    public async Task AnExistingCompany_GetsTheMissingAccountsAtStartup_Once()
    {
        var company = await InitAsync();
        var added = SaudiChartOfAccounts.AddedForExistingCompanies;
        _db.Accounts.RemoveRange(await _db.Accounts.Where(a => a.CompanyId == company && added.Contains(a.Code)).ToListAsync());
        await _db.SaveChangesAsync();

        await InitAsync();

        Assert.Equal(added.Count, await _db.Accounts.CountAsync(a => a.CompanyId == company && added.Contains(a.Code)));
        Assert.Equal(0, await ChartTemplateSync.AddMissingAsync(_db));
    }

    [Theory]
    [InlineData("1202", "12071")]
    [InlineData("1203", "12072")]
    [InlineData("1204", "12073")]
    [InlineData("1205", "12074")]
    [InlineData("1206", "12075")]
    [InlineData("1208", "12076")]
    [InlineData("1201", "1207")]
    public void EachAssetClass_HasItsOwnAccumulatedDepreciation(string asset, string accumulated)
    {
        Assert.Equal(accumulated, SaudiChartOfAccounts.AccumulatedDepreciationFor(asset));
        Assert.StartsWith("1207", accumulated, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FinanceCostsAndWithholding_ArePostingRoles_ResolvedToTheNewAccounts()
    {
        var company = await InitAsync();
        Assert.Equal("5221", (await PostingRoleService.ResolveAsync(_db, company, SaudiChartOfAccounts.PostingRoleCodes.FinanceCosts)));
        Assert.Equal("2118", (await PostingRoleService.ResolveAsync(_db, company, SaudiChartOfAccounts.PostingRoleCodes.WithholdingTaxPayable)));
    }

    private async Task<long> InitAsync() =>
        (await new DatabaseInitializer(_db).InitializeAsync("منشأة الدليل", "300000000000157", BusinessProfile.General)).Id;
}
