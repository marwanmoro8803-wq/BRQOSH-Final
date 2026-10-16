using System.IO;
using System.Text.RegularExpressions;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (ح-١٤١ · installer-deployment-ops#12) <b>القناةُ يبلغها البرنامجُ من نفسه.</b>
///
/// <para><b>ما كان:</b> الفحصُ مربعُ «اختر ملفّاً» في شاشةٍ لا يفتحها أحد، والتهيئةُ مبنيّةٌ
/// مختبَرةٌ ولا يستدعيها شيء. وسلوكُ القناة نفسُها مقيسٌ في <c>UpdateChannelOperableTests</c>؛
/// وهنا يُقاس <b>التوصيل</b>: أنّ النافذةَ تفحص عند الإقلاع، وأنّ الشاشةَ تحفظ وتُهيّئ.</para>
/// </summary>
public class UpdateChannelIsReachedTests
{
    private static string Source(params string[] parts) =>
        File.ReadAllText(Path.Combine([FindRepoRoot(), "src", "Brqosh.Desktop", .. parts]));

    [Fact]
    public void TheMainWindow_ChecksTheSavedChannelAtStartup()
    {
        var source = Source("MainWindow.xaml.cs");
        var ctor = source[source.IndexOf("public MainWindow(SetupState setup)", StringComparison.Ordinal)..];
        ctor = ctor[..ctor.IndexOf("\n    }", StringComparison.Ordinal)];

        Assert.Contains("_ = CheckForUpdateAtStartupAsync();", ctor, StringComparison.Ordinal);

        var method = Body(source, "private async Task CheckForUpdateAtStartupAsync()");
        Assert.Contains("UpdateChannelSettings.Load()", method, StringComparison.Ordinal);
        Assert.Contains(".CheckAsync(source)", method, StringComparison.Ordinal);
        // ‏البيانُ المرفوضُ يُقال ويُسجَّل — لا يمرّ صامتاً كانقطاع الشبكة.
        Assert.Contains("UpdateStatus.Untrusted", method, StringComparison.Ordinal);
        Assert.Contains("update-channel-untrusted", method, StringComparison.Ordinal);
        // ‏ولا يُنزَّل شيءٌ عند الإقلاع.
        Assert.DoesNotContain("Stage", method, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDiagnosticsPage_RemembersTheChannel_AndStagesFromIt()
    {
        var source = Source("Views", "Pages", "DiagnosticsPage.xaml.cs");

        Assert.Contains("UpdateChannelSettings.Load()", source, StringComparison.Ordinal);
        Assert.Contains("UpdateChannelSettings.Save(source)", Body(source, "private async void OnCheckUpdate("), StringComparison.Ordinal);
        Assert.Contains(".StageFromChannelAsync(", Body(source, "private async void OnStageUpdate("), StringComparison.Ordinal);

        var xaml = Source("Views", "Pages", "DiagnosticsPage.xaml");
        Assert.Matches(new Regex("x:Name=\"StageBtn\"[^>]*Click=\"OnStageUpdate\""), xaml);
    }

    private static string Body(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"لم يُعثر على {signature}");
        var rest = source[start..];
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
