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

Console.WriteLine();
Console.WriteLine("=== PASS: تثبيت PostgreSQL نظيف عبر الهجرات يعمل ===");
return 0;
