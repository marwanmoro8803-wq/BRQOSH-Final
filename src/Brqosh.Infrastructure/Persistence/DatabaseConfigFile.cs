using System.Security.Cryptography;
using System.Text.Json;
using Brqosh.Infrastructure.Security;
using Npgsql;

namespace Brqosh.Infrastructure.Persistence;

/// <summary>سطرٌ من <c>database.json</c> بعد فكّ الحماية — المزوّدُ وسلسلةُ الاتصال صريحةً.</summary>
public sealed record DatabaseConfigEntry(string Provider, string ConnectionString);

/// <summary>
/// (CEEF-236 · ت-٣) <b>ملفُّ الاتصال <c>%APPDATA%\BRQOSH\database.json</c> — يُكتب من التطبيق لا باليد.</b>
///
/// <para><b>ما كان:</b> في الإنتاج يرمي <c>AppServices.LoadDatabaseConfig</c> إن غاب الملفُّ،
/// <b>ولا شيءَ في المنتج يكتبه</b> — الكاتبون الوحيدون سكربتاتُ اختبار. فأوّلُ إقلاعٍ إنتاجيٍّ
/// على جهاز عميلٍ كان يشترط أن يفتح المشغّلُ محرّرَ نصوصٍ ويؤلّف JSON بكلمة مرور PostgreSQL
/// <b>صريحة</b>: فالحمايةُ بـDPAPI كانت مقبولةً عند القراءة (CEEF-206) <b>ولا أداةَ تُنتجها</b>
/// — <c>LocalSecretProtector.Protect</c> كان يستدعيه ZATCA وحدَه. وكتيّبُ التركيب أحال على
/// «القسم 3» وهو معالجُ التهيئة لا صيغةُ الملفّ.</para>
///
/// <para><b>وما صار:</b> معالجُ التهيئة يسأل عن الاتصال، يُجرّبه، ثمّ يكتب الملفَّ محميّاً من
/// هنا. والقراءةُ تقبل الصريحَ كما كانت — فملفٌّ كُتب باليد قبل اليوم يبقى يعمل.</para>
/// </summary>
public static class DatabaseConfigFile
{
    public const string FileName = "database.json";
    public const string PostgresProvider = "postgresql";
    public const string SqliteProvider = "sqlite";

    public static string Path() => System.IO.Path.Combine(BrqoshDataRoot.Path(), FileName);

    public static bool Exists() => File.Exists(Path());

    public static DatabaseConfigEntry? Load() => Load(Path());

