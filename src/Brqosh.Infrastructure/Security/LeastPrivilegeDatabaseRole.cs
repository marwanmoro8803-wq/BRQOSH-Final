using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Brqosh.Infrastructure.Security;

/// <summary>
/// <b>حسابُ التطبيق على PostgreSQL بأقلّ صلاحيّة — حدٌّ في الخادم لا في البرنامج.</b>
///
/// <para><b>ما كان:</b> كلُّ محطّةٍ تحمل اعتمادَ مالك القاعدة: صلاحيّاتُ الأدوار في البرنامج وسلسلةُ تجزئة سجلّ التدقيق
/// تمنعان ما يفعله البرنامج، <b>لا ما يفعله من يأخذ سلسلةَ الاتصال من المحطّة</b> — فيُعدّل الدفاترَ ويحذف سطورَ التدقيق
/// ويُعيد حسابَ السلسلة من أوّلها، ولا مفتاحَ في السلسلة يمنعه.</para>
///
/// <para><b>وما صار:</b> دورٌ يُنشئه المالكُ مرّةً لكلّ قاعدة: <b>قراءةٌ وكتابةٌ على الجداول وتسلسلاتها، ولا تعريفَ للمخطّط</b>
/// (لا إنشاءَ ولا تعديلَ ولا إسقاط)، <b>وسجلُّ التدقيق إضافةٌ فقط</b> — لا تعديلَ ولا حذفَ ولا تفريغ. فالمحطّاتُ تحمل هذا الدور،
/// واعتمادُ المالك عند المسؤول وحدَه للترقيات. <b>والسلسلةُ تصير حدّاً حقيقيّاً بلا مفتاح</b>: من لا يملك الحذفَ لا يُعيد كتابتها.</para>
/// </summary>
public static class LeastPrivilegeDatabaseRole
{
    /// <summary>جدولُ سجلّ التدقيق — إضافةٌ فقط للدور.</summary>
    public const string AuditTable = "audit_log";

    /// <summary>أقلُّ طول لكلمة مرور الدور — تُكتب في ملفّ اتصالٍ على كلّ محطّة.</summary>
    public const int MinimumPasswordLength = 16;

    private static readonly Regex RoleName = new("^[a-z_][a-z0-9_]{2,62}$", RegexOptions.CultureInvariant);

    /// <summary>
    /// ينشئ الدورَ (أو يُحدّث كلمةَ مروره) ويمنحه ما يلزم التطبيقَ وحدَه — <b>باتّصال المالك</b>. يُعاد بلا ضرر.
    /// </summary>
    public static async Task ApplyAsync(string ownerConnectionString, string role, string password, CancellationToken ct = default)
    {
        foreach (var statement in Statements(role, password, new NpgsqlConnectionStringBuilder(ownerConnectionString).Database
                     ?? throw new ArgumentException("سلسلةُ اتصال المالك بلا اسم قاعدة.", nameof(ownerConnectionString))))
        {
            await using var connection = new NpgsqlConnection(ownerConnectionString);
            await connection.OpenAsync(ct);
            await using var command = new NpgsqlCommand(statement, connection);
            await command.ExecuteNonQueryAsync(ct);
        }
    }

    /// <summary>جملُ المنح بالترتيب — <b>والاسمُ والكلمةُ مُتحقَّقٌ منهما قبل أن يدخلا نصَّ SQL</b> (أوامرُ الأدوار لا تقبل وسائط).</summary>
    internal static IReadOnlyList<string> Statements(string role, string password, string database)
    {
        if (!RoleName.IsMatch(role ?? ""))
            throw new ArgumentException("اسمُ الدور: حروفٌ لاتينيّةٌ صغيرةٌ وأرقامٌ وشرطةٌ سفليّة، ٣–٦٣ محرفاً، يبدأ بحرف.", nameof(role));
        if (string.IsNullOrEmpty(password) || password.Length < MinimumPasswordLength)
            throw new ArgumentException($"كلمةُ مرور الدور {MinimumPasswordLength} محرفاً على الأقلّ.", nameof(password));
        if (password.Contains('\0'))
            throw new ArgumentException("كلمةُ مرور الدور تحمل محرفاً غيرَ صالح.", nameof(password));

        var r = $"\"{role}\"";
        var literal = "'" + password.Replace("'", "''", StringComparison.Ordinal) + "'";
        var db = "\"" + database.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        return
        [
            $"DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '{role}') THEN CREATE ROLE {r} LOGIN; END IF; END $$;",
            $"ALTER ROLE {r} WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD {literal};",
            $"GRANT CONNECT ON DATABASE {db} TO {r};",
            "GRANT USAGE ON SCHEMA public TO " + r + ";",
            // ‏**لا تعريفَ للمخطّط** — وقبل PostgreSQL 15 يملك PUBLIC إنشاءً في public فيرثه كلُّ دور.
            "REVOKE CREATE ON SCHEMA public FROM PUBLIC;",
            "REVOKE CREATE ON SCHEMA public FROM " + r + ";",
            "GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO " + r + ";",
            "GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA public TO " + r + ";",
            // ‏وما تُنشئه ترقياتُ المالك لاحقاً يُمنح للدور من نفسه.
            "ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO " + r + ";",
            "ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO " + r + ";",
            // ‏**وسجلُّ التدقيق إضافةٌ فقط.**
            $"REVOKE UPDATE, DELETE, TRUNCATE ON {AuditTable} FROM {r};",
        ];
    }

    /// <summary>
    /// <b>هل يملك الاتّصالُ تعريفَ المخطّط</b> — المالكُ نعم، ودورُ التطبيق لا. وغيرُ PostgreSQL (SQLite للتجربة) نعم.
    /// </summary>
    public static async Task<bool> CanChangeSchemaAsync(DbContext db, CancellationToken ct = default)
    {
        if (!db.Database.IsNpgsql())
            return true;

        var connection = db.Database.GetDbConnection();
        var opened = connection.State != System.Data.ConnectionState.Open;
        if (opened)
            await connection.OpenAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT has_schema_privilege(current_user, 'public', 'CREATE')";
            return await command.ExecuteScalarAsync(ct) is true;
        }
        finally
        {
            if (opened)
                await connection.CloseAsync();
        }
    }
}
