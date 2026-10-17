using System.IO;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (ح-١٥٦ · tax-zatca#8) <b>المعلَّقُ لدى الهيئة يُعاد إرسالُه من نفسه.</b>
///
/// <para>سلوكُ الدفعة والقفلِ والتباعد مقيسٌ في اختبارات التطبيق؛ وهنا يُقاس <b>التوصيل</b>: أنّ النافذةَ تنبض بمؤقّت،
/// وأنّ النبضةَ تسأل التباعدَ أوّلاً، وتستدعي المسارَ الخلفيَّ لا مسارَ الزرّ (الذي يرفض أمينَ الصندوق)، وتسجّل الوقوف.</para>
/// </summary>
public class ZatcaPendingIsResubmittedInTheBackgroundTests
{
    private static readonly string Source =
        File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "Brqosh.Desktop", "MainWindow.xaml.cs"));

    [Fact]
    public void TheMainWindow_StartsATimer_ThatRunsTheResubmission()
    {
        var ctor = Source[Source.IndexOf("public MainWindow(SetupState setup)", StringComparison.Ordinal)..];
        ctor = ctor[..ctor.IndexOf("\n    }", StringComparison.Ordinal)];

        Assert.Contains("zatcaTimer.Tick += async (_, _) => await RunZatcaResubmitAsync();", ctor, StringComparison.Ordinal);
        Assert.Contains("zatcaTimer.Start();", ctor, StringComparison.Ordinal);
        Assert.Contains("Closed += (_, _) => zatcaTimer.Stop();", ctor, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTick_AsksTheBackoff_UsesTheBackgroundPath_AndRecordsBothOutcomes()
    {
        var method = Body("private async Task RunZatcaResubmitAsync()");

        Assert.Contains("!_zatcaBackoff.IsDue(DateTime.UtcNow)", method, StringComparison.Ordinal);
        Assert.Contains(".ResubmitPendingInBackgroundAsync(AppServices.CompanyId)", method, StringComparison.Ordinal);
        Assert.DoesNotContain(".ResubmitPendingAsync(", method, StringComparison.Ordinal);
        Assert.Contains("_zatcaBackoff.RecordFailure(DateTime.UtcNow)", method, StringComparison.Ordinal);
        Assert.Contains("_zatcaBackoff.RecordSuccess(DateTime.UtcNow)", method, StringComparison.Ordinal);
        Assert.Contains("\"zatca-resubmit-pending\"", method, StringComparison.Ordinal);
        Assert.Contains("\"zatca-resubmit\"", method, StringComparison.Ordinal);
    }

    private static string Body(string signature)
    {
        var start = Source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"لم يُعثر على {signature}");
        var rest = Source[start..];
        return rest[..rest.IndexOf("\n    }", StringComparison.Ordinal)];
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("لم يُعثر على جذر المستودع (Brqosh.sln).");
    }
}
