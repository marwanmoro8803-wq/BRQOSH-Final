using System.Windows;
using System.Windows.Controls;

namespace Brqosh.Desktop.Ui;

/// <summary>
/// («دفتر الحبر والنحاس») اختصاراتُ لوحة المفاتيح لصفحة القائمة الجارية — لم يكن في المشروع كلِّه <c>KeyBinding</c> واحد، وهي أوّلُ ما
/// يطلبه مستخدمُ برنامجٍ محاسبيّ. الاختصاراتُ تعمل على <b>اصطلاحٍ مقيس</b> لا على قائمة صفحات: ٥٥ من ٩٥ صفحةَ قائمةٍ تبدأ زرَّها
/// الأساسيّ بـ«＋» (إضافة)، فيكفي أن نجد أوّلَ زرٍّ ظاهرٍ مفعَّلٍ بهذا الاصطلاح — وهو يفتح حواراً فقط (لا يحذف ولا يرحّل).
/// <list type="bullet">
/// <item><c>Ctrl+N</c> — أوّلُ زرِّ «＋ …» في الصفحة.</item>
/// <item><c>Ctrl+F</c> / <c>F3</c> — حقلُ البحث (الاسمُ يحوي Search وإلا أوّلُ حقلٍ نصّيٍّ قابلٍ للكتابة).</item>
/// <item><c>F5</c> — إعادةُ تحميل الصفحة الجارية (في <c>MainWindow</c>).</item>
/// </list>
/// البحثُ في الشجرة <b>المنطقيّة</b> فيعمل قبل أوّل رسم، ويتجاوز كلَّ فرعٍ مخفيّ.
/// </summary>
internal static class PageShortcuts
{
    internal static Button? FindAddButton(DependencyObject root) =>
        Descendants(root).OfType<Button>().FirstOrDefault(b =>
            b.IsEnabled
            && b.Visibility == Visibility.Visible
            && b.Content is string label
            && (label.StartsWith('＋') || label.StartsWith('+')));

    internal static TextBox? FindSearchBox(DependencyObject root)
    {
        var boxes = Descendants(root).OfType<TextBox>()
            .Where(t => t.IsEnabled && !t.IsReadOnly && t.Visibility == Visibility.Visible)
            .ToList();
        return boxes.FirstOrDefault(t => t.Name.Contains("Search", StringComparison.OrdinalIgnoreCase))
               ?? boxes.FirstOrDefault();
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (child is UIElement { Visibility: not Visibility.Visible })
                continue;

            yield return child;
            foreach (var inner in Descendants(child))
                yield return inner;
        }
    }
}
