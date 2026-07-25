using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Treasury;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Reporting;

public class IntercompanyEliminationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public IntercompanyEliminationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
    }

    [Fact]
    public async Task ConsolidatedTrialBalance_eliminates_intercompany_entries()
    {
        var group = CompanyGroup.Create("GRP-IC", "مجموعة IC", "SAR");
        _db.CompanyGroups.Add(group);
        await _db.SaveChangesAsync();

        var companyA = await SeedCompanyAsync("شركة أ", "310123456700020", group.Id);
        var companyB = await new DatabaseInitializer(_db).CreateAdditionalCompanyAsync(
            "شركة ب", "310123456700021", BusinessProfile.General);
        companyB.AssignCompanyGroup(group.Id);
        await _db.SaveChangesAsync();

        await PostIntercompanyAsync(companyA.Id, companyB.Id, debitCode: "1104", creditCode: "4101", 1000m);
        await PostIntercompanyAsync(companyB.Id, companyA.Id, debitCode: "5101", creditCode: "2101", 1000m);

        var rateService = new ExchangeRateService(_db);
        var rows = await new ConsolidatedTrialBalanceQuery(_db, rateService).ExecuteAsync(
            group.Id, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));

        Assert.All(rows.Where(r => r.AccountCode is "1104" or "4101" or "2101" or "5101"),
            r => Assert.Equal(0m, r.TotalDebit));
        Assert.All(rows.Where(r => r.AccountCode is "1104" or "4101" or "2101" or "5101"),
            r => Assert.Equal(0m, r.TotalCredit));
    }

    [Fact]
    public async Task ConsolidatedTrialBalance_respects_intercompany_account_map_filter()
    {
        var group = CompanyGroup.Create("GRP-MAP", "مجموعة خريطة", "SAR");
        _db.CompanyGroups.Add(group);
        await _db.SaveChangesAsync();

        _db.IntercompanyAccountMaps.Add(IntercompanyAccountMap.Create(group.Id, "1104"));
        await _db.SaveChangesAsync();

        var companyA = await SeedCompanyAsync("شركة أ", "310123456700030", group.Id);
        var companyB = await new DatabaseInitializer(_db).CreateAdditionalCompanyAsync(
            "شركة ب", "310123456700031", BusinessProfile.General);
        companyB.AssignCompanyGroup(group.Id);
        await _db.SaveChangesAsync();

        await PostIntercompanyAsync(companyA.Id, companyB.Id, debitCode: "1104", creditCode: "4101", 1000m);
        await PostIntercompanyAsync(companyB.Id, companyA.Id, debitCode: "5101", creditCode: "2101", 1000m);

        var rows = await new ConsolidatedTrialBalanceQuery(_db, new ExchangeRateService(_db)).ExecuteAsync(
            group.Id, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));

        var row1104 = rows.Single(r => r.AccountCode == "1104");
        Assert.Equal(0m, row1104.TotalDebit);
        Assert.Equal(0m, row1104.TotalCredit);

        var row4101 = rows.Single(r => r.AccountCode == "4101");
        Assert.NotEqual(0m, row4101.TotalCredit);
    }

    private async Task<Company> SeedCompanyAsync(string name, string vat, long groupId)
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync(name, vat, BusinessProfile.General);
        if (company.CompanyGroupId != groupId)
        {
            company.AssignCompanyGroup(groupId);
            await _db.SaveChangesAsync();
        }
        return company;
    }

    private async Task PostIntercompanyAsync(
        long companyId, long counterpartyId, string debitCode, string creditCode, decimal amount)
    {
        var debit = await _db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == debitCode);
        var credit = await _db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == creditCode);
        var entry = JournalEntry.CreateDraft(companyId, new DateOnly(2026, 7, 10), "معاملة بين الشركات", "IC-TEST");
        entry.MarkIntercompany(counterpartyId);
        entry.AddLine(debit, amount, 0m);
        entry.AddLine(credit, 0m, amount);
        entry.Post($"IC-{companyId}-{counterpartyId}");
        _db.JournalEntries.Add(entry);
        await _db.SaveChangesAsync();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
