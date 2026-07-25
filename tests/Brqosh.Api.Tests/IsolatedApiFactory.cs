using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Brqosh.Api.Tests;

/// <summary>مصنع API بقاعدة SQLite مؤقتة معزولة — يمنع تداخل الاختبارات المتوازية.</summary>
public sealed class IsolatedApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"brqosh-api-{Guid.NewGuid():N}.db");
    private readonly Action<IWebHostBuilder>? _configure;

    public IsolatedApiFactory() : this(null) { }

    public IsolatedApiFactory(Action<IWebHostBuilder>? configure) => _configure = configure;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Brqosh"] = $"Data Source={_dbPath}",
            });
        });
        _configure?.Invoke(builder);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { /* */ }
        var wal = _dbPath + "-wal";
        var shm = _dbPath + "-shm";
        try { if (File.Exists(wal)) File.Delete(wal); } catch { /* */ }
        try { if (File.Exists(shm)) File.Delete(shm); } catch { /* */ }
    }
}
