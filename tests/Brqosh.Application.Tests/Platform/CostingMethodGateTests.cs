using System.Text.RegularExpressions;

namespace Brqosh.Application.Tests.Platform;

/// <summary>
/// (CEEF-229 · م1-٢٢) <b>لا يُبدَّل مسلكُ التكلفة بالسنّارة.</b>
///
/// <para>حارسُ <c>CostingMethodService</c> يمنع التبديلَ وفي المستودعات بضاعة. <b>وهو بلا
/// قيمةٍ إن استدعى أحدٌ <c>SetCostingMethod</c> من النطاق مباشرةً</b> — وهو بالضبط ما كانت
/// تفعله شاشةُ إعدادات المنشأة قبل هذه الدفعة.</para>
///
/// <para>فهذا الحارسُ يمسح المستودعَ ويمنع عودةَ الباب. <b>وهو حارسُ نصٍّ لا سلوك</b> —
/// يُثبت أنّ لا مستدعيَ آخر، ولا يُثبت أنّ الخدمةَ صحيحة؛ ذاك عملُ
/// <c>CostingMethodSwitchTests</c>. والاثنان لازمان: أحدُهما يحرس المنطق والآخرُ يحرس
/// <b>الطريقَ إليه</b>.</para>
///
/// <para><b>ونطاقُ المسح <c>src/</c> وحدَه — وهو قرارٌ لا سهو.</b> الاختباراتُ تبني حالاتٍ
/// عمداً، ومنها ما يلزمه ضبطُ الطريقة قبل أيّ مخزون؛ فإدراجُها يُنشئ قائمةَ إقرارٍ تطول مع
/// كلّ اختبارٍ جديد <b>حتى تصير القائمةُ هي الثغرة</b>. والخطرُ في مسارات الإنتاج: شاشةٌ
/// أو خدمةٌ تُبدّل بلا فحص.</para>
/// </summary>
public class CostingMethodGateTests
{
    /// <summary>المستدعون المسموح لهم — ولكلٍّ سببٌ مكتوب.</summary>
    private static readonly Dictionary<string, string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Company.cs"] =
            "‏تعريفُ الدالّة نفسِها في النطاق.",
        ["CostingMethodService.cs"] =
            "‏الخدمةُ الحارسة — وهي الطريقُ الوحيد المقصود.",
        ["DatabaseInitializer.cs"] =
            "‏تهيئةُ منشأةٍ جديدة من قالب القطاع: لا مخزونَ بعد، فلا شيءَ يُفسده الضبط.",
        ["TenantProvisioningService.cs"] =
            "‏إنشاءُ مستأجرٍ جديد — السطرُ يلي CreateForTenant مباشرةً، فالمنشأةُ لم توجد بعد.",
    };

    [Fact]
    public void NoOneSetsTheCostingMethodOutsideTheGuardedService()
    {
        var root = RepositoryRoot();
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories))
        {
            if (IsGenerated(file)) continue;

            var name = Path.GetFileName(file);
            if (Allowed.ContainsKey(name)) continue;

            var source = StripComments(File.ReadAllText(file));
            if (!source.Contains("SetCostingMethod(", StringComparison.Ordinal)) continue;

            offenders.Add(Path.GetRelativePath(root, file));
        }

        Assert.True(offenders.Count == 0,
            "استُدعيت SetCostingMethod خارج الخدمة الحارسة — فالتبديلُ يمرّ بلا فحصِ المخزون:\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>
    /// <b>وحارسٌ على قائمة الإقرار نفسِها.</b> إقرارٌ لملفٍّ لم يعد يستدعي الدالّة يبقى
    /// ثغرةً مفتوحةً باسمٍ قديم: يكفي أن يُنشئ أحدٌ ملفّاً بذلك الاسم ليمرّ. <b>وهو درسُ
    /// «الاسم بلا جسد» من م2-١٢.</b>
    /// </summary>
    [Fact]
    public void EveryDeclaredExceptionStillCallsIt()
    {
        var root = RepositoryRoot();
        var live = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsGenerated(f))
            .Where(f => StripComments(File.ReadAllText(f))
                .Contains("SetCostingMethod(", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var phantom = Allowed.Keys.Where(k => !live.Contains(k)).ToList();

        Assert.True(phantom.Count == 0,
            "إقراراتٌ لملفّاتٍ لم تعد تستدعي SetCostingMethod — تُحذف، وإلا صارت باباً "
            + "يفتحه اسمٌ قديم:\n  " + string.Join("\n  ", phantom));
    }

    private static bool IsGenerated(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase)
        || path.Contains("CostingMethodGateTests", StringComparison.Ordinal)
        || path.Contains("CostingMethodSwitchTests", StringComparison.Ordinal);

    /// <summary>
    /// ‏يُجرَّد التعليقُ قبل المطابقة — <b>درسٌ من مسح الابتلاع</b>: حارسٌ هناك طابق تعليقاً
    /// يقتبس العطلَ نصّاً فأبلغ عن ملفَّين أُصلحا للتوّ.
    /// </summary>
    private static string StripComments(string source) =>
        Regex.Replace(
            Regex.Replace(source, @"/\*.*?\*/", "", RegexOptions.Singleline),
            @"//[^\n]*", "");

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
