using System.IO;
using Brqosh.Infrastructure.Persistence;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (إعدادُ القاعدة التلقائيّ) الخادمُ المدمجُ يُهيَّأ ويُشغَّل بلا إدخال: بلا حزمةِ خادمٍ لا أثرَ ولا ضرر،
/// ومع خادمٍ حقيقيٍّ تُقاس الدورةُ كلُّها (initdb ← تشغيل ← قاعدة ← ملفّ اتصالٍ محميّ ← إيقاف ← إعادة تشغيل).
/// </summary>
[Collection("EmbeddedPostgres")]
public class EmbeddedPostgresTests
{
    private static string? LocalServer()
    {
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("BRQOSH_PGSERVER"),
            @"C:\Program Files\PostgreSQL\17",
        };
        return candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c) && File.Exists(Path.Combine(c, "bin", "postgres.exe")));
    }

    private sealed class Scope : IDisposable
    {
        private readonly string? _oldRoot = Environment.GetEnvironmentVariable("BRQOSH_DATA_ROOT");
        private readonly string? _oldServer = Environment.GetEnvironmentVariable("BRQOSH_PGSERVER");
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "brq-pg-" + Guid.NewGuid().ToString("N")[..8]);

        public Scope(string? server)
        {
            Environment.SetEnvironmentVariable("BRQOSH_DATA_ROOT", Root);
            Environment.SetEnvironmentVariable("BRQOSH_PGSERVER", server ?? Path.Combine(Root, "no-server"));
        }

        public void Dispose()
        {
            EmbeddedPostgres.Stop();
            Environment.SetEnvironmentVariable("BRQOSH_DATA_ROOT", _oldRoot);
            Environment.SetEnvironmentVariable("BRQOSH_PGSERVER", _oldServer);
            try { Directory.Delete(Root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    [Fact]
    public async Task WithoutABundledServer_NothingHappens_AndProvisioningSaysSo()
    {
        using var scope = new Scope(null);
        Assert.False(EmbeddedPostgres.IsAvailable);
        Assert.False(EmbeddedPostgres.IsProvisioned);
        Assert.True((await EmbeddedPostgres.EnsureRunningAsync()).Ok);      // لا خادمَ مدمجاً ⟵ لا أثر
        var outcome = await EmbeddedPostgres.ProvisionAsync();
        Assert.False(outcome.Ok);
        Assert.False(File.Exists(Path.Combine(scope.Root, "database.json")));   // ‏ولا ملفَّ اتصالٍ يُكتب بلا خادم
    }

    [Fact]
    public async Task AFullCycle_ProvisionsStartsConnectsStopsAndRestartsWithoutAnyInput()
    {
        var server = LocalServer();
        if (server is null)
            return;   // ‏لا خادمَ PostgreSQL على هذا الجهاز: الدورةُ الحيّةُ تُقاس حيث يوجد (جهازُ البناء/CI).

        using var scope = new Scope(server);
        var provisioned = await EmbeddedPostgres.ProvisionAsync();
        Assert.True(provisioned.Ok, provisioned.Message);

        var config = DatabaseConfigFile.Load();
        Assert.NotNull(config);
        Assert.Equal(DatabaseConfigFile.PostgresProvider, config!.Provider);
        Assert.Contains("127.0.0.1", config.ConnectionString);   // ‏loopback وحدَه — لا منفذَ للشبكة
        Assert.False(File.ReadAllText(Path.Combine(scope.Root, "database.json")).Contains(DatabaseConfigFile.ReadPassword(config.ConnectionString)),
            "كلمةُ المرور على القرص صريحة.");

        Assert.True((await DatabaseConfigFile.TestPostgresAsync(config.ConnectionString)).Ok);

        EmbeddedPostgres.Stop();
        Npgsql.NpgsqlConnection.ClearAllPools();   // ‏مجمَّعُ الاتصالات يُبقي اتصالاً ميّتاً يُقرأ «ناجحاً»
        Assert.False((await DatabaseConfigFile.TestPostgresAsync(config.ConnectionString)).Ok);   // ‏توقّف فعلاً

        Assert.True((await EmbeddedPostgres.EnsureRunningAsync()).Ok);                       // ‏يُعاد تشغيله وحدَه
        Assert.True((await DatabaseConfigFile.TestPostgresAsync(config.ConnectionString)).Ok);

        var again = await EmbeddedPostgres.ProvisionAsync();                                 // ‏التكرارُ لا يمسّ بياناتٍ ولا كلمة
        Assert.True(again.Ok, again.Message);
        Assert.Equal(config.ConnectionString, DatabaseConfigFile.Load()!.ConnectionString);
    }

    [Fact]
    public void TheSetupScreenOffersTheAutomaticSetupAndTheAppStopsTheServerOnExit()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var desktop = Path.Combine(dir!.FullName, "src", "Brqosh.Desktop");

        var panel = File.ReadAllText(Path.Combine(desktop, "Views", "Controls", "DatabaseConnectionPanel.xaml.cs"));
        Assert.Contains("EmbeddedPostgres.ProvisionAsync()", panel, StringComparison.Ordinal);

        var app = File.ReadAllText(Path.Combine(desktop, "App.xaml.cs"));
        Assert.Contains("EmbeddedPostgres.EnsureRunningAsync()", app, StringComparison.Ordinal);
        Assert.Contains("EmbeddedPostgres.Stop()", app, StringComparison.Ordinal);

        var build = File.ReadAllText(Path.Combine(dir.FullName, "installer", "build-msi.ps1"));
        Assert.Contains("pgserver", build, StringComparison.Ordinal);
    }
}
