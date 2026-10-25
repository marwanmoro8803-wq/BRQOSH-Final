using System.IO;
using System.Text.RegularExpressions;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// («دفتر الحبر والنحاس» · إمكانيّة الوصول) زرٌّ محتواه سهمٌ أو رمزٌ فقط (→ ← × ＋ …) يقرؤه قارئُ الشاشة «سهم» لا «الصفحة السابقة». كان في سبع صفحات أربعةَ عشر
/// زرَّ تنقّلٍ كذلك، لها تلميحٌ لكنّ التلميحَ مساعدةٌ لا اسم. يمسح هذا الحارسُ كلَّ XAML: زرٌّ بلا حرفٍ ولا رقمٍ في محتواه يلزمه
/// <c>AutomationProperties.Name</c> (ويجوز أن يكون نصَّ تلميحه المترجَم أصلاً).
/// </summary>
public class SymbolOnlyButtonsHaveANameTests
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
    public void EveryButtonWhoseContentIsOnlyASymbol_HasAnAutomationName()
    {
        var root = Path.Combine(FindRepoRoot(), "src", "Brqosh.Desktop");
        var comment = new Regex("<!--.*?-->", RegexOptions.Singleline);
        var button = new Regex("<Button\\b[^>]*>", RegexOptions.Singleline);
        var content = new Regex("\\bContent=\"([^\"]*)\"");
        var hasText = new Regex("[A-Za-z0-9\\u0600-\\u06FF]");
        var missing = new List<string>();
        var buttons = 0;
        foreach (var file in Directory.EnumerateFiles(root, "*.xaml", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                continue;

            var text = comment.Replace(File.ReadAllText(file), string.Empty);
            foreach (Match m in button.Matches(text))
            {
                buttons++;
                var c = content.Match(m.Value);
                if (!c.Success || hasText.IsMatch(c.Groups[1].Value) || c.Groups[1].Value.Length == 0)
                    continue;
                if (!m.Value.Contains("AutomationProperties.Name", StringComparison.Ordinal))
                    missing.Add($"{Path.GetRelativePath(root, file)}: «{c.Groups[1].Value}»");
            }
        }

        Assert.True(buttons > 300, $"فُحص {buttons} زرّاً فقط — تغيّر موضعُ الواجهة؟");
        Assert.True(missing.Count == 0, "أزرارُ رموزٍ بلا اسم أتمتة:\n" + string.Join("\n", missing));
    }
}
