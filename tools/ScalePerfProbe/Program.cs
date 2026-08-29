// ‏مسبار الكلفة على دفترٍ حقيقيّ الحجم (CEEF-229)
//
// ‏السؤال الذي يجيب عنه:
//   متجرٌ محلّيّ يمرّ عليه عامٌ من العمل يبلغ دفترُه عشرات الآلاف من القيود.
//   **هل يبقى الترحيلُ والتقاريرُ محتملةً عند ذلك الحجم، أم يزحف النظامُ حتى يقف؟**
//
// ‏لماذا لا يجيب عنه أيُّ اختبار قائم:
//   كلُّ اختبارٍ في المستودع يعمل على SQLite في الذاكرة بعشرات القيود. وذلك يقيس الصحّة
//   ولا يقيس الكلفة: خطّةُ التنفيذ في SQLite غير خطّة PostgreSQL، ومسحُ جدولٍ من ٣٠ صفّاً
//   لا يُفرَّق عن استعمال فهرس. فالكلفةُ عند الحجم **مقيسةٌ صفرَ مرّة** حتى هذا المسبار.
//
// ‏وما لا يفعله:
//   لا يمسّ قاعدةَ المستخدم إطلاقاً — ينشئ قاعدةً مستقلّة ويحذفها لا يُبقيها.
//   ولا يحكم بأرقامٍ مطلقة عن العتاد، بل يقيس **شكلَ النموّ**: الكلفة عند دفترٍ فارغ
//   مقابلها عند دفترٍ ممتلئ. فالجهازُ البطيء يرفع الرقمين معاً، والفهرسُ المفقود يرفع
//   أحدَهما وحدَه — وهذا وحده ما يُشخّص.
//
// ‏التشغيل:  dotnet run --project tools/ScalePerfProbe  [عدد القيود]

using System.Diagnostics;
using Brqosh.Application.Setup;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

const string TestDb = "brqosh_perfprobe";
var target = args.Length > 0 && int.TryParse(args[0], out var parsed) ? parsed : 50_000;

var pwPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BRQOSH", "pg_password.txt");
if (!File.Exists(pwPath))
{
    Console.WriteLine($"[SKIP] لا يوجد {pwPath} — يتطلب المسبار PostgreSQL محلياً مهيّأً.");
    return 0;
}

var pw = (await File.ReadAllTextAsync(pwPath)).Trim();
var admin = $"Host=localhost;Port=5433;Database=postgres;Username=postgres;Password={pw}";
var cs = $"Host=localhost;Port=5433;Database={TestDb};Username=postgres;Password={pw}";

try
{
    await using var c = new NpgsqlConnection(admin);
    await c.OpenAsync();
    await using (var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {TestDb} WITH (FORCE)", c))
        await drop.ExecuteNonQueryAsync();
    await using (var create = new NpgsqlCommand($"CREATE DATABASE {TestDb}", c))
        await create.ExecuteNonQueryAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"[SKIP] تعذر الاتصال بـ PostgreSQL: {ex.Message}");
    return 0;
}

Console.WriteLine($"[OK] أُنشئت قاعدة قياس نظيفة: {TestDb}");

// ‏فاعلٌ مُعلَن — فحارسُ الصلاحيات يرفض كلَّ ترحيلٍ بلا مستخدم (م2-١)، وهو الصواب.
// ‏والمسبارُ يقيس المسارَ الحقيقيّ بحارسه لا مساراً مُطفأً.
Brqosh.Infrastructure.Security.SecurityContext.CurrentRole = Brqosh.Domain.Security.UserRole.Administrator;
Brqosh.Infrastructure.Security.SecurityContext.CurrentUsername = "مسبار القياس";

long companyId;
await using (var setup = BrqoshDbContextFactory.Create(DatabaseProvider.PostgreSql, cs))
{
    AppEnvironmentConfig.ForceForTests(AppEnvironment.Production);
    try { await new DatabaseSchemaService(setup).EnsureSchemaAsync(); }
    finally { AppEnvironmentConfig.ResetForTests(); }

    var company = await new DatabaseInitializer(setup)
        .InitializeAsync("متجر القياس", "310123456700003", BusinessProfile.Retail);
    companyId = company.Id;
    Console.WriteLine($"[OK] منشأةٌ مهيّأة — Id={companyId}");
}

