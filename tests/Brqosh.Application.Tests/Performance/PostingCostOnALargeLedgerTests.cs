using System.Diagnostics;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Performance;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١٣١ · <c>data-upgrade-performance#4</c>) <b>كم يكلّف القيدُ على دفترٍ بحجمٍ واقعيّ — على PostgreSQL.</b>
///
/// <para>‏البند: لا دليلَ أداءٍ صالحٌ على PostgreSQL بحجم دفترٍ واقعيّ، وأرقامُ SQLite توحي بأنّ ثمنَ القيد ينمو مع الدفتر.
/// <b>واختبارُ الحمل يرحّل ألفين في سياقٍ واحد</b> فيقيس نموَّ متتبّع التغييرات معه، لا الدفتر.</para>
///
/// <para>‏<b>فالقياس هنا:</b> دفترٌ يُبنى بسرعةٍ بقيودٍ مرحَّلةٍ تُحفظ دفعاتٍ (بلا خدمة الترحيل — فالمقصودُ الحجمُ لا مسارُه)، ثمّ
/// <b>خدمةُ الترحيل نفسُها بسياقٍ جديدٍ لكلّ قيد</b> كما يفعل البرنامج — ويُقارَن ثمنُ القيد بثمنه على دفترٍ فارغ في الجولة
/// نفسِها، <b>بعد إحماءٍ</b> (درسُ <c>JournalEntryNumberGenerator</c>: مقارنةٌ بلا إحماءٍ قرأت ضجيجاً نموّاً).</para>
/// </summary>
public class PostingCostOnALargeLedgerTests(Xunit.Abstractions.ITestOutputHelper output)
{
    public const string LedgerSizeVariable = "BRQOSH_LEDGER_SIZE";
    private const int Block = 60;
    private const int Rounds = 5;
    private const int WarmUp = 30;

    [PostgresFact]
    public async Task PostingCost_DoesNotGrowWithTheLedger_OnPostgres()
    {
        TestActor.Administrator();
        var size = int.TryParse(Environment.GetEnvironmentVariable(LedgerSizeVariable), out var s) && s > 0 ? s : 50_000;

        await using var empty = await PostgresTestDatabase.CreateAsync("ledger_empty");
        var emptyCompany = await PrepareAsync(empty, seeded: 0);
        await using var large = await PostgresTestDatabase.CreateAsync("ledger_large");
        var largeCompany = await PrepareAsync(large, seeded: size);

        // ‏**كتلٌ متناوبة ووسيطُها** — فلا يدفع أحدُ الدفترين وحدَه ثمنَ أنّه قِيس أوّلاً، ولا تُقرأ كتلةٌ شاذّةٌ نموّاً.
        var emptyBlocks = new List<double>();
        var largeBlocks = new List<double>();
        for (var round = 0; round < Rounds; round++)
        {
            emptyBlocks.Add(await TimeAsync(empty, emptyCompany, $"ف{round}"));
            largeBlocks.Add(await TimeAsync(large, largeCompany, $"ك{round}"));
        }
        var onEmpty = Median(emptyBlocks);
        var onLarge = Median(largeBlocks);

        var ratio = onLarge / onEmpty;
        output.WriteLine($"[أداء · PostgreSQL] القيدُ على دفترٍ فارغ {onEmpty:N2}ms · على {size:N0} قيدٍ {onLarge:N2}ms · النسبة {ratio:N2}×");

        Assert.True(ratio < 2.0,
            $"ثمنُ القيد على {size:N0} قيدٍ {onLarge:N2}ms مقابل {onEmpty:N2}ms على دفترٍ فارغ ({ratio:N2}×) — ينمو مع الدفتر.");
    }

    private static async Task<long> PrepareAsync(PostgresTestDatabase.PostgresScope scope, int seeded)
    {
        var company = await new DatabaseInitializer(scope.Db)
            .InitializeAsync("شركة دفترٍ كبير", "310123456700003", BusinessProfile.Retail);
        var date = new DateOnly(2026, 9, 1);

        for (var done = 0; done < seeded; done += 5_000)
        {
            await using var db = scope.OpenIndependentContext();
            var (cash, capital) = await AccountsAsync(db, company.Id);
            for (var i = done; i < Math.Min(done + 5_000, seeded); i++)
            {
                var entry = JournalEntry.CreateDraft(company.Id, date, $"بذرة {i}", sourceDocument: $"Seed:{i}");
                entry.AddLine(cash, 1m, 0m);
                entry.AddLine(capital, 0m, 1m);
                entry.Post($"JV-2026-{i + 1:D6}");
                db.JournalEntries.Add(entry);
            }
            await db.SaveChangesAsync();
        }

        // ‏قاعدةٌ حيّةٌ إحصاءاتُها حاضرة؛ والإدراجُ الكبيرُ يتركها بائتةً حتى يمرّ autovacuum — فيُقاس المُخطِّطُ لا الدفتر.
        await scope.Db.Database.ExecuteSqlRawAsync("ANALYZE");
        await PostAsync(scope, company.Id, date, WarmUp, "إحماء");
        return company.Id;
    }

    private static async Task<double> TimeAsync(PostgresTestDatabase.PostgresScope scope, long companyId, string label)
    {
        var sw = Stopwatch.StartNew();
        await PostAsync(scope, companyId, new DateOnly(2026, 9, 1), Block, label);
        return sw.Elapsed.TotalMilliseconds / Block;
    }

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return sorted[sorted.Count / 2];
    }

    private static async Task PostAsync(PostgresTestDatabase.PostgresScope scope, long companyId, DateOnly date, int count, string label)
    {
        for (var i = 0; i < count; i++)
        {
            await using var db = scope.OpenIndependentContext();
            var (cash, capital) = await AccountsAsync(db, companyId);
            var draft = JournalEntry.CreateDraft(companyId, date, $"{label} {i}", sourceDocument: $"{label}:{i}");
            draft.AddLine(cash, 1m, 0m);
            draft.AddLine(capital, 0m, 1m);
            await new JournalPostingService(db).PostAsync(draft);
        }
    }

    private static async Task<(Account Cash, Account Capital)> AccountsAsync(BrqoshDbContext db, long companyId) =>
        (await db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == "1101"),
         await db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == "3101"));
}
