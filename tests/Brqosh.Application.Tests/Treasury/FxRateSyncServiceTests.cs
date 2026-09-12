using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Treasury;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Treasury;

public class FxRateSyncServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public FxRateSyncServiceTests()
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

    [Fact]
    public async Task SyncCompanyRates_records_quotes_from_provider()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "300000000000010", BusinessProfile.General);

        var provider = new StubFxRateProvider(new Dictionary<(string, string), decimal>
        {
            [("USD", "SAR")] = 3.75m,
            [("EUR", "SAR")] = 4.10m,
        });
        var sync = new FxRateSyncService(_db, provider, new ExchangeRateService(_db));
        var asOf = new DateOnly(2026, 7, 1);

        var result = await sync.SyncCompanyRatesAsync(
            company.Id, asOf, ["USD", "EUR", "SAR"]);

        Assert.Equal("stub", result.ProviderId);
        Assert.Equal(2, result.RecordedCount);

        var usd = await new ExchangeRateService(_db).GetRateAsync(company.Id, "USD", "SAR", asOf);
        Assert.Equal(3.75m, usd);
    }

    [Fact]
    public async Task SyncCompanyRates_includes_bank_account_currencies_when_targets_null()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "300000000000011", BusinessProfile.General);

        var cashAccount = await _db.Accounts.SingleAsync(a =>
            a.CompanyId == company.Id && a.Code == "1101");
        var bank = Domain.Treasury.BankAccount.Create(
            company.Id, "BNK-USD", "حساب دولار", "بنك", "123", "SA000000000", cashAccount.Id);
        bank.SetCurrency("USD");
        _db.BankAccounts.Add(bank);
        await _db.SaveChangesAsync();

        var provider = new StubFxRateProvider(new Dictionary<(string, string), decimal>
        {
            [("USD", "SAR")] = 3.76m,
        });
        var sync = new FxRateSyncService(_db, provider, new ExchangeRateService(_db));

        var result = await sync.SyncCompanyRatesAsync(company.Id, new DateOnly(2026, 7, 8));

        Assert.True(result.RecordedCount >= 1);
        Assert.Contains(result.Quotes, q => q.FromCurrency == "USD");
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
