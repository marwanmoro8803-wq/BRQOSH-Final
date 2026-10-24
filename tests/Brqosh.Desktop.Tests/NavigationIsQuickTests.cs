using System.Diagnostics;
using System.Windows;
using Brqosh.Desktop.Navigation;
using Xunit.Abstractions;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (أسئلةُ المالك 2026-10-24 · «هل التنقّل سريع؟ هل في تأخيرٍ في تحميل الصفحات؟») <b>زمنُ فتح كلِّ شاشةٍ في القائمة، مقيساً.</b>
///
/// <para>يُبنى كلُّ بندٍ في القائمة كما يفعل <c>Navigate</c> (المصنعُ نفسُه) ثم يُقاس ويُرتَّب (Measure/Arrange) بعرض نافذةٍ عاديّة — وهذا
/// ما يسبق أوّل رسم. <b>وهو لا يشمل قراءةَ البيانات</b>: تلك مقيسةٌ على دفاترَ لخمس سنوات في <c>FiveYearsOfBooksTests</c>. فالرقمان معاً
/// = زمنُ فتح الشاشة.</para>
///
/// <para>الحدّان: بناءٌ دافئ ≤ ٣٠٠ms لكلّ شاشة، وبناءٌ بارد (أوّلُ مرّة في العملية، يحمل JIT وتحليل XAML) ≤ ٢٫٥s.</para>
/// </summary>
public class NavigationIsQuickTests(ITestOutputHelper output)
{
    private const double WarmBudgetMs = 300;
    private const double ColdBudgetMs = 2_500;

    [Fact]
    public void EveryScreenInTheMenuOpensWithinTheBudget()
    {
        var keys = NavigationCatalog.Groups.SelectMany(g => g.Pages).Select(p => p.Key)
            .Where(PageFactory.HasScreen).Where(k => !PageFactory.WindowScopedKeys.Contains(k)).Distinct().ToList();
        Assert.True(keys.Count > 90, $"المسحُ وجد {keys.Count} شاشة فقط.");

        var cold = new Dictionary<string, double>();
        var warm = new Dictionary<string, double>();

        double Open(string key)
        {
            return WpfTestHost.Invoke(() =>
            {
                var sw = Stopwatch.StartNew();
                var page = PageFactory.Create(key)!;
                page.Measure(new Size(1_100, 650));
                page.Arrange(new Rect(0, 0, 1_100, 650));
                sw.Stop();
                WpfTestHost.Release(page);
                return sw.Elapsed.TotalMilliseconds;
            });
        }

        foreach (var key in keys) cold[key] = Open(key);
        foreach (var key in keys) warm[key] = Open(key);

        var slowestWarm = warm.OrderByDescending(p => p.Value).Take(5).Select(p => $"{p.Key} {p.Value:N0}ms");
        var slowestCold = cold.OrderByDescending(p => p.Value).Take(5).Select(p => $"{p.Key} {p.Value:N0}ms");
        output.WriteLine($"[تنقّل] {keys.Count} شاشة · الدافئ: متوسط {warm.Values.Average():N1}ms · الأبطأ: {string.Join(" · ", slowestWarm)}");
        output.WriteLine($"[تنقّل] البارد: متوسط {cold.Values.Average():N1}ms · الأبطأ: {string.Join(" · ", slowestCold)}");

        var tooSlowWarm = warm.Where(p => p.Value > WarmBudgetMs).Select(p => $"{p.Key} {p.Value:N0}ms").ToList();
        var tooSlowCold = cold.Where(p => p.Value > ColdBudgetMs).Select(p => $"{p.Key} {p.Value:N0}ms").ToList();
        Assert.True(tooSlowWarm.Count == 0, "شاشاتٌ تفتح ببطءٍ دافئةً: " + string.Join("، ", tooSlowWarm));
        Assert.True(tooSlowCold.Count == 0, "شاشاتٌ تفتح ببطءٍ باردةً: " + string.Join("، ", tooSlowCold));
    }
}
