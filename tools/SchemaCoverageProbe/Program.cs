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

using System.Globalization;
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

// (CEEF-229 · م2-١٠) **المفاتيحُ الأجنبية — والمُرقِّعُ لا يُنشئ واحداً.**
//
// `InitialCreate` تُنشئ الجداولَ بمفاتيحها الأجنبية، **و`BrqoshSchemaUpdater` يُنشئ
// اثنين وثلاثين جدولاً بلا قيدٍ واحد**. فقاعدتان بالاسم نفسه: واحدةٌ يحرسها المحرّك
// من سطرٍ يتيم، وأخرى لا. والفرقُ لا يظهر حتى يُحذف مستندٌ فتبقى سطورُه معلّقة.
var actualForeignKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
await using (var fkConn = new NpgsqlConnection(cs))
{
    await fkConn.OpenAsync();
    await using var fkCmd = new NpgsqlCommand("""
    SELECT c.conrelid::regclass::text AS child,
           a.attname                  AS col,
           c.confrelid::regclass::text AS parent
    FROM pg_constraint c
    JOIN LATERAL unnest(c.conkey) WITH ORDINALITY AS k(attnum, ord) ON TRUE
    JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = k.attnum
    WHERE c.contype = 'f' AND c.connamespace = 'public'::regnamespace
    """, fkConn);
    await using var fkReader = await fkCmd.ExecuteReaderAsync();
    while (await fkReader.ReadAsync())
        actualForeignKeys.Add($"{fkReader.GetString(0)}.{fkReader.GetString(1)}->{fkReader.GetString(2)}");
}

Console.WriteLine($"[..] القاعدة تحوي {actual.Count} جدولاً و{actualUniqueSets.Count} قيدَ تفرّد "
                  + $"و{actualForeignKeys.Count} مفتاحاً أجنبياً");

// ── ما يتوقّعه النموذج ────────────────────────────────────────────────────
var missingTables = new List<string>();
var missingColumns = new List<string>();
var wrongType = new List<string>();
var wrongNullability = new List<string>();
var missingUnique = new List<string>();
var missingForeignKeys = new List<string>();
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

    // ‏والمفاتيحُ الأجنبية: كلُّ ما يُعلنه النموذجُ يجب أن يوجد في القاعدة.
    foreach (var fk in entity.GetForeignKeys())
    {
        var principal = fk.PrincipalEntityType.GetTableName();
        if (principal is null) continue;

        // ‏النوعُ المملوك الذي يشترك جدولَ مالكه (‏table splitting) يُنتج في النموذج مفتاحاً
        // ‏أجنبياً من الجدول إلى نفسه على مفتاحه الأساسيّ — ولا تُنشئه القاعدةُ ولا ينبغي.
        // ‏وهو ما أبلغ عنه المسبارُ أوّلَ مرّة: «companies.Id ⟵ companies». فيُستبعد.
        if (string.Equals(principal, table, StringComparison.OrdinalIgnoreCase)
            && fk.Properties.All(pr => entity.FindPrimaryKey()?.Properties.Contains(pr) == true))
            continue;

        foreach (var property in fk.Properties)
        {
            var column = property.GetColumnName(id) ?? property.Name;
            var key = $"{table}.{column}->{principal}";
            if (actualForeignKeys.Contains(key)) continue;

            var line = $"{table}.{column} ⟵ {principal}";
            if (!missingForeignKeys.Contains(line)) missingForeignKeys.Add(line);
        }
    }
}

Console.WriteLine($"[..] النموذج يتوقّع {modelTables.Count} جدولاً");
Console.WriteLine();

