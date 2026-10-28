using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;

namespace Brqosh.Infrastructure.Persistence;

/// <summary>
/// ‏ما يجعل تجميدَ <see cref="BrqoshSchemaUpdater"/> آمناً للقواعد القائمة:
/// <list type="bullet">
/// <item>‏<b>PostgreSQL بلا تاريخ هجرات</b> (أُنشئت بـEnsureCreated/المُرقِّع): بعد المُرقِّع تُسجَّل الهجراتُ حتى نقطة التجميد
/// ‏مطبَّقةً — فالمُرقِّعُ يُغطّيها — ثمّ تُطبَّق الأحدثُ بـ<c>MigrateAsync</c>.</item>
/// <item>‏<b>SQLite</b> (هجراتُ EF مولَّدةٌ لـPostgreSQL فلا تُطبَّق عليه): بعد المُرقِّع يُكمَل الناقصُ من نموذج EF نفسِه —
/// ‏جداولُ كاملةٌ بفهارسها، وأعمدةٌ بـ<c>ADD COLUMN</c> وقيمةٍ افتراضيّة.</item>
/// </list>
/// </summary>
public static class SchemaBaseline
{
    /// <summary>‏يسجّل في تاريخ الهجرات كلَّ هجرةٍ حتى <see cref="BrqoshSchemaUpdater.FrozenAtMigration"/> لم تُسجَّل. يُعيد عددَ ما سُجِّل.</summary>
    public static async Task<int> RecordUpToFreezeAsync(BrqoshDbContext db, CancellationToken ct = default)
    {
        var history = db.GetService<IHistoryRepository>();
        await history.CreateIfNotExistsAsync(ct);
        var applied = (await history.GetAppliedMigrationsAsync(ct)).Select(r => r.MigrationId).ToHashSet(StringComparer.Ordinal);
        var toRecord = db.GetService<IMigrationsAssembly>().Migrations.Keys
            .Where(id => string.CompareOrdinal(id, BrqoshSchemaUpdater.FrozenAtMigration) <= 0 && !applied.Contains(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
        var version = ProductInfo.GetVersion();
        foreach (var id in toRecord)
            await ExecuteAsync(db, history.GetInsertScript(new HistoryRow(id, version)), ct);
        return toRecord.Count;
    }

    /// <summary>‏يُكمل قاعدةَ SQLite قائمةً من نموذج EF: الجداولُ الناقصة بفهارسها، والأعمدةُ الناقصة. يُعيد عددَ ما أُضيف.</summary>
    public static async Task<int> CompleteSqliteFromModelAsync(BrqoshDbContext db, CancellationToken ct = default)
    {
        if (!db.Database.IsSqlite())
            return 0;

        await db.Database.OpenConnectionAsync(ct);
        try
        {
            var conn = db.Database.GetDbConnection();
            var tables = (await QueryAsync(conn, "SELECT name FROM sqlite_master WHERE type='table'", 0, ct))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var added = 0;

            var statements = Regex.Split(db.Database.GenerateCreateScript(), @";\s*\r?\n")
                .Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            var newTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var sql in statements)
            {
                var m = Regex.Match(sql, @"^CREATE TABLE ""(\w+)""");
                if (m.Success && !tables.Contains(m.Groups[1].Value))
                {
                    await ExecuteAsync(db, sql, ct);
                    newTables.Add(m.Groups[1].Value);
                    added++;
                }
            }
            foreach (var sql in statements)
            {
                var m = Regex.Match(sql, @"^CREATE (?:UNIQUE )?INDEX ""\w+"" ON ""(\w+)""");
                if (m.Success && newTables.Contains(m.Groups[1].Value))
                    await ExecuteAsync(db, sql, ct);
            }

            var columnsByTable = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var entity in db.Model.GetEntityTypes())
            {
                var table = entity.GetTableName();
                if (table is null || newTables.Contains(table) || !tables.Contains(table))
                    continue;
                var store = StoreObjectIdentifier.Table(table, entity.GetSchema());
                if (!columnsByTable.TryGetValue(table, out var columns))
                    columnsByTable[table] = columns = (await QueryAsync(conn, $"PRAGMA table_info(\"{table}\")", 1, ct))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var property in entity.GetProperties())
                {
                    var column = property.GetColumnName(store);
                    if (column is null || columns.Contains(column))
                        continue;
                    var ddl = AddColumnSql(property, store, table, column);
                    if (ddl is null)
                        continue;   // ‏عمودٌ إلزاميٌّ بلا قيمةٍ افتراضيّةٍ ممكنة — لا يُضاف بلا بيانات
                    await ExecuteAsync(db, ddl, ct);
                    columns.Add(column);
                    added++;
                }
            }
            return added;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static string? AddColumnSql(IProperty property, StoreObjectIdentifier store, string table, string column)
    {
        var type = property.GetColumnType(store);
        var nullable = property.IsColumnNullable(store);
        string? defaultSql = null;
        if (property.GetDefaultValueSql(store) is string sqlDefault)
            defaultSql = $"({sqlDefault})";
        else
        {
            var value = property.GetDefaultValue(store);
            if (value is null && !nullable)
            {
                var clr = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                try { value = clr == typeof(string) ? "" : clr.IsValueType ? Activator.CreateInstance(clr) : null; }
                catch { value = null; }
            }
            if (value is not null)
                defaultSql = property.GetRelationalTypeMapping().GenerateSqlLiteral(value);
        }
        if (!nullable && defaultSql is null)
            return null;
        return $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {type}"
               + (nullable ? " NULL" : " NOT NULL")
               + (defaultSql is null ? "" : $" DEFAULT {defaultSql}");
    }

    private static async Task<List<string>> QueryAsync(DbConnection conn, string sql, int ordinal, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var result = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add(reader.GetString(ordinal));
        return result;
    }

    private static async Task ExecuteAsync(BrqoshDbContext db, string sql, CancellationToken ct)
    {
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        if (cmd.Connection!.State != System.Data.ConnectionState.Open)
            await cmd.Connection.OpenAsync(ct);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
