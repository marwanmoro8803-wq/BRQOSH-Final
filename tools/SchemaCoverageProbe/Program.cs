// مسبار تغطية المخطط (CEEF-229 · م2-٩)
//
// ‏السؤال الذي يجيب عنه:
//   هل المخطط الذي تحصل عليه قاعدة إنتاجٍ نظيفة يطابق ما **يتوقّعه الكود** — اسماً ونوعاً
//   وقابليةَ إفراغٍ وقيدَ تفرّد؟
//
// ‏لماذا لا يكفي أيّ اختبار قائم:
//   كلّ الاختبارات تعمل على SQLite، وSQLite يُنشأ بـ EnsureCreated **من النموذج نفسه** —
//   فالمخطط هناك مطابقٌ للنموذج بحكم التعريف، والسؤال أعلاه بلا معنى هناك. أمّا الإنتاج
//   (PostgreSQL) فمخططه يأتي من مصدرين: خمس هجرات متوقّفة عند ٢٥ يوليو، ثمّ مُرقِّعٌ يدويّ
//   (BrqoshSchemaUpdater، ١٥٩٣ سطراً) يُكمل ما بعدها بـ DDL مكتوبٍ باليد. وDDL اليد يخطئ
//   بصمت: عمودٌ نصّيّ حيث ينتظر النموذج numeric(18,2)، أو عمودٌ يقبل الفراغ حيث يمنعه،
//   أو فهرسٌ فريدٌ نُسي — فتتكرّر أرقام الفواتير ولا يشتكي أحد.
//
// ‏ما يفعله المسبار:
//   ١) ينشئ قاعدة PostgreSQL نظيفة مستقلّة (brqosh_schemaprobe) — لا يمسّ قاعدة المستخدم.
//   ٢) يُجبر وضع الإنتاج، فيسلك DatabaseSchemaService **مسار الإنتاج بعينه**: Migrate ثمّ المُرقِّع.
//      (ووضعُ الإنتاج مقصود: هو وحده يمنع السقوط إلى EnsureCreated — وEnsureCreated يبني
//       المخطط من النموذج، فيجعل المقارنة تحصيلَ حاصلٍ ويُخفي بالضبط ما جاء المسبار ليكشفه.)
//   ٣) يقابل النموذج بالقاعدة على أربعة مستويات: الجدول، العمود، النوع وقابلية الإفراغ، والتفرّد.
//
// ‏التشغيل:  dotnet run --project tools/SchemaCoverageProbe

using Brqosh.Application.Setup;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

const string TestDb = "brqosh_schemaprobe";

// ‏أعمدة نظام PostgreSQL: موجودةٌ على كلّ جدول ولا يسردها information_schema إطلاقاً.
// ‏وxmin منها، ويستعمله EF رمزَ تزامنٍ — فاستثناؤه تصحيحٌ للمسبار لا تساهلٌ مع المخطط.
var systemColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    { "xmin", "xmax", "cmin", "cmax", "ctid", "tableoid" };

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

AppEnvironmentConfig.ForceForTests(AppEnvironment.Production);
try
{
    Console.WriteLine("[..] تشغيل مسار الإنتاج: DatabaseSchemaService.EnsureSchemaAsync ...");
    await new DatabaseSchemaService(db).EnsureSchemaAsync();
    Console.WriteLine("[OK] تهيئة المخطط انتهت دون رمي");
}
catch (Exception ex)
{
    Console.WriteLine($"[FAIL] تهيئة المخطط: {ex.GetType().Name}: {ex.Message}");
    if (ex.InnerException is not null) Console.WriteLine($"       inner: {ex.InnerException.Message}");
    return 2;
}
finally
{
    AppEnvironmentConfig.ResetForTests();
}

// ── ما يوجد فعلاً في القاعدة: العمود ونوعه وقابليةُ إفراغه ────────────────
var actual = new Dictionary<string, Dictionary<string, (string Type, bool Nullable)>>(StringComparer.OrdinalIgnoreCase);
await using (var conn = new NpgsqlConnection(cs))
{
    await conn.OpenAsync();
    var columnSql =
        "SELECT table_name, column_name, data_type, character_maximum_length, " +
        "numeric_precision, numeric_scale, is_nullable " +
        "FROM information_schema.columns WHERE table_schema = 'public'";
    await using var cmd = new NpgsqlCommand(columnSql, conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        var table = reader.GetString(0);
        var column = reader.GetString(1);
        var dataType = reader.GetString(2);
        var maxLength = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3);
        var precision = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4);
        var scale = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5);
        var nullable = reader.GetString(6) == "YES";

        var store = dataType switch
        {
            "character varying" or "character" when maxLength is int n => $"{dataType}({n})",
            "numeric" when precision is int p && scale is int s => $"numeric({p},{s})",
            _ => dataType,
        };

        if (!actual.TryGetValue(table, out var cols))
            actual[table] = cols = new Dictionary<string, (string, bool)>(StringComparer.OrdinalIgnoreCase);
        cols[column] = (store, nullable);
    }
}

