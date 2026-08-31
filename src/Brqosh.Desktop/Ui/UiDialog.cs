using System.Windows;

namespace Brqosh.Desktop.Ui;

/// <summary>
/// (CEEF-229) <b>غلافُ الحوارات — وهو آليّةُ ترجمتها الوحيدة.</b>
///
/// <para><c>MessageBox.Show</c> نافذةُ Win32 لا عنصرٌ في شجرة الواجهة، <b>ولا يبلغها
/// <see cref="XamlUiLocalizer"/> مهما وُضع في السجلّ</b>. فكانت نصوصُ الحوارات — ١٨٦ استدعاءً
/// في ٥١ ملفّاً — <b>خارج آلية الترجمة كلِّها</b>: تبقى عربيةً في واجهةٍ إنجليزية، ولا تشكو
/// بوّابةُ الترجمة لأنّها تستثنيها صراحةً وبحقّ (إدخالُها في السجلّ كان يُخضِّر البوّابةَ
/// والنصُّ عربيٌّ كما هو — أيْ يجعل الحارسَ يكذب).</para>
///
/// <para><b>فالترجمةُ هنا عند الاستدعاء</b>: يُبحث عن النصّ في السجلّ حين تكون الواجهةُ
/// إنجليزية، ويُعاد كما هو إن لم يوجد — فلا يختفي نصٌّ ولا يُستبدل بفراغ.</para>
///
/// <para><b>والتواقيعُ مطابقةٌ لـ<c>MessageBox.Show</c> حرفاً بحرف</b>، عمداً: الهجرةُ
/// استبدالُ اسمٍ لا إعادةَ صياغة، فلا يتغيّر سلوكٌ ولا تُقرأ نتيجةٌ بغير ما كانت.</para>
///
/// <para><b>وما لا يبلغه هذا الغلاف:</b> النصوصُ المركَّبة (<c>$"…{x}…"</c>) ورسائلُ
/// الاستثناءات — لا تُطابَق ككلٍّ فتبقى عربية. وهي <b>معدودةٌ ومحروسةٌ من النموّ</b> في
/// <c>DialogLocalizationTests</c>، وعلاجُها صياغتُها بـ<see cref="LocaleManager.T"/>.</para>
/// </summary>
public static class UiDialog
{
    public static MessageBoxResult Show(string text) =>
        MessageBox.Show(Localize(text));

    public static MessageBoxResult Show(string text, string caption) =>
        MessageBox.Show(Localize(text), Localize(caption));

    public static MessageBoxResult Show(string text, string caption, MessageBoxButton button) =>
        MessageBox.Show(Localize(text), Localize(caption), button);

    public static MessageBoxResult Show(
        string text, string caption, MessageBoxButton button, MessageBoxImage icon) =>
        MessageBox.Show(Localize(text), Localize(caption), button, icon);

    public static MessageBoxResult Show(
        string text, string caption, MessageBoxButton button, MessageBoxImage icon,
        MessageBoxResult defaultResult) =>
        MessageBox.Show(Localize(text), Localize(caption), button, icon, defaultResult);

    public static MessageBoxResult Show(Window owner, string text) =>
        MessageBox.Show(owner, Localize(text));

    public static MessageBoxResult Show(Window owner, string text, string caption) =>
        MessageBox.Show(owner, Localize(text), Localize(caption));

    public static MessageBoxResult Show(
        Window owner, string text, string caption, MessageBoxButton button) =>
        MessageBox.Show(owner, Localize(text), Localize(caption), button);

    public static MessageBoxResult Show(
        Window owner, string text, string caption, MessageBoxButton button, MessageBoxImage icon) =>
        MessageBox.Show(owner, Localize(text), Localize(caption), button, icon);

    /// <summary>
    /// ‏الترجمةُ: بحثٌ في السجلّ حين تكون الواجهةُ إنجليزية، وإعادةُ الأصل إن لم يوجد.
    ///
    /// <para><b>عامّةٌ عمداً</b>: الحرّاسُ البنيويّون يُثبتون أنّ كلَّ نداءٍ يمرّ بالغلاف،
    /// <b>ولا يُثبتون أنّ الترجمةَ تقع</b> — ونافذةُ الحوار لا تُفتح في اختبار. فتُقاس
    /// الآليّةُ هنا مباشرةً.</para>
    /// </summary>
    public static string Localize(string text) =>
        LocaleManager.IsEnglish && !string.IsNullOrEmpty(text)
            ? UiBilingualRegistry.Lookup(text)
            : text;
}
