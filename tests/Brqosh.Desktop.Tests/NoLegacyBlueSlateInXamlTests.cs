using System.IO;
using System.Text.RegularExpressions;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// («دفتر الحبر والنحاس») بقايا لوحة Tailwind الزرقاء/الرماديّة-الزرقاء كانت مثبّتةً داخل أنماط الثيم وصفحاتِه
/// (#12384F كحليّ في القائمة البنّيّة · #1E293B بطاقةُ مستخدمٍ كحليّة · #DC2626 و#10B981 أحمرُ وأخضرُ قديمان…) فلا تتبع الثيم ولا تنقلب في الداكن،
/// وتُغيّر وجه البرنامج عمّا اعتمده المالك. يمسح هذا الحارسُ كلَّ ملفّ XAML في Desktop (بعد تجريد التعليقات) ويرفض أيّاً منها —
/// والمطلوبُ مفتاحٌ من الثيم (<c>DynamicResource</c>)، لا لونٌ جديدٌ يُضاف إلى هذه القائمة.
/// </summary>
public class NoLegacyBlueSlateInXamlTests
{
    private static readonly string[] Banned =
    {
        "12384F", "1D4B6A", "1E293B", "334155", "0F172A", "64748B", "94A3B8", "CBD5E1", "E2E8F0", "F1F5F9",
        "7F1D1D", "FEE2E2", "FECACA", "FCA5A5", "DC2626", "EF4444", "10B981", "059669", "047857",
        "2563EB", "3B82F6", "7C3AED", "4F46E5", "F59E0B",
    };

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
    public void NoXamlFileCarriesALegacyBlueOrSlateColour()
    {
        var root = Path.Combine(FindRepoRoot(), "src", "Brqosh.Desktop");
        var comment = new Regex("<!--.*?-->", RegexOptions.Singleline | RegexOptions.Compiled);
        var hex = new Regex("#([0-9A-Fa-f]{6})\\b", RegexOptions.Compiled);
        var hits = new List<string>();
        var scanned = 0;
        foreach (var file in Directory.EnumerateFiles(root, "*.xaml", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                continue;

            scanned++;
            var text = comment.Replace(File.ReadAllText(file), string.Empty);
            foreach (Match m in hex.Matches(text))
            {
                if (Banned.Contains(m.Groups[1].Value, StringComparer.OrdinalIgnoreCase))
                    hits.Add($"{Path.GetRelativePath(root, file)}: #{m.Groups[1].Value}");
            }
        }

        Assert.True(scanned > 150, $"فُحص {scanned} ملفّاً فقط — تغيّر موضعُ الواجهة؟");
        Assert.True(hits.Count == 0, "ألوانٌ قديمة في XAML — استعمل مفتاحاً من الثيم:\n" + string.Join("\n", hits.Distinct()));
    }
}
