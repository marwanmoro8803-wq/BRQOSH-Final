using System.IO;
using System.Text.RegularExpressions;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (CEEF-231 · ط2-٧) <b>لا شاشةَ تبني سطرَ رواتبَ بأصابعها.</b>
///
/// <para><b>ما كان:</b> <c>PayrollDialog</c> جدولٌ فارغ <b>يُكتب فيه اسمُ الموظّف نصّاً
/// حرّاً</b> وتُكتب حصّتا التأمينات بالأصابع، ثمّ يُستدعى <c>PayrollRun.AddLine</c>
/// مباشرةً. <b>فسطرٌ بلا <c>EmployeeId</c></b> — ومَن يقرأ الرواتبَ بعدُ يمرّ بالمعرّف:
/// <c>WageProtectionFileBuilder</c> يتخطّاه، <b>فيخرج ملفُّ حماية الأجور فارغاً</b> وهو
/// إلزاميٌّ نظاماً؛ والقسيمةُ بلا آيبان؛ <b>والسُّلفةُ لا تُستقطَع أبداً</b>.</para>
///
/// <para><b>ولماذا حارسٌ بنيويٌّ لا اختبارُ سلوك:</b> السلوكُ مقيسٌ في
/// <c>PayrollRealPathTests</c> — <b>وهو يقيس المسارَ الصحيح ولا يمنع مساراً ثانياً</b>.
/// وشاشةٌ تُضاف غداً تستدعي <c>AddLine</c> تُعيد العطلَ بحذافيره <b>والحزمةُ خضراء</b>.
/// فهذا يقيس أنّ للرواتب باباً واحداً.</para>
///
/// <para><b>وحدُّه مُعلَن</b>: يقيس أنّ الشاشةَ لا تبني السطرَ بيدها. <b>ولا يُثبت أنّ ما
/// تعرضه صحيح</b> — ذلك يقيسه <c>PayrollRealPathTests</c> على الآلة نفسِها.</para>
/// </summary>
public class PayrollEntryPathTests
{
    /// <summary>
    /// <b>القياسُ المركزيّ: لا استدعاءَ لـ<c>AddLine</c> على دورة رواتبَ في الواجهة.</b>
    ///
    /// <para><b>والبحثُ عن <c>AddLine</c> وحدَه يتّهم بالباطل</b> — فللمستندات كلِّها
    /// دالّةٌ بهذا الاسم، والفواتيرُ تبنيها في شاشاتها بحقّ. <b>فيُقيَّد المسحُ بالملفّات
    /// التي تذكر <c>PayrollRun</c></b>، وهي وحدَها موضعُ هذا العطل.</para>
    /// </summary>
    [Fact]
    public void NoDesktopScreen_BuildsAPayrollLineByHand()
    {
        var offenders = new List<string>();

        foreach (var (path, source) in DesktopSources())
        {
            if (!source.Contains("PayrollRun", StringComparison.Ordinal)) continue;
            if (!Regex.IsMatch(source, @"\bAddLine\s*\(")) continue;

            offenders.Add(Path.GetFileName(path));
        }

        Assert.True(offenders.Count == 0,
            "شاشاتٌ تبني سطرَ رواتبَ بيدها — والسطرُ يخرج بلا معرّف موظّف، "
            + "فيسقط من ملفّ حماية الأجور ومن القسيمة ولا تُستقطَع منه سُلفة:\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>
    /// <b>وحارسٌ للحارس: شاشةُ الرواتب تستدعي الآلةَ فعلاً.</b>
    ///
    /// <para>فحارسٌ يمنع الطريقَ الخطأ <b>ولا يُثبت أنّ الصحيحَ مسلوك</b> يمرّ خضراءَ على
    /// شاشةٍ لا تُنشئ دورةً أصلاً. <b>والفراغُ ليس إغلاقاً.</b></para>
    /// </summary>
    [Fact]
    public void ThePayrollDialog_BuildsThroughTheRegisterBackedBuilder()
    {
        var dialog = DesktopSources()
            .FirstOrDefault(s => Path.GetFileName(s.Path) == "PayrollDialog.xaml.cs");

        Assert.False(dialog.Source is null, "PayrollDialog.xaml.cs غير موجود.");
        Assert.Contains("PayrollRunBuilder", dialog.Source!, StringComparison.Ordinal);
        Assert.Contains("BuildAsync", dialog.Source!, StringComparison.Ordinal);
    }

    /// <summary>مصادرُ الواجهة، بلا تعليقاتٍ — فذِكرُ الاسم في تعليقٍ ليس استدعاءً.</summary>
    private static IReadOnlyList<(string Path, string Source)> DesktopSources()
    {
        var root = FindRepoDirectory("src/Brqosh.Desktop");
        if (root is null) return [];

        var result = new List<(string, string)>();
        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (file.EndsWith(".g.cs", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                continue;

            result.Add((file, StripComments(File.ReadAllText(file))));
        }

        return result;
    }

    private static string StripComments(string source)
    {
        var text = Regex.Replace(source, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        return Regex.Replace(text, @"//.*?$", " ", RegexOptions.Multiline);
    }

    private static string? FindRepoDirectory(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        return null;
    }
}
