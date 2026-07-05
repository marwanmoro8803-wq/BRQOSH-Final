using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Persistence;

public class DatabaseInitializerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public DatabaseInitializerTests()
    {
        // SQLite في الذاكرة: نفس مخطط EF بلا خادم — بيئة اختبار سريعة ومعزولة
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new BrqoshDbContext(options);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Initialize_CreatesCompany_AndSeedsFullChartOfAccounts()
    {
        var initializer = new DatabaseInitializer(_db);
        var company = await initializer.InitializeAsync("شركة الاختبار", "310123456700003", BusinessProfile.Construction);

        Assert.True(company.Id > 0);
        var accountCount = await _db.Accounts.CountAsync(a => a.CompanyId == company.Id);
        Assert.Equal(SaudiChartOfAccounts.Template.Count, accountCount);

        // الشجرة سليمة: حساب النقدية ابن لمجموعة الأصول المتداولة
        var cash = await _db.Accounts.SingleAsync(a => a.Code == "1101");
        var currentAssets = await _db.Accounts.SingleAsync(a => a.Code == "11");
        Assert.Equal(currentAssets.Id, cash.ParentId);
    }

    [Fact]
    public async Task Initialize_IsIdempotent()
    {
        var initializer = new DatabaseInitializer(_db);
        var first = await initializer.InitializeAsync("شركة", "", BusinessProfile.Retail);
        var second = await initializer.InitializeAsync("شركة أخرى", "", BusinessProfile.General);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await _db.Companies.CountAsync());
    }

    [Fact]
    public async Task JournalEntry_RoundTrips_WithLines()
    {
        var initializer = new DatabaseInitializer(_db);
        var company = await initializer.InitializeAsync("شركة", "", BusinessProfile.General);

        var cash = await _db.Accounts.SingleAsync(a => a.Code == "1101");
        var sales = await _db.Accounts.SingleAsync(a => a.Code == "4101");

        var entry = JournalEntry.CreateDraft(company.Id, new DateOnly(2026, 7, 5), "قيد اختبار");
        entry.AddLine(cash, 115m, 0m);
        entry.AddLine(sales, 0m, 115m);
        entry.Post("JV-2026-000001");

        _db.JournalEntries.Add(entry);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var loaded = await _db.JournalEntries.SingleAsync(e => e.EntryNumber == "JV-2026-000001");
        Assert.Equal(2, loaded.Lines.Count);
        Assert.True(loaded.IsBalanced);
        Assert.Equal(JournalEntryStatus.Posted, loaded.Status);
    }

    [Fact]
    public async Task NumberGenerator_IncrementsPerYear()
    {
        var initializer = new DatabaseInitializer(_db);
        var company = await initializer.InitializeAsync("شركة", "", BusinessProfile.General);
        var generator = new JournalEntryNumberGenerator(_db);

        var first = await generator.NextAsync(company.Id, new DateOnly(2026, 7, 5));
        Assert.Equal("JV-2026-000001", first);

        var cash = await _db.Accounts.SingleAsync(a => a.Code == "1101");
        var sales = await _db.Accounts.SingleAsync(a => a.Code == "4101");
        var entry = JournalEntry.CreateDraft(company.Id, new DateOnly(2026, 7, 5), "قيد");
        entry.AddLine(cash, 10m, 0m);
        entry.AddLine(sales, 0m, 10m);
        entry.Post(first);
        _db.JournalEntries.Add(entry);
        await _db.SaveChangesAsync();

        var second = await generator.NextAsync(company.Id, new DateOnly(2026, 12, 31));
        Assert.Equal("JV-2026-000002", second);

        var nextYear = await generator.NextAsync(company.Id, new DateOnly(2027, 1, 1));
        Assert.Equal("JV-2027-000001", nextYear);
    }
}
