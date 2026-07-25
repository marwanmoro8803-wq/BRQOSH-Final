using System.Windows;
using System.Windows.Media;

namespace Brqosh.Desktop.Ui;

/// <summary>
/// حل موارد WPF الصحيح — UserControl.Resources فارغ محلياً؛
/// FindResource يبحث في Application.Resources المدمج.
/// </summary>
public static class UiResources
{
    public static Style StyleOf(FrameworkElement host, string key) =>
        (Style)host.FindResource(key);

    /// <summary>
    /// نسخة الفرشاة الحالية — للرسم على Canvas وما لا يقبل مرجع مورد.
    /// **لا تستعمله للون نصّ أو خلفية عنصر يبقى في الشجرة**؛ استعمل
    /// <see cref="BindBrush"/> وإلا تجمّد العنصر على لون الثيم وقت الإنشاء.
    /// </summary>
    public static Brush BrushOf(FrameworkElement host, string key) =>
        (Brush)host.FindResource(key);

    /// <summary>
    /// يربط خاصية بمورد فرشاة برباط حيّ يتتبّع تبديل الثيم (CEEF-217).
    ///
    /// <c>ThemeManager.SetColor</c> يستبدل الفرشاة المجمّدة بنسخة جديدة في القاموس،
    /// بينما <see cref="BrushOf"/> يلتقط النسخة القديمة ويثبّتها على العنصر — فيبقى
    /// بلون الثيم السابق. قِيس أثره فعلياً بلقطة شاشة: نصّ لوحة المساعد كان يُرسم
    /// بألوان الثيم الداكن (#F8FAFC و#CBD5E1) فوق خلفية فاتحة، بتباين 1.05 و1.48
    /// مقابل 4.5 المطلوبة — نصّ يكاد لا يُرى. مرجع المورد يُحدّث العنصر عند كل تبديل.
    /// </summary>
    public static T BindBrush<T>(this T element, DependencyProperty property, string key)
        where T : FrameworkElement
    {
        element.SetResourceReference(property, key);
        return element;
    }
}