// ── القياس الأوّل: دفترٌ فارغ ──────────────────────────────────────────────
var empty = await MeasureAsync(cs, companyId, "دفترٌ فارغ");

// ── تعبئةُ الدفتر: نسخُ قيدٍ حقيقيّ بأسطره على الخادم ─────────────────────
var seeded = await SeedAsync(cs, companyId, target);
Console.WriteLine($"[OK] الدفتر مُعبّأ — {seeded:N0} قيداً");

await using (var conn = new NpgsqlConnection(cs))
{
    await conn.OpenAsync();
    await using var an = new NpgsqlCommand("ANALYZE journal_entries; ANALYZE journal_entry_lines;", conn);
    await an.ExecuteNonQueryAsync();
}

// ── القياس الثاني: الدفتر نفسه ممتلئاً ────────────────────────────────────
var full = await MeasureAsync(cs, companyId, $"دفترٌ فيه {seeded:N0} قيداً");

Console.WriteLine();
Console.WriteLine("─────────────────────────────────────────────────────────────");
Console.WriteLine("  العملية                     فارغ      ممتلئ     النموّ");
Console.WriteLine("─────────────────────────────────────────────────────────────");

var failures = new List<string>();
foreach (var key in empty.Keys)
{
    var a = empty[key];
    var b = full[key];
    var growth = a <= 0 ? 1.0 : b / (double)a;
    var flag = MustNotGrow(key) && growth >= 5.0 && b > 250 ? " ✗" : "";
    Console.WriteLine($"  {key,-24} {a,6} ms {b,7} ms   ×{growth,5:0.0}{flag}");

    // ‏العتبة على **شكل النموّ** لا على الرقم المطلق: عمليةٌ يجب أن تكون لوغاريتمية
    // ‏(بحث بفهرس) تزحف خطّياً إن غاب الفهرس، فتظهر هنا مضاعفةً لا تُخطئها العين.
    // ‏والشرطُ الثاني (‏> 250 ms‎) يمنع إنذاراً كاذباً من عمليةٍ رخيصةٍ أصلاً: الانتقالُ
    // ‏من ٢ ms إلى ١٢ ms مضاعفةٌ ستّ مرّات ولا يشعر بها أحد.
    // ‏والحكمُ يُفرّق: **ما يجب ألّا ينمو** (ترحيلُ مستندٍ واحد — بحثٌ بفهرسٍ ثمّ إدراج)
    // ‏و**ما ينمو بطبيعته** (ميزانُ فترةٍ يجمع كلَّ سطرٍ فيها — نموُّه هو عملُه).
    // ‏وبلا هذا التفريق يصرخ المسبارُ على ميزان السنة صرخةً كاذبة، فيُهمَل كلُّه.
    if (MustNotGrow(key) && growth >= 5.0 && b > 250)
        failures.Add($"{key}: ×{growth:0.0} ({a} ms ⟵ {b} ms) — نموٌّ يتناسب مع حجم الدفتر، لا مع الفهرس.");
}
Console.WriteLine("─────────────────────────────────────────────────────────────");
Console.WriteLine();

// ── وخطّةُ التنفيذ تُقرأ لا تُفترض ────────────────────────────────────────
await using (var conn = new NpgsqlConnection(cs))
{
    await conn.OpenAsync();
    var numberQuery =
        "SELECT \"EntryNumber\" FROM journal_entries " +
        $"WHERE \"CompanyId\" = {companyId} ORDER BY \"EntryNumber\" DESC LIMIT 1";
    var plan = await ExplainAsync(conn, numberQuery);
    var usesIndex = plan.Contains("Index", StringComparison.OrdinalIgnoreCase);
    Console.WriteLine($"[{(usesIndex ? "OK" : "FAIL")}] خطّةُ توليد الرقم التالي: {plan.Split('\n')[0].Trim()}");
    if (!usesIndex)
        failures.Add("توليدُ الرقم التالي يمسح الجدول — لا يستعمل الفهرس الفريد (CompanyId, EntryNumber).");
}

