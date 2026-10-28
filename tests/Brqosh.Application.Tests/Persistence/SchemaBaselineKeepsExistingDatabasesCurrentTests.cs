using Brqosh.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Brqosh.Application.Tests.Persistence;

/// <summary>
/// ‏تجميدُ المُرقِّع لا يحرم القواعدَ القائمة من المخطّط الجديد: SQLite يُكمَل من نموذج EF،
/// ‏وPostgreSQL بلا تاريخ هجراتٍ يُسجَّل له التاريخُ حتى نقطة التجميد فتصله الهجراتُ الأحدث.
/// </summary>
public class SchemaBaselineKeepsExistingDatabasesCurrentTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public SchemaBaselineKeepsExistingDatabasesCurrentTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>().UseSqlite(_connection).Options);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private List<string> Columns(string table)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info(\"{table}\")";
        using var r = cmd.ExecuteReader();
        var cols = new List<string>();
        while (r.Read()) cols.Add(r.GetString(1));
        return cols;
    }

    private void Exec(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public async Task AnOldSqliteDatabase_GetsMissingTablesAndColumnsFromTheModel()
    {
        await _db.Database.EnsureCreatedAsync();
        // ‏قاعدةٌ «قديمة»: عمودان وجدولٌ أُضيفت بعد إنشائها.
        Exec("ALTER TABLE sales_orders DROP COLUMN CurrencyCode");
        Exec("ALTER TABLE delivery_notes DROP COLUMN JournalEntryId");
        Exec("DROP TABLE stock_reservations");
        Assert.DoesNotContain("CurrencyCode", Columns("sales_orders"));

        var added = await SchemaBaseline.CompleteSqliteFromModelAsync(_db);

        Assert.Equal(3, added);
        Assert.Contains("CurrencyCode", Columns("sales_orders"));
        Assert.Contains("JournalEntryId", Columns("delivery_notes"));
        Assert.NotEmpty(Columns("stock_reservations"));
        Assert.Equal(0, await SchemaBaseline.CompleteSqliteFromModelAsync(_db));   // ‏ولا شيءَ ثانيةً
        Assert.Equal(0, await _db.SalesOrders.CountAsync());                          // ‏والنموذجُ يقرأ الجدول
    }

    [Fact]
    public async Task ADatabaseWithoutMigrationHistory_IsBaselinedUpToTheFreeze_AndOnlyNewerMigrationsStayPending()
    {
        await _db.Database.EnsureCreatedAsync();
        var all = _db.GetService<IMigrationsAssembly>().Migrations.Keys.ToList();
        var covered = all.Count(id => string.CompareOrdinal(id, BrqoshSchemaUpdater.FrozenAtMigration) <= 0);

        Assert.Equal(covered, await SchemaBaseline.RecordUpToFreezeAsync(_db));
        Assert.Equal(0, await SchemaBaseline.RecordUpToFreezeAsync(_db));
        var pending = (await _db.Database.GetPendingMigrationsAsync()).ToList();
        Assert.All(pending, id => Assert.True(string.CompareOrdinal(id, BrqoshSchemaUpdater.FrozenAtMigration) > 0));
        Assert.Equal(all.Count - covered, pending.Count);
    }
}
