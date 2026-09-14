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
        // (CEEF-236 · م-١٧) ‏الحمايةُ DPAPI ويندوزيّة؛ وعلى غيرها يُعيد `Unprotect` فراغاً بلا رمي.
        // ‏فالحارسُ يُقاس حيث يعمل — وشقيقُه في هذا الملفّ محروسٌ كذلك، وكان هذا وحدَه بلا حارس.
        if (!OperatingSystem.IsWindows())
            return;

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

    /// <summary>
    /// (CEEF-236 · م-١٨) <b>مسبارُ الاتصال — أوّلُ قياسٍ له.</b>
    ///
    /// <para>كان <c>TestPostgresAsync</c> بلا اختبارٍ واحد رغم أنّ الوثيقة تزعم «لا سطرٌ في
    /// Infrastructure بلا اختبار»، <b>وفيه ستّةُ فروعِ التقاطٍ منها تعيينُ <c>28000</c> الذي
    /// أُصلح بيدٍ في هذه الحملة نفسِها</b> — فلو عاد الخطأُ لبقيت الحزمةُ خضراء.</para>
    ///
    /// <para>وهذا يقيس الفروعَ التي لا تحتاج خادماً: سلسلةٌ غيرُ صالحة، ومضيفٌ لا يُبلَغ.
    /// وفروعُ <c>SQLSTATE</c> تحتاج خادماً حيّاً وتبقى مُعلَنةً غيرَ مقيسة.</para>
    /// </summary>
    [Fact]
    public async Task TestPostgres_OnAnInvalidConnectionString_FailsWithANamedReason()
    {
        var probe = await DatabaseConfigFile.TestPostgresAsync("Data Source=not-a-postgres-string");

        Assert.False(probe.Ok);
        Assert.False(string.IsNullOrWhiteSpace(probe.Message));
        Assert.Null(probe.ServerVersion);
    }

    [Fact]
    public async Task TestPostgres_OnAnUnreachableHost_FailsWithoutThrowing()
    {
        // ‏عنوانٌ محجوزٌ للتوثيق (RFC 5737) لا يُبلَغ — والمهلةُ خمسُ ثوانٍ في المسبار نفسِه.
        var cs = DatabaseConfigFile.BuildPostgresConnectionString("192.0.2.1", 5432, "brqosh", "app", "pw");

        var probe = await DatabaseConfigFile.TestPostgresAsync(cs);

        Assert.False(probe.Ok);
        Assert.False(string.IsNullOrWhiteSpace(probe.Message));
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

    /// <summary>
    /// (م-٦ · تبعيّةُ ت-٣) <b>ملفٌّ قديمٌ غيرُ محميٍّ يُرقّى عند أوّل قراءة.</b>
    ///
    /// <para><b>الفجوةُ المقيسة:</b> CEEF-236 جعل <c>Save</c> يحمي بـDPAPI، و<c>Load</c>
    /// يقبل الشكلَين — <b>ولا شيءَ يُرقّي القديم</b>. فتركيبٌ سابقٌ للحماية يُبقي كلمةَ
    /// مرور PostgreSQL <b>صريحةً على القرص إلى الأبد</b>، ولا شيءَ يُخبر المُشغّلَ.
    /// وقِيس على جهاز التطوير: <c>%APPDATA%\BRQOSH\database.json</c> بتاريخ ٢٠٢٦-٠٧-٠٦
    /// <b>بلا بادئة <c>BRQOSH1:</c> وفيه حقلُ <c>Password</c> صريح</b>.</para>
    /// </summary>
    [Fact]
    public void AnUnprotectedLegacyFile_IsUpgradedOnFirstRead()
    {
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        const string connection = "Host=localhost;Port=5433;Database=brqosh;Username=app;Password=PlainSecret123";

        // ‏ملفٌّ قديمٌ كما كان يُكتب قبل الحماية — صريحٌ بلا بادئة.
        File.WriteAllText(path,
            "{\"Provider\":\"postgresql\",\"ConnectionString\":\"" + connection + "\"}");

        // ‏شاهدُ ضبط: الملفُّ **صريحٌ فعلاً** قبل القراءة — وإلّا كان الاختبارُ يقيس لا شيء.
        Assert.Contains("Password=PlainSecret123", File.ReadAllText(path), StringComparison.Ordinal);

        var loaded = DatabaseConfigFile.Load(path);

        // ‏والقراءةُ تُعيد الصريحَ سليماً — فالترقيةُ لا تُفسد ما يقرؤه المُستدعي.
        Assert.NotNull(loaded);
        Assert.Equal(connection, loaded!.ConnectionString);

        // ‏**والملفُّ على القرص لم يعد يحمل الكلمةَ صريحة.**
        var onDisk = File.ReadAllText(path);
        Assert.DoesNotContain("PlainSecret123", onDisk, StringComparison.Ordinal);
        Assert.Contains("BRQOSH1:", onDisk, StringComparison.Ordinal);

        // ‏وقراءةٌ ثانيةٌ تُعطي القيمةَ نفسَها — فالترقيةُ مُطاوِعةٌ للتكرار.
        Assert.Equal(connection, DatabaseConfigFile.Load(path)!.ConnectionString);

        Cleanup(path);
    }

    /// <summary>
    /// <b>وملفٌّ محميٌّ أصلاً لا يُمَسّ</b> — شاهدُ ضبطٍ على أنّ الترقيةَ تُميّز، ولا تُعيد
    /// الكتابةَ في كلّ قراءة.
    /// </summary>
    [Fact]
    public void AnAlreadyProtectedFile_IsLeftUntouched()
    {
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        DatabaseConfigFile.Save(path, "postgresql", "Host=h;Database=d;Username=u;Password=p");

        var before = File.ReadAllText(path);
        Assert.NotNull(DatabaseConfigFile.Load(path));

        Assert.Equal(before, File.ReadAllText(path));

        Cleanup(path);
    }

    /// <summary>
    /// <b>وشرطُ الترقية يوافق ما تفعله الحمايةُ فعلاً — على أيّ منصّة.</b>
    ///
    /// <para><b>وهذا اختبارُ عطلٍ أدخلتُه أنا:</b> أوّلُ صياغةٍ للترقية اشترطت
    /// «غيرُ محميّ» وحدَها. وعلى منصّةٍ بلا DPAPI يُعيد <c>Protect</c> الصريحَ كما هو،
    /// <b>فلا تُرضى الشرطيّةُ أبداً وتُعيد كلُّ قراءةٍ كتابةَ الملفّ</b> — وعلى مسارٍ للقراءة
    /// فقط تُسجَّل رسالةُ عطلٍ مع كلّ قراءة.</para>
    ///
    /// <para><b>ويُقاس بلا فرعٍ للمنصّة في الاختبار</b>: يُجزَم أنّ <c>ProtectionIsAvailable</c>
    /// و<c>Protect</c> <b>يقولان الشيءَ نفسَه</b>. فأيُّهما تغيّر وحدَه أسقط الاختبار،
    /// <b>ويسقط على لينكس كما يسقط على ويندوز</b>.</para>
    /// </summary>
    [Fact]
    public void TheUpgradeConditionAgreesWithWhatProtectionActuallyDoes()
    {
        const string plain = "Host=h;Database=d;Username=u;Password=NotASecretHere";

        var protectedForm = Brqosh.Infrastructure.Security.LocalSecretProtector.Protect(plain);
        var actuallyProtected = Brqosh.Infrastructure.Security.LocalSecretProtector.IsProtected(protectedForm);

        Assert.Equal(Brqosh.Infrastructure.Security.LocalSecretProtector.ProtectionIsAvailable, actuallyProtected);

        // ‏**وشاهدُ ضبط**: القيمةُ تعود سليمةً في الحالتين — فالحمايةُ لا تُفسد ما تحمي،
        // ‏وغيابُها لا يُفسد ما يمرّ.
        Assert.Equal(plain, Brqosh.Infrastructure.Security.LocalSecretProtector.Unprotect(protectedForm));
    }

    /// <summary>
    /// <b>وملفٌّ قديمٌ ناقصٌ لا ينقلب استثناءً.</b>
    ///
    /// <para><b>وهذا انحدارٌ أدخلتُه أنا وكُشف قبل الإيداع:</b> <c>Save</c> يرفض سلسلةَ
    /// اتّصالٍ فارغةً بـ<c>ArgumentException</c> — <b>وهو خارج التقاط الترقية عمداً</b>
    /// (فالالتقاطُ للأعطال العارضة لا لخطإ برمجيّ). فلو رقّت الترقيةُ ملفّاً فارغَ السلسلة
    /// لانقلبت القراءةُ من <b>قيمةٍ فارغةٍ يفحصها المُستدعون</b> — وهم يفحصونها فعلاً:
    /// <c>PasswordReset</c> بـ<c>Length > 0</c> و<c>PrintEvidence</c> بـ
    /// <c>IsNullOrWhiteSpace</c> — <b>إلى استثناءٍ يُسقط الأداةَ قبل أن تقول شيئاً</b>.</para>
    /// </summary>
    [Fact]
    public void ALegacyFileWithNoConnectionString_IsReadNotThrown()
    {
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{\"Provider\":\"postgresql\",\"ConnectionString\":\"\"}");

        var loaded = DatabaseConfigFile.Load(path);

        Assert.NotNull(loaded);
        Assert.Equal("", loaded!.ConnectionString);

        // ‏**ولا يُكتب الملفُّ**: لا شيءَ يُحمى، فلا داعيَ لمسّه.
        Assert.Equal("{\"Provider\":\"postgresql\",\"ConnectionString\":\"\"}", File.ReadAllText(path));

        Cleanup(path);
    }
}