// ── ولا يكفي أن يُقال «بطيء»: أيُّ أمرٍ بالضبط؟ ───────────────────────────
//
// ‏الرقمُ وحده يقول إنّ الترحيل يزحف، ولا يقول أين. فيُعاد ترحيلٌ واحدٌ على الدفتر
// ‏الممتلئ بمُعترِضٍ يقيس **كلَّ أمرٍ يصل القاعدة** — فيُسمّى الجاني بنصّه لا بالظنّ.
Console.WriteLine("[..] تشخيص: أيُّ أوامر الترحيل تحمل الكلفة على دفترٍ ممتلئ؟");
var capture = new SqlTimer();
await using (var db = new BrqoshDbContext(
    new DbContextOptionsBuilder<BrqoshDbContext>().UseNpgsql(cs).AddInterceptors(capture).Options))
{
    var cash = await db.Accounts.FirstAsync(a => a.CompanyId == companyId && a.Code == "1101");
    var sales = await db.Accounts.FirstAsync(a => a.CompanyId == companyId && a.Code == "4101");
    capture.Reset();

    var entry = JournalEntry.CreateDraft(companyId, new DateOnly(2026, 6, 15), "تشخيص");
    entry.AddLine(cash, 100m, 0m);
    entry.AddLine(sales, 0m, 100m);
    await new JournalPostingService(db).PostAsync(entry);
}

Console.WriteLine();
Console.WriteLine("  أغلى خمسة أوامر في ترحيلٍ واحد:");
foreach (var (sql, ms) in capture.Slowest(5))
    Console.WriteLine($"    {ms,5} ms · {Squash(sql)}");
Console.WriteLine($"  (‏مجموع {capture.Count} أمراً · {capture.TotalMs} ms‎)");
Console.WriteLine();

if (failures.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine("[FAIL] الكلفةُ تزحف مع حجم الدفتر:");
    foreach (var f in failures) Console.WriteLine($"     • {f}");
    return 3;
}

Console.WriteLine();
Console.WriteLine("=== PASS: الكلفة لا تزحف مع حجم الدفتر — العملياتُ اليومية تبقى محتملة ===");
return 0;

// ─────────────────────────────────────────────────────────────────────────
/// <summary>
/// عملياتُ المستند الواحد لا يجوز أن تنمو مع الدفتر — بحثٌ بفهرسٍ ثمّ إدراج.
/// أمّا تقاريرُ الفترة فنموُّها هو عملُها: تجمع كلَّ سطرٍ في الفترة.
/// </summary>
static bool MustNotGrow(string operation) => operation.StartsWith("ترحيل", StringComparison.Ordinal);

static async Task<Dictionary<string, long>> MeasureAsync(string cs, long companyId, string label)
{
    Console.WriteLine($"[..] قياس: {label} ...");
    var r = new Dictionary<string, long>();

    // ‏كلُّ عمليةٍ في سياقها الخاصّ — كما يفعل التطبيق، سياقٌ لكلّ عملية.
    r["ترحيل قيد يوميّة"] = await TimeAsync(cs, async db =>
    {
        var cash = await db.Accounts.FirstAsync(a => a.CompanyId == companyId && a.Code == "1101");
        var sales = await db.Accounts.FirstAsync(a => a.CompanyId == companyId && a.Code == "4101");
        var entry = JournalEntry.CreateDraft(companyId, new DateOnly(2026, 6, 15), "قياس");
        entry.AddLine(cash, 100m, 0m);
        entry.AddLine(sales, 0m, 100m);
        await new JournalPostingService(db).PostAsync(entry);
    });

    r["ترحيل فاتورة مبيعات"] = await TimeAsync(cs, async db =>
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.CompanyId == companyId);
        if (customer is null)
        {
            customer = Customer.Create(companyId, "عميل القياس");
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
        }
        var invoice = SalesInvoice.CreateDraft(companyId, customer.Id, new DateOnly(2026, 6, 15));
        invoice.AddLine("صنف", 1m, 100m);
        await new SalesInvoicePostingService(db).PostAsync(invoice);
    });

    r["ميزان مراجعة (شهر)"] = await TimeAsync(cs, async db =>
        await new TrialBalanceQuery(db).ExecuteAsync(
            companyId, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30)));

    r["ميزان مراجعة (سنة)"] = await TimeAsync(cs, async db =>
        await new TrialBalanceQuery(db).ExecuteAsync(
            companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));

    return r;
}