    /// <summary>
    /// يقرأ الملفَّ ويفكّ حمايةَ سلسلة الاتصال إن كانت محميّة.
    /// <c>null</c> لملفٍّ غائبٍ أو غيرِ مقروءٍ كـJSON. ويرمي بعبارةٍ صريحةٍ إن كانت السلسلةُ
    /// محميّةً بملفّ تعريفِ مستخدمٍ آخر — لا يُسقَط التشغيلُ بصمتٍ على قاعدةٍ خاطئة (CEEF-206).
    /// </summary>
    public static DatabaseConfigEntry? Load(string path)
    {
        if (!File.Exists(path))
            return null;

        RawFile? raw;
        try
        {
            raw = JsonSerializer.Deserialize<RawFile>(File.ReadAllText(path), JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        if (raw is null || string.IsNullOrWhiteSpace(raw.Provider))
            return null;

        try
        {
            return new DatabaseConfigEntry(raw.Provider.Trim(), LocalSecretProtector.Unprotect(raw.ConnectionString));
        }
        catch (CryptographicException)
        {
            throw new InvalidOperationException(
                "تعذّر فكّ تشفير سلسلة الاتصال في database.json — أُنشئت على حساب مستخدم آخر. " +
                "أعد ضبط الاتصال من هذا الحساب.");
        }
        catch (FormatException)
        {
            // (CEEF-236) ‏**والتالفُ غيرُ المُشفَّر بحسابٍ آخر.** `Unprotect` يفكّ Base64 قبل DPAPI،
            // ‏فسلسلةٌ تحمل البادئة وحمولتُها مبتورة ترمي `FormatException` لا `CryptographicException`.
            // ‏ولو تُركت لخرجت من هنا إلى **مُنشئ معالج التهيئة** فيسقط التطبيق قبل أن تظهر شاشةٌ
            // ‏تُصلح الملفّ — عطلٌ لا مخرجَ منه إلا حذفُ ملفٍّ لا يعرف المستخدمُ مكانه.
            throw new InvalidOperationException(
                "ملفّ الاتصال database.json تالف — تعذّرت قراءة سلسلة الاتصال المحميّة. " +
                "أعد إدخال بيانات الاتصال من معالج التهيئة.");
        }
    }

    public static void Save(string provider, string connectionString) => Save(Path(), provider, connectionString);

    /// <summary>يكتب الملفَّ وسلسلةُ الاتصال <b>محميّةٌ</b> (DPAPI على ويندوز) — لا كلمةَ مرورٍ صريحةً على القرص.</summary>
    public static void Save(string path, string provider, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException("المزوّد مطلوب.", nameof(provider));
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("سلسلة الاتصال مطلوبة.", nameof(connectionString));

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)
            ?? throw new ArgumentException("مسارُ الملفّ بلا مجلّد.", nameof(path)));

        var raw = new RawFile
        {
            Provider = provider.Trim().ToLowerInvariant(),
            ConnectionString = LocalSecretProtector.Protect(connectionString),
        };
        File.WriteAllText(path, JsonSerializer.Serialize(raw, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>
    /// (CEEF-236) <b>وضعُ التشفير صريحٌ لا متروكٌ لافتراض المُشغِّل.</b>
    ///
    /// <para>افتراضُ Npgsql هو <c>Prefer</c>: يُشفّر <b>إن عرض الخادمُ ذلك</b>، ولا يتحقّق من
    /// شهادةٍ البتّة، <b>ويمضي بالنصّ الصريح إن أجاب الخادمُ بالرفض</b>. فخادمٌ على شبكة المكتب
    /// بـ<c>ssl=off</c> — وهو افتراضُ مُنصِّب ويندوز — يمرّ باختبار الاتصال ثمّ يُرسل كلمةَ المرور
    /// ودفترَ الأستاذ كلَّه في العراء. <b>ووثيقتنا نفسُها تكتب <c>SSL Mode=Require</c></b>
    /// (`docs/postgres-production.md §2`)، فكان المنتجُ يكتب أضعفَ ممّا يُوصي به.</para>
    ///
    /// <para><b>والافتراضُ هنا <see cref="SslMode.Require"/></b>، ويُترك للمُشغِّل خفضُه صراحةً من
    /// الشاشة لتركيبٍ على الجهاز نفسِه. و<c>Require</c> يمنع النصَّ الصريح والهبوطَ إليه، <b>ولا
    /// يمنع وسيطاً يُقدّم شهادةً مزوّرة</b> — ذاك يحتاج <c>VerifyFull</c> وشهادةَ جذر، وهو قرارُ
    /// منشأةٍ لا افتراضُ مُنصِّب.</para>
    /// </summary>
    public static string BuildPostgresConnectionString(string host, int port, string database, string username,
        string password, SslMode sslMode = SslMode.Require)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host.Trim(),
            Port = port,
            Database = database.Trim(),
            Username = username.Trim(),
            Password = password,
            SslMode = sslMode,
        };
        return builder.ConnectionString;
    }

    /// <summary>
    /// وضعُ التشفير المُعلَن في سلسلةٍ قائمة — <b>يُقرأ ليُحفَظ عند إعادة البناء</b>.
    ///
    /// <para>بدونه كان ملفٌّ كُتب باليد بـ<c>SSL Mode=Require</c> يفقد اشتراطَه <b>بصمت</b> بمجرّد
    /// أن يكتب المُشغّلُ كلمةَ المرور في المعالج: تُبنى السلسلةُ من الحقول الخمسة فيسقط ما ليس
    /// منها، ويمرّ اختبارُ الاتصال فلا يُنبَّه أحد.</para>
    /// </summary>
    public static SslMode ReadSslMode(string? connectionString, SslMode fallback = SslMode.Require)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return fallback;
        try
        {
            return new NpgsqlConnectionStringBuilder(connectionString).SslMode;
        }
        catch (ArgumentException)
        {
            return fallback;
        }
    }

