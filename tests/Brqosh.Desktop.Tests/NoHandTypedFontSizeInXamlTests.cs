using System.IO;
using System.Text.RegularExpressions;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// حجم الخط في XAML يأتي من سلّم الخطوط في الثيم (<c>{DynamicResource FontSize.*}</c>)، لا رقماً يدوياً،
/// حتى يبقى للواجهة سلّمٌ واحد يمكن تعديله من مكان واحد.
/// </summary>
public class NoHandTypedFontSizeInXamlTests
{
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

    [Fact]
    public void NoXamlFileTypesAFontSizeByHand()
    {
        var root = Path.Combine(FindRepoRoot(), "src", "Brqosh.Desktop");
        var comment = new Regex("<!--.*?-->", RegexOptions.Singleline | RegexOptions.Compiled);
        var literal = new Regex("FontSize=\"[0-9.]+\"", RegexOptions.Compiled);
        var hits = new List<string>();
        var scanned = 0;
        foreach (var file in Directory.EnumerateFiles(root, "*.xaml", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                continue;

            scanned++;
            var text = comment.Replace(File.ReadAllText(file), string.Empty);
            foreach (Match m in literal.Matches(text))
                hits.Add($"{Path.GetRelativePath(root, file)}: {m.Value}");
        }

        Assert.True(scanned > 150, $"فُحص {scanned} ملفّاً فقط — تغيّر موضعُ الواجهة؟");
        Assert.True(hits.Count == 0, "أحجام خطوط يدوية في XAML — استعمل مفتاحاً من FontSize.* في الثيم:\n" + string.Join("\n", hits.Distinct()));
    }
}
