using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Treasury;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Reporting;

public class ConsolidatedTrialBalanceQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public ConsolidatedTrialBalanceQueryTests()
    {
        // ‏(م-٤ · ن-١٦) ‏**فاعلٌ مُعلَن** — صار كتابةُ أسعار الصرف واللقطات تلزمها صلاحيّةٌ
        // ‏محاسبيّة. **ومحاسبٌ لا مدير**: أقلُّ صلاحيّةٍ تكفي، فمرورُ الصنف يُثبت أنّ
        // ‏المحاسبَ يستطيعها — ولو أُعلن «مدير» في كلّ مكانٍ لما أخبر شيءٌ بشيء.
        TestActor.Accountant();
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ConsolidatedTrialBalance_ConvertsFunctional_ToReportingCurrency()
    {
        var group = CompanyGroup.Create("GRP1", "مجموعة اختبار", "SAR");
        _db.CompanyGroups.Add(group);
        await _db.SaveChangesAsync();

        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700003", BusinessProfile.Retail);
        company.AssignCompanyGroup(group.Id);
        company.SetCurrencies("USD", "USD");
        await _db.SaveChangesAsync();

        await PostCashSaleAsync(company.Id, 100m);

        var rows = await new ConsolidatedTrialBalanceQuery(_db, new ExchangeRateService(_db)).ExecuteAsync(
            group.Id,
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 31),
            new Dictionary<long, decimal> { [company.Id] = 3.75m });

        var cash = rows.Single(r => r.AccountCode == "1101");
        Assert.Equal(115m * 3.75m, cash.TotalDebit);
        Assert.Equal(1, cash.CompanyCount);
    }

    [Fact]
    public async Task ConsolidatedTrialBalance_ResolvesRate_FromStoredExchangeRates()
    {
        var group = CompanyGroup.Create("GRP2", "مجموعة FX", "SAR");
        _db.CompanyGroups.Add(group);
        await _db.SaveChangesAsync();

        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة دولار", "310123456700004", BusinessProfile.Retail);
        company.AssignCompanyGroup(group.Id);
        company.SetCurrencies("USD", "USD");
        await _db.SaveChangesAsync();

        await new ExchangeRateService(_db).RecordRateAsync(
            company.Id, "USD", "SAR", new DateOnly(2026, 7, 1), 3.75m);

        await PostCashSaleAsync(company.Id, 100m);

        var rows = await new ConsolidatedTrialBalanceQuery(_db, new ExchangeRateService(_db)).ExecuteAsync(
            group.Id,
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 31),
            fxRatesFromFunctionalToReporting: null);

        var cash = rows.Single(r => r.AccountCode == "1101");
        Assert.Equal(115m * 3.75m, cash.TotalDebit);
    }

    private async Task PostCashSaleAsync(long companyId, decimal subtotal)
    {
        var cash = await _db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == "1101");
        var sales = await _db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == "4101");
        var vat = await _db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == "2106");
        var vatAmt = Math.Round(subtotal * 0.15m, 2, MidpointRounding.AwayFromZero);
        var total = subtotal + vatAmt;

        var entry = JournalEntry.CreateDraft(companyId, new DateOnly(2026, 7, 5), "بيع");
        entry.AddLine(cash, total, 0m);
        entry.AddLine(sales, 0m, subtotal);
        if (vatAmt > 0)
            entry.AddLine(vat, 0m, vatAmt);
        entry.Post("JE-TEST");
        _db.JournalEntries.Add(entry);
        await _db.SaveChangesAsync();
    }
}
