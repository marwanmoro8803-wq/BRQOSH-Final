using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Operations;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Brqosh.Application.Tests.Persistence;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١٢٩ · <c>data-upgrade-performance#2</c>) <b>الترقيةُ كانت تُهاجر الدفاترَ بلا نسخةٍ قبلها.</b>
///
/// <para>‏إصدارٌ جديدٌ يُقلع على قاعدةٍ فيها هجراتٌ معلَّقة فيُطبّقها فوراً. <b>وأحدثُ نسخةٍ هي نسخةُ الجدول</b> —
/// بنتُ يومٍ أو أسبوع، أو لا نسخةَ إن غابت أدواتُ PostgreSQL. فهجرةٌ تُفسد بياناتٍ لا رجوعَ منها إلّا إلى ما قبل
/// عملِ ذلك اليوم. <b>والرجوعُ نفسُه لم يُجرَّب قطّ.</b></para>
///
/// <para>‏مسارُ الترقية في <see cref="DatabaseSchemaService"/> لـPostgreSQL وحدَه (SQLite يُبنى بـ<c>EnsureCreated</c>).
/// <b>فالقرارُ يُقاس بلا خادمٍ على <see cref="PreUpgradeBackup"/> بنسخةٍ حقيقيّةٍ على SQLite</b>، وموضعُه قبل
/// <c>MigrateAsync</c> بنيويّاً، <b>والرجوعُ كاملاً على PostgreSQL</b> حيث يوجد.</para>
/// </summary>
public sealed class AnUpgradeBacksUpTheBooksFirstTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "brqosh-preupgrade-" + Guid.NewGuid().ToString("N")[..8]);

    public AnUpgradeBacksUpTheBooksFirstTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true); }
        catch (IOException) { /* ‏تنظيفٌ لا يُسقط اختباراً */ }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// ‏<b>القياسُ الحاكم:</b> هجراتٌ معلَّقة ⟵ نسخةٌ حقيقيّةٌ مُتحقَّقٌ منها في مجلّد الترقية، <b>باسمِ أوّلِ هجرةٍ تسبقها</b>،
    /// ويقرؤها المحرّكُ ويجد فيها الدفاترَ نفسَها.
    /// </summary>
    [Fact]
    public async Task PendingMigrations_AreBackedUpFirst_IntoAVerifiedCopy()
    {
        await using var db = await LiveBooksAsync();
        var snapshots = Path.Combine(_folder, "pre-upgrade");

        var path = await new PreUpgradeBackup(db, snapshots)
            .TakeAsync(["20261001000000_Next", "20261002000000_After"], isProduction: true);

        Assert.NotNull(path);
        Assert.True(File.Exists(path), "‏قيل «نُسخت» ولا ملفّ.");
        Assert.Equal(snapshots, Path.GetDirectoryName(path));
        Assert.EndsWith("_before_20261001000000_Next.db", path, StringComparison.Ordinal);

        await new BackupService(db).VerifyAsync(path);
        await using var copy = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, $"Data Source={path}");
        Assert.Equal("منشأةُ ما قبل الترقية", (await copy.Companies.SingleAsync()).NameAr);
    }

    /// <summary>‏<b>ولا هجرةَ معلَّقة ⟵ لا نسخة</b> (شاهدُ ضبط): كلُّ إقلاعٍ عاديٍّ لا ينسخ القاعدةَ كلَّها.</summary>
    [Fact]
    public async Task NothingPending_TakesNoCopy()
    {
        await using var db = await LiveBooksAsync();
        var snapshots = Path.Combine(_folder, "pre-upgrade");

        Assert.Null(await new PreUpgradeBackup(db, snapshots).TakeAsync([], isProduction: true));
        Assert.False(Directory.Exists(snapshots) && Directory.EnumerateFiles(snapshots).Any());
    }

    /// <summary>
    /// ‏<b>وفي الإنتاج نسخةٌ فاشلةٌ تمنع الترقية</b> — برسالةٍ تقول إنّ الدفاترَ لم تُمسّ وما يُفعل. وخارجَه يمضي
    /// (خادمُ اختبارٍ بلا أدوات لا يتوقّف).
    /// </summary>
    [Fact]
    public async Task AFailedCopy_RefusesTheUpgradeInProduction_AndOnlyThere()
    {
        await using var db = await LiveBooksAsync();
        static Task NoTools(string _, CancellationToken __) =>
            throw new AccountingException("لم يُعثر على pg_dump.");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new PreUpgradeBackup(db, _folder, NoTools).TakeAsync(["20261001000000_Next"], isProduction: true));

        Assert.Contains("لم تُرقَّ قاعدةُ البيانات", ex.Message, StringComparison.Ordinal);
        Assert.Contains("لم يُعثر على pg_dump.", ex.Message, StringComparison.Ordinal);
        Assert.Contains("لم يتغيّر شيءٌ في الدفاتر", ex.Message, StringComparison.Ordinal);
        Assert.Contains(PreUpgradeBackup.OptOutVariable, ex.Message, StringComparison.Ordinal);

        Assert.Null(await new PreUpgradeBackup(db, _folder, NoTools)
            .TakeAsync(["20261001000000_Next"], isProduction: false));
    }

    /// <summary>‏<b>والمخرجُ الصريحُ يتجاوز النسخ</b> — ولا يكتب شيئاً، فمن ضبطه أخذ نسخته بنفسه.</summary>
    [Fact]
    public async Task TheExplicitOptOut_SkipsTheCopy()
    {
        await using var db = await LiveBooksAsync();
        var called = false;
        Task Spy(string _, CancellationToken __) { called = true; return Task.CompletedTask; }

        var before = Environment.GetEnvironmentVariable(PreUpgradeBackup.OptOutVariable);
        Environment.SetEnvironmentVariable(PreUpgradeBackup.OptOutVariable, "1");
        try
        {
            Assert.Null(await new PreUpgradeBackup(db, _folder, Spy)
                .TakeAsync(["20261001000000_Next"], isProduction: true));
            Assert.False(called);
        }
        finally
        {
            Environment.SetEnvironmentVariable(PreUpgradeBackup.OptOutVariable, before);
        }
    }

    /// <summary>
    /// ‏<b>والموضع: النسخةُ قبل <c>MigrateAsync</c> في فرع الدفاتر القائمة</b> — بنيويّاً، فالفرعُ لا يُبلَغ بلا
    /// PostgreSQL. ونسخةٌ بعد الهجرة نسخةٌ لما بعد العطل.
    /// </summary>
    [Fact]
    public void TheSchemaService_TakesTheCopyBeforeItMigratesExistingBooks()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(),
            "src", "Brqosh.Infrastructure", "Persistence", "DatabaseSchemaService.cs"));

        var branch = source.IndexOf("await EnsureNotNewerThanThisBuildAsync(ct);", StringComparison.Ordinal);
        Assert.True(branch > 0, "‏فرعُ الدفاتر القائمة لم يُعثر عليه.");

        var take = source.IndexOf(".TakeAsync(", branch, StringComparison.Ordinal);
        var migrate = source.IndexOf("MigrateAsync(ct)", branch, StringComparison.Ordinal);
        Assert.True(take > 0 && migrate > 0 && take < migrate,
            "‏الهجرةُ تسبق النسخةَ في فرع الدفاتر القائمة — أو لا نسخةَ فيه.");
    }

    /// <summary>
    /// ‏<b>والرجوعُ مُجرَّبٌ كاملاً:</b> قاعدةٌ على هجرةٍ سابقة ⟵ الإقلاعُ يرقّيها وينسخها قبل ذلك ⟵ النسخةُ تُستعاد في
    /// قاعدةٍ فارغة ⟵ <b>تعود بسجلّ الهجرات الذي كانت عليه</b>، لا الذي رُقّيت إليه.
    /// </summary>
    [PostgresFact]
    public async Task TheCopy_RestoresTheBooksToTheSchemaBeforeTheUpgrade()
    {
        await using var scope = await PostgresTestDatabase.CreateAsync("pre_upgrade_books");
        var all = scope.Db.Database.GetMigrations().ToList();
        var previous = all[^2];
        await scope.Db.GetService<IMigrator>().MigrateAsync(previous);

        var snapshots = Path.Combine(_folder, "pre-upgrade");
        await new DatabaseSchemaService(scope.Db, new PreUpgradeBackup(scope.Db, snapshots)).EnsureSchemaAsync();

        Assert.Equal(all.Count, (await scope.Db.Database.GetAppliedMigrationsAsync()).Count());
        var copy = Assert.Single(Directory.GetFiles(snapshots, "*.dump"));
        Assert.EndsWith($"_before_{all[^1]}.dump", copy, StringComparison.Ordinal);

        await using var rollback = await PostgresTestDatabase.CreateAsync("pre_upgrade_rollback");
        await new BackupService(rollback.Db, Path.Combine(_folder, "pre-restore")).RestoreAsync(copy);

        var restored = (await rollback.Db.Database.GetAppliedMigrationsAsync()).ToList();
        Assert.Equal(previous, restored[^1]);
        Assert.Equal(all.Count - 1, restored.Count);
    }

    private async Task<BrqoshDbContext> LiveBooksAsync()
    {
        var live = Path.Combine(_folder, "live.db");
        var db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, $"Data Source={live}");
        await db.Database.EnsureCreatedAsync();
        db.Companies.Add(Brqosh.Domain.Companies.Company.Create(
            "منشأةُ ما قبل الترقية", "300000000000003", Brqosh.Domain.Modules.BusinessProfile.General));
        await db.SaveChangesAsync();
        return db;
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("‏جذرُ المستودع لم يُعثر عليه.");
    }
}
