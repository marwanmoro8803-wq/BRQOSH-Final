using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Accounting;

public class JournalReversalTests
{
    [Fact]
    public async Task Manual_journal_entry_can_be_reversed()
    {
        var path = Path.Combine(Path.GetTempPath(), $"brqosh-rev-{Guid.NewGuid():N}.db");
        try
        {
            await using var db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, $"Data Source={path}");
            var company = await new DatabaseInitializer(db).InitializeAsync("شركة", "300000000000004", BusinessProfile.General);

            var cash = await db.Accounts.SingleAsync(a => a.CompanyId == company.Id && a.Code == "1101");
            var capital = await db.Accounts.SingleAsync(a => a.CompanyId == company.Id && a.Code == "3101");

            var entry = JournalEntry.CreateDraft(company.Id, DateOnly.FromDateTime(DateTime.Today), "قيد افتتاحي");
            entry.AddLine(cash, 1000m, 0m);
            entry.AddLine(capital, 0m, 1000m);
            entry.Post("JE-001");
            db.JournalEntries.Add(entry);
            await db.SaveChangesAsync();

            var reversal = await new JournalReversalService(db).ReverseAsync(
                company.Id, entry.Id, DateOnly.FromDateTime(DateTime.Today), "admin");

            Assert.Equal(JournalEntryStatus.Reversed, entry.Status);
            Assert.Equal(JournalEntryStatus.Posted, reversal.Status);
            Assert.Equal(1000m, reversal.TotalDebit);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Source_document_journal_cannot_be_reversed_manually()
    {
        var path = Path.Combine(Path.GetTempPath(), $"brqosh-rev-src-{Guid.NewGuid():N}.db");
        try
        {
            await using var db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, $"Data Source={path}");
            var company = await new DatabaseInitializer(db).InitializeAsync("شركة", "300000000000021", BusinessProfile.General);

            var cash = await db.Accounts.SingleAsync(a => a.CompanyId == company.Id && a.Code == "1101");
            var capital = await db.Accounts.SingleAsync(a => a.CompanyId == company.Id && a.Code == "3101");

            var entry = JournalEntry.CreateDraft(company.Id, DateOnly.FromDateTime(DateTime.Today),
                "رصيد افتتاحي مخزون", "StockOpening:WH1");
            entry.AddLine(cash, 500m, 0m);
            entry.AddLine(capital, 0m, 500m);
            entry.Post("JE-STK-001");
            db.JournalEntries.Add(entry);
            await db.SaveChangesAsync();

            await Assert.ThrowsAsync<AccountingException>(() =>
                new JournalReversalService(db).ReverseAsync(
                    company.Id, entry.Id, DateOnly.FromDateTime(DateTime.Today), "admin"));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
