using System.IO;
using Brqosh.Desktop.Navigation;
using Brqosh.Domain.Modules;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (CEEF-230 · ط6-١) <b>بندٌ في القائمة لا يفتح شيئاً.</b>
///
/// <para><b>ما كان:</b> الربطُ بين مفتاح القائمة والشاشة <c>switch</c> داخل
/// <c>MainWindow</c>، <b>وفرعُه الافتراضيّ يعرض «هذه الشاشة قيد البناء»</b>. فبندٌ بلا
/// حالةٍ في التبديل <b>يُفتح على لوحةٍ فارغة</b> — والمالكُ يضغط فلا يجد شيئاً.</para>
///
/// <para><b>ولا اختبارَ كان يراه:</b> <c>PageConstructionTests</c> يُثبت أنّ الصفحاتِ
/// <b>تقوم</b>، و<c>NavigationCatalogTests</c> يُثبت أنّ لها <b>عناوين</b> — <b>ولا شيءَ
/// يُثبت أنّ القائمةَ تصل إليها</b>. وثلاثُ قوائمَ كان يجب أن تتّفق ولا شيءَ يُلزمها:
/// <c>NavigationCatalog</c> والتبديلُ في النافذة و<c>NavKeyModuleMap</c> في أداة التدقيق.</para>
///
/// <para><b>وهذا القياسُ هو الآلة التي تُبنى قبل الشاشات</b>: بغيرها يبقى كلُّ بناءٍ
/// للواجهة ادّعاءً — <b>شيفرةٌ في المستودع لا يصل إليها أحد</b>.</para>
/// </summary>
public class NavigationReachabilityTests
{
    /// <summary>
    /// بنودٌ مُعلَنةٌ في القائمة <b>ولا شاشةَ لها بعد</b> — ولكلٍّ سببٌ مكتوب.
    ///
    /// <para><b>وإضافةُ سطرٍ هنا اعترافٌ يُوقَّع</b>: البندُ يُعرض للمالك ويفتح لوحةً
    /// فارغة. <b>وحذفُ السطر هو ما يُثبت أنّ الشاشةَ بُنيت</b> — لا وصفٌ في تقرير.</para>
    /// </summary>
    private static readonly Dictionary<string, string> DeclaredWithoutScreen = new(StringComparer.Ordinal)
    {
        // ‏يُملأ بأوّل قياس.
    };

    private static IEnumerable<string> CatalogKeys() =>
        NavigationCatalog.Groups.SelectMany(g => g.Pages).Select(p => p.Key).Distinct();

    /// <summary>
    /// <b>القياسُ المركزيّ: كلُّ بندٍ في القائمة يفتح شاشةً — أو مُقَرٌّ به بسببٍ مكتوب.</b>
    /// </summary>
    [Fact]
    public void EveryMenuItem_OpensARealScreen()
    {
        var missing = CatalogKeys()
            .Where(key => !PageFactory.HasScreen(key))
            .Where(key => !DeclaredWithoutScreen.ContainsKey(key))
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        Assert.True(missing.Count == 0,
            "بنودٌ في القائمة تفتح لوحةً فارغة — والمالكُ يراها وعداً لا ميزة:\n  "
            + string.Join("\n  ", missing));
    }

    /// <summary>
    /// <b>وحارسٌ للحارس: كلُّ ما أُقرّ به موجودٌ فعلاً في القائمة وبلا شاشة.</b>
    ///
    /// <para>فسطرٌ يبقى في الإقرار بعد بناء شاشته <b>يُخفي أنّ العمل تمّ</b>، ويجعل
    /// التقريرَ أسوأَ من الحقيقة. <b>وإقرارٌ لمفتاحٍ لا وجودَ له في القائمة أثرُ حذفٍ
    /// نُسي</b>.</para>
    /// </summary>
    [Fact]
    public void TheDeclaredList_HasNoStaleEntries()
    {
        var keys = CatalogKeys().ToHashSet(StringComparer.Ordinal);

        var stale = DeclaredWithoutScreen.Keys
            .Where(k => !keys.Contains(k) || PageFactory.HasScreen(k))
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        Assert.True(stale.Count == 0,
            "إقراراتٌ بائدة — إمّا بُنيت شاشتُها وإمّا حُذف بندُها:\n  " + string.Join("\n  ", stale));
    }

