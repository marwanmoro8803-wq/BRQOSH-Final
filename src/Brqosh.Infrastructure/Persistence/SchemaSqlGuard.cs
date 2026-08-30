using System.Text.RegularExpressions;

namespace Brqosh.Infrastructure.Persistence;

/// <summary>
/// تحقق من أسماء الجداول/الأعمدة قبل بناء أوامر DDL — قيم ثابتة داخلية فقط.
/// </summary>
internal static class SchemaSqlGuard
{
    private static readonly Regex TableNamePattern = new(@"^[a-z][a-z0-9_]*$", RegexOptions.Compiled);
    private static readonly Regex ColumnNamePattern = new(@"^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.Compiled);

    private static readonly HashSet<string> AllowedPgTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "BIGINT NULL",
        "UUID NULL",
        "UUID NOT NULL DEFAULT gen_random_uuid()",
        "TIMESTAMPTZ NULL",
        // (O-21) تاريخ إلغاء المستندات الضريبية.
        "DATE NULL",
        "character varying(16) NULL",
        "character varying(3) NOT NULL DEFAULT 'SAR'",
        "character varying(32) NOT NULL DEFAULT 'WeightedAverage'",
        "character varying(64) NOT NULL DEFAULT ''",
        "character varying(512) NOT NULL DEFAULT ''",
        "character varying(512) NULL",
        "character varying(16) NOT NULL DEFAULT 'Active'",
        // (CEEF-207) فئة الضريبة لأسطر المستندات.
        "character varying(16) NOT NULL DEFAULT 'Standard'",
        // (O-19) أعمدة سلسلة تجزئة سجل التدقيق.
        "BIGINT NOT NULL DEFAULT 0",
        "character varying(64) NOT NULL DEFAULT ''",
        "character varying(64) NULL",
        "character varying(128) NULL",
        "character varying(256) NULL",
        "character varying(64) NOT NULL DEFAULT 'Tahoma'",
        // (CEEF-229 · Z-10) هوية المنشأة النظامية — نصوص غير قابلة للعدم، فارغُها يعني «لم يُدخَل».
        "character varying(16) NOT NULL DEFAULT ''",
        "character varying(128) NOT NULL DEFAULT ''",
        "character varying(256) NOT NULL DEFAULT ''",
        "INTEGER NOT NULL DEFAULT 0",
        "BOOLEAN NOT NULL DEFAULT false",
        // (CEEF-229 · م1-١ب الشقّ الثاني) سببُ الإعفاء — نصٌّ غيرُ محدود الطول كما يُعلنه
        // النموذج (‏string?‎ بلا HasMaxLength ⟵ text). ولو حُدَّ هنا وحده لاختلف عن النموذج
        // ولأسقط مسبارَ تغطية المخطّط بفارق نوع.
        "text NULL",
        // (CEEF-229 · م2-٢٤) لحظةُ الترحيل.
        "timestamp with time zone NULL",
        "NUMERIC(18,2) NOT NULL DEFAULT 0",
    };

    private static readonly HashSet<string> AllowedSqliteTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "INTEGER NULL",
        "INTEGER NOT NULL",
        "INTEGER NOT NULL DEFAULT 0",
        "TEXT NULL",
        "TEXT NOT NULL DEFAULT 'SAR'",
        "TEXT NOT NULL DEFAULT 'Tahoma'",
        "TEXT NOT NULL DEFAULT 'Active'",
        // (CEEF-207) فئة الضريبة لأسطر المستندات — قيمة ثابتة ضمن قائمة السماح.
        "TEXT NOT NULL DEFAULT 'Standard'",
        // (O-19) أعمدة سلسلة تجزئة سجل التدقيق.
        "TEXT NOT NULL DEFAULT ''",
        "REAL NULL",
        "REAL NOT NULL DEFAULT 0",
    };

    public static void ValidateTable(string table)
    {
        if (!TableNamePattern.IsMatch(table))
            throw new ArgumentException($"Invalid table identifier: {table}", nameof(table));
    }

    public static void ValidateColumn(string column)
    {
        if (!ColumnNamePattern.IsMatch(column))
            throw new ArgumentException($"Invalid column identifier: {column}", nameof(column));
    }

    public static void ValidatePgType(string type)
    {
        if (!AllowedPgTypes.Contains(type))
            throw new ArgumentException($"Unsupported PostgreSQL column type: {type}", nameof(type));
    }

    public static void ValidateSqliteType(string type)
    {
        if (!AllowedSqliteTypes.Contains(type))
            throw new ArgumentException($"Unsupported SQLite column type: {type}", nameof(type));
    }

    public static string BuildPgAddColumnSql(string table, string column, string type)
    {
        ValidateTable(table);
        ValidateColumn(column);
        ValidatePgType(type);
        return $"""ALTER TABLE {table} ADD COLUMN IF NOT EXISTS "{column}" {type};""";
    }

    public static string BuildSqliteAddColumnSql(string table, string column, string type)
    {
        ValidateTable(table);
        ValidateColumn(column);
        ValidateSqliteType(type);
        return $"ALTER TABLE {table} ADD COLUMN {column} {type};";
    }
}
