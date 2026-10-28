using Brqosh.Application.Setup;
using Microsoft.EntityFrameworkCore;
using Brqosh.Infrastructure.Operations;

namespace Brqosh.Infrastructure.Persistence;

/// <summary>
/// تهيئة/ترقية المخطط — يدعم EF Migrations للقواعد الجديدة
/// وEnsureCreated + SchemaUpdater للقواعد القديمة (تطوير/اختبار فقط).
/// في الإنتاج: Migrate + SchemaUpdater — بدون EnsureCreated من الصفر.
/// </summary>
public class DatabaseSchemaService(BrqoshDbContext db, PreUpgradeBackup? preUpgradeBackup = null)
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

        // SQLite = تطوير/اختبار فقط، والهجرات مولَّدة لـ PostgreSQL حصراً .
        // تهيئته من النموذج مباشرة بدل تطبيق DDL بأنواع مزوّد آخر.
        if (db.Database.IsSqlite())
        {
            if (isProduction)
                throw new InvalidOperationException(
                    "وضع الإنتاج لا يدعم SQLite — استخدم PostgreSQL.");

            await db.Database.EnsureCreatedAsync(ct);
            await new BrqoshSchemaUpdater(db).ApplyAsync(ct);
            // ‏المُرقِّعُ مجمَّد — وما بعده (جدولاً أو عموداً) يُكمَل من نموذج EF، فقاعدةُ SQLite القائمة لا يفوتها جديد.
            await SchemaBaseline.CompleteSqliteFromModelAsync(db, ct);
            return;
        }

        // ‏ ‏**دورُ التطبيق لا يُعرّف المخطّط** — فلا يُرقَّع ولا يُهاجَر بها؛
        // ‏المخطّطُ الحاليُّ يُقبَل، والناقصُ أو المتأخّرُ يُرفض باسمه: الترقيةُ عملُ المالك.
        if (!await Security.LeastPrivilegeDatabaseRole.CanChangeSchemaAsync(db, ct))
        {
            await EnsureCurrentWithoutChangingItAsync(ct);
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

            // ‏المُرقِّعُ مجمَّدٌ عند `FrozenAtMigration` — فما يُغطّيه يُسجَّل في التاريخ، والأحدثُ يُطبَّق هجرةً
            // ‏(بنسخةٍ قبله كمسار الترقية المعتاد). وسقوطُ الهجرة يُسجَّل ولا يوقف الإقلاع، والإقلاعُ التالي يُعيدها.
            await SchemaBaseline.RecordUpToFreezeAsync(db, ct);
            var newer = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
            if (newer.Count > 0)
            {
                await (preUpgradeBackup ?? new PreUpgradeBackup(db)).TakeAsync(newer, isProduction, ct);
                try
                {
                    await db.Database.MigrateAsync(ct);
                }
                catch (Exception ex) when (IsIgnorableSchemaConflict(ex) || ex is InvalidOperationException)
                {
                    CrashLog.Write(ex, "schema-migrate-after-baseline");
                }
            }
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
            // ‏(ح-٥١) **خارج الـ`try` عمداً** — فالتقاطُ `InvalidOperationException` أدناه كان
            // ‏سيبتلع الرفضَ ويُكمل بالمُرقِّع، وهو عينُ الإقلاع الصامت الذي يُمنع هنا.
            await EnsureNotNewerThanThisBuildAsync(ct);

            // ‏(ح-١٢٩ · data-upgrade-performance#2) **نسخةٌ مُتحقَّقٌ منها قبل أن تُمَسّ الدفاتر** — وفي الإنتاج
            // ‏يمنع فشلُها الترقيةَ. وخارجَ الـ`try` كالحارس فوقه: الالتقاطُ أدناه كان سيبتلع الرفضَ ويُرقّي بلا نسخة.
            var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
            await (preUpgradeBackup ?? new PreUpgradeBackup(db)).TakeAsync(pending, isProduction, ct);

            try
            {
                await db.Database.MigrateAsync(ct);
            }
            catch (Exception ex) when (IsIgnorableSchemaConflict(ex) || ex is InvalidOperationException)
            {
                // ‏ **الابتلاعُ يُسجَّل — وكان صامتاً تماماً.**
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

    /// <summary>
    /// ‏ <b>محطّةٌ بإصدارٍ أقدمَ لا تعمل على
    /// قاعدةٍ رقّاها إصدارٌ أحدث.</b>
    ///
    /// <para>‏المحطّاتُ تتشارك قاعدةً واحدة، <b>و<c>MigrateAsync</c> يتجاهل هجرةً مسجَّلةً لا يعرفها</b> —
    /// فيمضي الإقلاعُ صامتاً والإصدارُ الأقدمُ يكتب بلا ما أُضيف بعده من أعمدةٍ وقواعد. <b>فالرفضُ عند
    /// الإقلاع برسالةٍ تقول ما يُفعل</b>، وسطحُ المكتب يعرضها ويُغلق (<c>App.OnStartup</c>).</para>
    ///
    /// <para>‏<b>والحدُّ المعلَن:</b> قاعدةٌ أُنشئت قبل سجلّ الهجرات (مسارُ المُرقِّع وحدَه، <c>appliedCount == 0</c>)
    /// لا تاريخَ فيها يُقارَن — فلا يبلغها هذا الحارس.</para>
    /// </summary>
    private async Task EnsureNotNewerThanThisBuildAsync(CancellationToken ct)
    {
        var applied = await db.Database.GetAppliedMigrationsAsync(ct);
        var unknown = MigrationsUnknownToThisBuild(applied, db.Database.GetMigrations());
        if (unknown.Count == 0)
            return;

        throw new InvalidOperationException(
            $"قاعدةُ البيانات رُقّيت بإصدارٍ أحدثَ من هذا البرنامج — فيها {unknown.Count} هجرةً لا يعرفها "
            + $"(آخرُها «{unknown[^1]}»). حدِّث البرنامج على هذه المحطّة قبل تشغيله: الإصدارُ الأقدمُ يكتب "
            + "بلا الأعمدة والقواعد التي أُضيفت بعده.");
    }

    /// <summary>‏الهجراتُ المسجَّلةُ في القاعدة ولا يعرفها هذا البناء — مرتَّبةً، فآخرُها أحدثُها.</summary>
    internal static IReadOnlyList<string> MigrationsUnknownToThisBuild(
        IEnumerable<string> applied, IEnumerable<string> known)
    {
        var knownSet = new HashSet<string>(known, StringComparer.Ordinal);
        return applied.Where(m => !knownSet.Contains(m))
            .OrderBy(m => m, StringComparer.Ordinal)
            .ToList();
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

    /// <summary>
    /// (ح-١٥١) <b>دورُ التطبيق يقبل المخطّطَ كما هو</b> — موجوداً وغيرَ متأخّرٍ ولا أحدثَ من البناء؛ وما سوى ذلك يُرفض برسالةٍ تقول
    /// من يُصلحه: <b>الترقيةُ باتّصال المالك</b> (الأداةُ <c>tools/DbRoles</c> أو إقلاعٌ واحدٌ بملفّ اتّصال المالك).
    /// </summary>
    private async Task EnsureCurrentWithoutChangingItAsync(CancellationToken ct)
    {
        if (!await CoreTableExistsAsync(ct))
            throw new InvalidOperationException(
                "هذه القاعدة بلا دفاتر، واتّصالُ هذه المحطّة بدورٍ لا يُنشئ المخطّط — أنشئ الدفاترَ بإقلاعٍ أوّلَ باتّصال مالك القاعدة.");

        await EnsureNotNewerThanThisBuildAsync(ct);
        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
        if (pending.Count > 0)
            throw new InvalidOperationException(
                $"للقاعدة {pending.Count} ترقيةً معلّقةً لهذا الإصدار، واتّصالُ هذه المحطّة بدورٍ لا يُعدّل المخطّط — "
                + "شغّل الترقيةَ باتّصال مالك القاعدة (tools/DbRoles upgrade) ثمّ أعد تشغيل البرنامج.");
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
