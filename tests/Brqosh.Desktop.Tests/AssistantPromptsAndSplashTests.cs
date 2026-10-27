using System.IO;
using Brqosh.Application.Intelligence;
using Brqosh.Desktop.Navigation;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (1.0.32) ثلاثةُ أمورٍ طلبها المالك: خياراتُ المساعد تتبع الشاشة، وصفحةُ انتظارٍ بالشعار عند أوّل تهيئة، والشعارُ لا يُعكس في RTL.
/// </summary>
public class AssistantPromptsAndSplashTests
{
    private static string Src(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine([dir!.FullName, "src", "Brqosh.Desktop", .. parts]));
    }

    [Fact]
    public void EveryNavigationGroup_IsKnownToTheAssistant_AndEveryPageGetsFourPrompts()
    {
        foreach (var group in NavigationCatalog.Groups)
        {
            Assert.Contains(group.TitleAr, PageAssistantPrompts.KnownGroups);
            foreach (var page in group.Pages)
                Assert.Equal(4, PageAssistantPrompts.For(page.Key, group.TitleAr, page.TitleAr, null).Count);
        }
    }

    [Fact]
    public void TheMainWindow_RebuildsThePromptsOnEveryNavigation_FromThePageDomain()
    {
        var code = Src("MainWindow.xaml.cs");
        Assert.Contains("PageAssistantPrompts.For(_activePageKey", code, StringComparison.Ordinal);
        Assert.Contains("RenderAiQuickPrompts();   // ", code, StringComparison.Ordinal);
    }

    [Fact]
    public void TheFirstRun_ShowsTheSplashAfterTheWizardAndWhileTheMainWindowOpens_AndNeverLeavesItOpen()
    {
        var app = Src("App.xaml.cs");
        Assert.Contains("var firstRun = adminUsername is not null;", app, StringComparison.Ordinal);
        Assert.Contains("new Views.StartupSplashWindow()", app, StringComparison.Ordinal);
        Assert.Contains("opening?.Close();", app, StringComparison.Ordinal);
        Assert.Contains("Windows.OfType<Views.StartupSplashWindow>()", app, StringComparison.Ordinal);   // ‏يُغلق عند العطل أيضاً

        var splash = Src("Views", "StartupSplashWindow.xaml");
        Assert.Contains("BrandLogo", splash, StringComparison.Ordinal);
        Assert.Contains("ShineSeconds", splash, StringComparison.Ordinal);
    }

    [Fact]
    public void TheLogo_IsNotForcedLeftToRight_ItIsFlippedOnceExplicitlyUnderRtl()
    {
        var xaml = Src("Views", "Controls", "BrandLogo.xaml");
        Assert.DoesNotContain("FlowDirection=\"LeftToRight\"", xaml, StringComparison.Ordinal);   // ‏كان يُنتج الشعارَ معكوساً على الجهاز الحقيقيّ
        Assert.Contains("FaceFlip", xaml, StringComparison.Ordinal);
        var code = Src("Views", "Controls", "BrandLogo.xaml.cs");
        Assert.Contains("FlowDirection == FlowDirection.RightToLeft ? -1 : 1", code, StringComparison.Ordinal);
    }
}
