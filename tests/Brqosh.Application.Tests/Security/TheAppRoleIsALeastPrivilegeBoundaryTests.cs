using System.Security.Cryptography;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Brqosh.Application.Tests.Security;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٥١ · <c>security-licensing-tenancy#5</c>) <b>دورُ التطبيق حدٌّ في الخادم.</b>
///
/// <para><b>ما كان:</b> كلُّ محطّةٍ بمالك القاعدة — فصلاحيّاتُ البرنامج وسلسلةُ التدقيق تمنعان البرنامجَ لا حاملَ سلسلة الاتصال.
/// وهذه الاختباراتُ على PostgreSQL حقيقيّ: الدورُ يُنشأ ويُقاس بما يرفضه الخادمُ نفسُه (<c>42501</c>).</para>
/// </summary>
public class TheAppRoleIsALeastPrivilegeBoundaryTests
{
    private const string InsufficientPrivilege = "42501";

    /// <summary>
    /// <b>القياسُ الحاكم: الدورُ يُقلع ويكتب الدفاترَ والتدقيق، ولا يعدّل سطرَ تدقيقٍ ولا يحذفه ولا يُفرّغه ولا يعرّف المخطّط.</b>
    /// </summary>
    [PostgresFact]
    public async Task TheAppRole_BootsAndWrites_ButCannotRewriteTheAuditLogOrTheSchema()
    {
        await using var scope = await PostgresTestDatabase.CreateAsync("leastpriv");
        TestActor.Administrator();
        var (role, password) = NewRole();
        try
        {
            await new DatabaseBootstrapService(scope.Db).InitializeAsync("منشأة الحدّ", "300000000000151", BusinessProfile.General);
            await LeastPrivilegeDatabaseRole.ApplyAsync(scope.ConnectionString, role, password);

            await using var app = BrqoshDbContextFactory.Create(DatabaseProvider.PostgreSql, As(scope.ConnectionString, role, password));

            Assert.True(await LeastPrivilegeDatabaseRole.CanChangeSchemaAsync(scope.Db));
            Assert.False(await LeastPrivilegeDatabaseRole.CanChangeSchemaAsync(app));

            // ‏يُقلع بلا ترقيعٍ ولا هجرة، ويكتب.
            var booted = await new DatabaseBootstrapService(app).InitializeAsync("منشأة الحدّ", "300000000000151", BusinessProfile.General);
            await new AuditService(app).LogAsync(booted.CompanyId, "admin", "اختبار الحدّ", "Test", "1", "سطرٌ من دور التطبيق");
            Assert.True(await app.AuditLogEntries.AnyAsync(a => a.Action == "اختبار الحدّ"));

            foreach (var sql in new[]
                     {
                         """UPDATE audit_log SET "Action" = 'مُزوَّر'""",
                         "DELETE FROM audit_log",
                         "TRUNCATE audit_log",
                         "CREATE TABLE intruder (id int)",
                         """ALTER TABLE accounts ADD COLUMN intruder int""",
                         "DROP TABLE journal_entries",
                     })
            {
                var ex = await Assert.ThrowsAnyAsync<PostgresException>(() => app.Database.ExecuteSqlRawAsync(sql));
                Assert.True(ex.SqlState == InsufficientPrivilege || ex.SqlState == "42501",
                    $"«{sql}» رُفض بـ{ex.SqlState} لا بنقص الصلاحيّة.");
            }
        }
        finally
        {
            TestActor.None();
            await DropRoleAsync(scope.ConnectionString, role);
        }
    }

    /// <summary>
    /// <b>وترقيةٌ معلّقةٌ تُرفض باسمها بدور التطبيق</b> — لا يحاول هجرةً تسقط في منتصفها، ويقول إنّ الترقيةَ عملُ المالك.
    /// </summary>
    [PostgresFact]
    public async Task APendingUpgrade_IsRefusedByName_UnderTheAppRole()
    {
        await using var scope = await PostgresTestDatabase.CreateAsync("leastprivpending");
        TestActor.Administrator();
        var (role, password) = NewRole();
        try
        {
            await new DatabaseBootstrapService(scope.Db).InitializeAsync("منشأة الحدّ", "300000000000151", BusinessProfile.General);
            await LeastPrivilegeDatabaseRole.ApplyAsync(scope.ConnectionString, role, password);
            var last = scope.Db.Database.GetMigrations().Last();
            await scope.Db.Database.ExecuteSqlRawAsync("""DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = {0}""", last);

            await using var app = BrqoshDbContextFactory.Create(DatabaseProvider.PostgreSql, As(scope.ConnectionString, role, password));
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new DatabaseBootstrapService(app).InitializeAsync("منشأة الحدّ", "300000000000151", BusinessProfile.General));

            Assert.Contains("ترقيةً معلّقة", ex.Message, StringComparison.Ordinal);
            Assert.Contains("مالك القاعدة", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            TestActor.None();
            await DropRoleAsync(scope.ConnectionString, role);
        }
    }

    [Theory]
    [InlineData("App")]
    [InlineData("a")]
    [InlineData("brq-app")]
    [InlineData("x; DROP ROLE postgres")]
    public void AnUnsafeRoleName_IsRefusedBeforeAnySql(string role)
    {
        Assert.Throws<ArgumentException>(() => LeastPrivilegeDatabaseRole.Statements(role, new string('p', 20), "db"));
    }

    [Fact]
    public void AShortPassword_IsRefused_AndAQuoteInThePasswordIsEscaped()
    {
        Assert.Throws<ArgumentException>(() => LeastPrivilegeDatabaseRole.Statements("brq_app", "short", "db"));

        var statements = LeastPrivilegeDatabaseRole.Statements("brq_app", "it's-a-long-password!", "db");
        Assert.Contains(statements, s => s.Contains("PASSWORD 'it''s-a-long-password!'", StringComparison.Ordinal));
        Assert.Contains(statements, s => s == "REVOKE UPDATE, DELETE, TRUNCATE ON audit_log FROM \"brq_app\";");
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private static (string Role, string Password) NewRole() =>
        ("brq_app_" + Guid.NewGuid().ToString("N")[..10], Convert.ToHexString(RandomNumberGenerator.GetBytes(16)));

    private static string As(string ownerConnection, string role, string password) =>
        new NpgsqlConnectionStringBuilder(ownerConnection) { Username = role, Password = password, Pooling = false }.ConnectionString;

    /// <summary>الأدوارُ على مستوى العنقود لا القاعدة — فتُنزع منحُها ثمّ تُسقط قبل إسقاط قاعدة الاختبار.</summary>
    private static async Task DropRoleAsync(string ownerConnection, string role)
    {
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(ownerConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"DO $$ BEGIN IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '{role}') THEN "
            + $"EXECUTE 'DROP OWNED BY \"{role}\"'; EXECUTE 'DROP ROLE \"{role}\"'; END IF; END $$;", connection);
        await command.ExecuteNonQueryAsync();
    }
}
