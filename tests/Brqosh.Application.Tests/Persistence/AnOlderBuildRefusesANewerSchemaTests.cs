using Brqosh.Application.Tests.TestSupport;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Persistence;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-٥١ · <c>data-upgrade-performance#7</c>) <b>محطّةٌ بإصدارٍ أقدمَ لا تعمل على قاعدةٍ
/// رقّاها إصدارٌ أحدث — وكانت تعمل.</b>
///
/// <para>‏المحطّاتُ تتشارك قاعدةَ PostgreSQL واحدة. <b>فمحطّةٌ حُدِّثت ترقّي المخطَّط، وأخرى لم تُحدَّث
/// تُقلع بعدها</b>: <c>MigrateAsync</c> يطبّق ما يعرفه وحده <b>ويتجاهل ما سُجّل ولا يعرفه</b> — فيمضي
/// الإقلاعُ صامتاً، والإصدارُ الأقدمُ يكتب بلا الأعمدة والقواعد التي أُضيفت بعده. <b>أوّلُ من يعلم
/// هو المحاسب</b>، حين يجد مستنداتٍ ناقصةً لا يعرف من كتبها.</para>
/// </summary>
public class AnOlderBuildRefusesANewerSchemaTests
{
    private const string FromTheFuture = "29991231235959_FromANewerBuild";

    [PostgresFact]
    public async Task AnOlderBuild_RefusesASchemaANewerBuildMigrated()
    {
        await using var scope = await PostgresTestDatabase.CreateAsync("newer_schema");
        await new DatabaseSchemaService(scope.Db).EnsureSchemaAsync();

        // ‏ما يتركه إصدارٌ أحدثُ في سجلّ الهجرات: صفٌّ لا يعرفه هذا البناء.
        await scope.Db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") "
            + $"VALUES ('{FromTheFuture}', '9.9.9')");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new DatabaseSchemaService(scope.Db).EnsureSchemaAsync());

        // ‏**ترتيبيٌّ لا ثقافيّ**: الثقافيُّ يعدّ «ثَ» محرفاً واحداً فلا يجد «أحدث» في «أحدثَ».
        Assert.Contains(FromTheFuture, ex.Message, StringComparison.Ordinal);
        Assert.Contains("أحدث", ex.Message, StringComparison.Ordinal);
        Assert.Contains("حدِّث البرنامج", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// ‏<b>والمقارنةُ نفسُها تُقاس بلا خادم</b> — فتبقى مقيسةً حيث لا PostgreSQL. <b>وقاعدةٌ أقدمُ من
    /// البناء</b> (هجراتٌ معلَّقة) <b>ليست «أحدث»</b>: تلك يُرقّيها <c>MigrateAsync</c> كما كان.
    /// </summary>
    [Fact]
    public void OnlyMigrationsThisBuildDoesNotKnow_AreReported_InOrder()
    {
        string[] known = ["20260101000000_A", "20260201000000_B"];

        Assert.Empty(DatabaseSchemaService.MigrationsUnknownToThisBuild(["20260101000000_A"], known));
        Assert.Empty(DatabaseSchemaService.MigrationsUnknownToThisBuild(known, known));
        Assert.Equal(["29990101000000_Y", "29991231000000_Z"],
            DatabaseSchemaService.MigrationsUnknownToThisBuild(
                ["20260101000000_A", "29991231000000_Z", "29990101000000_Y"], known));
    }

    /// <summary>‏<b>وإقلاعُ البناء نفسِه على مخطَّطه يمضي</b> (شاهدُ ضبط) — فالحارسُ لا يرفض كلَّ قاعدةٍ مُهاجَرة.</summary>
    [PostgresFact]
    public async Task TheSameBuild_StartsAgainAgainstItsOwnSchema()
    {
        await using var scope = await PostgresTestDatabase.CreateAsync("same_schema");
        await new DatabaseSchemaService(scope.Db).EnsureSchemaAsync();

        await new DatabaseSchemaService(scope.Db).EnsureSchemaAsync();

        var applied = await scope.Db.Database.GetAppliedMigrationsAsync();
        Assert.Equal(scope.Db.Database.GetMigrations().Count(), applied.Count());
    }
}
