using System.Diagnostics;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Performance;

/// <summary>
/// حمل موسّع قابل للتكرار — افتراضي 2 000 قيد (مستقر في CI).
/// توسعة اختيارية: BRQOSH_LOAD_ENTRIES=5000
/// تخطّي: BRQOSH_SKIP_STRESS=1
/// </summary>
public class JournalStressLoadTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public JournalStressLoadTests()
    {
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
    public async Task Posts_large_journal_batch_under_extended_budget()
    {
        if (string.Equals(Environment.GetEnvironmentVariable("BRQOSH_SKIP_STRESS"), "1", StringComparison.Ordinal))
            return;

        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة ضغط", "310123456700003", BusinessProfile.Retail);

        var cash = await _db.Accounts.SingleAsync(a => a.CompanyId == company.Id && a.Code == "1101");
        var capital = await _db.Accounts.SingleAsync(a => a.CompanyId == company.Id && a.Code == "3101");

        var count = 2_000;
        if (int.TryParse(Environment.GetEnvironmentVariable("BRQOSH_LOAD_ENTRIES"), out var envCount) && envCount >= 500)
            count = Math.Min(envCount, 100_000);

        var date = new DateOnly(2026, 9, 1);
        var sw = Stopwatch.StartNew();

        for (var i = 0; i < count; i++)
        {
            var draft = JournalEntry.CreateDraft(company.Id, date, $"ضغط {i}", sourceDocument: $"Stress:{i}");
            draft.AddLine(cash, 1m, 0m);
            draft.AddLine(capital, 0m, 1m);
            await new JournalPostingService(_db).PostAsync(draft);
        }

        sw.Stop();
        var posted = await _db.JournalEntries.CountAsync(e => e.CompanyId == company.Id);
        Assert.True(posted >= count);

        var avgMs = sw.Elapsed.TotalMilliseconds / count;
        // ميزانية متسامحة مع أجهزة أبطأ / تحميل متوازي
        Assert.True(avgMs < 100,
            $"متوسط ترحيل {avgMs:N2}ms لـ {count} قيود — الإجمالي {sw.Elapsed.TotalSeconds:N1}s");
    }
}