// ── قيود التفرّد الفعلية (فهارس فريدة ومفاتيح أوّلية) ─────────────────────
// ‏المقارنة على مستوى **مجموعة الأعمدة** لا اسم الفهرس: الأسماء تختلف بين المُرقِّع وEF،
// ‏والذي يحمي الدفاتر هو الأعمدة لا التسمية.
var actualUniqueSets = new List<(string Table, SortedSet<string> Columns)>();
await using (var conn = new NpgsqlConnection(cs))
{
    await conn.OpenAsync();
    var indexSql =
        "SELECT t.relname AS tbl, i.relname AS idx, a.attname AS col " +
        "FROM pg_class t " +
        "JOIN pg_index ix ON t.oid = ix.indrelid " +
        "JOIN pg_class i ON i.oid = ix.indexrelid " +
        "JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = ANY(ix.indkey) " +
        "JOIN pg_namespace n ON n.oid = t.relnamespace " +
        "WHERE n.nspname = 'public' AND t.relkind = 'r' AND ix.indisunique";
    await using var cmd = new NpgsqlCommand(indexSql, conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    var grouped = new Dictionary<(string, string), SortedSet<string>>();
    while (await reader.ReadAsync())
    {
        var key = (reader.GetString(0), reader.GetString(1));
        if (!grouped.TryGetValue(key, out var set))
            grouped[key] = set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        set.Add(reader.GetString(2));
    }
    foreach (var ((table, _), columns) in grouped)
        actualUniqueSets.Add((table, columns));
}

Console.WriteLine($"[..] القاعدة تحوي {actual.Count} جدولاً و{actualUniqueSets.Count} قيدَ تفرّد");

// ── ما يتوقّعه النموذج ────────────────────────────────────────────────────
var missingTables = new List<string>();
var missingColumns = new List<string>();
var wrongType = new List<string>();
var wrongNullability = new List<string>();
var missingUnique = new List<string>();
var modelTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

foreach (var entity in db.Model.GetEntityTypes().OrderBy(e => e.GetTableName(), StringComparer.Ordinal))
{
    var table = entity.GetTableName();
    if (string.IsNullOrEmpty(table)) continue;            // ToView / keyless / غير معيَّن لجدول
    modelTables.Add(table!);

    var id = StoreObjectIdentifier.Table(table!, entity.GetSchema());

    if (!actual.TryGetValue(table!, out var present))
    {
        var line = $"{table}  ({entity.GetProperties().Count()} عموداً)";
        if (!missingTables.Contains(line)) missingTables.Add(line);
        continue;
    }

    foreach (var property in entity.GetProperties())
    {
        var column = property.GetColumnName(id);
        if (string.IsNullOrEmpty(column) || systemColumns.Contains(column!)) continue;

        if (!present.TryGetValue(column!, out var live))
        {
            missingColumns.Add($"{table}.{column}");
            continue;
        }

        var expectedType = property.GetColumnType(id);
        if (!string.IsNullOrEmpty(expectedType)
            && !string.Equals(Normalize(expectedType!), Normalize(live.Type), StringComparison.OrdinalIgnoreCase))
        {
            wrongType.Add($"{table}.{column}: النموذج «{expectedType}» — القاعدة «{live.Type}»");
        }

        var expectedNullable = property.IsColumnNullable(id);
        if (expectedNullable != live.Nullable)
        {
            wrongNullability.Add(
                $"{table}.{column}: النموذج {(expectedNullable ? "يقبل الفراغ" : "يمنعه")} — "
                + $"القاعدة {(live.Nullable ? "تقبله" : "تمنعه")}");
        }
    }

    // ‏قيودُ التفرّد وحدها هي التي تحمي الدفاتر (رقمُ فاتورةٍ مكرّر، كودُ صنفٍ مكرّر).
    // ‏والفهارس غير الفريدة أداءٌ لا صحّة، فلا يفشل المسبار لأجلها.
    var uniqueSpecs = entity.GetIndexes().Where(i => i.IsUnique)
        .Select(i => i.Properties.Select(p => p.GetColumnName(id) ?? p.Name).ToList())
        .Concat(entity.GetKeys().Select(k => k.Properties.Select(p => p.GetColumnName(id) ?? p.Name).ToList()));

    foreach (var spec in uniqueSpecs)
    {
        var want = new SortedSet<string>(spec, StringComparer.OrdinalIgnoreCase);
        var covered = actualUniqueSets.Any(u =>
            string.Equals(u.Table, table, StringComparison.OrdinalIgnoreCase) && u.Columns.SetEquals(want));
        var line = $"{table} ({string.Join(", ", want)})";
        if (!covered && !missingUnique.Contains(line)) missingUnique.Add(line);
    }
}

Console.WriteLine($"[..] النموذج يتوقّع {modelTables.Count} جدولاً");
Console.WriteLine();

var problems = missingTables.Count + missingColumns.Count
             + wrongType.Count + wrongNullability.Count + missingUnique.Count;

if (problems == 0)
{
    Console.WriteLine("[PASS] المخطط الذي تحصل عليه قاعدة إنتاجٍ نظيفة يطابق النموذج:");
    Console.WriteLine("       الجداول والأعمدة والأنواع وقابليةُ الإفراغ وقيودُ التفرّد.");
    Console.WriteLine();
    return await ProveUpgradePathAsync(pw);
}

Console.WriteLine("[FAIL] المخطط الإنتاجيّ يخالف ما يتوقّعه الكود:");
Console.WriteLine();
Report("جداول مفقودة — كلّ قراءةٍ منها ترمي 42P01 undefined_table", missingTables);
Report("أعمدة مفقودة — كلّ استعلامٍ يلمسها يرمي 42703 undefined_column", missingColumns);
Report("أنواع مخالفة — تقريبٌ صامتٌ أو رمي عند التحويل", wrongType);
Report("قابليةُ إفراغٍ مخالفة — إمّا 23502 عند الإدراج أو صفٌّ ناقصٌ يمرّ بلا حارس", wrongNullability);
Report("قيودُ تفرّدٍ مفقودة — تكرارٌ صامتٌ في الدفاتر", missingUnique);
return 4;

// ── المرحلة الثانية: مسارُ الترقية ────────────────────────────────────────
// ‏قاعدةُ العميل ليست نظيفة. مخططها بُني بهجراتٍ **قديمة** ثمّ رقّعه المُرقِّع باليد، فأعمدةُ
// ‏أيّ هجرةٍ جديدة موجودةٌ فيها سلفاً. وهجرةٌ تُصدر ALTER TABLE ADD COLUMN عارياً ترتطم بـ
// ‏42701، فيبتلعها IsIgnorableSchemaConflict ولا يُكتب صفُّ التاريخ — فتبقى السلسلة عالقةً
// ‏عند تلك الهجرة، ولا تُطبَّق أيّ هجرةٍ بعدها أبداً، وكلّ ذلك بلا رسالةٍ واحدة.
// ‏فهذه المرحلة تعيد بناء تلك الحالة بعينها وتسأل: هل سُجّلت الهجرة الجديدة فعلاً؟
static async Task<int> ProveUpgradePathAsync(string password)
{
    const string UpgradeDb = "brqosh_upgradeprobe";
    var adminCs = $"Host=localhost;Port=5433;Database=postgres;Username=postgres;Password={password}";
    var upgradeCs = $"Host=localhost;Port=5433;Database={UpgradeDb};Username=postgres;Password={password}";

    await using (var c = new NpgsqlConnection(adminCs))
    {
        await c.OpenAsync();
        await using (var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {UpgradeDb} WITH (FORCE)", c))
            await drop.ExecuteNonQueryAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {UpgradeDb}", c))
            await create.ExecuteNonQueryAsync();
    }

    await using var db = BrqoshDbContextFactory.Create(DatabaseProvider.PostgreSql, upgradeCs);

    var all = db.Database.GetMigrations().ToList();
    if (all.Count < 2)
    {
        Console.WriteLine("[SKIP] هجرةٌ واحدة فقط — لا مسار ترقيةٍ يُقاس.");
        return 0;
    }

    var previous = all[^2];
    var newest = all[^1];
    Console.WriteLine($"[..] بناءُ حالة عميلٍ قائم: الهجرات حتى «{previous}» ثمّ المُرقِّع اليدويّ ...");

    try
    {
        // ‏الترحيل إلى هجرةٍ بعينها: هذه هي حالةُ العميل قبل التحديث.
        await db.GetService<IMigrator>().MigrateAsync(previous);
        await new BrqoshSchemaUpdater(db).ApplyAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[FAIL] تعذّر بناءُ حالة العميل: {ex.GetType().Name}: {ex.Message}");
        return 5;
    }

    Console.WriteLine($"[..] تشغيل التحديث: هل تُطبَّق «{newest}» على قاعدةٍ رُقِّعت سلفاً؟");

    AppEnvironmentConfig.ForceForTests(AppEnvironment.Production);
    try
    {
        await new DatabaseSchemaService(db).EnsureSchemaAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[FAIL] التحديث رمى: {ex.GetType().Name}: {ex.Message}");
        return 6;
    }
    finally
    {
        AppEnvironmentConfig.ResetForTests();
    }

    var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
    if (!applied.Contains(newest))
    {
        Console.WriteLine($"[FAIL] «{newest}» لم تُسجَّل في __EFMigrationsHistory رغم أنّ التحديث لم يرمِ.");
        Console.WriteLine("       هذا هو الابتلاع بعينه: السلسلة عالقة، وكلّ هجرةٍ بعدها ميّتة.");
        Console.WriteLine($"       المسجَّل: {applied.Count} من {all.Count}");
        return 7;
    }

    Console.WriteLine($"[PASS] مسارُ الترقية سليم — «{newest}» طُبّقت وسُجّلت على قاعدةٍ مرقّعةٍ سلفاً.");
    Console.WriteLine($"       المسجَّل: {applied.Count} من {all.Count} هجرة.");
    Console.WriteLine();

    return await ProveLegacyRepairAsync(upgradeCs);
}

// ── المرحلة الثالثة: القاعدةُ القديمة ─────────────────────────────────────
// ‏هناك فرعٌ ثالث لا تمرّ به قاعدةٌ نظيفة ولا قاعدةٌ مُرحَّلة: DatabaseSchemaService.cs:48-52 —
// ‏إن وُجد جدول companies وكان تاريخُ الهجرات **فارغاً**، يُشغَّل المُرقِّع وحده و«return»،
// ‏فلا يُستدعى MigrateAsync إطلاقاً. ولأنّ التاريخ يبقى فارغاً، **لا تخرج تلك القاعدة من هذا
// ‏الفرع أبداً**: كلّ هجرةٍ كُتبت أو ستُكتب ميّتةٌ بالنسبة إليها، والمُرقِّع وحده مسؤولٌ عن
// ‏كامل مخططها. وهذه هي حالُ قاعدةٍ أُنشئت بالمسار المهجور قبل أن تُضاف ميزة.
//
// ‏فالسؤال: **هل يفي المُرقِّع بما ادّعاه؟** يُعاد بناءُ قاعدةٍ أقدم من موجة «دورة حياة
// ‏المبيعات» (عروض الأسعار وأوامر البيع) — وهي الموجة التي يُصرّح EnsureV12LifecycleTablesAsync
// ‏بأنّه يتكفّل بها — ثمّ يُسأل: هل عادت جداولُها كلّها بعد الإقلاع؟
static async Task<int> ProveLegacyRepairAsync(string connectionString)
{
    // ‏موجةٌ واحدة متماسكة: الأبوان وسطراهما. والمُرقِّع يدّعي ملكيّتها صراحةً.
    string[] wave = ["sales_quotation_lines", "sales_order_lines", "sales_quotations", "sales_orders"];

    await using (var conn = new NpgsqlConnection(connectionString))
    {
        await conn.OpenAsync();
        foreach (var table in wave)
        {
            await using var drop = new NpgsqlCommand($"DROP TABLE IF EXISTS \"{table}\" CASCADE", conn);
            await drop.ExecuteNonQueryAsync();
        }

        // ‏تفريغُ التاريخ هو ما يضع القاعدة على الفرع المهجور — لا حذفُ الجدول.
        await using var truncate = new NpgsqlCommand("""DELETE FROM "__EFMigrationsHistory";""", conn);
        await truncate.ExecuteNonQueryAsync();
    }

    Console.WriteLine("[..] بناءُ قاعدةٍ قديمة: موجةُ دورة حياة المبيعات محذوفة، وتاريخُ الهجرات مُفرَّغ ...");

    await using var db = BrqoshDbContextFactory.Create(DatabaseProvider.PostgreSql, connectionString);

    AppEnvironmentConfig.ForceForTests(AppEnvironment.Production);
    try
    {
        await new DatabaseSchemaService(db).EnsureSchemaAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[FAIL] إقلاعُ القاعدة القديمة رمى: {ex.GetType().Name}: {ex.Message}");
        return 8;
    }
    finally
    {
        AppEnvironmentConfig.ResetForTests();
    }

    // ‏عودةُ الجدول لا تكفي: DDL اليد قد يُعيده بأعمدةٍ ناقصةٍ أو بأنواعٍ مخالفة، فيتحوّل
    // ‏42P01 إلى 42703 وهو أخبثُ لأنّه يظهر في شاشةٍ واحدة لا في كلّ شيء. فتُقاس الأعمدة.
    var restored = new Dictionary<string, Dictionary<string, (string Type, bool Nullable)>>(StringComparer.OrdinalIgnoreCase);
    await using (var conn = new NpgsqlConnection(connectionString))
    {
        await conn.OpenAsync();
        var sql =
            "SELECT table_name, column_name, data_type, character_maximum_length, " +
            "numeric_precision, numeric_scale, is_nullable " +
            "FROM information_schema.columns WHERE table_schema = 'public'";
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var table = reader.GetString(0);
            var maxLength = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3);
            var precision = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4);
            var scale = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5);
            var dataType = reader.GetString(2);
            var store = dataType switch
            {
                "character varying" or "character" when maxLength is int n => $"{dataType}({n})",
                "numeric" when precision is int p && scale is int s => $"numeric({p},{s})",
                _ => dataType,
            };
            if (!restored.TryGetValue(table, out var cols))
                restored[table] = cols = new Dictionary<string, (string, bool)>(StringComparer.OrdinalIgnoreCase);
            cols[reader.GetString(1)] = (store, reader.GetString(6) == "YES");
        }
    }

    var faults = new List<string>();
    foreach (var table in wave)
    {
        if (!restored.TryGetValue(table, out var present))
        {
            faults.Add($"{table} — الجدول لم يعد أصلاً؛ كلّ حفظٍ يلمسه يرمي 42P01 undefined_table");
            continue;
        }

        var entity = db.Model.GetEntityTypes()
            .FirstOrDefault(e => string.Equals(e.GetTableName(), table, StringComparison.OrdinalIgnoreCase));
        if (entity is null) continue;

        var id = StoreObjectIdentifier.Table(table, entity.GetSchema());
        foreach (var property in entity.GetProperties())
        {
            var column = property.GetColumnName(id);
            if (string.IsNullOrEmpty(column)) continue;

            if (!present.TryGetValue(column!, out var live))
            {
                faults.Add($"{table}.{column} — عمودٌ مفقود؛ 42703 undefined_column");
                continue;
            }

            var expectedType = property.GetColumnType(id);
            if (!string.IsNullOrEmpty(expectedType)
                && !string.Equals(Normalize(expectedType!), Normalize(live.Type), StringComparison.OrdinalIgnoreCase))
            {
                faults.Add($"{table}.{column} — النموذج «{expectedType}» والمُرقِّع «{live.Type}»");
            }

            if (property.IsColumnNullable(id) != live.Nullable)
                faults.Add($"{table}.{column} — قابليةُ الإفراغ تخالف النموذج");
        }
    }

    if (faults.Count > 0)
    {
        Console.WriteLine("[FAIL] المُرقِّع لم يُعِد كلّ ما ادّعى ملكيّته على القاعدة القديمة:");
        foreach (var f in faults) Console.WriteLine($"     • {f}");
        Console.WriteLine();
        Console.WriteLine("       وهذه القاعدة لا تخرج من فرع DatabaseSchemaService.cs:48-52 أبداً،");
        Console.WriteLine("       فلا هجرةَ تُنقذها — المُرقِّع وحده، وهو ناقص.");
        return 9;
    }

    Console.WriteLine("[PASS] القاعدةُ القديمة أُصلحت بالكامل — جداولُ الموجة عادت بأعمدتها");
    Console.WriteLine("       وأنواعها كما يتوقّعها النموذج، بالإقلاع وحده.");
    return 0;
}

static string Normalize(string storeType) => storeType.Replace(" ", "").ToLowerInvariant();

static void Report(string title, List<string> items)
{
    if (items.Count == 0) return;
    Console.WriteLine($"  ── {title} ({items.Count}):");
    foreach (var item in items) Console.WriteLine($"     • {item}");
    Console.WriteLine();
}
