using System.Reflection;
using System.Windows;
using System.Windows.Threading;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (CEEF-229 · م2-٥) <b>كلُّ عنصرٍ مسمّى في XAML يصير كائناً حقيقياً بعد البناء.</b>
///
/// <para><b>ما تفعله اختباراتُ النصّ:</b> تقرأ ملفَّ XAML وتتحقّق أنّ
/// <c>x:Name="KpiGrid"</c> <b>مكتوبٌ فيه</b>. وهذا يُثبت وجود الكلمة، لا وجود العنصر: اسمٌ
/// داخل <c>DataTemplate</c> أو <c>ControlTemplate</c> لا يُولّد حقلاً في الصنف، واسمٌ في
/// مورد لا يُربط، وعنصرٌ حُذف من الشجرة وبقي ذِكرُه في تعليقٍ يبقى الاختبار خضراء.</para>
///
/// <para><b>وما يفعله هذا — بلا قراءة نصٍّ إطلاقاً:</b> مولّدُ <c>InitializeComponent</c>
/// يُنشئ <b>حقلاً</b> في الصنف لكلّ <c>x:Name</c> في الشجرة، ويُسنده أثناء البناء. فيُبنى
/// الكائن، ثمّ يُقرأ كلُّ حقلٍ من هذه الحقول بالانعكاس: أيُّ حقلٍ بقي <c>null</c> يعني اسماً
/// معلَناً لم يتّصل بشيء — والكودُ الذي يستعمله سيرمي عند أوّل لمسة.</para>
///
/// <para>وهذا يشمل <b>كلّ</b> شاشةٍ وكلّ اسمٍ فيها، لا أربعَ عشرة شاشةً ببضعة أسماءٍ منتقاة.</para>
/// </summary>
public class NamedElementResolutionTests
{
    /// <summary>
    /// حقولٌ لا تُسنَد عند البناء — <b>ولكلٍّ سببٌ مكتوب</b>. الاسمُ بصيغة
    /// <c>Screen.Field</c>.
    /// </summary>
    private static readonly Dictionary<string, string> DeclaredUnbound = new()
    {
        // ‏تُملأ بعد أوّل قياس.
    };

    /// <summary>
    /// حقولُ العناصر التي يولّدها مُصرّف XAML: خاصّة، غيرُ ساكنة، ونوعُها عنصرُ واجهة.
    /// وتُستثنى الحقولُ التي يكتبها المبرمج بيده (المجموعات، الخدمات) لأنها ليست أسماءَ XAML.
    /// </summary>
    private static IReadOnlyList<FieldInfo> NamedElementFields(Type screen) =>
        [.. screen.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(f => typeof(DependencyObject).IsAssignableFrom(f.FieldType))
            .Where(f => !f.IsInitOnly)];

    /// <summary>
    /// <b>القياس المركزيّ.</b> لكلّ شاشةٍ تُبنى: كلُّ حقلِ عنصرٍ مسمّى غيرُ فارغ.
    /// </summary>
    [Fact]
    public void EveryNamedElement_IsBoundAfterConstruction()
    {
        var unbound = new List<string>();
        var screensChecked = 0;
        var fieldsChecked = 0;

        foreach (var screen in PageConstructionTests.ConstructablePageTypes())
        {
            var fields = NamedElementFields(screen);
            if (fields.Count == 0) continue;

            var result = WpfTestHost.Invoke(() =>
            {
                object? instance;
                try
                {
                    instance = Activator.CreateInstance(screen);
                }
                catch
                {
                    // ‏شاشةٌ لا تُبنى يحكم عليها PageConstructionTests — لا تُحسب هنا مرّتين.
                    return null as List<string>;
                }

                var missing = fields
                    .Where(f => f.GetValue(instance) is null)
                    .Select(f => $"{screen.Name}.{f.Name}")
                    .ToList();

                WpfTestHost.Release(instance);
                return missing;
            });

            if (result is null) continue;

            screensChecked++;
            fieldsChecked += fields.Count;
            unbound.AddRange(result.Where(n => !DeclaredUnbound.ContainsKey(n)));
        }

        Assert.True(screensChecked >= 60,
            $"لم تُفحص إلا {screensChecked} شاشة — القياس لا يمسّ المشروع.");
        Assert.True(fieldsChecked >= 300,
            $"لم يُفحص إلا {fieldsChecked} عنصراً مسمّى — العدد أقلّ من أن يكون مسحاً.");

        Assert.True(unbound.Count == 0,
            $"أسماءٌ معلَنة لم تتّصل بشيء ({unbound.Count}) — والكودُ الذي يستعملها يرمي عند أوّل لمسة:\n  "
            + string.Join("\n  ", unbound));
    }

    /// <summary>
    /// <b>حارسٌ للحارس.</b> لو صار الانعكاسُ لا يجد حقولاً — تغيّر مولّدُ XAML، أو صارت
    /// الحقولُ عامّةً أو <c>readonly</c> — لمرّ الاختبارُ أعلاه فارغاً. فيُتحقَّق من شاشةٍ
    /// بعينها نعرف أسماءها.
    /// </summary>
    [Fact]
    public void TheReflection_ReallySeesTheNamedElements()
    {
        var fields = NamedElementFields(typeof(Brqosh.Desktop.Views.Pages.ReceiptVoucherDialog))
            .Select(f => f.Name)
            .ToHashSet();

        Assert.Contains("MethodCombo", fields);
        Assert.Contains("BankPanel", fields);
        Assert.Contains("AllocGrid", fields);
    }
}
