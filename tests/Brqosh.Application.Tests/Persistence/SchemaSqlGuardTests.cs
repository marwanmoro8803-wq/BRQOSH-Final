using Brqosh.Infrastructure.Persistence;

namespace Brqosh.Application.Tests.Persistence;

public class SchemaSqlGuardTests
{
    [Theory]
    [InlineData("customers", "ModifiedBy", "character varying(128) NULL")]
    [InlineData("suppliers", "ModifiedBy", "character varying(128) NULL")]
    [InlineData("audit_log", "Reason", "character varying(512) NULL")]
    [InlineData("companies", "NameEn", "character varying(256) NULL")]
    [InlineData("companies", "PrintFontFamily", "character varying(64) NOT NULL DEFAULT 'Tahoma'")]
    public void BuildPgAddColumnSql_accepts_schema_updater_column_types(
        string table, string column, string type)
    {
        var sql = SchemaSqlGuard.BuildPgAddColumnSql(table, column, type);

        Assert.Contains($"ALTER TABLE {table} ADD COLUMN IF NOT EXISTS \"{column}\" {type}", sql);
    }
}
