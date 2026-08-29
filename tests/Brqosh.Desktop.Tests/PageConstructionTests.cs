using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (CEEF-229 · م2-٥) <b>هل تُفتح الشاشةُ أصلاً؟</b>
///
/// <para><b>ما كان يقوم مقام هذا:</b> تسعةٌ وستون اختباراً في ملفٍّ واحد
/// (<c>GoldenPathUiMarkupTests</c>) تقرأ ملفَّ XAML <b>نصّاً</b> وتتحقّق أنّ سلسلةً موجودةٌ
/// فيه. وهي تُثبت أنّ الكلمة مكتوبة، لا أنّ الصفحة تُبنى. فمفتاحُ موردٍ مكتوبٌ خطأً، أو مُحوِّلٌ
/// مفقود، أو رميٌ في الباني — كلُّها تُبقي الاختبارات التسعة والستّين خضراء بينما يضغط المالك
/// على القائمة فينهار البرنامج أو تظهر شاشةٌ فارغة.</para>
///
/// <para><b>وما يفعله هذا:</b> يُنشئ <b>كلّ</b> صفحةٍ وحوارٍ في المشروع على خيط واجهةٍ حقيقيّ
/// بموارد الثيم. والبناءُ وحده يُشغّل <c>InitializeComponent</c>: يُحلّل XAML، ويحلّ كلّ
/// <c>StaticResource</c>، ويُنفّذ جسمَ الباني. وتحميلُ البيانات مؤجَّلٌ إلى حدث
/// <c>Loaded</c> فلا يُطلق هنا — فالسؤال «هل تقوم الشاشة؟» لا «هل تعمل بلا قاعدة بيانات؟».</para>
///
/// <para><b>وهذا يغطّي ١١٢ صفحةً وحواراً</b> بدل الأربع عشرة التي تلمسها اختباراتُ النصّ.</para>
/// </summary>
public class PageConstructionTests
{
    /// <summary>
    /// شاشاتٌ لا تُبنى بلا سياق — <b>ولكلٍّ سببٌ مكتوب</b>. وإضافةُ سطرٍ هنا اعترافٌ بأنّ
    /// الشاشة تحتاج ما لا يوفّره الاختبار، لا تهرّبٌ من فشلٍ حقيقيّ.
    /// </summary>
    private static readonly Dictionary<string, string> DeclaredUnconstructable = new()
    {
        // ‏تُملأ بعد أوّل قياس.
    };

    /// <summary>كلُّ صفحةٍ أو حوارٍ له بانٍ بلا وسائط — وهو ما يستدعيه التنقّل الحقيقيّ.</summary>
    internal static IReadOnlyList<Type> ConstructablePageTypes() =>
        [.. typeof(Brqosh.Desktop.Views.Pages.DashboardPage).Assembly
            .GetTypes()
            .Where(t => !t.IsAbstract
                        && (typeof(UserControl).IsAssignableFrom(t) || typeof(Window).IsAssignableFrom(t))
                        && t.Namespace is not null
                        && t.Namespace.StartsWith("Brqosh.Desktop.Views", StringComparison.Ordinal)
                        && t.GetConstructor(Type.EmptyTypes) is not null)
            .OrderBy(t => t.FullName, StringComparer.Ordinal)];

    /// <summary>
    /// <b>القياس المركزيّ.</b> كلُّ شاشةٍ تُبنى دون رمي — أو تكون مُقَرّاً بها بسببٍ مكتوب.
    /// والرسالةُ تحمل نوعَ الاستثناء ورسالتَه، فالفشلُ يقول ما ينقص لا «سقط».
    /// </summary>
    [Fact]
    public void EveryScreen_ConstructsWithoutThrowing()
    {
        var failures = new List<string>();

        foreach (var type in ConstructablePageTypes())
        {
            if (DeclaredUnconstructable.ContainsKey(type.Name)) continue;

            var error = WpfTestHost.Invoke(() =>
            {
                try
                {
                    WpfTestHost.Release(Activator.CreateInstance(type));
                    return null as string;
                }
                catch (Exception ex)
                {
                    var root = ex;
                    while (root.InnerException is not null) root = root.InnerException;
                    return $"{type.Name}: {root.GetType().Name}: {root.Message}";
                }
            });

            if (error is not null) failures.Add(error);
        }

        Assert.True(failures.Count == 0,
            $"شاشاتٌ لا تقوم أصلاً ({failures.Count}) — والمالك يضغط على القائمة فينهار البرنامج:\n  "
            + string.Join("\n  ", failures));
    }

    /// <summary>
    /// <b>حارسٌ للحارس.</b> لو صار الانعكاسُ لا يجد شيئاً — تغيّرَ فضاءُ الأسماء، أو صارت
    /// الشاشاتُ بوسائط — لمرّ الاختبارُ أعلاه فارغاً وهو لا يقيس شيئاً.
    /// </summary>
    [Fact]
    public void TheSweep_ActuallyFindsTheScreens()
    {
        var types = ConstructablePageTypes();

        Assert.True(types.Count >= 60,
            $"لم يُعثر إلا على {types.Count} شاشة — الانعكاسُ لا يمسح المشروع فعلاً.");

        // ‏وشاشاتٌ بعينها يجب أن تكون فيها: العمودُ الفقريّ لمنشأةٍ تجارية.
        string[] backbone =
        [
            nameof(Brqosh.Desktop.Views.Pages.DashboardPage),
            nameof(Brqosh.Desktop.Views.Pages.SalesInvoiceDialog),
            nameof(Brqosh.Desktop.Views.Pages.PurchaseInvoiceDialog),
            nameof(Brqosh.Desktop.Views.Pages.ProductDialog),
        ];

        var names = types.Select(t => t.Name).ToHashSet();
        var missing = backbone.Where(n => !names.Contains(n)).ToList();

        Assert.True(missing.Count == 0, "شاشاتٌ أساسية خارج المسح: " + string.Join(" · ", missing));
    }

    /// <summary>
    /// <b>والإقرارُ ينظّف نفسه:</b> شاشةٌ مُقَرٌّ بأنها لا تُبنى ثمّ صارت تُبنى — يجب حذفُ
    /// سطرها، وإلا بقي الإقرارُ يُعفيها من القياس بعد أن زال سببُه.
    /// </summary>
    [Fact]
    public void NoDeclaration_OutlivesTheGapItDeclares()
    {
        var stillBroken = new List<string>();

        foreach (var (name, _) in DeclaredUnconstructable)
        {
            var type = ConstructablePageTypes().FirstOrDefault(t => t.Name == name);
            if (type is null) continue;

            var threw = WpfTestHost.Invoke(() =>
            {
                try
                {
                    WpfTestHost.Release(Activator.CreateInstance(type));
                    return false;
                }
                catch
                {
                    return true;
                }
            });

            if (threw) stillBroken.Add(name);
        }

        var stale = DeclaredUnconstructable.Keys.Except(stillBroken).ToList();

        Assert.True(stale.Count == 0,
            "إقراراتٌ بائتة — هذه الشاشات صارت تُبنى، فاحذف سطورها: " + string.Join(" · ", stale));
    }
}
