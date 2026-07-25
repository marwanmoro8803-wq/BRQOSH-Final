// مسبار هجرات PostgreSQL (ح-2، CEEF-201)
//
// يُثبت أن تثبيتاً إنتاجياً نظيفاً يعمل فعلاً: ينشئ قاعدة PostgreSQL فارغة منفصلة،
// يشغّل MigrateAsync عليها، ثم يُدرج شركة — وهو ما كان يفشل سابقاً بـ
// «null value in column "Id"» لأن الهجرات كانت مولَّدة بأنواع SQLite.
//
// آمن: يعمل على قاعدة باسم مستقل (brqosh_migprobe) ولا يمسّ قاعدة المستخدم إطلاقاً.
// التشغيل:  dotnet run --project tools/MigrationProbe

using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

const string TestDb = "brqosh_migprobe";

var pwPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BRQOSH", "pg_password.txt");
if (!File.Exists(pwPath))
{
    Console.WriteLine($"[SKIP] لا يوجد {pwPath} — يتطلب المسبار PostgreSQL محلياً مهيّأً.");
    return 0;
}

var pw = (await File.ReadAllTextAsync(pwPath)).Trim();
var admin = $"Host=localhost;Port=5433;Database=postgres;Username=postgres;Password={pw}";

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

Console.WriteLine($"[OK] أُنشئت قاعدة اختبار نظيفة: {TestDb}");

var cs = $"Host=localhost;Port=5433;Database={TestDb};Username=postgres;Password={pw}";
await using var db = BrqoshDbContextFactory.Create(DatabaseProvider.PostgreSql, cs);

try
{
    Console.WriteLine("[..] تشغيل MigrateAsync على PostgreSQL فارغة...");
    await db.Database.MigrateAsync();
    Console.WriteLine("[OK] MigrateAsync نجح");
}
catch (Exception ex)
{
    Console.WriteLine($"[FAIL] MigrateAsync: {ex.GetType().Name}: {ex.Message}");
    return 2;
}

// الاختبار الحاسم: هل تعمل أعمدة identity فعلاً؟ (هنا كان الانكسار)
try
{
    var company = await new DatabaseInitializer(db).InitializeAsync(
        "شركة المسبار", "310123456700077", Brqosh.Domain.Modules.BusinessProfile.General);
    Console.WriteLine($"[OK] إدراج شركة نجح — Id={company.Id}");
}
catch (Exception ex)
{
    Console.WriteLine($"[FAIL] إدراج شركة: {ex.GetType().Name}: {ex.Message}");
    if (ex.InnerException is not null) Console.WriteLine($"       inner: {ex.InnerException.Message}");
    return 3;
}

// الاختبار الحاسم الثاني (CEEF-203): ترحيل قيد فعلي على PostgreSQL.
// كل الاختبارات تعمل على SQLite حيث يُتخطّى قفل pg_advisory_xact_lock — فعيب
// ابتلاع CancellationToken كمعامل SQL كان يكسر **كل ترحيل محاسبي على PostgreSQL**
// دون أن يكشفه أي اختبار. هذه الخطوة هي الحارس.
try
{
    Brqosh.Infrastructure.Security.SecurityContext.CurrentRole =
        Brqosh.Domain.Security.UserRole.Administrator;
    Brqosh.Infrastructure.Security.SecurityContext.CurrentUsername = "migration-probe";

    var companyId = (await db.Companies.OrderBy(c => c.Id)
        .Select(c => c.Id).FirstAsync());

    var cash = await db.Accounts.FirstAsync(a => a.CompanyId == companyId && a.Code == "1101" && !a.IsGroup);
    var capital = await db.Accounts.FirstAsync(a => a.CompanyId == companyId && a.Code == "3101" && !a.IsGroup);

    var entry = Brqosh.Domain.Accounting.JournalEntry.CreateDraft(
        companyId, new DateOnly(2026, 7, 25), "قيد مسبار على PostgreSQL");
    entry.AddLine(cash, 1000m, 0m);
    entry.AddLine(capital, 0m, 1000m);

    var posted = await new Brqosh.Infrastructure.Accounting.JournalPostingService(db).PostAsync(entry);
    Console.WriteLine($"[OK] ترحيل قيد على PostgreSQL نجح — {posted.EntryNumber}");
}
catch (Exception ex)
{
    Console.WriteLine($"[FAIL] ترحيل قيد على PostgreSQL: {ex.GetType().Name}: {ex.Message}");
    if (ex.InnerException is not null) Console.WriteLine($"       inner: {ex.InnerException.Message}");
    return 4;
}

// الاختبار الحاسم الثالث (ع-D1، CEEF-203): ترحيل متزامن على PostgreSQL.
// حماية ACC-006 (pg_advisory_xact_lock) لم تكن تعمل إطلاقاً — فلم يكن هناك ما يمنع
// رقمَي قيد متطابقين تحت التوازي. هذه الخطوة تُثبت أن القفل يعمل فعلاً.
try
{
    const int Parallel = 8;
    var companyId2 = await db.Companies.OrderBy(c => c.Id).Select(c => c.Id).FirstAsync();

    var tasks = Enumerable.Range(0, Parallel).Select(async i =>
    {
        // سياق مستقل لكل مهمة = اتصال مستقل = تزامن حقيقي لا تسلسل
        await using var ctx = BrqoshDbContextFactory.Create(DatabaseProvider.PostgreSql, cs);
        Brqosh.Infrastructure.Security.SecurityContext.CurrentRole =
            Brqosh.Domain.Security.UserRole.Administrator;
        Brqosh.Infrastructure.Security.SecurityContext.CurrentUsername = "probe-concurrency";

        var cashAcc = await ctx.Accounts.FirstAsync(a => a.CompanyId == companyId2 && a.Code == "1101" && !a.IsGroup);
        var capAcc = await ctx.Accounts.FirstAsync(a => a.CompanyId == companyId2 && a.Code == "3101" && !a.IsGroup);

        var je = Brqosh.Domain.Accounting.JournalEntry.CreateDraft(
            companyId2, new DateOnly(2026, 7, 25), $"قيد متزامن {i}");
        je.AddLine(cashAcc, 10m, 0m);
        je.AddLine(capAcc, 0m, 10m);

        var p = await new Brqosh.Infrastructure.Accounting.JournalPostingService(ctx).PostAsync(je);
        return p.EntryNumber!;
    });

    var numbers = await Task.WhenAll(tasks);
    var distinct = numbers.Distinct().Count();

    if (distinct != numbers.Length)
    {
        var dups = numbers.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key);
        Console.WriteLine($"[FAIL] أرقام قيود مكررة تحت التوازي: {string.Join(", ", dups)}");
        return 5;
    }

    Console.WriteLine($"[OK] {Parallel} ترحيلات متزامنة — {distinct} رقماً فريداً بلا تكرار");
}
catch (Exception ex)
{
    Console.WriteLine($"[FAIL] الترحيل المتزامن: {ex.GetType().Name}: {ex.Message}");
    if (ex.InnerException is not null) Console.WriteLine($"       inner: {ex.InnerException.Message}");
    return 6;
}

Console.WriteLine();
Console.WriteLine("=== PASS: تثبيت PostgreSQL نظيف + ترحيل محاسبي + تزامن آمن ===");
return 0;
