using System.IO;
using System.Text.RegularExpressions;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (أسئلةُ المالك · توحيدُ الثيم) <b>التطبيقُ بالكحليّ والذهبيّ كالمطبوعات</b> — لا فيروزيَّ ولا سماويَّ يتسلّل من لوحةٍ قديمة.
/// يقرأ المصدرَ كلَّه: لونٌ من عائلة الفيروزيّ/السماويّ القديمة يُسقط الاختبارَ فيُرجَع إلى لوحة الثيم.
/// </summary>
public class TheAppWearsThePrintPaletteTests
{
    private static readonly string[] OldTeal =
    [
        "0D9488", "0F766E", "14B8A6", "134E4A", "115E59", "CCFBF1", "99F6E4", "5EEAD4", "2DD4BF", "22D3EE", "06B6D4", "67E8F9", "0E7490", "0891B2",
    ];

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, "src", "Brqosh.Desktop");
    }

    [Fact]
    public void NoOldTealSurvivesInTheDesktopSources()
    {
        var files = Directory.EnumerateFiles(Root(), "*.*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".xaml", StringComparison.Ordinal) || f.EndsWith(".cs", StringComparison.Ordinal))
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();
        Assert.True(files.Count > 100, "القارئُ وجد ملفّاتٍ قليلة — أعمى.");

        var pattern = new Regex("#(" + string.Join("|", OldTeal) + ")\b", RegexOptions.IgnoreCase);
        var offenders = files.Where(f => pattern.IsMatch(File.ReadAllText(f))).Select(f => Path.GetRelativePath(Root(), f)).ToList();
        Assert.True(offenders.Count == 0, "ألوانٌ من اللوحة القديمة:\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void TheLightPrimaryIsThePrintNavy()
    {
        var theme = File.ReadAllText(Path.Combine(Root(), "Themes", "ThemeManager.cs"));
        Assert.Contains("\"PrimaryBrush\", \"#0F3D5E\"", theme, StringComparison.Ordinal);   // = PrintTheme.Navy
        Assert.Contains("\"#C9A227\"", theme, StringComparison.Ordinal);                      // = PrintTheme.Gold
    }
}
