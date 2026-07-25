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
        if (root is FrameworkElement fe)
            LocaleManager.ApplyTo(fe);
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
            case TextBlock tb when tb.Text is { Length: > 0 } text:
                tb.Text = Resolve(node, text);
                break;
            case TextBox box when box.Text is { Length: > 0 } text && ContainsArabic(text):
                // Preserve user input — only localize placeholders/tooltips
                break;
            case Button btn when btn.Content is string content:
                btn.Content = Resolve(node, content);
                break;
            case Label lbl when lbl.Content is string content:
                lbl.Content = Resolve(node, content);
                break;
            case CheckBox cb when cb.Content is string content:
                cb.Content = Resolve(node, content);
                break;
            case RadioButton rb when rb.Content is string content:
                rb.Content = Resolve(node, content);
                break;
            case GroupBox gb when gb.Header is string header:
                gb.Header = Resolve(node, header);
                break;
            case Expander exp when exp.Header is string header:
                exp.Header = Resolve(node, header);
                break;
            case TabItem tab when tab.Header is string header:
                tab.Header = Resolve(node, header);
                break;
        }

        if (node is FrameworkElement fe)
        {
            if (fe.ToolTip is string tip)
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
