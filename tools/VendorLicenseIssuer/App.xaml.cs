using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Brqosh.VendorIssuer;

public partial class App : Application
{
    /// <summary>
    /// عطلٌ غيرُ ملتقَطٍ في أداةٍ تُمسك مفتاحاً خاصّاً يجب أن يترك أثراً مقروءاً —
    /// لا نافذةَ ويندوز بيضاء تختفي ولا يُعرف بعدها أوقّعت الأداةُ شيئاً أم لا.
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            WriteCrashLog(args.ExceptionObject as Exception);

        // ‏مسارٌ بلا نافذة: `--selftest <ملف>` يُشغّل الفحوصَ ويكتبها ويخرج برمزٍ يُقرأ.
        // ‏وبدونه لا يملك أحدٌ — لا خطُّ بناءٍ ولا مورِّدٌ على جهازٍ بعيد — طريقاً لمعرفة
        // ‏أنّ نسخةَ قواعد التحقّق ما زالت تطابق المنتج إلا بفتح نافذةٍ والنظرِ إليها.
        var selfTestIndex = Array.FindIndex(e.Args, a =>
            string.Equals(a, "--selftest", StringComparison.OrdinalIgnoreCase));
        if (selfTestIndex >= 0)
        {
            // ‏الوضعُ بلا نافذة: لا تُعرض صناديقُ رسائلَ فيه — صندوقٌ يظهر لأحدٍ لا يراه
            // ‏يُعلّق العمليةَ إلى الأبد، وهو ما وقع أوّلَ مرّة.
            _headless = true;
            var outputPath = selfTestIndex + 1 < e.Args.Length ? e.Args[selfTestIndex + 1] : null;
            Shutdown(RunHeadlessSelfTest(outputPath));
            return;
        }

        base.OnStartup(e);
        new MainWindow().Show();
    }

    private bool _headless;

    private static int RunHeadlessSelfTest(string? outputPath)
    {
        var rows = SelfTest.RunAll();
        var text = string.Join(Environment.NewLine,
            rows.Select(r => $"[{(r.Ok ? "OK" : "FAIL")}] {r.Name} — {r.Detail}"));
        var failed = rows.Count(r => !r.Ok);
        text += $"{Environment.NewLine}RESULT={(failed == 0 ? "OK" : "FAIL")} passed={rows.Count - failed}/{rows.Count}";

        try
        {
            File.WriteAllText(outputPath ?? Path.Combine(Path.GetTempPath(), "brqosh-issuer-selftest.txt"), text);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return 2;
        }

        return failed == 0 ? 0 : 1;
    }

    private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var path = WriteCrashLog(e.Exception);
        if (_headless)
        {
            Shutdown(3);
            e.Handled = true;
            return;
        }

        MessageBox.Show(
            $"عطلٌ غيرُ متوقَّع:\n{e.Exception.Message}\n\nسُجِّل في:\n{path}",
            "BRQOSH — مُصدِّر التراخيص", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private static string WriteCrashLog(Exception? ex)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BRQOSH", "vendor-signing");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir,
                $"issuer-crash-{DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.log");
            File.WriteAllText(path, ex?.ToString() ?? "(no exception object)");
            return path;
        }
        catch (Exception writeFailure) when (writeFailure is IOException or UnauthorizedAccessException)
        {
            return "(تعذّر كتابة سجلّ العطل)";
        }
    }
}