    /// <summary>
    /// <b>وكلُّ شاشةٍ في المصنع لها بندٌ يصل إليها.</b>
    ///
    /// <para>فشاشةٌ مبنيّةٌ ومربوطةٌ <b>ولا بندَ لها في القائمة</b> شيفرةٌ ميّتة: تُصان
    /// وتُترجَم وتُختبَر، <b>ولا يفتحها أحد</b>.</para>
    /// </summary>
    [Fact]
    public void EveryWiredScreen_IsReachableFromAMenu()
    {
        var keys = CatalogKeys().ToHashSet(StringComparer.Ordinal);

        var orphans = PageFactoryKeys()
            .Where(k => !keys.Contains(k))
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        Assert.True(orphans.Count == 0,
            "شاشاتٌ مربوطةٌ لا بندَ لها في القائمة — تُصان ولا يفتحها أحد:\n  "
            + string.Join("\n  ", orphans));
    }

    /// <summary>
    /// <b>وكلُّ بندٍ في القائمة له وحدةٌ في خريطة أداة التدقيق.</b>
    ///
    /// <para>وثالثةُ القوائم الثلاث. <b>وبندٌ يغيب عنها لا تفحصه الأداةُ في أيّ ملفّ عمل</b> —
    /// فيمرّ عطلُه إلى المالك، وتقولُ الأداةُ «تمّ الفحص».</para>
    /// </summary>
    [Fact]
    public void EveryMenuItem_HasAModuleInTheAuditMap()
    {
        var mapped = AuditMapKeys();
        if (mapped.Count == 0) return;   // ‏الأداةُ غيرُ مرجعيّةٍ من هذا المشروع.

        var missing = CatalogKeys().Where(k => !mapped.Contains(k))
            .OrderBy(k => k, StringComparer.Ordinal).ToList();

        Assert.True(missing.Count == 0,
            "بنودٌ لا تفحصها أداةُ التدقيق:\n  " + string.Join("\n  ", missing));
    }

    /// <summary>وكلُّ ملفِّ عملٍ يعرض بنوداً — فلا نظامَ عملٍ بقائمةٍ فارغة.</summary>
    [Theory]
    [MemberData(nameof(AllProfiles))]
    public void EveryProfile_ShowsReachableItems(BusinessProfile profile)
    {
        var keys = NavigationCatalog.ForProfile(profile)
            .SelectMany(g => g.Pages).Select(p => p.Key).ToList();

        Assert.NotEmpty(keys);
        Assert.All(keys, key =>
            Assert.True(PageFactory.HasScreen(key) || DeclaredWithoutScreen.ContainsKey(key),
                $"«{profile}» يعرض «{key}» ولا شاشةَ له."));
    }

    public static TheoryData<BusinessProfile> AllProfiles()
    {
        var data = new TheoryData<BusinessProfile>();
        foreach (var profile in Enum.GetValues<BusinessProfile>()) data.Add(profile);
        return data;
    }

    /// <summary>مفاتيحُ الربط — من مصدرها نفسِه، فلا تُكرَّر قائمةٌ رابعة.</summary>
    private static IEnumerable<string> PageFactoryKeys() => PageFactory.WiredKeys;

    private static HashSet<string> AuditMapKeys()
    {
        var source = FindRepoFile("tools/UiAudit/NavKeyModuleMap.cs");
        if (source is null) return [];

        return [.. System.Text.RegularExpressions.Regex
            .Matches(File.ReadAllText(source), "\\[\"([a-z0-9_]+)\"\\] = ModuleKey")
            .Select(m => m.Groups[1].Value)];
    }

    private static string? FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        return null;
    }
}
