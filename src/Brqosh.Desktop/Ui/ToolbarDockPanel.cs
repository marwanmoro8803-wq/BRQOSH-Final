using System.Windows;
using System.Windows.Controls;

namespace Brqosh.Desktop.Ui;

/// <summary>
/// شريطُ أدواتٍ يُوزَّع كـ<see cref="DockPanel"/> (يسارٌ/يمين) <b>لكنّه يلتفّ إلى سطرٍ ثانٍ بدل أن يعصر ما بعده</b>.
///
/// <para><b>ما كان:</b> أشرطةُ الأدوات (٨٤ صفحةً) <c>DockPanel LastChildFill="False"</c> — فإذا ضاق العرضُ (نافذةٌ صغيرة،
/// أو لوحةُ المساعد مفتوحة) أخذ كلُّ عنصرٍ ما بقي من العرض بالترتيب، <b>وصار آخرُ الأزرار والتواريخ بعرضٍ صفريٍّ أو
/// فوق ما قبله</b>: زرٌّ لا يظهر، وحقلُ تاريخٍ بعرض ٠، وزرُّ «عرض» خارجَ الشاشة بلا تمرير.</para>
///
/// <para><b>والآن:</b> تُجمَع العناصرُ في أسطرٍ بحسب العرض المتاح؛ في السطر يُرصّ ما اتّجاهُه <c>Left</c> من البداية
/// وما اتّجاهُه <c>Right</c> من النهاية — فإذا اتّسع العرضُ لكلّ شيءٍ فالنتيجةُ هي نتيجةُ <c>DockPanel</c> نفسُها
/// بالتمام. <b>لا عنصرَ يُخفى ولا يُصغَّر.</b></para>
/// </summary>
public sealed class ToolbarDockPanel : Panel
{
    public static readonly DependencyProperty RowSpacingProperty = DependencyProperty.Register(
        nameof(RowSpacing), typeof(double), typeof(ToolbarDockPanel),
        new FrameworkPropertyMetadata(6d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>الفراغُ العموديّ بين سطرين عند الالتفاف.</summary>
    public double RowSpacing
    {
        get => (double)GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    private sealed class Row
    {
        public readonly List<UIElement> Items = [];
        public double Width;
        public double Height;
    }

    private readonly List<Row> _rows = [];

    protected override Size MeasureOverride(Size available)
    {
        _rows.Clear();
        var limit = available.Width;
        var current = new Row();
        _rows.Add(current);

        foreach (UIElement child in InternalChildren)
        {
            if (child.Visibility == Visibility.Collapsed)
                continue;

            child.Measure(new Size(double.PositiveInfinity, available.Height));
            var width = child.DesiredSize.Width;
            // ‏مجموعةٌ واحدةٌ أعرضُ من السطر (WrapPanel بأزرارٍ كثيرة) تُقاس بعرض السطر فتلتفّ هي من الداخل.
            if (!double.IsInfinity(limit) && width > limit)
            {
                child.Measure(new Size(limit, available.Height));
                width = Math.Min(child.DesiredSize.Width, limit);
            }

            if (current.Items.Count > 0 && !double.IsInfinity(limit) && current.Width + width > limit + 0.5)
            {
                current = new Row();
                _rows.Add(current);
            }

            current.Items.Add(child);
            current.Width += width;
            current.Height = Math.Max(current.Height, child.DesiredSize.Height);
        }

        var widest = _rows.Max(r => r.Width);
        var height = _rows.Sum(r => r.Height) + RowSpacing * Math.Max(0, _rows.Count - 1);
        return new Size(double.IsInfinity(limit) ? widest : Math.Min(widest, limit), height);
    }

    protected override Size ArrangeOverride(Size final)
    {
        var y = 0d;
        foreach (var row in _rows)
        {
            var left = 0d;
            var right = final.Width;
            foreach (var item in row.Items)
            {
                var w = Math.Min(item.DesiredSize.Width, final.Width);
                if (DockPanel.GetDock(item) == Dock.Right)
                {
                    right -= w;
                    item.Arrange(new Rect(Math.Max(0, right), y, w, row.Height));
                }
                else
                {
                    item.Arrange(new Rect(left, y, w, row.Height));
                    left += w;
                }
            }

            y += row.Height + RowSpacing;
        }

        return final;
    }
}
