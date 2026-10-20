using System.IO;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (جاهزيّةُ الإطلاق · installer-deployment-ops#13) <b>اتّفاقيّةُ الترخيص تُعرض قبل الإعداد</b> — و«لا أوافق» يُغلق، ونصُّها يُشحن
/// بجوار التنفيذيّ. والقبولُ ببصمة النصّ مقيسٌ في <c>TheLicenceAgreementIsAcceptedBeforeUseTests</c>؛ وهنا التوصيلُ بترتيبه.
/// </summary>
public class TheLicenceAgreementComesFirstTests
{
    private static readonly string Root = FindRepoRoot();

    [Fact]
    public void TheAgreement_IsAskedBeforeTheSetupWizard_AndDecliningShutsDown()
    {
        var app = File.ReadAllText(Path.Combine(Root, "src", "Brqosh.Desktop", "App.xaml.cs"));
        var eula = app.IndexOf("new Views.EulaWindow(eula).ShowDialog() != true", StringComparison.Ordinal);
        var wizard = app.IndexOf("new SetupWizardWindow(_setupStore)", StringComparison.Ordinal);

        Assert.True(eula >= 0 && wizard > eula, "الاتّفاقيّةُ قبل معالج الإعداد");
        Assert.Contains("Shutdown();", app[eula..wizard], StringComparison.Ordinal);
        Assert.Contains("!EulaAcceptance.IsAccepted(eula)", app[..eula], StringComparison.Ordinal);
    }

    [Fact]
    public void TheText_ShipsNextToTheExecutable()
    {
        var project = File.ReadAllText(Path.Combine(Root, "src", "Brqosh.Desktop", "Brqosh.Desktop.csproj"));

        Assert.Contains("Include=\"..\\..\\assets\\legal\\eula.txt\" Link=\"legal\\eula.txt\"", project, StringComparison.Ordinal);
        Assert.Contains("CopyToPublishDirectory=\"PreserveNewest\"",
            project[project.IndexOf("eula.txt", StringComparison.Ordinal)..], StringComparison.Ordinal);
    }

    [Fact]
    public void TheAcceptButton_WaitsForTheCheckbox()
    {
        var xaml = File.ReadAllText(Path.Combine(Root, "src", "Brqosh.Desktop", "Views", "EulaWindow.xaml"));

        Assert.Contains("IsEnabled=\"False\" Click=\"OnAccept\"", xaml, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("لم يُعثر على جذر المستودع.");
    }
}
