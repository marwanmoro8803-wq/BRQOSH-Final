using System.IO;
using System.Text.Json;
using Brqosh.Infrastructure.Persistence;

namespace Brqosh.Desktop;

public record DatabaseConfig(string Provider, string ConnectionString);

/// <summary>
/// نقطة وصول الخدمات للواجهة. قاعدة البيانات: PostgreSQL إذا كان مُعدّاً في config.json،
/// وإلا SQLite محلي حتى يعمل البرنامج فوراً بدون خادم (وفق ADR-001 الإنتاج على PostgreSQL).
/// </summary>
public static class AppServices
{
    public static long CompanyId { get; set; }

    private static DatabaseProvider _provider = DatabaseProvider.Sqlite;
    private static string _connectionString = $"Data Source={BrqoshDbContextFactory.DefaultSqlitePath()}";

    public static void LoadDatabaseConfig()
    {
        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BRQOSH", "database.json");
        if (!File.Exists(configPath))
            return;

        try
        {
            var config = JsonSerializer.Deserialize<DatabaseConfig>(File.ReadAllText(configPath));
            if (config is not null &&
                config.Provider.Equals("postgresql", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(config.ConnectionString))
            {
                _provider = DatabaseProvider.PostgreSql;
                _connectionString = config.ConnectionString;
            }
        }
        catch (JsonException)
        {
            // ملف إعداد تالف — نبقى على SQLite بدل إسقاط التطبيق
        }
    }

    /// <summary>سياق جديد لكل عملية — لا سياق مشترك طويل العمر في تطبيق Desktop.</summary>
    public static BrqoshDbContext CreateDb() =>
        BrqoshDbContextFactory.Create(_provider, _connectionString);
}
