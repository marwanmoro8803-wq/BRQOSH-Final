using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Npgsql;

namespace Brqosh.Infrastructure.Persistence;

/// <summary>
/// (إعدادُ القاعدة التلقائيّ) <b>خادمُ PostgreSQL مدمجٌ يُهيَّأ ويُشغَّل من البرنامج نفسِه — بلا أيّ إدخالٍ من المستخدم.</b>
///
/// <para><b>ما كان:</b> شاشةُ الإعداد تطلب خادماً ومنفذاً ومستخدماً وكلمةَ مرورٍ وتشفيراً، وعلى المستخدم أن يثبّت PostgreSQL
/// بنفسه ثمّ يُنشئ الدورَ والقاعدةَ بـSQL. وهذا لا يُطلب من محاسبٍ.</para>
///
/// <para><b>وما صار:</b> يشحن المُنصِّبُ خادماً مُصغَّراً تحت <c>pgserver</c> بجوار البرنامج. وعند أوّل إعدادٍ:
/// <c>initdb</c> بكلمةِ مرورٍ عشوائيّةٍ (٣٢ بايتاً) لا يراها أحد ← تشغيلٌ على <c>127.0.0.1</c> وحدَه ← إنشاءُ القاعدة
/// ← كتابةُ <c>database.json</c> محميّاً بـDPAPI كأيّ اتصالٍ آخر. وعند كلّ إقلاعٍ يتأكّد البرنامجُ أنّه يعمل ويُشغّله إن لم يكن،
/// وعند الخروج يُوقفه. <b>والخادمُ الخارجيُّ يبقى خياراً</b> للمنشآت التي لها خادمُها.</para>
///
/// <para><b>الأمان:</b> لا منفذَ خارجيّ (loopback فقط)، وتوثيقُ <c>scram-sha-256</c> لكلّ الاتصالات، وكلمةُ المرور لا تُكتب إلا محميّةً،
/// والخادمُ يعمل بحساب المستخدم الحاليّ لا مديراً (PostgreSQL يرفض العمل بصلاحيات المدير).</para>
/// </summary>
public static class EmbeddedPostgres
{
    public const string DatabaseName = "brqosh";
    public const string RoleName = "brqosh_owner";
    private const int PreferredPort = 54329;
    private const string MarkerFile = "embedded-postgres.json";

    private sealed class Marker
    {
        public int Port { get; set; }
    }

    public sealed record Outcome(bool Ok, string Message);

    /// <summary>مجلّدُ الخادم المشحون، أو <c>null</c> إن لم يُشحن (بناءٌ بلا خادمٍ مدمج).</summary>
    public static string? BundleRoot()
    {
        // ‏`BRQOSH_PGSERVER` للقياس وحدَه: يُشير إلى مجلّد خادمٍ آخر (تثبيتِ PostgreSQL مثلاً) دون شحنه.
        var overridden = Environment.GetEnvironmentVariable("BRQOSH_PGSERVER");
        var root = !string.IsNullOrWhiteSpace(overridden) ? overridden : System.IO.Path.Combine(AppContext.BaseDirectory, "pgserver");
        return File.Exists(System.IO.Path.Combine(root, "bin", "postgres.exe")) ? root : null;
    }

    public static bool IsAvailable => BundleRoot() is not null;

    private static string DataDir => System.IO.Path.Combine(BrqoshDataRoot.Path(), "pgdata");
    private static string MarkerPath => System.IO.Path.Combine(BrqoshDataRoot.Path(), MarkerFile);
    private static string LogPath => System.IO.Path.Combine(BrqoshDataRoot.Path(), "logs", "postgres.log");

    /// <summary>هل أُعدَّ خادمٌ مدمجٌ على هذا الجهاز (له مجلّدُ بياناتٍ ووسمٌ)؟</summary>
    public static bool IsProvisioned =>
        File.Exists(MarkerPath) && File.Exists(System.IO.Path.Combine(DataDir, "PG_VERSION"));

    /// <summary>
    /// يُعدّ الخادمَ أوّلَ مرّةٍ ويكتب ملفَّ الاتصال. آمنٌ عند التكرار: إن كان معدّاً يُشغّله فقط ولا يمسّ بياناتٍ ولا كلمةَ مرور.
    /// </summary>
    public static async Task<Outcome> ProvisionAsync(CancellationToken ct = default)
    {
        var root = BundleRoot();
        if (root is null)
            return new Outcome(false, "الخادمُ المدمج غير مشحونٍ في هذه النسخة.");

        try
        {
            if (IsProvisioned)
                return await EnsureRunningAsync(ct);

            var port = FindFreePort();
            var password = RandomPassword();
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(LogPath)!);

            // ‏مجلّدُ بياناتٍ ناقصٌ من محاولةٍ سابقةٍ فشلت: يُبدأ من الصفر — لا دفاترَ فيه لأنّ الوسمَ لم يُكتب بعد.
            if (Directory.Exists(DataDir))
                Directory.Delete(DataDir, recursive: true);

            var pwFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"brqosh-pw-{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(pwFile, password);
                var init = await RunAsync(System.IO.Path.Combine(root, "bin", "initdb.exe"),
                    ["-D", DataDir, "-U", RoleName, "-A", "scram-sha-256", $"--pwfile={pwFile}", "-E", "UTF8", "--locale=C"], ct);
                if (init.ExitCode != 0)
                    return new Outcome(false, Describe("تعذّر تهيئة مجلّد قاعدة البيانات", init));
            }
            finally
            {
                TryDelete(pwFile);
            }

