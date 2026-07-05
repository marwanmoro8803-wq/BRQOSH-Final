using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Persistence;

public enum DatabaseProvider
{
    /// <summary>PostgreSQL — الخيار المعتمد للإنتاج (5M+ سجل).</summary>
    PostgreSql,

    /// <summary>SQLite — للتشغيل الفوري بدون خادم وللاختبارات؛ ليس خيار الإنتاج.</summary>
    Sqlite,
}

/// <summary>
/// مصنع سياق قاعدة البيانات. الإنتاج على PostgreSQL وفق ADR-001؛
/// SQLite يُستخدم تلقائياً إذا لم يتوفر خادم PostgreSQL حتى لا يتعطل المستخدم.
/// </summary>
public static class BrqoshDbContextFactory
{
    public static BrqoshDbContext Create(DatabaseProvider provider, string connectionString)
    {
        var builder = new DbContextOptionsBuilder<BrqoshDbContext>();
        switch (provider)
        {
            case DatabaseProvider.PostgreSql:
                builder.UseNpgsql(connectionString);
                break;
            case DatabaseProvider.Sqlite:
                builder.UseSqlite(connectionString);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(provider));
        }

        return new BrqoshDbContext(builder.Options);
    }

    public static string DefaultSqlitePath()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BRQOSH");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "brqosh.db");
    }
}