    /// <summary>يفكّ سلسلةَ PostgreSQL إلى أجزائها لملء حقولِ شاشةٍ — بلا كلمة المرور.</summary>
    public static bool TryParsePostgres(string? connectionString,
        out string host, out int port, out string database, out string username)
    {
        host = "";
        port = 5432;
        database = "";
        username = "";
        if (string.IsNullOrWhiteSpace(connectionString))
            return false;

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            host = builder.Host ?? "";
            port = builder.Port;
            database = builder.Database ?? "";
            username = builder.Username ?? "";
            return !string.IsNullOrWhiteSpace(host);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public sealed record ConnectionProbe(bool Ok, string Message, Version? ServerVersion);

    /// <summary>
    /// يفتح اتصالاً فعلياً بمهلةٍ قصيرة ويُعيد سبباً مسمّىً عند الإخفاق — لا «تعذّر الاتصال».
    /// القاعدةُ الغائبة (<c>3D000</c>) واعتمادٌ مرفوض (<c>28P01</c>) ومضيفٌ لا يُبلَغ ثلاثةُ
    /// أعطالٍ مختلفةٍ علاجُها مختلف.
    /// </summary>
    public static async Task<ConnectionProbe> TestPostgresAsync(string connectionString, CancellationToken ct = default)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString) { Timeout = 5 };
            await using var connection = new NpgsqlConnection(builder.ConnectionString);
            await connection.OpenAsync(ct);
            var version = connection.PostgreSqlVersion;
            return new ConnectionProbe(true, $"الاتصال ناجح — PostgreSQL {version}", version);
        }
        catch (PostgresException ex) when (ex.SqlState == "3D000")
        {
            return new ConnectionProbe(false,
                "القاعدة غير موجودة على الخادم — أنشئها أولاً (docs/postgres-production.md §3) ثم أعد المحاولة.", null);
        }
        catch (PostgresException ex) when (ex.SqlState == "28P01")
        {
            return new ConnectionProbe(false, "رُفضت بيانات الاعتماد — تحقّق من اسم المستخدم وكلمة المرور.", null);
        }
        catch (PostgresException ex) when (ex.SqlState == "28000")
        {
            // (CEEF-236) ‏`28000` ليس كلمةَ مرورٍ خاطئة بل **رفضُ `pg_hba.conf`** — قاعدةٌ لا تسمح
            // ‏لهذا المستخدم من هذا العنوان، أو تشترط تشفيراً. وإرسالُ المشغّل يُراجع كلمةَ المرور
            // ‏يُضيّع وقتَه على ما ليس عطلاً.
            return new ConnectionProbe(false,
                $"رفض الخادم الاتصال بقواعد pg_hba.conf — راجع سطرَ السماح لهذا المستخدم وهذا العنوان (وطريقة التوثيق). التفصيل: {ex.MessageText}", null);
        }
        catch (PostgresException ex)
        {
            return new ConnectionProbe(false, $"رفض الخادم الاتصال ({ex.SqlState}) — {ex.MessageText}", null);
        }
        catch (ArgumentException ex)
        {
            return new ConnectionProbe(false, $"سلسلة الاتصال غير صالحة — {ex.Message}", null);
        }
        catch (NpgsqlException ex) when (ex.Message.Contains("SSL", StringComparison.OrdinalIgnoreCase))
        {
            // ‏الخادمُ لا يعرض TLS ونحن نشترطه — يُقال ذلك باسمه، فالعلاجُ تفعيلُ ssl على الخادم
            // ‏لا تغييرُ كلمة المرور.
            return new ConnectionProbe(false,
                "الخادم لا يدعم الاتصال المشفّر (TLS) والاتصالُ مشروطٌ به. فعّل ssl على خادم PostgreSQL، " +
                "أو أزل اشتراط التشفير من الشاشة إن كان الخادم على هذا الجهاز نفسِه.", null);
        }
        catch (Exception ex) when (ex is NpgsqlException or System.Net.Sockets.SocketException or TimeoutException)
        {
            return new ConnectionProbe(false, $"تعذّر بلوغ الخادم — {ex.GetBaseException().Message}", null);
        }
    }

    private sealed class RawFile
    {
        public string? Provider { get; set; }
        public string? ConnectionString { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
}
