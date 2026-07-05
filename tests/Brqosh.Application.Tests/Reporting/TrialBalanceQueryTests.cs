using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Reporting;

public class TrialBalanceQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public TrialBalanceQueryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task TrialBalance_BalancesAndExcludesDrafts()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "", BusinessProfile.General);
        var cash = await _db.Accounts.SingleAsync(a => a.Code == "1101");
        var sales = await _db.Accounts.SingleAsync(a => a.Code == "4101");
        var vat = await _db.Accounts.SingleAsync(a => a.Code == "2106");

        // قيد مرحّل: بيع 115 (100 + 15 ضريبة)
        var posted = JournalEntry.CreateDraft(company.Id, new DateOnly(2026, 7, 1), "بيع نقدي");
        posted.AddLine(cash, 115m, 0m);
        posted.AddLine(sales, 0m, 100m);
        posted.AddLine(vat, 0m, 15m);
        posted.Post("JV-2026-000001");
        _db.JournalEntries.Add(posted);

        // مسودة — يجب ألا تدخل الميزان
        var draft = JournalEntry.CreateDraft(company.Id, new DateOnly(2026, 7, 2), "مسودة");
        draft.AddLine(cash, 999m, 0m);
        draft.AddLine(sales, 0m, 999m);
        _db.JournalEntries.Add(draft);

        await _db.SaveChangesAsync();

        var rows = await new TrialBalanceQuery(_db).ExecuteAsync(
            company.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        Assert.Equal(3, rows.Count);
        Assert.Equal(rows.Sum(r => r.TotalDebit), rows.Sum(r => r.TotalCredit));

        var cashRow = rows.Single(r => r.AccountCode == "1101");
        Assert.Equal(115m, cashRow.TotalDebit);   // المسودة (999) مستبعدة
        Assert.Equal(115m, cashRow.BalanceDebit);

        var salesRow = rows.Single(r => r.AccountCode == "4101");
        Assert.Equal(100m, salesRow.BalanceCredit);
    }

    [Fact]
    public async Task TrialBalance_RespectsDateRange()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "", BusinessProfile.General);
        var cash = await _db.Accounts.SingleAsync(a => a.Code == "1101");
        var sales = await _db.Accounts.SingleAsync(a => a.Code == "4101");

        var entry = JournalEntry.CreateDraft(company.Id, new DateOnly(2026, 3, 15), "قيد");
        entry.AddLine(cash, 50m, 0m);
        entry.AddLine(sales, 0m, 50m);
        entry.Post("JV-2026-000001");
        _db.JournalEntries.Add(entry);
        await _db.SaveChangesAsync();

        var outside = await new TrialBalanceQuery(_db).ExecuteAsync(
            company.Id, new DateOnly(2026, 4, 1), new DateOnly(2026, 12, 31));
        Assert.Empty(outside);
    }
}
