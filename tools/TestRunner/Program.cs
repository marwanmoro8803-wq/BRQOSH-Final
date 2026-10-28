using System.Reflection;
using System.IO;
using Xunit;
using Xunit.Abstractions;

// مشغّل xUnit في نفس العملية.
// الاستعمال: TestRunner <test.dll> [filter]
// الفلتر: نصٌّ يُقارَن باحتوائه على DisplayName (تجاهُلُ حالةِ الأحرف).

if (args.Length == 0)
{
    Console.Error.WriteLine("usage: TestRunner <test.dll> [filter]");
    return 1;
}

var dll = Path.GetFullPath(args[0]);
if (!File.Exists(dll))
{
    Console.Error.WriteLine($"test assembly not found: {dll}");
    return 1;
}

// يعمل المشغّل من مجلده لا من مجلد الاختبارات؛ فتُدار تبعيّاتُ xunit المفقودة
// (‏xunit.execution.dotnet ونظائرها) إلى مجلد اختبارات المجمَّعة المطلوبة.
var testDir = Path.GetDirectoryName(dll)!;

// **قاعدةُ تجميع الاختبارات لا قاعدةُ المشغّل** (٢٠٢٦-١٠-٢٧): اختباراتٌ تقرأ ملفّاتٍ مجاورةً
// (‏`Brqosh.Desktop.deps.json` و`THIRD-PARTY-NOTICES.txt`) تبحث عن `AppContext.BaseDirectory`
// وتجده مجلّدَ المشغّل فتُسقط — لا لعيبٍ في المنتج بل لأثرِ أداتِنا. فتُعاد القاعدةُ إلى
// مجلّد الاختبارات كما يفعل testhost، ويُطابَق مجلّدُ العمل.
AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY", testDir + Path.DirectorySeparatorChar);
Environment.CurrentDirectory = testDir;

// ومكتباتُ SQLite الأصليّة تُبحث من مجلد التطبيق الأساسيّ (المشغّل) لا من مجلد
// الاختبارات — فيُضاف مجلدا الاختبار و runtimes إلى PATH.
var nativeDir = Path.Combine(testDir, "runtimes", "win-x64", "native");
var path = Environment.GetEnvironmentVariable("PATH") ?? "";
Environment.SetEnvironmentVariable("PATH",
    (Directory.Exists(nativeDir) ? nativeDir + ";" : "") + testDir + ";" + path);

System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += (ctx, name) =>
{
    var candidate = Path.Combine(testDir, name.Name + ".dll");
    return File.Exists(candidate) ? ctx.LoadFromAssemblyPath(candidate) : null;
};

var filter = args.Length > 1 ? args[1] : null;

using var controller = new XunitFrontController(
    AppDomainSupport.Denied, dll, configFileName: null, shadowCopy: false);

// ── الاكتشاف ────────────────────────────────────────────────────────────
var discoverySink = new DiscoveryCollector();
var discoveryOptions = TestFrameworkOptions.ForDiscovery();
discoveryOptions.SetSynchronousMessageReporting(true);
controller.Find(includeSourceInformation: false, discoverySink, discoveryOptions);
if (!discoverySink.Finished.Wait(TimeSpan.FromMinutes(30)))
{
    Console.Error.WriteLine("TIMEOUT after 30m during discovery — incomplete.");
    return 2;
}

var cases = discoverySink.TestCases
    .Where(c => filter is null
                || c.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase))
    .ToList();

Console.WriteLine($"discovered {discoverySink.TestCases.Count}, selected {cases.Count} "
                  + (filter is null ? "" : $"by \"{filter}\""));
if (cases.Count == 0)
{
    Console.WriteLine("no tests selected");
    return 1;
}

// ── التنفيذ ─────────────────────────────────────────────────────────────
var execSink = new ExecutionCollector();
var execOptions = TestFrameworkOptions.ForExecution();
execOptions.SetSynchronousMessageReporting(true);
execOptions.SetValue("xunit.execution.ParallelizeTestCollections", false);
execOptions.SetValue("xunit.execution.ParallelizeAssembly", false);
execOptions.SetValue("xunit.execution.DisableParallelization", true);

int timeoutMinutes = int.TryParse(
        Environment.GetEnvironmentVariable("TESTRUNNER_TIMEOUT_MINUTES"), out int requested) && requested > 0
    ? requested
    : 60;

controller.RunTests(cases, execSink, execOptions);

// **التوقّفُ الصامت عيب** (٢٠٢٦-١٠-٢٧): كان الانتظارُ ١٥ دقيقة، وحين تنتهي المهلةُ يمضي
// المشغّلُ ويطبع «passed N» **لجزءٍ من المجموعة** — فيُقرأ أخضرَ وهو ناقص. صار يفحص
// نتيجةَ الانتظار، ويطبع المنجَزَ من الكلّ، ويخرج برمز ٢.
if (!execSink.Finished.Wait(TimeSpan.FromMinutes(timeoutMinutes)))
{
    Console.Error.WriteLine(
        $"TIMEOUT after {timeoutMinutes}m — incomplete: {execSink.Total} of {cases.Count} ran. "
        + "ارفع TESTRUNNER_TIMEOUT_MINUTES أو قلّل الفلتر.");
    return 2;
}

var failed = execSink.Failed;
foreach (var f in failed)
    Console.WriteLine($"FAIL {f.Test.DisplayName}\n{f.Message}\n{f.StackTrace}\n");

Console.WriteLine($"passed {execSink.Passed} · failed {failed.Count} · skipped {execSink.Skipped} "
                  + $"· total {execSink.Total}");
return failed.Count == 0 ? 0 : 1;

sealed class DiscoveryCollector : IMessageSink, IDisposable
{
    public List<ITestCase> TestCases { get; } = new();
    public ManualResetEventSlim Finished { get; } = new();

    public bool OnMessage(IMessageSinkMessage message)
    {
        if (message is ITestCaseDiscoveryMessage tcdm)
        {
            lock (TestCases) TestCases.Add(tcdm.TestCase);
        }
        else if (message is IDiscoveryCompleteMessage)
        {
            Finished.Set();
        }
        return true;
    }

    public void Dispose() => Finished.Dispose();
}

sealed class ExecutionCollector : IMessageSink, IDisposable
{
    public int Passed { get; private set; }
    public int Skipped { get; private set; }
    public int Total { get; private set; }
    public List<(ITestCase Test, string Message, string StackTrace)> Failed { get; } = new();
    public ManualResetEventSlim Finished { get; } = new();

    public bool OnMessage(IMessageSinkMessage message)
    {
        switch (message)
        {
            case ITestPassed:
                Passed++;
                Total++;
                break;
            case ITestFailed f:
                lock (Failed)
                    Failed.Add((f.TestCase, string.Join(Environment.NewLine, f.Messages),
                        string.Join(Environment.NewLine, f.StackTraces)));
                Total++;
                break;
            case ITestSkipped:
                Skipped++;
                Total++;
                break;
            case ITestAssemblyFinished:
                Finished.Set();
                break;
        }
        return true;
    }

    public void Dispose() => Finished.Dispose();
}
