using Brqosh.Application.Tests.TestSupport;
using System.Diagnostics;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Performance;

/// <summary>
/// حمولة قيود يومية — بوابة أداء داخلية (ليس 1M في CI؛ الهدف إثبات مسار قابل للقياس).
/// تشغيل محلي موسّع: BRQOSH_LOAD_ENTRIES=10000
/// </summary>
public class JournalLoadTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public JournalLoadTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        _output = output;

        // (CEEF-229 · م2-٤) فاعلٌ مُصرَّح — وغيابُه هو ما كانت البوّابةُ المعطَّلة تُخفيه.
        // نزعُ AutomatedTestBypass في م2-١ أسقط ٧٣ اختباراً فأُصلحت كلُّها إلا هذين،
        // لأنّهما لم يعملا: BRQOSH_SKIP_STRESS=1 كان يُعيدهما صامتَين فيُحسبان ناجحَين.
        TestActor.Administrator();
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

    [StressFact]
    public async Task Posts_journal_batch_under_budget()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة حمل", "310123456700003", BusinessProfile.Retail);

        var cash = await _db.Accounts.SingleAsync(a => a.CompanyId == company.Id && a.Code == "1101");
        var capital = await _db.Accounts.SingleAsync(a => a.CompanyId == company.Id && a.Code == "3101");

        var count = 200;
        if (int.TryParse(Environment.GetEnvironmentVariable("BRQOSH_LOAD_ENTRIES"), out var envCount) && envCount > 0)
            count = Math.Min(envCount, 100_000);

        var date = new DateOnly(2026, 8, 1);
        var sw = Stopwatch.StartNew();

        for (var i = 0; i < count; i++)
        {
            var draft = JournalEntry.CreateDraft(company.Id, date, $"حمل {i}", sourceDocument: $"Load:{i}");
            draft.AddLine(cash, 1m, 0m);
            draft.AddLine(capital, 0m, 1m);
            await new JournalPostingService(_db).PostAsync(draft);
        }

        sw.Stop();
        var posted = await _db.JournalEntries.CountAsync(e => e.CompanyId == company.Id);
        Assert.True(posted >= count);

        var avgMs = sw.Elapsed.TotalMilliseconds / count;
        // بوابة انحدار وليست معيار أجهزة؛ تحت suite متوازٍ قد يرتفع المتوسط.
        // الحمل المشدّد: JournalStressLoadTests (مع BRQOSH_SKIP_STRESS).
        _output.WriteLine($"[أداء · الحمل الأساسيّ] {count} قيداً · متوسط {avgMs:N2}ms · إجمالي {sw.Elapsed.TotalSeconds:N1}s");

        Assert.True(avgMs < 150,
            $"متوسط ترحيل القيد {avgMs:N1}ms لـ {count} قيود — المتوقع أقل من 150ms");
    }
}
