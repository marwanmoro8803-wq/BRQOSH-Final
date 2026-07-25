using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Treasury;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Treasury;

public class ExchangeRateServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private readonly ExchangeRateService _service;

    public ExchangeRateServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _service = new ExchangeRateService(_db);
    }

    [Fact]
    public async Task GetRate_returns_one_for_same_currency()
    {
        var rate = await _service.GetRateAsync(1, "SAR", "SAR", new DateOnly(2026, 7, 1));
        Assert.Equal(1m, rate);
    }

    [Fact]
    public async Task GetRate_uses_latest_rate_on_or_before_asOf()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "300000000000001", BusinessProfile.General);

        await _service.RecordRateAsync(company.Id, "USD", "SAR", new DateOnly(2026, 6, 1), 3.70m);
        await _service.RecordRateAsync(company.Id, "USD", "SAR", new DateOnly(2026, 7, 1), 3.75m);
        await _service.RecordRateAsync(company.Id, "USD", "SAR", new DateOnly(2026, 8, 1), 3.80m);

        var rate = await _service.GetRateAsync(company.Id, "USD", "SAR", new DateOnly(2026, 7, 15));
        Assert.Equal(3.75m, rate);
    }

    [Fact]
    public async Task GetRate_inverts_opposite_pair()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "300000000000002", BusinessProfile.General);

        await _service.RecordRateAsync(company.Id, "USD", "SAR", new DateOnly(2026, 7, 1), 4m);
        var rate = await _service.GetRateAsync(company.Id, "SAR", "USD", new DateOnly(2026, 7, 8));
        Assert.Equal(0.25m, rate);
    }

    [Fact]
    public async Task GetAverageRate_uses_mean_of_period_rates()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "300000000000007", BusinessProfile.General);

        await _service.RecordRateAsync(company.Id, "USD", "SAR", new DateOnly(2026, 7, 1), 3.70m);
        await _service.RecordRateAsync(company.Id, "USD", "SAR", new DateOnly(2026, 7, 31), 3.80m);

        var avg = await _service.GetAverageRateAsync(
            company.Id, "USD", "SAR", new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));
        Assert.Equal(3.75m, avg);
    }

    [Fact]
    public async Task RecordRate_rejects_non_positive_rate()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "300000000000003", BusinessProfile.General);

        await Assert.ThrowsAsync<AccountingException>(() =>
            _service.RecordRateAsync(company.Id, "USD", "SAR", new DateOnly(2026, 7, 1), 0m));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
