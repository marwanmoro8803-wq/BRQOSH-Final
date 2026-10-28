using System.IO;
using System.Text.RegularExpressions;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// اتجاه الواجهة يأتي من مكان واحد: <c>LocaleManager.RegisterWindows</c> يضبطه على كل نافذة ويقلبه مع اللغة،
/// والصفحات ترثه. فلا تكتب أي شاشة <c>FlowDirection="RightToLeft"</c> على جذرها (كان مكرراً في 188 ملفاً).
/// استثناءات <c>LeftToRight</c> المقصودة داخل الشاشات (أرقام، رموز، بريد، IBAN) مسموحة.
/// </summary>
public class RtlDirectionHasOneSourceTests
{
    private static string DesktopRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
                return Path.Combine(dir.FullName, "src", "Brqosh.Desktop");
            dir = dir.Parent;
        }

        throw new InvalidOperationException("لم يُعثر على جذر المستودع (Brqosh.sln).");
    }

    private static IEnumerable<string> Files(string pattern) =>
        Directory.EnumerateFiles(DesktopRoot(), pattern, SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    [Fact]
    public void NoScreenRootHardCodesRightToLeft()
    {
        var comment = new Regex("<!--.*?-->", RegexOptions.Singleline | RegexOptions.Compiled);
        var rootTag = new Regex(@"<(?![?!])(?<tag>[\w:.]+)[^>]*>", RegexOptions.Singleline | RegexOptions.Compiled);
        var hits = new List<string>();
        var roots = 0;
        foreach (var file in Files("*.xaml"))
        {
            var text = comment.Replace(File.ReadAllText(file), string.Empty);
            var m = rootTag.Match(text);
            if (!m.Success)
                continue;
            var tag = m.Groups["tag"].Value;
            if (tag is not ("Window" or "UserControl" or "Page"))
                continue;
            roots++;
            if (m.Value.Contains("FlowDirection=\"RightToLeft\"", StringComparison.Ordinal))
                hits.Add(Path.GetRelativePath(DesktopRoot(), file));
        }

        Assert.True(roots > 150, $"scanned only {roots} screens");
        Assert.True(hits.Count == 0, "FlowDirection=\"RightToLeft\" on screen root:\n" + string.Join("\n", hits));
    }

    [Fact]
    public void LocaleManagerIsTheOnlySourceOfRightToLeftInCode()
    {
        // ‏EulaWindow يحدّد اتجاه كل سطر حسب لغته، وBrandLogo يقرأ الاتجاه ليعكس الوجه — قراءةٌ لا ضبط.
        var allowed = new[] { "LocaleManager.cs", "EulaWindow.xaml.cs", "BrandLogo.xaml.cs" };
        var hits = Files("*.cs")
            .Where(f => !allowed.Contains(Path.GetFileName(f)))
            .Where(f => File.ReadAllText(f).Contains("FlowDirection.RightToLeft", StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(DesktopRoot(), f))
            .ToList();
        Assert.True(hits.Count == 0, "FlowDirection.RightToLeft outside LocaleManager:\n" + string.Join("\n", hits));

        var locale = File.ReadAllText(Path.Combine(DesktopRoot(), "Ui", "LocaleManager.cs"));
        Assert.Contains("public static void RegisterWindows()", locale, StringComparison.Ordinal);
        Assert.Contains("LocaleManager.RegisterWindows();", File.ReadAllText(Path.Combine(DesktopRoot(), "App.xaml.cs")), StringComparison.Ordinal);
    }
}
