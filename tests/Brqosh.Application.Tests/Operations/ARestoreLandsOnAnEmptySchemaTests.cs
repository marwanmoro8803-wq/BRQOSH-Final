using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Operations;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Brqosh.Application.Tests.Operations;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١٣٠ · <c>data-upgrade-performance#1</c>) <b>الاستعادةُ كانت تُجرى فوق القاعدة الحيّة لا في مخطَّطٍ فارغ.</b>
///
/// <para>‏<c>pg_restore --clean</c> <b>يحذف ما في الأرشيف وحدَه، وبلا <c>CASCADE</c></b>. فقاعدةٌ أحدثُ من النسخة — رُقّيت بعدها،
/// وهو عينُ الرجوع عن ترقية — فيها جدولٌ لا يعرفه الأرشيفُ ويعتمد على جدولٍ فيه: <b>فيرفض PostgreSQL حذفَ الجدول المعتمَد
/// عليه</b>، ويمضي <c>pg_restore</c> بلا <c>--exit-on-error</c> ثمّ يخرج بخطأ — <b>والقاعدةُ نصفُ مستعادة</b>. والمُثبَتُ كان
/// الاستعادةَ في قاعدةٍ فارغة وحدَها.</para>
/// </summary>
public sealed class ARestoreLandsOnAnEmptySchemaTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "brqosh-restore-" + Guid.NewGuid().ToString("N")[..8]);

    public ARestoreLandsOnAnEmptySchemaTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try { if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true); }
        catch (IOException) { /* ‏تنظيفٌ لا يُسقط اختباراً */ }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// ‏<b>القياسُ الحاكم:</b> نسخةٌ ⟵ القاعدةُ تكتسب جدولاً أحدثَ يعتمد على <c>companies</c> وصفّاً جديداً ⟵ الاستعادة
    /// ⟵ <b>القاعدةُ هي النسخةُ بالضبط</b>: الجدولُ الأحدثُ غائب، والمنشأةُ الجديدةُ غائبة، وسجلُّ الهجرات سجلُّها.
    /// </summary>
    [PostgresFact]
    public async Task ARestoreOverNewerBooks_LeavesExactlyTheBackup()
    {
        await using var scope = await PostgresTestDatabase.CreateAsync("restore_over_newer");
        await new DatabaseSchemaService(scope.Db).EnsureSchemaAsync();
        await AddCompanyAsync(scope.Db, "منشأةُ النسخة");

        var backup = Path.Combine(_folder, "brqosh_before.dump");
        await new BackupService(scope.Db).CreateAsync(backup);

        // ‏ما يتركه إصدارٌ أحدث: جدولٌ لا يعرفه الأرشيف، ومفتاحٌ أجنبيٌّ يربطه بجدولٍ فيه.
        await scope.Db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE newer_feature (id bigint PRIMARY KEY, company_id bigint REFERENCES companies(\"Id\"))");
        await AddCompanyAsync(scope.Db, "منشأةٌ بعد النسخة");

        await new BackupService(scope.Db, Path.Combine(_folder, "pre-restore")).RestoreAsync(backup);

        await using var after = scope.OpenIndependentContext();
        Assert.Equal(["منشأةُ النسخة"], await after.Companies.Select(c => c.NameAr).ToListAsync());
        Assert.Equal(0, await after.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*)::int AS \"Value\" FROM information_schema.tables "
            + "WHERE table_schema = 'public' AND table_name = 'newer_feature'").SingleAsync());
    }

    /// <summary>
    /// ‏<b>والرجوعُ عن ترقيةٍ ثمّ الإقلاعُ</b>: نسخةٌ على الهجرة السابقة ⟵ ترقية ⟵ استعادةٌ فوقها ⟵ السجلُّ سجلُّ النسخة ⟵
    /// والإقلاعُ يُرقّيها كاملةً من جديد، <b>لا يتعثّر بما بقي من الأحدث</b>.
    /// </summary>
    [PostgresFact]
    public async Task RollingBackAnUpgrade_ThenStarting_MigratesCleanlyAgain()
    {
        await using var scope = await PostgresTestDatabase.CreateAsync("rollback_then_start");
        var all = scope.Db.Database.GetMigrations().ToList();
        await scope.Db.GetService<IMigrator>().MigrateAsync(all[^2]);

        var backup = Path.Combine(_folder, "brqosh_previous.dump");
        await new BackupService(scope.Db).CreateAsync(backup);
        await new DatabaseSchemaService(scope.Db, new PreUpgradeBackup(scope.Db, _folder)).EnsureSchemaAsync();

        await new BackupService(scope.Db, Path.Combine(_folder, "pre-restore")).RestoreAsync(backup);

        await using (var restored = scope.OpenIndependentContext())
            Assert.Equal(all[^2], (await restored.Database.GetAppliedMigrationsAsync()).Last());

        await using var started = scope.OpenIndependentContext();
        await new DatabaseSchemaService(started, new PreUpgradeBackup(started, _folder)).EnsureSchemaAsync();
        Assert.Equal(all, (await started.Database.GetAppliedMigrationsAsync()).ToList());
    }

    /// <summary>
    /// ‏<b>والاستعادةُ تنسخ ما تُتلفه قبل أن تُتلفه</b> — فاستعادةُ الملفّ الخطأ أو سقوطُها في منتصفها لا تُضيّع دفاترَ اليوم.
    /// </summary>
    [PostgresFact]
    public async Task ARestore_FirstCopiesTheBooksItIsAboutToReplace()
    {
        await using var scope = await PostgresTestDatabase.CreateAsync("restore_copies_first");
        await new DatabaseSchemaService(scope.Db).EnsureSchemaAsync();
        var backup = Path.Combine(_folder, "brqosh_empty.dump");
        await new BackupService(scope.Db).CreateAsync(backup);
        await AddCompanyAsync(scope.Db, "دفاترُ اليوم");

        var safety = Path.Combine(_folder, "pre-restore");
        await new BackupService(scope.Db, safety).RestoreAsync(backup);

        var copy = Assert.Single(Directory.GetFiles(safety, "brqosh_*.dump"));
        await using var check = await PostgresTestDatabase.CreateAsync("restore_copies_first_check");
        await new BackupService(check.Db, Path.Combine(_folder, "check-safety")).RestoreAsync(copy);
        await using var books = check.OpenIndependentContext();
        Assert.Equal(["دفاترُ اليوم"], await books.Companies.Select(c => c.NameAr).ToListAsync());
    }

    private static async Task AddCompanyAsync(BrqoshDbContext db, string name)
    {
        db.Companies.Add(Company.Create(name, "300000000000003", BusinessProfile.General));
        await db.SaveChangesAsync();
    }
}
