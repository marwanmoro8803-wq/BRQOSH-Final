using Brqosh.Infrastructure.Persistence;

namespace Brqosh.Application.Tests.Persistence;

/// <summary>
/// (CEEF-236 · ت-٣) <b><c>database.json</c> يُكتب من المنتج محميّاً، ويُقرأ صريحاً كما كان.</b>
///
/// <para>كان الملفُّ يُقرأ فقط: حمايةُ DPAPI مقبولةٌ عند القراءة (CEEF-206) ولا شيءَ يُنتجها،
/// فكلمةُ مرور PostgreSQL كانت تُكتب باليد صريحةً. تُقاس هنا الكتابةُ المحميّة، وتوافقُ الملفّات
/// القائمة، وتحويلُ الحقول إلى سلسلةٍ وعودتها — على مساراتٍ مؤقّتةٍ صريحة، لا على جذر البيانات
/// المشترك بين الاختبارات.</para>
/// </summary>
public class DatabaseConfigFileTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), "brqosh-dbcfg-" + Guid.NewGuid().ToString("N"), DatabaseConfigFile.FileName);

    private static void Cleanup(string path)
    {
        var dir = Path.GetDirectoryName(path)!;
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void Save_ProtectsTheConnectionStringOnDisk_AndLoadReturnsItPlain()
    {
        var path = TempPath();
        try
        {
            const string password = "S3cret-Pa55!x";
            var cs = DatabaseConfigFile.BuildPostgresConnectionString("db.example.local", 5433, "brqosh", "brqosh_app", password);

            DatabaseConfigFile.Save(path, DatabaseConfigFile.PostgresProvider, cs);

            var raw = File.ReadAllText(path);
            Assert.Contains("\"Provider\"", raw);
            if (OperatingSystem.IsWindows())
            {
                // ‏DPAPI متاحٌ — فلا كلمةَ مرورٍ صريحةً على القرص، والسلسلةُ تحمل بادئةَ الحماية.
                Assert.Contains("BRQOSH1:", raw);
                Assert.DoesNotContain(password, raw);
            }

            var loaded = DatabaseConfigFile.Load(path);
            Assert.NotNull(loaded);
            Assert.Equal(DatabaseConfigFile.PostgresProvider, loaded!.Provider);
            Assert.Equal(cs, loaded.ConnectionString);
        }
        finally
        {
            Cleanup(path);
        }
    }

    /// <summary>ملفٌّ كُتب باليد قبل اليوم — صريحاً وبأسماء الخصائص نفسِها — يبقى يعمل.</summary>
    [Fact]
    public void APlainFileWrittenByHand_StillLoads()
    {
        var path = TempPath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path,
                "{ \"Provider\": \"PostgreSQL\", \"ConnectionString\": \"Host=h;Port=5432;Database=d;Username=u;Password=p\" }");

            var loaded = DatabaseConfigFile.Load(path);

            Assert.NotNull(loaded);
            Assert.Equal("PostgreSQL", loaded!.Provider);
            Assert.Equal("Host=h;Port=5432;Database=d;Username=u;Password=p", loaded.ConnectionString);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void AMissingOrMalformedFile_ReturnsNull()
    {
        var path = TempPath();
        try
        {
            Assert.Null(DatabaseConfigFile.Load(path));

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "{ not json");
            Assert.Null(DatabaseConfigFile.Load(path));

            File.WriteAllText(path, "{ \"ConnectionString\": \"Host=h\" }");
            Assert.Null(DatabaseConfigFile.Load(path));
        }
        finally
        {
            Cleanup(path);
        }
    }

    /// <summary>
    /// (CEEF-236) <b>الاتصالُ الجديد يشترط التشفير — لا يترك افتراضَ Npgsql يقرّر.</b>
    ///
    /// <para>افتراضُ المكتبة <c>Prefer</c>: يمضي بالنصّ الصريح إن رفض الخادمُ التشفير، ولا يتحقّق
    /// من شهادة. <b>ووثيقتنا تكتب <c>SSL Mode=Require</c></b> — فكان المنتجُ يكتب أضعفَ ممّا يُوصي به.</para>
    /// </summary>
    [Fact]
    public void ANewConnectionString_RequiresTls_ByDefault()
    {
        var cs = DatabaseConfigFile.BuildPostgresConnectionString("db.local", 5432, "brqosh", "app", "pw");

        Assert.Equal(Npgsql.SslMode.Require, DatabaseConfigFile.ReadSslMode(cs));
        Assert.Equal(Npgsql.SslMode.Prefer, DatabaseConfigFile.ReadSslMode(
            DatabaseConfigFile.BuildPostgresConnectionString("db.local", 5432, "brqosh", "app", "pw", Npgsql.SslMode.Prefer)));
    }

    /// <summary>
    /// <b>وملفٌّ كُتب باليد بـ<c>SSL Mode=Require</c> لا يفقد اشتراطَه بصمت.</b>
    ///
    /// <para>‏<c>TryParsePostgres</c> يُخرج خمسةَ حقولٍ فقط؛ فلو أُعيد البناءُ منها وحدَها — وهو ما
    /// يقع بمجرّد أن يكتب المُشغّلُ كلمةَ المرور في المعالج — <b>لسقط كلُّ ما ليس منها</b>، ومرّ
    /// اختبارُ الاتصال (‏<c>Prefer</c> ينجح حيث ينجح <c>Require</c>‎) فلا يُنبَّه أحد.</para>
    /// </summary>
    [Fact]
    public void ReadSslMode_RecoversTheDeclaredModeFromAHandWrittenString()
    {
        Assert.Equal(Npgsql.SslMode.Require, DatabaseConfigFile.ReadSslMode(
            "Host=h;Port=5432;Database=d;Username=u;Password=p;SSL Mode=Require"));
        Assert.Equal(Npgsql.SslMode.VerifyFull, DatabaseConfigFile.ReadSslMode(
            "Host=h;Database=d;Username=u;Password=p;SSL Mode=VerifyFull"));

        // ‏سلسلةٌ بلا إعلانٍ تُقرأ بافتراض المكتبة — فلا نزعم تشدّداً لم يكتبه أحد.
        Assert.Equal(Npgsql.SslMode.Prefer, DatabaseConfigFile.ReadSslMode("Host=h;Database=d;Username=u;Password=p"));
        // ‏وغيرُ القابل للتحليل يقع على الافتراض الآمن لا على الأضعف.
        Assert.Equal(Npgsql.SslMode.Require, DatabaseConfigFile.ReadSslMode("Data Source=x.db"));
        Assert.Equal(Npgsql.SslMode.Require, DatabaseConfigFile.ReadSslMode(null));
    }

    /// <summary>
    /// (CEEF-236) <b>ملفٌّ محميٌّ تالفٌ يُقال باسمه ولا يُسقط التطبيق.</b>
    ///
    /// <para><c>Unprotect</c> يفكّ Base64 قبل DPAPI، فحمولةٌ مبتورةٌ ترمي <c>FormatException</c> لا
    /// <c>CryptographicException</c>. ولو خرجت لبلغت <b>مُنشئ معالج التهيئة</b> فسقط التطبيقُ
    /// <b>قبل أن تظهر الشاشةُ التي تُصلح الملفّ</b> — عطلٌ لا مخرجَ منه إلا حذفُ ملفٍّ لا يعرف
    /// المستخدمُ مكانه.</para>
    /// </summary>
    [Fact]
    public void ACorruptProtectedString_ThrowsANamedError_NotARawFormatException()
    {
        var path = TempPath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "{ \"Provider\": \"postgresql\", \"ConnectionString\": \"BRQOSH1:not-valid-base64!!\" }");

            var ex = Assert.Throws<InvalidOperationException>(() => DatabaseConfigFile.Load(path));
            Assert.Contains("تالف", ex.Message);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void BuildAndParse_RoundTrip_WithoutLeakingThePassword()
    {
        var cs = DatabaseConfigFile.BuildPostgresConnectionString(" db.local ", 5433, " brqosh ", " brqosh_app ", "pw;with=odd chars");

        Assert.True(DatabaseConfigFile.TryParsePostgres(cs, out var host, out var port, out var database, out var username));
        Assert.Equal("db.local", host);
        Assert.Equal(5433, port);
        Assert.Equal("brqosh", database);
        Assert.Equal("brqosh_app", username);

        Assert.False(DatabaseConfigFile.TryParsePostgres("Data Source=brqosh.db", out _, out _, out _, out _));
        Assert.False(DatabaseConfigFile.TryParsePostgres(null, out _, out _, out _, out _));
    }
}
