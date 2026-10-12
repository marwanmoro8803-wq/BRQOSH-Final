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
/// حمل موسّع قابل للتكرار — افتراضي 2 000 قيد (مستقر في CI).
/// توسعة اختيارية: BRQOSH_LOAD_ENTRIES=5000 · ‏وحتى المليون (O-04): <c>run-stress-benchmark.ps1 -FullMillion</c>
/// المزوّد: BRQOSH_LOAD_PROVIDER=postgres ⟵ خادمُ الاختبار (<c>brqosh_t_</c>) · وإلّا SQLite بالذاكرة
/// تخطّي: BRQOSH_SKIP_STRESS=1
///
/// <para>‏(جاهزيّةُ الإطلاق · ح-٥٩ · data-upgrade-performance#3) ‏<b>وكان «وضعُ المليون» لا يُشغّل مليوناً</b>: السكربتُ
/// لا يضبط <c>BRQOSH_LOAD_ENTRIES</c> في فرعَيه، <b>والاختبارُ يقصّ أيَّ رقمٍ عند مئة ألف</b> — فالحاصلُ ألفا قيدٍ على
/// SQLite بالذاكرة، والوثيقةُ تقول «1,000,000». <b>والسطرُ المطبوعُ يُسمّي المزوّدَ والعدد</b> — فلا يُقرأ سجلٌّ
/// على SQLite قياساً لـPostgreSQL.</para>
/// </summary>
public class JournalStressLoadTests(Xunit.Abstractions.ITestOutputHelper output)
{
    public const string EntriesVariable = "BRQOSH_LOAD_ENTRIES";
    public const string ProviderVariable = "BRQOSH_LOAD_PROVIDER";

    /// <summary>
    /// (ح-٥٩) ‏<b>العددُ المطلوبُ هو المُشغَّل</b> — ألفان افتراضاً، وما دون خمسمئةٍ يُهمَل (ضجيجٌ لا قياس)،
    /// <b>وحتى المليون</b> هدفِ O-04. وكان يُقصّ عند مئة ألف بلا كلمة.
    /// </summary>
    internal static int EntriesFrom(string? value) =>
        int.TryParse(value, out var requested) && requested >= 500
            ? Math.Min(requested, 1_000_000)
            : 2_000;

    /// <summary>(ح-٥٩) ‏PostgreSQL حين يُطلب نصّاً — وإلّا SQLite بالذاكرة.</summary>
    internal static bool UsesPostgres(string? value) =>
        string.Equals(value, "postgres", StringComparison.OrdinalIgnoreCase);

    [StressFact]
    public async Task Posts_large_journal_batch_under_extended_budget()
    {
        // (CEEF-229 · م2-٤) فاعلٌ مُصرَّح — وغيابُه هو ما كانت البوّابةُ المعطَّلة تُخفيه.
        TestActor.Administrator();
        var count = EntriesFrom(Environment.GetEnvironmentVariable(EntriesVariable));

        if (UsesPostgres(Environment.GetEnvironmentVariable(ProviderVariable)))
        {
            // ‏(ح-٥٩) ‏خادمُ الاختبار وحدَه (`brqosh_t_…`) — **ويرمي إن غاب** فلا يمرّ «وضعُ PostgreSQL» على SQLite.
            await using var scope = await PostgresTestDatabase.CreateAsync("journal-stress");
            await PostBatchAsync(scope.Db, scope.OpenIndependentContext, count, "PostgreSQL");
            return;
        }

        await using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<BrqoshDbContext>().UseSqlite(connection).Options;
        await using var db = new BrqoshDbContext(options);
        db.Database.EnsureCreated();
        await PostBatchAsync(db, () => new BrqoshDbContext(options), count, "SQLite بالذاكرة");
    }

    /// <summary>
    /// ‏(ح-١٣١ · data-upgrade-performance#4) **سياقٌ جديدٌ لكلّ قيد — كما يفعل البرنامج** (<c>using var db = AppServices.CreateDb()</c>
    /// في كلّ عمليّة). كان القياسُ يرحّل الألفين في سياقٍ واحد، فيكبر متتبّعُ التغييرات وتمسح <c>JournalEntries.Local</c> ما
    /// تراكم مع كلّ قيد — <b>فكان يقيس نموَّ السياق لا الدفتر</b>، وهو ما قُرئ «ثمنُ القيد ينمو مع الدفتر».
    /// </summary>
    private async Task PostBatchAsync(BrqoshDbContext db, Func<BrqoshDbContext> fresh, int count, string provider)
    {
        var company = await new DatabaseInitializer(db)
            .InitializeAsync("شركة ضغط", "310123456700003", BusinessProfile.Retail);

        var cash = await db.Accounts.SingleAsync(a => a.CompanyId == company.Id && a.Code == "1101");
        var capital = await db.Accounts.SingleAsync(a => a.CompanyId == company.Id && a.Code == "3101");

        var date = new DateOnly(2026, 9, 1);
        var sw = Stopwatch.StartNew();

        for (var i = 0; i < count; i++)
        {
            await using var post = fresh();
            var draft = JournalEntry.CreateDraft(company.Id, date, $"ضغط {i}", sourceDocument: $"Stress:{i}");
            draft.AddLine(await post.Accounts.SingleAsync(a => a.Id == cash.Id), 1m, 0m);
            draft.AddLine(await post.Accounts.SingleAsync(a => a.Id == capital.Id), 0m, 1m);
            await new JournalPostingService(post).PostAsync(draft);
        }

        sw.Stop();
        var posted = await db.JournalEntries.CountAsync(e => e.CompanyId == company.Id);
        Assert.True(posted >= count);

        var avgMs = sw.Elapsed.TotalMilliseconds / count;
        // ميزانية متسامحة مع أجهزة أبطأ / تحميل متوازي
        output.WriteLine($"[أداء · الحمل المشدّد · {provider}] {count} قيداً · متوسط {avgMs:N2}ms · إجمالي {sw.Elapsed.TotalSeconds:N1}s");

        Assert.True(avgMs < 100,
            $"متوسط ترحيل {avgMs:N2}ms لـ {count} قيود على {provider} — الإجمالي {sw.Elapsed.TotalSeconds:N1}s");
    }
}
