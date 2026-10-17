using System.IO;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٥٠ · <c>installer-deployment-ops#9</c>) <b>التوصيل</b>: تركيبٌ جديدٌ لا يتجاوز فحصَ القاعدة بعد اليوم.
///
/// <para>كان <c>isFreshInstall ? null : DescribeIdentityProblemAsync(...)</c> — فالتركيبُ الجديدُ يتجاوز السؤالَ كلَّه،
/// وقاعدةٌ فيها منشأةٌ تُتبنّى صامتة. والقرارُ نفسُه مقيسٌ في <c>JoiningAnExistingCompanyTests</c>.</para>
/// </summary>
public class ASecondComputerJoinsByDecisionTests
{
    private static string AppSource =>
        File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "Brqosh.Desktop", "App.xaml.cs"));

    [Fact]
    public void AFreshInstall_IsAskedToJoin_AndTheEnteredAdministratorIsDropped()
    {
        var source = AppSource;

        Assert.DoesNotContain("isFreshInstall\n                        ? null", source.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.Contains("var join = await JoinExistingCompanyAsync(setup!);", source, StringComparison.Ordinal);

        // ‏الفرعُ حتى «else» الذي يليه — لا حتى أوّل «}» (وأوّلُها في النمط `is { }` نفسِه).
        var branch = source[source.IndexOf("if (join.Joined is { } joined)", StringComparison.Ordinal)..];
        branch = branch[..branch.IndexOf("problem = await DescribeIdentityProblemAsync", StringComparison.Ordinal)];
        Assert.Contains("adminUsername = null;", branch, StringComparison.Ordinal);
        Assert.Contains("adminPassword = null;", branch, StringComparison.Ordinal);

        Assert.Contains("JoiningAnExistingCompany.MustAsk(identity)", source, StringComparison.Ordinal);
        Assert.Contains("JoiningAnExistingCompany.Declined(setup)", source, StringComparison.Ordinal);
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
