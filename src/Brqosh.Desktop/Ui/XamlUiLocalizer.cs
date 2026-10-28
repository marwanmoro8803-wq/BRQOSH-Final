using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Brqosh.Desktop.Ui;

/// <summary>
/// G-27 runtime sweep — localizes static XAML Arabic text via <see cref="UiBilingualRegistry"/>.
/// </summary>
public static class XamlUiLocalizer
{
    private static readonly ConditionalWeakTable<DependencyObject, string> OriginalAr =
        new();

    public static void Apply(DependencyObject root)
    {
        // ‏الاتجاهُ لا يُضبط هنا: النافذةُ تأخذه من LocaleManager.RegisterWindows والصفحاتُ ترثه منها.
        Walk(root);
    }

    private static void Walk(DependencyObject node)
    {
        LocalizeNode(node);
        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < count; i++)
        {
            if (System.Windows.Media.VisualTreeHelper.GetChild(node, i) is DependencyObject child)
                Walk(child);
        }
    }

    private static void LocalizeNode(DependencyObject node)
    {
        switch (node)
        {
            case DataGrid grid:
                foreach (var col in grid.Columns)
                {
                    if (col.Header is string header)
                        col.Header = ResolveText(header);
                }
                break;
        }

        switch (node)
        {
            case TextBlock tb when tb.Text is { Length: > 0 } text && !IsLocBound(tb, TextBlock.TextProperty):
                tb.Text = Resolve(node, text);
                break;
            case TextBox box when box.Text is { Length: > 0 } text && ContainsArabic(text):
                // Preserve user input — only localize placeholders/tooltips
                break;
            case Button btn when btn.Content is string content && !IsLocBound(btn, ContentControl.ContentProperty):
                btn.Content = Resolve(node, content);
                break;
            case Label lbl when lbl.Content is string content && !IsLocBound(lbl, ContentControl.ContentProperty):
                lbl.Content = Resolve(node, content);
                break;
            case CheckBox cb when cb.Content is string content && !IsLocBound(cb, ContentControl.ContentProperty):
                cb.Content = Resolve(node, content);
                break;
            case RadioButton rb when rb.Content is string content && !IsLocBound(rb, ContentControl.ContentProperty):
                rb.Content = Resolve(node, content);
                break;
            case GroupBox gb when gb.Header is string header && !IsLocBound(gb, HeaderedContentControl.HeaderProperty):
                gb.Header = Resolve(node, header);
                break;
            case Expander exp when exp.Header is string header && !IsLocBound(exp, HeaderedContentControl.HeaderProperty):
                exp.Header = Resolve(node, header);
                break;
            case TabItem tab when tab.Header is string header && !IsLocBound(tab, HeaderedContentControl.HeaderProperty):
                tab.Header = Resolve(node, header);
                break;
        }

        if (node is FrameworkElement fe)
        {
            if (IsLocBound(fe, FrameworkElement.ToolTipProperty))
            {
                // {loc:Str} already tracks the language; assigning here would drop its binding.
            }
            else if (fe.ToolTip is string tip)
                fe.ToolTip = Resolve(node, tip);
            else if (fe.ToolTip is ToolTip tt && tt.Content is string tipContent)
                tt.Content = Resolve(node, tipContent);
        }
    }

    private static string Resolve(DependencyObject node, string text)
    {
        var hasStored = OriginalAr.TryGetValue(node, out var original);

        // نصّ غير عربيّ لم يُسجَّل له أصل — لا شأن للترجمة به.
        if (!ContainsArabic(text) && !hasStored)
            return text;

        // نصّ عربيّ جديد: إمّا أوّل مرور على العنصر، أو تغيّرت بياناته (بطاقة لوحة،
        // سطر نشاط، رسالة مساعد) — فيصير هو الأصل المحفوظ.
        //
        // **AddOrUpdate لا Add**: `ConditionalWeakTable.Add` يرمي
        // ArgumentException("An item with the same key has already been added")
        // متى وُجد المفتاح؛ وهذا الفرع لا يُدخَل إلا والمفتاح موجود فعلاً — أي
        // رمية مضمونة، تصعد إلى DispatcherUnhandledException كنافذة خطأ للمستخدم.
        if (ContainsArabic(text) && (!hasStored || text != original))
        {
            OriginalAr.AddOrUpdate(node, text);
            original = text;
        }

        return ResolveText(original ?? text);
    }

    /// <summary>A property fed by <c>{loc:Str}</c> updates itself; overwriting it would replace the binding with a fixed string.</summary>
    private static bool IsLocBound(DependencyObject node, DependencyProperty property) =>
        System.Windows.Data.BindingOperations.GetBinding(node, property)?.Source is LocSource;

    private static string ResolveText(string arabic) =>
        LocaleManager.T(arabic, UiBilingualRegistry.Lookup(arabic));

    private static bool ContainsArabic(string text)
    {
        foreach (var ch in text)
        {
            if (ch is >= '\u0600' and <= '\u06FF')
                return true;
        }
        return false;
    }
}