static async Task<long> TimeAsync(string cs, Func<BrqoshDbContext, Task> body)
{
    // ‏تشغيلةُ إحماءٍ تُطرح: أوّلُ استعلامٍ في سياقٍ جديد يدفع كلفةَ بناء نموذج EF،
    // ‏وهي كلفةُ إقلاعٍ لمرّةٍ واحدة لا كلفةَ عملية. وخلطُها بالقياس أعطاني من قبل
    // ‏«نموّاً تربيعياً» لم يكن إلا إحماءً — فتُطرح صراحةً.
    await using (var warm = BrqoshDbContextFactory.Create(DatabaseProvider.PostgreSql, cs))
    {
        try { await body(warm); }
        catch { /* الإحماء قد يفشل بتكرارٍ أو تعارض — ولا يُحتسب في القياس */ }
    }

    var best = long.MaxValue;
    for (var i = 0; i < 3; i++)
    {
        await using var db = BrqoshDbContextFactory.Create(DatabaseProvider.PostgreSql, cs);
        var sw = Stopwatch.StartNew();
        await body(db);
        sw.Stop();
        best = Math.Min(best, sw.ElapsedMilliseconds);
    }
    return best;
}

static async Task<int> SeedAsync(string cs, long companyId, int target)
{
    Console.WriteLine($"[..] تعبئةُ الدفتر إلى {target:N0} قيد ...");
    await using var db = BrqoshDbContextFactory.Create(DatabaseProvider.PostgreSql, cs);

    var cash = await db.Accounts.FirstAsync(a => a.CompanyId == companyId && a.Code == "1101");
    var sales = await db.Accounts.FirstAsync(a => a.CompanyId == companyId && a.Code == "4101");
    var seed = JournalEntry.CreateDraft(companyId, new DateOnly(2026, 3, 1), "بذرةُ التعبئة");
    seed.AddLine(cash, 100m, 0m);
    seed.AddLine(sales, 0m, 100m);
    var posted = await new JournalPostingService(db).PostAsync(seed);

    await using var conn = new NpgsqlConnection(cs);
    await conn.OpenAsync();

    // ‏قائمةُ الأعمدة تُقرأ من القاعدة لا تُكتب باليد — فالمسبار لا يتعفّن مع كلّ عمودٍ جديد.
    var entryCols = await ColumnsAsync(conn, "journal_entries", "Id");
    var lineCols = await ColumnsAsync(conn, "journal_entry_lines", "id");

    var entrySelect = string.Join(", ", entryCols.Select(c => c switch
    {
        // ‏**البادئةُ هي بادئةُ القيود الحقيقية `JV-{السنة}-`، لا بادئةٌ مخترَعة.**
        //
        // ‏أوّلُ تشغيلٍ زرع `PF-…`، فأظهر ترحيلاً خطّيّ الكلفة (107 ms من 118). وكان
        // ‏**عطلَ الأداة لا عطلَ النظام**: مولّدُ الرقم يمسح تنازلياً بحثاً عن `JV-…`،
        // ‏و`PF-` يسبق `JV-` ترتيبياً فيمرّ على الدفتر كلِّه قبل أن يبلغ صفّاً مطابقاً.
        // ‏ولا يقع ذلك في دفترٍ حقيقيّ تحمل قيودُه كلُّها البادئةَ نفسها.
        "EntryNumber" => "'JV-2026-' || lpad((g + 1000)::text, 6, '0')",
        // ‏التواريخُ موزَّعةٌ على السنة — وإلا لَقاس ميزانُ الشهر صفراً وميزانُ السنة كلَّ شيء.
        "EntryDate" => "(DATE '2026-01-01' + ((g % 330) || ' days')::interval)::date",
        _ => "e.\"" + c + "\"",
    }));

    var entryColumnList = string.Join(", ", entryCols.Select(c => "\"" + c + "\""));
    var lineColumnList = string.Join(", ", lineCols.Select(c => "\"" + c + "\""));
    var lineSelect = string.Join(", ", lineCols.Select(c =>
        c == "journal_entry_id" ? "n.\"Id\"" : "l.\"" + c + "\""));

    var sql =
        $"INSERT INTO journal_entries ({entryColumnList}) SELECT {entrySelect} " +
        $"FROM journal_entries e, generate_series(1, {target}) AS g WHERE e.\"Id\" = {posted.Id}; " +
        $"INSERT INTO journal_entry_lines ({lineColumnList}) SELECT {lineSelect} " +
        "FROM journal_entry_lines l JOIN journal_entries n ON n.\"EntryNumber\" LIKE 'PF-%' " +
        $"WHERE l.\"journal_entry_id\" = {posted.Id};";

    var sw = Stopwatch.StartNew();
    await using (var cmd = new NpgsqlCommand(sql, conn) { CommandTimeout = 900 })
        await cmd.ExecuteNonQueryAsync();
    sw.Stop();
    Console.WriteLine($"[OK] التعبئة تمّت في {sw.ElapsedMilliseconds:N0} ms");

    await using var count = new NpgsqlCommand(
        $"SELECT COUNT(*) FROM journal_entries WHERE \"CompanyId\" = {companyId}", conn);
    return Convert.ToInt32(await count.ExecuteScalarAsync());
}

