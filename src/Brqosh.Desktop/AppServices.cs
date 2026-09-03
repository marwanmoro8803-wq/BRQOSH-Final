using Brqosh.Application.Setup;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Brqosh.Infrastructure;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Brqosh.Desktop;

public record DatabaseConfig(string Provider, string ConnectionString);

/// <summary>
/// نقطة وصول الخدمات للواجهة.
/// Development/Test: SQLite مسموح.
/// Production: PostgreSQL فقط — فشل الاتصال يوقف التشغيل بأمان.
/// </summary>
public static class AppServices
{
    public static long CompanyId { get; set; }

    private static AppUser? _currentUser;
    private static int _boundSessionVersion;
    public static int BoundSessionVersion => _boundSessionVersion;

    public static AppUser? CurrentUser
    {
        get => _currentUser;
        set
        {
            _currentUser = value;
            _boundSessionVersion = value?.SessionVersion ?? 0;
            SecurityContext.CurrentRole = value?.Role;
            SecurityContext.CurrentUsername = value?.Username;
            if (value is not null)
                SessionIdleTracker.MarkActivity();
        }
    }

    /// <summary>مهلة الخمول قبل طلب إعادة تسجيل الدخول — 30 دقيقة.</summary>
    public static TimeSpan SessionTimeout { get; set; } = TimeSpan.FromMinutes(30);

    public static bool IsSessionExpired()
    {
        if (_currentUser is null) return true;
        return SessionIdleTracker.IsIdle(SessionTimeout);
    }

    public static async Task<bool> IsSessionRevokedAsync()
    {
        if (_currentUser is null) return true;
        await using var db = CreateDb();
        var version = await db.AppUsers
            .Where(u => u.Id == _currentUser.Id)
            .Select(u => u.SessionVersion)
            .SingleAsync();
        return version != _boundSessionVersion;
    }

    public static void TouchSession() => SessionIdleTracker.MarkActivity();

    public static long? CurrentShiftId { get; set; }

    /// <summary>الفرع التشغيلي للجلسة — إن فُرغ يُستخدم HQ عند الترحيل.</summary>
    private static long? _currentBranchId;
    public static long? CurrentBranchId
    {
        get => _currentBranchId;
        set
        {
            _currentBranchId = value;
            Brqosh.Infrastructure.Companies.BranchSessionContext.PreferredBranchId = value;
        }
    }

    public static string? CurrentBranchLabel { get; set; }

    private static DatabaseProvider _provider = DatabaseProvider.Sqlite;
    private static string _connectionString = $"Data Source={BrqoshDbContextFactory.DefaultSqlitePath()}";
    private static ServiceProvider? _infrastructureServices;

    public static string ActiveProviderLabel => _provider == DatabaseProvider.PostgreSql ? "PostgreSQL" : "SQLite";

    /// <summary>
    /// نصّ تحذير يُملأ عند التحويل الاضطراري من PostgreSQL إلى SQLite (تطوير فقط) —
    /// تعرضه الواجهة عند الإقلاع. null يعني لم يحدث تحويل (ع-D3، CEEF-202).
    /// </summary>
    public static string? DatabaseFallbackWarning { get; private set; }