var problems = missingTables.Count + missingColumns.Count
             + wrongType.Count + wrongNullability.Count + missingUnique.Count + missingForeignKeys.Count;

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
Report("مفاتيحُ أجنبية مفقودة — سطورٌ يتيمةٌ لا يمنعها المحرّك", missingForeignKeys);
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

    // (CEEF-229 · م2-١٠) **والتكاملُ المرجعيّ يعود كذلك — أم يُعاد الجدولُ بلا حارس؟**
    //
    // `InitialCreate` تُنشئ الجداولَ بمفاتيحها الأجنبية، **والمُرقِّعُ يُنشئها بـSQL يدويّ**.
    // فما لا يُكتب في ذلك النصّ لا يوجد أبداً — ولا هجرةَ تصل هذه القاعدة لتُصلحه.
    // والفرقُ لا يظهر حتى يُحذف مستندٌ فتبقى سطورُه معلّقةً في الدفاتر بلا أب.
    await using (var fkConn = new NpgsqlConnection(connectionString))
    {
        await fkConn.OpenAsync();
        await using var fkCmd = new NpgsqlCommand("""
            SELECT c.conrelid::regclass::text
            FROM pg_constraint c
            WHERE c.contype = 'f' AND c.connamespace = 'public'::regnamespace
            """, fkConn);
        await using var fkReader = await fkCmd.ExecuteReaderAsync();

        var withForeignKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (await fkReader.ReadAsync())
            withForeignKeys.Add(fkReader.GetString(0));

        foreach (var table in wave)
        {
            var entity = db.Model.GetEntityTypes()
                .FirstOrDefault(e => string.Equals(e.GetTableName(), table, StringComparison.OrdinalIgnoreCase));
            if (entity is null) continue;

            var expects = entity.GetForeignKeys().Any(fk =>
                !string.Equals(fk.PrincipalEntityType.GetTableName(), table, StringComparison.OrdinalIgnoreCase));

            if (expects && !withForeignKeys.Contains(table))
                faults.Add($"{table} — عاد بلا مفتاحٍ أجنبيّ؛ سطرٌ يتيمٌ لا يمنعه المحرّك");
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
    Console.WriteLine();

    return await ProvePatcherNumbersLegacyAuditAsync(connectionString);
}

// ── المرحلة الخامسة: المُرقِّع على أثرٍ قديم ───────────────────────────────
// ‏(CEEF-229) للترقية مساران لا مسارٌ واحد: الهجرات (أعلاه)، و**المُرقِّع اليدويّ** —
// ‏وهو ما يسلكه كلُّ عميلٍ جدولُ هجراته فارغ (DatabaseSchemaService.cs: coreTableExists
// ‏&& appliedCount == 0). وكان المُرقِّع يُضيف الأعمدة الثلاثة **ويقف**: لا ترقيمَ ولا فهرس.
//
// ‏فانقسم النظام قسمَين — قاعدةٌ يمنع فيها الفهرسُ ازدواجَ الترتيب وقاعدةٌ لا يمنعه،
// ‏وصفوفٌ قديمة تبقى أصفاراً فيها بينما تبدأ السلسلةُ فوقها من ١.
//
// ‏فهذه المرحلة تبني تلك الحالة بعينها: تُسقط الفهرس، وتُعيد صفوفاً قديمةً إلى الصفر،
// ‏ثمّ تسأل المُرقِّع: هل رقّمتَ ما قبل السلسلة، وهل أنشأتَ الفهرس؟
static async Task<int> ProvePatcherNumbersLegacyAuditAsync(string connectionString)
{
    await using (var conn = new NpgsqlConnection(connectionString))
    {
        await conn.OpenAsync();


        await using var undo = new NpgsqlCommand(
            "DROP INDEX IF EXISTS \"IX_audit_log_CompanyId_Sequence\"", conn);
        await undo.ExecuteNonQueryAsync();
        // ‏أثرٌ قديمٌ لمنشأةٍ **واحدة** — والوحدةُ هي الشرط: صفّان لمنشأتين لا يتصادمان،
        // ‏وقياسٌ لا يتصادم لا يقيس شيئاً. (وهو بعينه ما أعمى أوّلَ نسخةٍ من بذّار السادسة.)
        await using var seed = new NpgsqlCommand(
            "INSERT INTO companies (\"NameAr\", \"VatRegistrationNumber\", \"Profile\", \"CostingMethod\", " +
            "\"BaseCurrencyCode\", \"FunctionalCurrencyCode\", \"TenantId\", \"OperationalMode\", \"CreatedAtUtc\") " +
            "SELECT 'منشأةُ الأثر', '310123456700003', 'Retail', 'Fifo', 'SAR', 'SAR', gen_random_uuid(), 'Active', now() " +
            "WHERE NOT EXISTS (SELECT 1 FROM companies); " +
            "INSERT INTO audit_log (\"CompanyId\", \"Username\", \"Action\", \"EntityType\", \"EntityKey\", \"Details\", \"OccurredAtUtc\") " +
            "SELECT c.\"Id\", 'admin', 'ترحيل فاتورة', 'SalesInvoice', 'INV-1', '', now() FROM companies c ORDER BY c.\"Id\" LIMIT 1; " +
            "INSERT INTO audit_log (\"CompanyId\", \"Username\", \"Action\", \"EntityType\", \"EntityKey\", \"Details\", \"OccurredAtUtc\") " +
            "SELECT c.\"Id\", 'admin', 'ترحيل فاتورة', 'SalesInvoice', 'INV-2', '', now() FROM companies c ORDER BY c.\"Id\" LIMIT 1;", conn);
        await seed.ExecuteNonQueryAsync();

        await using var zero = new NpgsqlCommand(
            "UPDATE audit_log SET \"Sequence\" = 0 WHERE \"EntryHash\" = ''", conn);
        await zero.ExecuteNonQueryAsync();

        await using var count = new NpgsqlCommand(
            "SELECT COUNT(*) FROM audit_log WHERE \"EntryHash\" = '' AND \"Sequence\" = 0", conn);
        var zeros = Convert.ToInt64(await count.ExecuteScalarAsync());
        Console.WriteLine($"[..] قاعدةُ المُرقِّع: الفهرسُ مُسقَط و{zeros} صفّاً قديماً بترتيبٍ صفر ...");

        if (zeros < 2)
        {
            Console.WriteLine("[FAIL] القياسُ أجوف — لا صفوفَ متصادمةً أصلاً.");
            return 12;
        }
    }

    await using var db = BrqoshDbContextFactory.Create(DatabaseProvider.PostgreSql, connectionString);

    try
    {
        await new BrqoshSchemaUpdater(db).ApplyAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[FAIL] المُرقِّع رمى على أثرٍ قديم: {ex.GetType().Name}: {ex.Message.Split('\n')[0]}");
        return 13;
    }

    await using (var conn = new NpgsqlConnection(connectionString))
    {
        await conn.OpenAsync();

        await using var stillZero = new NpgsqlCommand(
            "SELECT COUNT(*) FROM audit_log WHERE \"EntryHash\" = '' AND \"Sequence\" >= 0", conn);
        if (Convert.ToInt64(await stillZero.ExecuteScalarAsync()) > 0)
        {
            Console.WriteLine("[FAIL] المُرقِّع لم يُرقّم ما قبل السلسلة — صفوفٌ غير مربوطةٍ ما زالت بترتيبٍ غيرِ سالب.");
            Console.WriteLine("       والمتحقّقُ يعدّها دخيلةً، فيصير دفترُ العميل «معبوثاً به» عند أوّل فحص.");
            return 14;
        }

        await using var hasIndex = new NpgsqlCommand(
            "SELECT COUNT(*) FROM pg_indexes WHERE tablename = 'audit_log' AND indexdef LIKE '%UNIQUE%' " +
            "AND indexdef LIKE '%CompanyId%' AND indexdef LIKE '%Sequence%'", conn);
        if (Convert.ToInt64(await hasIndex.ExecuteScalarAsync()) == 0)
        {
            Console.WriteLine("[FAIL] المُرقِّع لم يُنشئ الفهرسَ الفريد — فقاعدةُ العميل تقبل ترتيبَين متطابقَين");
            Console.WriteLine("       بينما تمنعهما القاعدةُ الجديدة. تركيبٌ يعمل وتركيبٌ يسقط، ولا يكشف أحدُهما الآخر.");
            return 15;
        }
    }

    Console.WriteLine("[PASS] المُرقِّع رقّم ما قبل السلسلة في حقبةٍ سالبة وأنشأ الفهرسَ الفريد.");
    Console.WriteLine();

    var duplicates = await ProveNoDuplicateUniqueIndexesAsync(connectionString);
    if (duplicates != 0) return duplicates;
    Console.WriteLine();

    return await ProveMigrationsSurviveRealDataAsync(connectionString);
}



// ── المرحلة السادسة: كلُّ الهجرات على قاعدةٍ **فيها بيانات** ──────────────
// ‏(CEEF-229) المرحلتان الرابعة والخامسة أمسكتا عيباً حقيقياً — هجرةُ سلسلة التدقيق تُضيف
// ‏عموداً بصفرٍ لكلّ الصفوف ثمّ تُنشئ عليه فهرساً فريداً، فتسقط ترقيةُ أيّ قاعدةٍ فيها
// ‏سطرا تدقيق. لكنّهما تقيسان **جدولاً واحداً**، والعيبُ ليس خاصّاً به:
//
//   **كلُّ هجرةٍ تُضيف عموداً بقيمةٍ افتراضية ثمّ تُقيّده تحمل الخطرَ نفسَه** — فريداً،
//   أو غيرَ قابلٍ للعدم، أو مفتاحاً أجنبياً يشير إلى ما لا وجود له في الصفوف القائمة.
//
// ‏والسببُ الجذريّ في **طريقة قياسنا** لا في هجرةٍ بعينها: كلُّ ما نُرحّله قواعدُ **فارغةٌ
// ‏من البيانات**، فلا صفوفَ تتصادم ولا قيدَ يُختبر. وقاعدةُ العميل عكسُ ذلك تماماً.
//
// ‏فهذه المرحلة تُعمّم القياس: تُرحّل إلى أوّل هجرة، ثمّ **تملأ كلّ جدولٍ بصفَّين**، ثمّ
// ‏تُطبّق الهجرات **واحدةً واحدة** — ومَلءُ الجداول يُعاد بين كلّ هجرتين ليشمل ما استحدثته
// ‏السابقة. فأيُّ هجرةٍ تسقط تُسمّى باسمها.
//
// ‏والبذّارُ عامّ لا يعرف النطاق: يقرأ information_schema، ويرتّب الجداول بمفاتيحها
// ‏الأجنبية، ويولّد قيمةً لكلّ عمودٍ إلزاميّ بلا قيمةٍ افتراضية. وما يعجز عن بذره **يُعلَن
// ‏عدداً واسماً** — فبذّارٌ يصمت عمّا فاته يُحوّل تغطيةً ناقصة إلى ادّعاء اكتمال.
static async Task<int> ProveMigrationsSurviveRealDataAsync(string connectionString)
{
    const string DataDb = "brqosh_dataprobe";

    // ‏قاعدةٌ مستقلّة: هذه المرحلة تبذر صفوفاً لا يقرؤها النطاق (قيمٌ عامّةٌ بلا معنى
    // ‏محاسبيّ)، فخلطُها بقاعدة المراحل السابقة يُفسد ما تقيسه تلك.
    var adminCs = new NpgsqlConnectionStringBuilder(connectionString) { Database = "postgres" }.ToString();
    var dataCs = new NpgsqlConnectionStringBuilder(connectionString) { Database = DataDb }.ToString();

    await using (var c = new NpgsqlConnection(adminCs))
    {
        await c.OpenAsync();
        await using (var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {DataDb} WITH (FORCE)", c))
            await drop.ExecuteNonQueryAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {DataDb}", c))
            await create.ExecuteNonQueryAsync();
    }

    await using var db = BrqoshDbContextFactory.Create(DatabaseProvider.PostgreSql, dataCs);
    var migrations = db.Database.GetMigrations().ToList();
    var migrator = db.GetService<IMigrator>();

    Console.WriteLine($"[..] ترحيلٌ متدرّجٌ عبر {migrations.Count} هجرة — وبين كلّ هجرتين تُملأ الجداول ...");

    await migrator.MigrateAsync(migrations[0]);

    var totalSeeded = 0;
    var depth = 0;
    var tableCount = 0;
    var shallow = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    for (var i = 1; i < migrations.Count; i++)
    {
        var (seeded, rowsWanted, tables, short_) = await SeedEveryEmptyTableAsync(dataCs);
        totalSeeded += seeded;
        depth = rowsWanted;
        tableCount = tables;
        foreach (var (table, rows) in short_)
            shallow[table] = rows;

        try
        {
            await migrator.MigrateAsync(migrations[i]);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FAIL] «{migrations[i]}» سقطت على قاعدةٍ فيها بيانات:");
            Console.WriteLine($"       {ex.GetType().Name}: {FirstLine(ex.Message)}");
            Console.WriteLine();
            Console.WriteLine("       الهجرةُ تمرّ على قاعدةٍ فارغة وتسقط على قاعدةٍ فيها صفوف —");
            Console.WriteLine("       وقاعدةُ العميل من الصنف الثاني. التعبئةُ تسبق القيدَ داخل الهجرة نفسِها.");
            return 16;
        }
    }

    if (totalSeeded == 0)
    {
        Console.WriteLine("[FAIL] القياسُ أجوف — لم يُبذَر صفٌّ واحد، فما اختُبر قيدٌ قطّ.");
        return 17;
    }

    Console.WriteLine($"[PASS] الهجرات الـ{migrations.Count} كلُّها تمرّ على قاعدةٍ فيها بيانات — "
                      + $"{totalSeeded} صفّاً مبذوراً، بعمق {depth} صفوفٍ للجدول.");

    // ‏(CEEF-230 · ط0-٢) **تقريرُ النقص على درجتين لا درجةً واحدة.** جدولٌ فيه صفٌّ واحد
    // ‏لا يتصادم أصلاً، فهو **خارج القياس**. وجدولٌ فيه صفّان يتصادم لكنّه لا يكشف
    // ‏«احذف مكرّراً ثمّ قيّد» — فهو **مقيسٌ ناقصُ العمق**. وخلطُ الحالتين في رقمٍ واحد
    // ‏يُخفي أيَّهما وقع.
    var blind = shallow.Where(x => x.Value < 2).Select(x => x.Key).Order(StringComparer.Ordinal).ToList();
    var thin = shallow.Where(x => x.Value >= 2).Select(x => $"{x.Key}({x.Value})")
                      .Order(StringComparer.Ordinal).ToList();

    // ‏(CEEF-230 · ط0-٢) **وتُعلَن التغطيةُ الكاملة إيجاباً لا بالصمت.** تحذيرٌ لا يُطبع
    // ‏لا يفرّق القارئُ بينه وبين تحذيرٍ لا يستطيع أن يُطبع — وهذا بعينه ما يجعل مسباراً
    // ‏معطوباً يبدو سليماً. فالسطرُ التالي يُطبع دائماً.
    Console.WriteLine($"       تغطيةُ العمق: {tableCount - shallow.Count} من {tableCount} جدولاً بلغت {depth} صفوفٍ كاملة.");

    if (blind.Count > 0)
    {
        Console.WriteLine($"       ⚠️ و{blind.Count} جدولاً لم يبلغ صفَّين — فهذه **خارج القياس**، لا تصادمَ فيها:");
        Console.WriteLine($"          {string.Join("، ", blind.Take(12))}{(blind.Count > 12 ? " ..." : "")}");
    }
    if (thin.Count > 0)
    {
        Console.WriteLine($"       ⚠️ و{thin.Count} جدولاً بلغ صفَّين ولم يبلغ {depth} — فالتصادمُ مقيسٌ فيها، "
                          + "و«احذف مكرّراً ثمّ قيّد» ليس كذلك:");
        Console.WriteLine($"          {string.Join("، ", thin.Take(12))}{(thin.Count > 12 ? " ..." : "")}");
    }
    return 0;
}

/// <summary>
/// ‏يملأ كلَّ جدولٍ فارغ بأربعة صفوف. عامٌّ لا يعرف النطاق: يقرأ الأعمدة والمفاتيح الأجنبية من
/// ‏information_schema، ويرتّب الجداول ترتيباً يجعل الأبَ قبل الابن، ويولّد قيمةً لكلّ عمودٍ
/// ‏إلزاميّ بلا قيمةٍ افتراضية. <b>والأعمدةُ ذاتُ القيم الافتراضية تُترك عمداً</b> — فقيمتُها
/// ‏الافتراضية هي بعينها ما يتصادم لاحقاً، وهي موضعُ القياس.
/// </summary>
static async Task<(int Seeded, int Depth, int Tables, Dictionary<string, int> Shallow)>
    SeedEveryEmptyTableAsync(string connectionString)
{
    // ‏(CEEF-230 · ط0-٢) **عمقُ البذر.** صفّان كانا يكشفان «قيّدْ ما هو قائم»، ولا
    // ‏يكشفان **«احذف المكرّرَ ثمّ قيّد»** — وهي أشيعُ شكلٍ لهجرةِ تنظيف: صفّان يبقى
    // ‏منهما واحدٌ فينجح الفهرسُ الفريد، وأربعةٌ يبقى منها ثلاثةٌ فيسقط. ومقيسٌ بطفرة.
    const int SeedRows = 4;

    await using var conn = new NpgsqlConnection(connectionString);
    await conn.OpenAsync();

    var tables = new List<string>();
    await using (var cmd = new NpgsqlCommand(
        "SELECT table_name FROM information_schema.tables " +
        "WHERE table_schema = 'public' AND table_type = 'BASE TABLE' " +
        "AND table_name <> '__EFMigrationsHistory' ORDER BY table_name", conn))
    await using (var r = await cmd.ExecuteReaderAsync())
        while (await r.ReadAsync()) tables.Add(r.GetString(0));

    var columns = new Dictionary<string, List<ColumnInfo>>(StringComparer.OrdinalIgnoreCase);
    await using (var cmd = new NpgsqlCommand(
        "SELECT table_name, column_name, is_nullable, column_default, " +
        "       is_identity, is_generated, character_maximum_length, udt_name, " +
        "       numeric_precision, numeric_scale " +
        "FROM information_schema.columns WHERE table_schema = 'public'", conn))
    await using (var r = await cmd.ExecuteReaderAsync())
        while (await r.ReadAsync())
        {
            var t = r.GetString(0);
            if (!columns.TryGetValue(t, out var list)) columns[t] = list = [];
            list.Add(new ColumnInfo(
                t, r.GetString(1), r.GetString(2) == "YES", !r.IsDBNull(3),
                r.GetString(4) == "YES", r.GetString(5) != "NEVER",
                r.IsDBNull(6) ? null : r.GetInt32(6), r.GetString(7),
                r.IsDBNull(8) ? null : r.GetInt32(8),
                r.IsDBNull(9) ? null : r.GetInt32(9)));
        }

    var foreignKeys = new Dictionary<(string Table, string Column), (string Parent, string ParentColumn)>();
    var dependsOn = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
    await using (var cmd = new NpgsqlCommand(
        "SELECT tc.table_name, kcu.column_name, ccu.table_name, ccu.column_name " +
        "FROM information_schema.table_constraints tc " +
        "JOIN information_schema.key_column_usage kcu " +
        "  ON tc.constraint_name = kcu.constraint_name AND tc.table_schema = kcu.table_schema " +
        "JOIN information_schema.constraint_column_usage ccu " +
        "  ON tc.constraint_name = ccu.constraint_name AND tc.table_schema = ccu.table_schema " +
        "WHERE tc.constraint_type = 'FOREIGN KEY' AND tc.table_schema = 'public'", conn))
    await using (var r = await cmd.ExecuteReaderAsync())
        while (await r.ReadAsync())
        {
            string t = r.GetString(0), c = r.GetString(1), parent = r.GetString(2), pc = r.GetString(3);
            foreignKeys[(t, c)] = (parent, pc);
            if (!dependsOn.TryGetValue(t, out var set))
                dependsOn[t] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.Equals(parent, t, StringComparison.OrdinalIgnoreCase)) set.Add(parent);
        }

    // ‏(CEEF-229) **أعمدةُ التنويع.** أوّلُ نسخةٍ من هذا البذّار نوّعت قيمةَ كلّ عمودٍ بين
    // ‏الصفَّين كي لا يتصادما على قيدِ تفرّد — **فكان ذلك بعينه ما أعماها**. التصادمُ الذي
    // ‏نصطاده يحتاج صفَّين **متشابهَين** في عمود التجميع: سطرا تدقيقٍ لنفس المنشأة، لا
    // ‏لمنشأتين. ومقيسٌ: `audit_log.CompanyId` بلا مفتاحٍ أجنبيّ، فأخذ الصفّان 1 و2 —
    // ‏ومرّت الهجرةُ المعطوبة **خضراء** على مسبارٍ بُني لالتقاطها.
    //
    // ‏فالقاعدة انقلبت: **الأصلُ التشابه**، ولا يُنوَّع إلا ما يفرض قيدُ تفرّدٍ تنويعَه —
    // ‏وعمودٌ واحدٌ من كلّ قيد يكفي.
    var varyColumns = new HashSet<(string Table, string Column)>();
    var uniqueSets = new Dictionary<uint, (string Table, List<string> Columns)>();
    await using (var cmd = new NpgsqlCommand(
        "SELECT i.indexrelid, c.relname, a.attname " +
        "FROM pg_index i " +
        "JOIN pg_class c ON c.oid = i.indrelid " +
        "JOIN pg_namespace n ON n.oid = c.relnamespace " +
        "JOIN pg_attribute a ON a.attrelid = c.oid AND a.attnum = ANY(i.indkey) " +
        "WHERE i.indisunique AND n.nspname = 'public'", conn))
    await using (var r = await cmd.ExecuteReaderAsync())
        while (await r.ReadAsync())
        {
            var oid = (uint)r.GetValue(0);
            if (!uniqueSets.TryGetValue(oid, out var set))
                uniqueSets[oid] = set = (r.GetString(1), []);
            set.Columns.Add(r.GetString(2));
        }

    foreach (var (table, cols) in uniqueSets.Values)
    {
        if (!columns.TryGetValue(table, out var all)) continue;

        // ‏عمودٌ واحدٌ يُنوَّع من كلّ قيد — ويُختار ممّا نبذره فعلاً (لا هويّةً ولا افتراضياً
        // ‏ولا مفتاحاً أجنبياً)، وإلا بقي القيدُ مخروقاً. وترتيبٌ أبجديّ ليكون الاختيارُ حتمياً.
        var candidate = cols.OrderBy(c => c, StringComparer.Ordinal).FirstOrDefault(c =>
            all.Any(x => x.Name == c && !x.IsIdentity && !x.IsGenerated && !x.HasDefault)
            && !foreignKeys.ContainsKey((table, c)));

        if (candidate is not null) varyColumns.Add((table, candidate));
    }

    // ‏ترتيبٌ يضع الأبَ قبل الابن. وحلقةٌ في الرسم تُكسر بأخذ ما بقي كما هو —
    // ‏فالأعمدةُ الرابطة في الحلقة قابلةٌ للعدم بالضرورة، وتُترك فارغة.
    var ordered = new List<string>();
    var placed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    for (var pass = 0; pass < tables.Count && placed.Count < tables.Count; pass++)
        foreach (var t in tables)
        {
            if (placed.Contains(t)) continue;
            if (dependsOn.TryGetValue(t, out var deps)
                && deps.Any(d => tables.Contains(d) && !placed.Contains(d))) continue;
            ordered.Add(t);
            placed.Add(t);
        }
    ordered.AddRange(tables.Where(t => !placed.Contains(t)));

    var seeded = 0;
    var shallow = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    foreach (var table in ordered)
    {
        await using (var count = new NpgsqlCommand($"SELECT COUNT(*) FROM \"{table}\"", conn))
            if (Convert.ToInt64(await count.ExecuteScalarAsync()) > 0) continue;

        if (!columns.TryGetValue(table, out var cols)) continue;

        for (var row = 1; row <= SeedRows; row++)
        {
            var names = new List<string>();
            var values = new List<string>();

            foreach (var col in cols)
            {
                // ‏الهويّةُ والمحسوبُ والافتراضيّ تُترك للقاعدة. **والافتراضيّ عمداً** —
                // ‏فقيمتُه هي ما يتصادم لاحقاً، وهي موضعُ القياس.
                if (col.IsIdentity || col.IsGenerated || col.HasDefault) continue;

                string value;
                if (foreignKeys.TryGetValue((table, col.Name), out var fk))
                {
                    if (string.Equals(fk.Parent, table, StringComparison.OrdinalIgnoreCase)) continue;
                    value = $"(SELECT \"{fk.ParentColumn}\" FROM \"{fk.Parent}\" ORDER BY 1 LIMIT 1)";
                }
                else
                {
                    if (col.IsNullable) continue;
                    value = LiteralFor(col, row, varyColumns.Contains((table, col.Name)));
                }

                names.Add($"\"{col.Name}\"");
                values.Add(value);
            }

            var sql = names.Count == 0
                ? $"INSERT INTO \"{table}\" DEFAULT VALUES"
                : $"INSERT INTO \"{table}\" ({string.Join(", ", names)}) VALUES ({string.Join(", ", values)})";

            try
            {
                await using var insert = new NpgsqlCommand(sql, conn);
                await insert.ExecuteNonQueryAsync();
                seeded++;
            }
            catch (PostgresException)
            {
                break;
            }
        }

        // ‏العدُّ بعد المحاولة لا أثناءها: **جدولٌ فيه صفٌّ واحد لا يتصادم**، فهو خارج
        // ‏القياس تماماً كالفارغ. وحصرُ الإخفاق في «الصفّ الأوّل» كان يُخفي ذلك ويُظهر
        // ‏تغطيةً أوسعَ من الحقّ.
        await using (var after = new NpgsqlCommand($"SELECT COUNT(*) FROM \"{table}\"", conn))
        {
            var rows = Convert.ToInt64(await after.ExecuteScalarAsync());
            if (rows < SeedRows) shallow[table] = (int)rows;
        }
    }

    return (seeded, SeedRows, tables.Count, shallow);
}

