using Brqosh.Infrastructure.Persistence;
using Npgsql;

namespace Brqosh.Application.Tests.TestSupport;

/// <summary>
/// (CEEF-229 · م2-٢) <b>قاعدةُ PostgreSQL للاختبار — منفصلةٌ ومُتلَفةٌ بعد الاستعمال.</b>
///
/// <para><b>ما كان قبلها:</b> اختبارُ PostgreSQL الوحيد في المستودع
/// (<c>PostgreSqlStartupTests</c>) كان يفتح <b>قاعدة المستخدم الحقيقية</b> من
/// <c>%APPDATA%/BRQOSH/database.json</c>، ويُنشئ فيها شركةً ومستخدماً باسم <c>pgadmin</c>.
/// واختبارٌ يكتب في دفاتر صاحبه عطلٌ مهما كان ما يؤكّده.</para>
///
/// <para><b>وهذه تُنشئ قاعدةً باسمٍ مستقلّ وتُسقطها بعد الانتهاء</b> — لا تلمس <c>brqosh</c>
/// إطلاقاً. وكلمةُ المرور تُقرأ من الملفّ نفسه الذي تستعمله المسابر، فلا سرَّ في المستودع.</para>
///
/// <para><b>والتخطّي مرئيّ لا صامت.</b> حين لا يوجد PostgreSQL تُعلَن الاختبارات
/// <c>Skipped</c> في تقرير التشغيل (انظر <see cref="PostgresFactAttribute"/>) بدل أن
/// <c>return</c> بصمتٍ فتُحسب ناجحة. والفرقُ جوهريّ: التخطّي المعلَن يُرى ويُحاسَب، والصامتُ
/// يزيّن العدد. وبضبط <c>BRQOSH_REQUIRE_PG=1</c> يصير غيابُ الخادم <b>فشلاً</b> لا تخطّياً —
/// وهو ما يجب أن تضبطه بوّابةٌ تدّعي أنها تحرس مسار الإنتاج.</para>
/// </summary>
public static class PostgresTestDatabase
{
    /// <summary>متغيّرُ بيئةٍ يحوّل التخطّي إلى فشل — لبوّابةٍ تدّعي حراسة الإنتاج.</summary>
    public const string RequireEnvironmentVariable = "BRQOSH_REQUIRE_PG";

    private const string Host = "localhost";
    private const int Port = 5433;
    private const string User = "postgres";

    private static readonly Lazy<string?> CachedPassword = new(ReadPassword);

    private static string PasswordPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BRQOSH", "pg_password.txt");