            File.AppendAllText(System.IO.Path.Combine(DataDir, "postgresql.conf"),
                $"{Environment.NewLine}# BRQOSH{Environment.NewLine}listen_addresses = '127.0.0.1'{Environment.NewLine}port = {port}{Environment.NewLine}" +
                $"max_connections = 60{Environment.NewLine}shared_buffers = 128MB{Environment.NewLine}");

            var started = await StartAsync(root, ct);
            if (!started.Ok)
                return started;

            await using (var admin = new NpgsqlConnection(DatabaseConfigFile.BuildPostgresConnectionString(
                             "127.0.0.1", port, "postgres", RoleName, password, Npgsql.SslMode.Disable)))
            {
                await admin.OpenAsync(ct);
                await using var create = admin.CreateCommand();
                create.CommandText = $"CREATE DATABASE {DatabaseName} OWNER {RoleName} ENCODING 'UTF8' TEMPLATE template0";
                await create.ExecuteNonQueryAsync(ct);
            }

            File.WriteAllText(MarkerPath, JsonSerializer.Serialize(new Marker { Port = port }));
            DatabaseConfigFile.Save(DatabaseConfigFile.PostgresProvider,
                DatabaseConfigFile.BuildPostgresConnectionString("127.0.0.1", port, DatabaseName, RoleName, password, Npgsql.SslMode.Disable));
            return new Outcome(true, "جاهزة — قاعدةُ بياناتٍ مدمجةٌ على هذا الجهاز.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NpgsqlException
                                      or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Operations.CrashLog.Write(ex, "EmbeddedPostgres.Provision");
            return new Outcome(false, "تعذّر إعداد قاعدة البيانات المدمجة: " + ex.GetBaseException().Message);
        }
    }

    /// <summary>يُشغّل الخادمَ المدمجَ إن لم يكن يعمل. لا شيءَ يُفعل إن لم يكن هناك خادمٌ مدمجٌ مُعَدّ.</summary>
    public static async Task<Outcome> EnsureRunningAsync(CancellationToken ct = default)
    {
        var root = BundleRoot();
        if (root is null || !IsProvisioned)
            return new Outcome(true, "لا خادمَ مدمجاً.");
        try
        {
            var status = await RunAsync(System.IO.Path.Combine(root, "bin", "pg_ctl.exe"), ["status", "-D", DataDir], ct);
            if (status.ExitCode == 0)
                return new Outcome(true, "يعمل.");
            return await StartAsync(root, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            Operations.CrashLog.Write(ex, "EmbeddedPostgres.EnsureRunning");
            return new Outcome(false, "تعذّر تشغيل قاعدة البيانات المدمجة: " + ex.GetBaseException().Message);
        }
    }

    /// <summary>يوقف الخادمَ عند خروج البرنامج (إيقافٌ سريعٌ آمن). لا يرمي.</summary>
    public static void Stop()
    {
        var root = BundleRoot();
        if (root is null || !IsProvisioned)
            return;
        try
        {
            RunAsync(System.IO.Path.Combine(root, "bin", "pg_ctl.exe"), ["stop", "-D", DataDir, "-m", "fast", "-w", "-t", "30"], CancellationToken.None)
                .GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            Operations.CrashLog.Write(ex, "EmbeddedPostgres.Stop");
        }
    }

    private static async Task<Outcome> StartAsync(string root, CancellationToken ct)
    {
        var start = await RunAsync(System.IO.Path.Combine(root, "bin", "pg_ctl.exe"),
            ["start", "-D", DataDir, "-l", LogPath, "-w", "-t", "60"], ct, captureError: false);
        return start.ExitCode == 0
            ? new Outcome(true, "يعمل.")
            : new Outcome(false, Describe("تعذّر تشغيل قاعدة البيانات المدمجة", start));
    }

    private sealed record RunResult(int ExitCode, string Output);

    // ‏`captureError=false` للتشغيل: الخادمُ الوليدُ يرث مقبضَ الخطأ فيبقى القراءةُ معلّقةً ما بقي الخادمُ حيّاً — وسببُ الفشل في ملفّ السجلّ.
    private static async Task<RunResult> RunAsync(string exe, IEnumerable<string> args, CancellationToken ct, bool captureError = true)
    {
        var info = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = captureError,
            RedirectStandardOutput = false,
        };
        foreach (var a in args)
            info.ArgumentList.Add(a);
        using var process = Process.Start(info) ?? throw new InvalidOperationException($"تعذّر تشغيل {System.IO.Path.GetFileName(exe)}.");
        var error = captureError ? process.StandardError.ReadToEndAsync(ct) : Task.FromResult("");
        await process.WaitForExitAsync(ct);
        return new RunResult(process.ExitCode, await error);
    }

    private static string Describe(string what, RunResult r) =>
        $"{what} (رمز {r.ExitCode}). {(string.IsNullOrWhiteSpace(r.Output) ? "" : r.Output.Trim())}";

    private static string RandomPassword() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private static int FindFreePort()
    {
        for (var port = PreferredPort; port < PreferredPort + 50; port++)
        {
            try
            {
                var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);
                l.Start();
                l.Stop();
                return port;
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                // ‏المنفذُ مشغولٌ — يُجرَّب التالي؛ ويُسجَّل ليظهر في حزمة الدعم إن نفدت المنافذُ كلُّها.
                Operations.CrashLog.Write(ex, $"EmbeddedPostgres.PortBusy:{port}");
            }
        }

        throw new InvalidOperationException("لا منفذَ محلّيّاً حرّاً لقاعدة البيانات.");
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // ‏ملفُّ كلمةِ المرور المؤقّتُ لم يُحذف: يُسجَّل لأنّه يحمل سرّاً (في مجلّد المؤقّت للمستخدم وحدَه).
            Operations.CrashLog.Write(ex, "EmbeddedPostgres.DeletePasswordFile");
        }
    }
}