/// <summary>
/// ‏(CEEF-230 · ط0-٢) <b>قيمةٌ تختلف بين الأعمدة وتتطابق بين الصفوف.</b>
///
/// <para><b>ما كان:</b> كلُّ رقمٍ مبذورٍ <c>1</c> وكلُّ نصٍّ <c>p1</c>. <b>والواحدُ يبتلع
/// الحساب</b>: <c>كمّية × سعر</c> و<c>كمّية ÷ سعر</c> و<c>كمّية</c> كلُّها واحد — فهجرةُ
/// تعبئةٍ بمعاملٍ خاطئ تمرّ <b>خضراء</b>، وقد قِيس ذلك بطفرة.</para>
///
/// <para><b>والعلاجُ اختلافٌ بين الأعمدة لا بين الصفوف.</b> فالتطابقُ بين الصفوف هو ما
/// يصطاد التصادم — وهو درسُ <c>CEEF-229</c> الذي لا يُنقض — والاختلافُ بين الأعمدة هو ما
/// يصطاد الحساب. <b>ولا يتعارضان</b>: قيمةُ العمود تُشتقّ من اسمه فتثبت عبر صفوفه.</para>
///
/// <para><b>وتنويعٌ صامتٌ أُوقف:</b> <c>now()</c> و<c>gen_random_uuid()</c> كانتا تُعطيان
/// كلَّ صفٍّ قيمةً مختلفة — <b>فالتصادمُ على عمودٍ زمنيّ أو uuid كان مستحيلاً</b>. وهو
/// عينُ العيب الذي أصلحته <c>CEEF-229</c> لبقيّة الأنواع <b>ونجا فيهما</b>، لأنّ التنويعَ
/// جاء من دالّة القاعدة لا من البذّار فلم تره القاعدةُ الجديدة. صارتا حرفيّتين حتميّتين.</para>
///
/// <para>و<paramref name="vary"/> يُرفع للعمود الواحد المختار من كلّ قيدِ تفرّد — <b>وهو
/// وحده ما يختلف بين الصفوف</b>، وبغيره لا يُدرَج صفٌّ ثانٍ أصلاً.</para>
/// </summary>
static string LiteralFor(ColumnInfo col, int row, bool vary)
{
    var seed = Fnv(col.Table + "." + col.Name);
    var bump = vary ? row : 0;
    var n = seed + (uint)bump;

    return col.UdtName switch
    {
        "int2" => ((seed % 90) + 7 + bump).ToString(CultureInfo.InvariantCulture),
        "int4" or "int8" => ((seed % 900) + 13 + bump).ToString(CultureInfo.InvariantCulture),
        "numeric" or "float4" or "float8" or "money" => MoneyLiteral(col, seed, bump),
        "bool" => (vary ? row % 2 == 0 : seed % 2 == 0) ? "true" : "false",
        "uuid" => $"'{n:x8}-0000-4000-8000-{n:x8}0000'",
        "date" => "'" + new DateOnly(2026, 1, 1).AddDays((int)(seed % 300) + bump)
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "'",
        "timestamp" or "timestamptz" => "'" + new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddMinutes((seed % 400_000) + bump)
            .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "+00'",
        "time" or "timetz" => $"'{n % 24:00}:{n % 60:00}:00'",
        "interval" => $"'{(seed % 90) + bump} seconds'",
        "bytea" => $"decode('{n % 256:x2}', 'hex')",
        "json" or "jsonb" => $"'{(seed % 1000) + bump}'",
        _ => Quoted(TextLiteral(col, seed, bump), col.MaxLength),
    };
}

