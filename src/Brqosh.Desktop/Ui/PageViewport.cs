using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Brqosh.Desktop.Ui;

/// <summary>
/// (UI-AUDIT · P1) <b>صفحةٌ أطولُ من مساحتها تُمرَّر — ولا يُكسر ترشيدُ الجداول.</b>
///
/// <para><b>ما كان:</b> <c>PageHost</c> في صفٍّ <c>*</c> بلا تمرير، و102 من 107 صفحات بلا تمريرٍ
/// خاصّ — فما زاد عن الشاشة يُقصّ ولا يُبلغ.</para>
///
/// <para><b>ولماذا لا <c>ScrollViewer</c> عارياً:</b> داخلَه يُقاس المحتوى بارتفاعٍ لا نهائيّ، فصفُّ
/// <c>*</c> يصير <c>Auto</c> والجدولُ يرسم كلَّ صفوفه (يسقط الـ virtualization) وتختفي أشرطةُ تمريره.
/// فقاعدتان بحسب الصفحة:</para>
/// <list type="bullet">
/// <item><b>صفحةٌ فيها جدولٌ أو قائمةٌ أو تمريرٌ خاص</b>: ارتفاعُها <b>محدود</b> =
/// <c>max(ارتفاع النافذة المرئيّ, <see cref="MinPageHeight"/>)</c>. فالجدولُ يملأ ويمرّر نفسَه كما كان،
/// وعلى شاشةٍ أقصرَ من الحدّ الأدنى تُمرَّر الصفحةُ كلُّها بدل أن يُقصّ رأسها أو ذيلها.</item>
/// <item><b>صفحةُ نموذجٍ أو تقرير بلا جدول</b>: ارتفاعُها الطبيعيّ (حدُّه الأدنى ارتفاعُ النافذة المرئيّ
/// ليملأ صفُّ <c>*</c>) — فتُمرَّر كاملةً مهما طالت.</item>
/// </list>
/// </summary>
public static class PageViewport
{
    /// <summary>أدنى ارتفاعٍ تُعطاه صفحةُ جدول — مساحةُ الصفحة القديمة عند MinHeight=700.</summary>
    public const double MinPageHeight = 520;

    public static void Attach(ScrollViewer viewport, FrameworkElement pageHost)
    {
        ArgumentNullException.ThrowIfNull(viewport);
        ArgumentNullException.ThrowIfNull(pageHost);

        void Apply()
        {
            var content = (pageHost as ContentControl)?.Content as DependencyObject;
            var available = viewport.ViewportHeight > 0 ? viewport.ViewportHeight : viewport.ActualHeight;
            if (double.IsNaN(available) || available <= 0)
                return;

            if (content is not null && HostsOwnScrolling(content))
            {
                pageHost.MinHeight = 0;
                pageHost.Height = Math.Max(available, MinPageHeight);
            }
            else
            {
                pageHost.Height = double.NaN;
                pageHost.MinHeight = available;
            }
        }

        viewport.SizeChanged += (_, _) => Apply();
        viewport.ScrollChanged += (_, e) =>
        {
            if (e.ViewportHeightChange != 0)
                Apply();
        };
        if (pageHost is ContentControl cc)
        {
            System.ComponentModel.DependencyPropertyDescriptor
                .FromProperty(ContentControl.ContentProperty, typeof(ContentControl))
                .AddValueChanged(cc, (_, _) =>
                {
                    Apply();
                    viewport.ScrollToTop();
                });
        }
        Apply();
    }

    /// <summary>هل في الشجرة المنطقيّة جدولٌ/قائمةٌ/تمريرٌ يحتاج ارتفاعاً محدوداً؟</summary>
    public static bool HostsOwnScrolling(DependencyObject root)
    {
        // ‏ListView يرث ListBox.
        if (root is DataGrid or ListBox or TreeView or ScrollViewer or DocumentViewerBase)
            return true;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            if (HostsOwnScrolling(child))
                return true;
        return false;
    }
}