static async Task<List<string>> ColumnsAsync(NpgsqlConnection conn, string table, string identity)
{
    var cols = new List<string>();
    await using var cmd = new NpgsqlCommand(
        "SELECT column_name FROM information_schema.columns " +
        $"WHERE table_schema='public' AND table_name='{table}' ORDER BY ordinal_position", conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        var name = reader.GetString(0);
        if (!string.Equals(name, identity, StringComparison.Ordinal)) cols.Add(name);
    }
    return cols;
}

/// <summary>يختصر نصَّ الأمر إلى ما يُعرَّف به — الجدولُ والشرط، لا الأعمدةُ كلُّها.</summary>
static string Squash(string sql)
{
    var one = string.Join(' ', sql.Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(l => l.Trim()));
    return one.Length <= 150 ? one : one[..150] + " …";
}

static async Task<string> ExplainAsync(NpgsqlConnection conn, string query)
{
    await using var cmd = new NpgsqlCommand($"EXPLAIN {query}", conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    var lines = new List<string>();
    while (await reader.ReadAsync()) lines.Add(reader.GetString(0));
    return string.Join("\n", lines);
}

/// <summary>يقيس زمنَ كلّ أمرٍ يصل القاعدة — فالكلفةُ تُنسب إلى نصٍّ بعينه لا إلى ظنّ.</summary>
sealed class SqlTimer : DbCommandInterceptor
{
    private readonly List<(string Sql, long Ms)> _seen = [];
    private readonly Dictionary<DbCommand, long> _started = [];

    public int Count => _seen.Count;
    public long TotalMs => _seen.Sum(x => x.Ms);
    public void Reset() { _seen.Clear(); _started.Clear(); }

    public IEnumerable<(string Sql, long Ms)> Slowest(int n) =>
        _seen.OrderByDescending(x => x.Ms).Take(n);

    private void Start(DbCommand c) => _started[c] = Stopwatch.GetTimestamp();

    private void Stop(DbCommand c)
    {
        if (!_started.Remove(c, out var t0)) return;
        _seen.Add((c.CommandText, (long)Stopwatch.GetElapsedTime(t0).TotalMilliseconds));
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData e, InterceptionResult<DbDataReader> r)
    { Start(command); return base.ReaderExecuting(command, e, r); }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData e, InterceptionResult<DbDataReader> r, CancellationToken ct = default)
    { Start(command); return base.ReaderExecutingAsync(command, e, r, ct); }

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData e, DbDataReader r)
    { Stop(command); return base.ReaderExecuted(command, e, r); }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command, CommandExecutedEventData e, DbDataReader r, CancellationToken ct = default)
    { Stop(command); return base.ReaderExecutedAsync(command, e, r, ct); }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command, CommandEventData e, InterceptionResult<object> r)
    { Start(command); return base.ScalarExecuting(command, e, r); }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData e, InterceptionResult<object> r, CancellationToken ct = default)
    { Start(command); return base.ScalarExecutingAsync(command, e, r, ct); }

    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData e, object? r)
    { Stop(command); return base.ScalarExecuted(command, e, r); }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command, CommandExecutedEventData e, object? r, CancellationToken ct = default)
    { Stop(command); return base.ScalarExecutedAsync(command, e, r, ct); }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData e, InterceptionResult<int> r)
    { Start(command); return base.NonQueryExecuting(command, e, r); }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData e, InterceptionResult<int> r, CancellationToken ct = default)
    { Start(command); return base.NonQueryExecutingAsync(command, e, r, ct); }

    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData e, int r)
    { Stop(command); return base.NonQueryExecuted(command, e, r); }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command, CommandExecutedEventData e, int r, CancellationToken ct = default)
    { Stop(command); return base.NonQueryExecutedAsync(command, e, r, ct); }
}