/// <summary>
/// ‏مبلغٌ بشكلٍ محاسبيّ — <b>ولا واحدَ فيه ولا صفر</b>، فهما وحدَهما ما يجعل الضربَ
/// والقسمةَ سواءً. ومحصورٌ في دقّة العمود ومقياسه، وإلا رفضته القاعدةُ فسقط الجدولُ من
/// القياس وهو أسوأُ من قيمةٍ عامّة.
/// </summary>
static string MoneyLiteral(ColumnInfo col, uint seed, int bump)
{
    decimal[] ladder = [3m, 7m, 12.5m, 25m, 48.75m, 125.50m, 340.25m, 999.99m, 1250.75m, 8400m];

    var scale = Math.Clamp(col.Scale ?? 2, 0, 6);
    var whole = Math.Clamp((col.Precision ?? 18) - (col.Scale ?? 2), 1, 9);
    var cap = (decimal)Math.Pow(10, whole) - 1m;

    var value = ladder[(int)(seed % (uint)ladder.Length)];
    if (value > cap) value = Math.Max(cap / 3m, 2m);

    value += bump;
    if (value > cap) value = cap - bump;

    return Math.Round(value, scale, MidpointRounding.AwayFromZero)
        .ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// ‏نصٌّ بشكلِ ما يحمله العمود فعلاً — كودٌ، بريدٌ، هاتفٌ، آيبان، رقمُ ضريبة، عملة.
///
/// <para><b>لأنّ هجرةَ تحويلِ بيانات تقرأ الشكلَ لا الطول</b>: هجرةٌ تستخرج مقطعاً من
/// رقم ضريبةٍ أو تُحلّل كوداً لا يختبرها <c>p1</c> إطلاقاً — تمرّ عليه بلا تحويل.</para>
///
/// <para><b>وهذا استدلالٌ بالاسم، وهو يُخطئ.</b> عمودٌ اسمُه <c>Code</c> وليس كوداً يأخذ
/// شكلَ كود. لكنّ الخطأ هنا في <b>اتّجاهٍ آمن</b>: قيمةٌ أغنى ممّا يلزم، لا أفقر.</para>
/// </summary>
static string TextLiteral(ColumnInfo col, uint seed, int bump)
{
    var n = (seed % 900) + 100 + (uint)bump;
    var name = col.Name.ToLowerInvariant();

    if (name.Contains("email")) return $"probe{n}@example.sa";
    if (name.Contains("iban")) return $"SA{n}0000006080101675";
    if (name.Contains("phone") || name.Contains("mobile")) return $"+9665{n}00000";
    if (name.Contains("currency"))
    {
        string[] codes = ["SAR", "USD", "EUR", "AED", "KWD"];
        return codes[(int)(n % (uint)codes.Length)];
    }
    if (name.Contains("vat") || name.Contains("tax")) return $"3{n}00000000003";
    if (name.Contains("code") || name.Contains("number") || name.Contains("reference"))
        return $"PRB-{n}";
    if (name.Contains("namear")) return $"اسمُ القياس {n}";
    if (name.Contains("name")) return $"Probe Name {n}";
    return $"probe-{n}";
}

/// <summary>
/// ‏بصمةٌ حتميّة (FNV-1a). و<c>string.GetHashCode()</c> <b>لا يصلح</b>: يختلف بين
/// تشغيلٍ وتشغيل، فيصير البذرُ غيرَ قابلٍ للإعادة — ومسبارٌ لا يُعيد نتيجتَه لا يُصدَّق.
/// </summary>
static uint Fnv(string text)
{
    var hash = 2166136261u;
    foreach (var ch in text)
    {
        hash ^= ch;
        hash *= 16777619u;
    }
    return hash;
}

static string Quoted(string raw, int? maxLength)
{
    var text = maxLength is > 0 && raw.Length > maxLength ? raw[..maxLength.Value] : raw;
    return "'" + text.Replace("'", "''") + "'";
}

static string FirstLine(string message)
{
    var index = message.IndexOf('\n');
    return index < 0 ? message : message[..index].TrimEnd('\r');
}

// ── المرحلة السابعة: فهارسُ فريدة مكرَّرة ─────────────────────────────────
// ‏(CEEF-229 · م2-١٠ / D-5) للمخطّط مصدران: النموذجُ يُنشئ `IX_...`، والمُرقِّعُ يُنشئ
// ‏`ix_..._snake` على **الأعمدة نفسِها**. فقاعدةٌ مرّت بالاثنين تحمل الفهرسَ مرّتين.
//
// ‏وليس عطلاً في الأرقام: القيدُ يُفرَض مرّةً واحدة سواء كان الفهرسُ واحداً أو اثنين.
// ‏لكنّ كلَّ إدراجٍ وتحديثٍ يُصان به **شجرتان متطابقتان** بدل واحدة، وكلُّ قراءةٍ للمخطّط
// ‏تُظهر قيداً لا وجودَ له في النموذج — **فيُخفي التكرارُ فهرساً حقيقياً مفقوداً** بين ضجيجه.
//
// ‏والمقيسُ قبل الإصلاح: **٢٠ زوجاً**، لا سبعةً كما قالت الخطة.
static async Task<int> ProveNoDuplicateUniqueIndexesAsync(string connectionString)
{
    await using var conn = new NpgsqlConnection(connectionString);
    await conn.OpenAsync();

    var duplicates = new List<string>();
    await using (var cmd = new NpgsqlCommand(
        "SELECT tablename, regexp_replace(indexdef, '.*USING btree ', '') AS cols, " +
        "       string_agg(indexname, ' | ' ORDER BY indexname) " +
        "FROM pg_indexes " +
        "WHERE schemaname = 'public' AND indexdef LIKE '%UNIQUE%' " +
        "GROUP BY tablename, regexp_replace(indexdef, '.*USING btree ', '') " +
        "HAVING count(*) > 1 " +
        "ORDER BY tablename", conn))
    await using (var r = await cmd.ExecuteReaderAsync())
        while (await r.ReadAsync())
            duplicates.Add($"{r.GetString(0)} {r.GetString(1)} ⟵ {r.GetString(2)}");

    if (duplicates.Count > 0)
    {
        Console.WriteLine($"[FAIL] {duplicates.Count} قيدَ تفرّدٍ مُنشأ مرّتين على الأعمدة نفسِها:");
        foreach (var d in duplicates.Take(8)) Console.WriteLine($"       • {d}");
        if (duplicates.Count > 8) Console.WriteLine($"       ... و{duplicates.Count - 8} غيرها");
        Console.WriteLine();
        Console.WriteLine("       النموذجُ والمُرقِّع يُنشئان الفهرسَ نفسَه باسمين. وكلُّ كتابةٍ تُصان بها");
        Console.WriteLine("       شجرتان بدل واحدة، والمخطّطُ يُظهر قيداً لا وجودَ له في النموذج.");
        return 18;
    }

    Console.WriteLine("[PASS] لا قيدَ تفرّدٍ مُنشأ مرّتين — لكلّ قيدٍ فهرسٌ واحد.");
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

record ColumnInfo(string Table, string Name, bool IsNullable, bool HasDefault,
    bool IsIdentity, bool IsGenerated, int? MaxLength, string UdtName,
    int? Precision, int? Scale);
