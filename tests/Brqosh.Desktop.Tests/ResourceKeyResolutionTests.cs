using System.IO;
using System.Text.RegularExpressions;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (CEEF-221) كل مفتاح مورد يُشار إليه في XAML يجب أن يكون معرَّفاً فعلاً.
///
/// مفتاح غير موجود **لا يُكتشف عند البناء**؛ يرمي وقت التشغيل لحظةَ تحليل الصفحة:
///   Cannot find resource named 'X'. Resource names are case sensitive.
/// فتفشل الصفحة في الفتح كلّياً، ويبقى محتوى الصفحة السابقة تحت عنوان الجديدة —
/// المستخدم يظنّ أن الشاشة «لا تعمل» بلا سبب ظاهر.
///
/// المقياس عند كتابة هذا الحارس: مفتاح واحد مفقود (`BorderSubtleBrush` في
/// `CompanySettingsPage.xaml`) أسقط **صفحة إعدادات المنشأة بالكامل** — وظهر في
/// المسح الحيّ 28 مرّة عبر اللغتين، ولم يمسّه أيٌّ من 590 اختباراً لأن لا اختبار
/// يُحلّل XAML الصفحات.
/// </summary>
public class ResourceKeyResolutionTests
{
    private static string DesktopRoot()
    {
        var root = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Brqosh.Desktop"));
        Assert.True(Directory.Exists(root), $"Desktop source root not found: {root}");
        return root;
    }

    private static IEnumerable<string> XamlFiles() =>
        Directory.EnumerateFiles(DesktopRoot(), "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static readonly Regex KeyDefinition = new(@"x:Key=""([^""{}]+)""", RegexOptions.Compiled);

    // {StaticResource Foo} و {DynamicResource Foo} — يُستثنى ما بدأ بـ{ (مثل {x:Static ...})
    private static readonly Regex KeyReference = new(
        @"\{(?:Static|Dynamic)Resource\s+(?:ResourceKey=)?([A-Za-z_][A-Za-z0-9_.]*)\s*\}", RegexOptions.Compiled);

    [Fact]
    public void EveryReferencedResourceKey_IsDefinedSomewhere()
    {
        var defined = new HashSet<string>(StringComparer.Ordinal);
        var files = XamlFiles().ToList();
        Assert.NotEmpty(files);

        foreach (var file in files)
        {
            foreach (Match m in KeyDefinition.Matches(File.ReadAllText(file)))
                defined.Add(m.Groups[1].Value);
        }

        var missing = new List<string>();
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            var lines = text.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                foreach (Match m in KeyReference.Matches(lines[i]))
                {
                    var key = m.Groups[1].Value;
                    if (!defined.Contains(key))
                        missing.Add($"{Path.GetFileName(file)}:{i + 1} → {key}");
                }
            }
        }

        Assert.True(missing.Count == 0,
            "مفاتيح موارد غير معرَّفة — تُسقط الصفحة وقت التشغيل:\n" +
            string.Join("\n", missing.Select(x => "  - " + x)));
    }

    /// <summary>حارس الحارس: التعبير النمطيّ يلتقط فعلاً — وإلا مرّ الاختبار فارغاً.</summary>
    [Fact]
    public void TheGuard_ActuallyScansReferences()
    {
        var count = XamlFiles().Sum(f => KeyReference.Matches(File.ReadAllText(f)).Count);

        Assert.True(count >= 500,
            $"عدد إشارات الموارد الملتقطة {count} — يُتوقع مئات؛ رقمٌ صغير يعني أن الحارس عمياء.");
    }
}
