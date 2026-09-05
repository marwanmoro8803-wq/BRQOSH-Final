using Brqosh.Application.Setup;
using Microsoft.EntityFrameworkCore;
using Brqosh.Infrastructure.Operations;

namespace Brqosh.Infrastructure.Persistence;

/// <summary>
/// تهيئة/ترقية المخطط — يدعم EF Migrations للقواعد الجديدة
/// وEnsureCreated + SchemaUpdater للقواعد القديمة (تطوير/اختبار فقط).
/// في الإنتاج: Migrate + SchemaUpdater — بدون EnsureCreated من الصفر.
/// </summary>
public class DatabaseSchemaService(BrqoshDbContext db)
{
    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        var isRelational = db.Database.IsRelational();
        var isProduction = AppEnvironmentConfig.IsProduction;

        if (!isRelational)
        {
            if (isProduction)
                throw new InvalidOperationException(
                    "وضع الإنتاج لا يدعم مزوّد قاعدة غير علائقي — استخدم PostgreSQL.");

            await db.Database.EnsureCreatedAsync(ct);
            await new BrqoshSchemaUpdater(db).ApplyAsync(ct);
            return;
        }

        // SQLite = تطوير/اختبار فقط، والهجرات مولَّدة لـ PostgreSQL حصراً (ح-2، CEEF-201).
        // تهيئته من النموذج مباشرة بدل تطبيق DDL بأنواع مزوّد آخر.
        if (db.Database.IsSqlite())
        {
            if (isProduction)
                throw new InvalidOperationException(
                    "وضع الإنتاج لا يدعم SQLite — استخدم PostgreSQL.");

            await db.Database.EnsureCreatedAsync(ct);
            await new BrqoshSchemaUpdater(db).ApplyAsync(ct);
            return;
        }

        var historyExists = await MigrationHistoryTableExistsAsync(ct);
        var appliedCount = historyExists ? await AppliedMigrationCountAsync(ct) : 0;
        var coreTableExists = await CoreTableExistsAsync(ct);

        // قاعدة حية أُنشئت سابقاً بـ EnsureCreated/SchemaUpdater:
        // جدول التاريخ قد يكون فارغاً أو غير موجود — لا تُعِد InitialCreate.
        if (coreTableExists && appliedCount == 0)
        {
            await new BrqoshSchemaUpdater(db).ApplyAsync(ct);
            return;
        }

        if (!coreTableExists)
        {
            var pending = await db.Database.GetPendingMigrationsAsync(ct);
            if (pending.Any())
            {
                try
                {
                    await db.Database.MigrateAsync(ct);
                }
                catch (Exception ex) when (!isProduction && IsIgnorableSchemaConflict(ex))
                {
                    await db.Database.EnsureCreatedAsync(ct);
                }
                catch (Exception ex) when (isProduction && IsIgnorableSchemaConflict(ex))
                {
                    // سباق إنشاء أو مخطط جزئي — أكمل بالترقيات التدريجية بدلاً من إيقاف التشغيل.
                    await new BrqoshSchemaUpdater(db).ApplyAsync(ct);
                    return;
                }
                catch (InvalidOperationException) when (!isProduction)
                {
                    await db.Database.EnsureCreatedAsync(ct);
                }
                catch (InvalidOperationException) when (isProduction)
                {
                    throw new InvalidOperationException(
                        "فشل تطبيق EF Migrations في الإنتاج. أصلح سلسلة الهجرات قبل التشغيل — EnsureCreated محظور.");
                }
            }
            else if (isProduction)
            {
                throw new InvalidOperationException(
                    "قاعدة الإنتاج بلا جداول وبلا migrations معلّقة. انشر migrations أولاً أو استعد من نسخة احتياطية.");
            }
            else
            {
                await db.Database.EnsureCreatedAsync(ct);
            }
        }
        else if (historyExists && appliedCount > 0)
        {
            try
            {
                await db.Database.MigrateAsync(ct);
            }
            catch (Exception ex) when (IsIgnorableSchemaConflict(ex) || ex is InvalidOperationException)
            {
                // ‏(CEEF-237 · ت-١١) **الابتلاعُ يُسجَّل — وكان صامتاً تماماً.**
                //
                // ‏سقوطُ `MigrateAsync` هنا يعني أنّ هجرةً لم تُطبَّق ولم يُكتب صفُّ تاريخها،
                // ‏**فتُعاد المحاولةُ عند كلّ إقلاعٍ إلى الأبد**، ولا تُطبَّق أيُّ هجرةٍ بعدها.
                // ‏والمُرقِّعُ يستر الأثرَ فيبدو كلُّ شيءٍ سليماً — حتى يُضاف عمودٌ جديدٌ فلا يصل.
                // ‏فالسببُ يُكتب في سجلّ الأعطال: لا يُوقف الإقلاع، ولا يُنسى.
                CrashLog.Write(ex, "schema-migrate-fallback");
                await new BrqoshSchemaUpdater(db).ApplyAsync(ct);
                return;
            }
        }

        await new BrqoshSchemaUpdater(db).ApplyAsync(ct);
    }

    public static bool IsIgnorableSchemaConflict(Exception ex)
    {
        var text = ex.ToString();
        return text.Contains("already exists", StringComparison.OrdinalIgnoreCase)
               || text.Contains("42P07", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<int> AppliedMigrationCountAsync(CancellationToken ct)
    {
        try
        {
            return await db.Database.SqlQueryRaw<int>(
                    "SELECT COUNT(*) AS \"Value\" FROM \"__EFMigrationsHistory\"")
                .SingleAsync(ct);
        }
        catch
        {
            return 0;
        }
    }

    private async Task<bool> MigrationHistoryTableExistsAsync(CancellationToken ct)
    {
        try
        {
            if (db.Database.IsNpgsql())
            {
                var count = await db.Database.SqlQuery<int>(
                    $"""SELECT COUNT(*) AS "Value" FROM information_schema.tables WHERE table_schema='public' AND table_name='__EFMigrationsHistory'""")
                    .SingleAsync(ct);
                return count > 0;
            }

            var sqlite = await db.Database.SqlQuery<int>(
                $"""SELECT COUNT(*) AS "Value" FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory'""")
                .SingleAsync(ct);
            return sqlite > 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> CoreTableExistsAsync(CancellationToken ct)
    {
        try
        {
            if (db.Database.IsNpgsql())
            {
                var count = await db.Database.SqlQuery<int>(
                    $"""SELECT COUNT(*) AS "Value" FROM information_schema.tables WHERE table_schema='public' AND table_name='companies'""")
                    .SingleAsync(ct);
                return count > 0;
            }

            var sqlite = await db.Database.SqlQuery<int>(
                $"""SELECT COUNT(*) AS "Value" FROM sqlite_master WHERE type='table' AND name='companies'""")
                .SingleAsync(ct);
            return sqlite > 0;
        }
        catch
        {
            return false;
        }
    }
}