    public static void LoadDatabaseConfig()
    {
        _provider = DatabaseProvider.Sqlite;
        _connectionString = $"Data Source={BrqoshDbContextFactory.DefaultSqlitePath()}";

        if (AppEnvironmentConfig.IsProduction)
        {
            var config = LoadDatabaseConfigFile();
            if (config is null ||
                !config.Provider.Equals("postgresql", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(config.ConnectionString))
            {
                // (CEEF-236 · ت-٣) الرسالةُ تُسمّي المخرجَ: المعالجُ يكتب الملفَّ الآن؛ والكتابةُ باليد
                // بديلٌ موثَّقٌ لا الطريقُ الوحيد.
                throw new InvalidOperationException(
                    "وضع الإنتاج يتطلب PostgreSQL ولا يوجد ملف اتصال صالح.\n" +
                    $"المسار: {DatabaseConfigFile.Path()}\n" +
                    "أعد تشغيل معالج التهيئة لإدخال بيانات الاتصال، أو أنشئ الملف يدوياً كما في docs/postgres-production.md §2.");
            }

            _provider = DatabaseProvider.PostgreSql;
            _connectionString = config.ConnectionString;
            // كان يُتخطّى سابقاً عبر return — فيُبقى DomainEventHub في الذاكرة فقط بالإنتاج.
            WireDomainEventPersistence();
            return;
        }

        var devConfig = LoadDatabaseConfigFile();
        if (devConfig is not null &&
            devConfig.Provider.Equals("postgresql", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(devConfig.ConnectionString))
        {
            _provider = DatabaseProvider.PostgreSql;
            _connectionString = devConfig.ConnectionString;
        }

        WireDomainEventPersistence();
    }

    /// <summary>يربط DomainEventHub بناشر يحفظ الأحداث في قاعدة البيانات.</summary>
    public static void WireDomainEventPersistence()
    {
        _infrastructureServices?.Dispose();
        var services = new ServiceCollection();
        services.AddBrqoshInfrastructure(_connectionString, _provider == DatabaseProvider.PostgreSql);
        _infrastructureServices = services.BuildServiceProvider();
    }

    public static async Task EnsureDatabaseReachableAsync()
    {
        try
        {
            await using var db = CreateDb();
            await db.Database.OpenConnectionAsync();
            await db.Database.CloseConnectionAsync();
        }
        catch (Exception ex)
        {
            if (AppEnvironmentConfig.IsProduction)
            {
                throw new InvalidOperationException(
                    $"تعذر الاتصال بـ PostgreSQL في وضع الإنتاج — تم إيقاف التشغيل.\n{ex.Message}", ex);
            }

            if (_provider == DatabaseProvider.PostgreSql)
            {
                var sqlitePath = BrqoshDbContextFactory.DefaultSqlitePath();
                _provider = DatabaseProvider.Sqlite;
                _connectionString = $"Data Source={sqlitePath}";
                WireDomainEventPersistence();

                // (ع-D3، CEEF-202): التحويل لم يعد صامتاً. العمل على قاعدة غير التي هيّأها
                // المستخدم يعني كتابة القيود في دفتر آخر — يجب أن يراه لا أن يكتشفه لاحقاً.
                DatabaseFallbackWarning =
                    "تعذر الاتصال بـ PostgreSQL المهيّأ، وتم التحويل إلى قاعدة SQLite محلية.\n\n" +
                    $"المسار: {sqlitePath}\n" +
                    $"السبب: {ex.Message}\n\n" +
                    "تنبيه: أي عمليات تُدخلها الآن ستُحفظ في هذه القاعدة المحلية، لا في PostgreSQL.";
            }
        }
    }

    public static BrqoshDbContext CreateDb()
    {
        // AsyncLocal لا يتدفق عبر كل نقاط دخول WPF — أعد ربط الجلسة قبل كل استدعاء خدمة.
        BindSecurityContext();
        return BrqoshDbContextFactory.Create(_provider, _connectionString);
    }

    public static WhatsAppOtpService CreateOtpService(BrqoshDbContext db) =>
        new(db, WhatsAppNotificationChannelFactory.Resolve());

    [Obsolete("استخدم CreateOtpService")]
    public static WhatsAppOtpService CreateWhatsAppOtpService(BrqoshDbContext db) => CreateOtpService(db);

    private static void BindSecurityContext()
    {
        SecurityContext.CurrentRole = _currentUser?.Role;
        SecurityContext.CurrentUsername = _currentUser?.Username;
    }

    /// <summary>نطاق DI للـ Use Cases — N3 Desktop facade. يعيد ربط SecurityContext (AsyncLocal لا يتدفق عبر كل نقاط دخول WPF).</summary>
    public static IServiceScope CreateInfrastructureScope()
    {
        BindSecurityContext();
        if (_infrastructureServices is null)
            WireDomainEventPersistence();
        return _infrastructureServices!.CreateScope();
    }

    /// <summary>
    /// (CEEF-236 · ت-٣) القراءةُ والكتابةُ صارتا في <see cref="DatabaseConfigFile"/> — فمعالجُ
    /// التهيئة يكتب الملفَّ محميّاً من الموضع الذي يقرؤه التطبيق، لا نسخةً ثانيةً من المنطق.
    /// (CEEF-206) سلسلةُ الاتصال تُقبل مشفّرةً بـDPAPI أو صريحةً (توافقٌ مع الملفّات القائمة)،
    /// وملفٌّ محميٌّ بحساب مستخدمٍ آخر يرمي بعبارةٍ صريحةٍ لا يُسقط التشغيلَ بصمتٍ على قاعدةٍ خاطئة.
    /// </summary>
    private static DatabaseConfig? LoadDatabaseConfigFile()
    {
        var entry = DatabaseConfigFile.Load();
        return entry is null ? null : new DatabaseConfig(entry.Provider, entry.ConnectionString);
    }
}
