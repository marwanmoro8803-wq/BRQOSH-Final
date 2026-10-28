using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Accounting;

/// <summary>
/// ‏(Phase 7) <b>تصحيحُ مسمّيات الدليل</b>: أصولُ العقود، ومخصّصُ الخسائر المتوقّعة، والعُهدُ منفصلةً عن السُّلَف، والخصمُ المكتسب تحت 51،
/// و5103 و2202 لا يُزرعان — وتصل التصحيحاتُ للقواعد القائمة دون أن تمسّ ما سمّاه المالك.
/// </summary>
public class TheChartNamesSayWhatTheAccountsHoldTests : IDisposable
{
    private readonly BrqoshDbContext _db;

    public TheChartNamesSayWhatTheAccountsHoldTests()
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

    [Fact]
    public async Task ANewCompany_GetsTheCorrectedNames_AndParents()
    {
        var company = await InitAsync();
        var a = await AccountsAsync(company);

        Assert.Equal("Contract Assets (Unbilled Work)", a["1113"].NameEn);
        Assert.Equal("مخصص خسائر الائتمان المتوقعة", a["1106"].NameAr);
        Assert.Equal("عُهد نقدية لدى الموظفين", a["1112"].NameAr);
        Assert.Equal("سُلَف الموظفين", a["1116"].NameAr);
        Assert.Equal(a["51"].Id, a["5109"].ParentId);
        Assert.Equal("5109", await PostingRoleService.ResolveAsync(_db, company, SaudiChartOfAccounts.PostingRoleCodes.PurchaseDiscountsEarned));
        Assert.False(a.ContainsKey("5214"));
        Assert.False(a.ContainsKey("5103"));
        Assert.False(a.ContainsKey("2202"));
    }

    [Fact]
    public async Task TheOptionalEndOfServiceProvision_IsCreatedWhenFirstNeeded()
    {
        var company = await InitAsync();

        var provision = await SystemAccountProvisioning.EnsureAsync(_db, company, SaudiChartOfAccounts.PostingRoleCodes.EndOfServiceProvision);

        Assert.Equal("مخصص مكافأة نهاية الخدمة", provision.NameAr);
        Assert.Equal((await AccountsAsync(company))["22"].Id, provision.ParentId);
    }

    [Fact]
    public async Task AnExistingCompany_IsCorrectedAtStartup_ButOwnerNamesAndUsedAccountsAreKept()
    {
        var company = await InitAsync();
        var a = await AccountsAsync(company);
        // ‏قاعدةٌ قديمة: أسماءُ القالب السابق، والخصمُ المكتسب على 5214 تحت 52 بلا 5109، و5103 موجود — و1106 أعاد المالكُ تسميتَه.
        a["1113"].Rename("أعمال تحت التنفيذ (مستخلصات غير مفوترة)", "Work in Progress - Unbilled");
        a["1106"].Rename("مخصص ديون العملاء", "Customer bad debt allowance");
        _db.Accounts.Remove(a["5109"]);
        _db.Accounts.Add(Account.Create("5214", "خصم مكتسب", "Purchase Discounts Earned",
            AccountType.Expense, a["52"].Id, isGroup: false, isSystem: false, company));
        _db.Accounts.Add(Account.Create("5103", "مردودات ومسموحات المشتريات", "Purchase Returns & Allowances",
            AccountType.Expense, a["51"].Id, isGroup: false, isSystem: false, company));
        (await _db.PostingRoleAccounts.SingleAsync(r => r.CompanyId == company && r.Role == "PurchaseDiscountsEarned")).Assign("5214");
        await _db.SaveChangesAsync();

        await InitAsync();
        a = await AccountsAsync(company);

        Assert.Equal("أصول العقود (أعمال منفذة غير مفوترة)", a["1113"].NameAr);
        Assert.Equal("خصم مكتسب (قديم — يُرحَّل الآن على 5109)", a["5214"].NameAr);
        Assert.Equal(a["51"].Id, a["5109"].ParentId);
        Assert.Equal("5109", await PostingRoleService.ResolveAsync(_db, company, SaudiChartOfAccounts.PostingRoleCodes.PurchaseDiscountsEarned));
        Assert.Equal("مخصص ديون العملاء", a["1106"].NameAr);
        Assert.False(a["5103"].IsActive);
        Assert.Equal(0, await ChartTemplateSync.ApplyNamingCorrectionsAsync(_db));
    }

    private async Task<Dictionary<string, Account>> AccountsAsync(long company) =>
        await _db.Accounts.Where(x => x.CompanyId == company).ToDictionaryAsync(x => x.Code);

    private async Task<long> InitAsync() =>
        (await new DatabaseInitializer(_db).InitializeAsync("منشأة المسميات", "300000000000157", BusinessProfile.General)).Id;
}
