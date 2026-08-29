using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Brqosh.Desktop.Themes;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (CEEF-222) نصّ الجداول يجب أن يُقرأ في الثيمين — قياساً لا ظنّاً.
///
/// كان نمط <c>DataGrid</c> يضبط <c>AlternatingRowBackground</c> بلون **مثبَّت نصّاً**
/// ولا يضبط <c>RowBackground</c> أصلاً، فتقع الصفوف الفردية على أبيض WPF الافتراضي.
/// و<c>Foreground</c> يتبع الثيم: في الوضع الداكن يصير <c>#F8FAFC</c> فوق <c>#FFFFFF</c>.
///
/// المقياس بلقطة شاشة حيّة على صفحة العملاء: تباين **1.05** لصفّ البيانات و**1.48**
/// لرأس العمود، مقابل 4.5 التي تشترطها WCAG AA. أي: نصٌّ غير مقروء في **كل** شاشة
/// فيها جدول — والثيم الداكن هو الافتراضي عند الإقلاع. هذا مصدر شكوى «الواجهة مبهمة».
///
/// الحارس يقرأ <see cref="Setter"/> الفعليّ من قاموس الثيم لا قيماً منسوخة هنا،
/// فلا ينفصل عن الحقيقة إن غُيّر النمط لاحقاً.
/// </summary>
public class DataGridContrastTests
{
    private const double WcagAaNormalText = 4.5;

    /// <summary>
    /// خيط STA واحد بمُرسِل حيّ، يُنشأ مرّة ويبقى.
    ///
    /// عنوان <c>pack://application:,,,</c> يحتاج تسجيل المخطّط **ومعالج طلباته** معاً؛
    /// وكلاهما يتمّ عند إنشاء <c>Application</c> لا بلمس <c>PackUriHelper</c> وحده
    /// (بدونه: "Invalid port specified"، ثم "The URI prefix is not recognized").
    /// وإنشاء Application مقصور على تطبيق واحد لكل عملية، فيُصنع هنا مرّة على خيطه
    /// وتُمرَّر إليه أجسام الاختبارات.
    /// </summary>
    private static readonly Dispatcher UiDispatcher = StartUiThread();

    /// <summary>
    /// (CEEF-229 · م2-٦) خيطُ الواجهة مشتركٌ الآن — انظر <see cref="WpfTestHost"/>.
    /// وكان كلُّ صنفٍ يُنشئ خيطاً وتطبيقاً، وWPF لا يسمح إلا بتطبيقٍ واحدٍ في العملية،
    /// فمن سبق ملكه — وضبطُ خصائصه من خيطٍ آخر يُسقط مُضيفَ الاختبارات كلَّه.
    /// </summary>
    private static Dispatcher StartUiThread() => WpfTestHost.Dispatcher;

    private static void OnUiThread(Action body) => UiDispatcher.Invoke(body);

    private static ResourceDictionary LoadTheme() => new()
    {
        Source = new Uri(
            "pack://application:,,,/Brqosh.Desktop;component/Themes/BrqoshTheme.xaml",
            UriKind.Absolute),
    };

    private static Style StyleFor(ResourceDictionary dict, Type target)
    {
        var style = dict[target] as Style;
        Assert.True(style is not null, $"نمط {target.Name} غير موجود في قاموس الثيم.");
        return style!;
    }

    /// <summary>يحلّ قيمة Setter سواء كانت فرشاة مباشرة أو {DynamicResource}.</summary>
    private static Color ResolveColor(Style style, DependencyProperty property, ResourceDictionary dict, string label)
    {
        var setter = style.Setters.OfType<Setter>().LastOrDefault(s => s.Property == property);
        Assert.True(setter is not null, $"{label}: لا Setter لـ{property.Name} — القيمة تقع على افتراضيّ WPF.");

        var value = setter!.Value;
        if (value is DynamicResourceExtension dyn)
            value = dict[dyn.ResourceKey];

        Assert.True(value is SolidColorBrush, $"{label}: قيمة {property.Name} ليست فرشاة لون صلبة ({value?.GetType().Name ?? "null"}).");
        return ((SolidColorBrush)value!).Color;
    }

    private static double RelativeLuminance(Color c)
    {
        static double Channel(byte v)
        {
            var s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
    }

    private static double Contrast(Color a, Color b)
    {
        var (la, lb) = (RelativeLuminance(a), RelativeLuminance(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DataGridRows_MeetWcagAa_InBothThemes(bool dark)
    {
        OnUiThread(() =>
        {
            var dict = LoadTheme();
            ThemeManager.ApplyTo(dict, dark);

            var grid = StyleFor(dict, typeof(DataGrid));
            var mode = dark ? "داكن" : "فاتح";

            var text = ResolveColor(grid, Control.ForegroundProperty, dict, $"DataGrid/{mode}");
            var row = ResolveColor(grid, DataGrid.RowBackgroundProperty, dict, $"DataGrid/{mode}");
            var alt = ResolveColor(grid, DataGrid.AlternatingRowBackgroundProperty, dict, $"DataGrid/{mode}");

            var rowContrast = Contrast(text, row);
            var altContrast = Contrast(text, alt);

            Assert.True(rowContrast >= WcagAaNormalText,
                $"[{mode}] صفّ عاديّ: نصّ {Hex(text)} فوق {Hex(row)} — تباين {rowContrast:N2} < {WcagAaNormalText}.");
            Assert.True(altContrast >= WcagAaNormalText,
                $"[{mode}] صفّ متناوب: نصّ {Hex(text)} فوق {Hex(alt)} — تباين {altContrast:N2} < {WcagAaNormalText}.");
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DataGridColumnHeaders_MeetWcagAa_InBothThemes(bool dark)
    {
        OnUiThread(() =>
        {
            var dict = LoadTheme();
            ThemeManager.ApplyTo(dict, dark);

            var header = StyleFor(dict, typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
            var mode = dark ? "داكن" : "فاتح";

            var text = ResolveColor(header, Control.ForegroundProperty, dict, $"Header/{mode}");
            var back = ResolveColor(header, Control.BackgroundProperty, dict, $"Header/{mode}");

            var contrast = Contrast(text, back);

            Assert.True(contrast >= WcagAaNormalText,
                $"[{mode}] رأس عمود: نصّ {Hex(text)} فوق {Hex(back)} — تباين {contrast:N2} < {WcagAaNormalText}.");
        });
    }

    /// <summary>
    /// وحارس للحارس: تبديل الثيم يغيّر الألوان فعلاً — وإلا كان الاختبار أعلاه
    /// يقيس نفس اللوحة مرّتين ويمرّ بلا معنى.
    /// </summary>
    [Fact]
    public void ThemeSwitch_ActuallyChangesGridColours()
    {
        OnUiThread(() =>
        {
            var dict = LoadTheme();

            ThemeManager.ApplyTo(dict, dark: true);
            var darkRow = ResolveColor(StyleFor(dict, typeof(DataGrid)), DataGrid.RowBackgroundProperty, dict, "dark");

            ThemeManager.ApplyTo(dict, dark: false);
            var lightRow = ResolveColor(StyleFor(dict, typeof(DataGrid)), DataGrid.RowBackgroundProperty, dict, "light");

            Assert.True(darkRow != lightRow,
                $"خلفية الصفّ لا تتبع الثيم — {Hex(darkRow)} في الوضعين.");
        });
    }
}