    private static string? ReadPassword()
    {
        try
        {
            return File.Exists(PasswordPath) ? File.ReadAllText(PasswordPath).Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>هل يوجد خادمٌ يستجيب فعلاً — لا مجرّد ملفّ كلمة مرور.</summary>
    public static bool IsAvailable => Availability.Value;

    private static readonly Lazy<bool> Availability = new(() =>
    {
        if (CachedPassword.Value is null) return false;
        try
        {
            using var connection = new NpgsqlConnection(AdminConnectionString);
            connection.Open();
            return true;
        }
        catch
        {
            return false;
        }
    });

    /// <summary>سببُ التخطّي بلغةٍ يفهمها قارئُ التقرير — أو فراغٌ إن كان الخادم متاحاً.</summary>
    public static string? SkipReason
    {
        get
        {
            if (IsAvailable) return null;
            if (Environment.GetEnvironmentVariable(RequireEnvironmentVariable) == "1") return null;
            return $"PostgreSQL غير متاح على {Host}:{Port} (أو لا يوجد {PasswordPath}). "
                 + $"اضبط {RequireEnvironmentVariable}=1 ليصير غيابُه فشلاً بدل تخطٍّ.";
        }
    }

    private static string AdminConnectionString =>
        $"Host={Host};Port={Port};Database=postgres;Username={User};Password={CachedPassword.Value}";

    private static string ConnectionStringFor(string database) =>
        $"Host={Host};Port={Port};Database={database};Username={User};Password={CachedPassword.Value}";

    /// <summary>
    /// ينشئ قاعدةً نظيفة باسمٍ مستقلّ ويُعيد سياقاً عليها. المُتلِف يُسقط القاعدة.
    /// ويرمي — لا يتخطّى — إن كان الخادم غائباً، فالتخطّي قرارُ سمة الاختبار لا الجسم.
    /// </summary>
    public static async Task<PostgresScope> CreateAsync(string name)
    {
        if (CachedPassword.Value is null)
            throw new InvalidOperationException(
                $"لا يوجد {PasswordPath} — تعذّر إنشاء قاعدة اختبار PostgreSQL.");

        var database = Sanitize(name);

        await using (var admin = new NpgsqlConnection(AdminConnectionString))
        {
            await admin.OpenAsync();
            await using (var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {database} WITH (FORCE)", admin))
                await drop.ExecuteNonQueryAsync();
            await using (var create = new NpgsqlCommand($"CREATE DATABASE {database}", admin))
                await create.ExecuteNonQueryAsync();
        }

        var db = BrqoshDbContextFactory.Create(DatabaseProvider.PostgreSql, ConnectionStringFor(database));
        return new PostgresScope(db, database, AdminConnectionString);
    }

    /// <summary>
    /// اسمُ قاعدةٍ آمنٌ للتركيب النصّي — وحارسٌ يمنع أن يصير الاسمُ حقناً.
    ///
    /// <para>عامٌّ عمداً: البادئة <c>brqosh_t_</c> هي ما يمنع اختباراً من فتح قاعدة المستخدم
    /// <c>brqosh</c>، وهذا الضمانُ يجب أن يُقاس مباشرةً لا أن يُوثَق.</para>
    /// </summary>
    public static string DatabaseNameFor(string name) => Sanitize(name);

    private static string Sanitize(string name)
    {
        var cleaned = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray()).ToLowerInvariant();
        if (cleaned.Length == 0)
            throw new ArgumentException("اسمُ قاعدة الاختبار فارغٌ بعد التنقية.", nameof(name));

        var database = $"brqosh_t_{cleaned}";
        return database.Length > 60 ? database[..60] : database;
    }

    /// <summary>قاعدةٌ حيّةٌ تُسقَط عند التخلّص — فلا تتراكم قواعدُ الاختبار على الخادم.</summary>
    public sealed class PostgresScope(BrqoshDbContext db, string database, string adminConnectionString)
        : IAsyncDisposable
    {
        public BrqoshDbContext Db { get; } = db;

        /// <summary>
        /// (CEEF-229 · م1-١٤ · ح-٢) <b>سياقٌ مستقلٌّ لكلّ كاتب — واتصالٌ مستقلّ معه.</b>
        ///
        /// <para>وهذا شرطُ صدقِ أيّ قياسِ تزامن: سياقان يتشاركان اتصالاً واحداً <b>يتسلسلان</b>،
        /// فلا يقع السباقُ أصلاً ويمرّ الاختبارُ على نظامٍ مكسور. والقفلُ الاستشاريّ نفسُه
        /// مربوطٌ بالمعاملة، فلا معنى له بلا معاملاتٍ متمايزة.</para>
        /// </summary>
        public BrqoshDbContext OpenIndependentContext() =>
            BrqoshDbContextFactory.Create(DatabaseProvider.PostgreSql, ConnectionStringFor(database));

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            NpgsqlConnection.ClearAllPools();

            try
            {
                await using var admin = new NpgsqlConnection(adminConnectionString);
                await admin.OpenAsync();
                await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {database} WITH (FORCE)", admin);
                await drop.ExecuteNonQueryAsync();
            }
            catch
            {
                // ‏إسقاطُ قاعدةِ اختبارٍ فشل لا يُفشل الاختبار — الاسمُ مستقلّ ويُعاد إنشاؤه بـ DROP IF EXISTS.
            }
        }
    }
}
