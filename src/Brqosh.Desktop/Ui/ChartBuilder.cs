using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Brqosh.Desktop.Ui;

/// <summary>نقطة رسم: قيمة وتسمية محورها.</summary>
public readonly record struct ChartPoint(string Label, decimal Value);

/// <summary>
/// (CEEF-216) رسوم لوحة التحكم — بمحاور وقيم وخطّ أساس.
///
/// كان الرسم خطاً وحيداً في صندوق داكن: بلا شبكة، بلا تسميات محاور، بلا قيم، وبلا
/// خطّ صفر — فيستحيل قراءة رقم منه، وتبدو السالبة كالموجبة. وأسوأ: كان يُرسم
/// كخطٍّ متصل حتى لمقارنة فئات مختلفة (إيراد/مصروف/ربح)، والخط يوحي بتدرّج زمنيّ
/// لا وجود له. الأعمدة هي الشكل الصحيح للمقارنة.
/// </summary>
public static class ChartBuilder
{
    private const double AxisGutter = 62;   // مساحة تسميات المحور الرأسي
    private const double LabelStrip = 18;   // شريط تسميات المحور الأفقي
    // مساحة علوية محجوزة لتسمية القيمة: بدونها يبلغ أطولُ عمود السقفَ فتتداخل
    // تسميته مع رأسه وتصير غير مقروءة (رُصد بلقطة شاشة).
    private const double ValueHeadroom = 20;

    public static UIElement CreateLine(
        IReadOnlyList<ChartPoint> points, double width, double height,
        Brush stroke, Brush fill, Brush axisText, Brush gridLine) =>
        Create(points, width, height, stroke, fill, axisText, gridLine, asBars: false);

    public static UIElement CreateBars(
        IReadOnlyList<ChartPoint> points, double width, double height,
        Brush stroke, Brush fill, Brush axisText, Brush gridLine) =>
        Create(points, width, height, stroke, fill, axisText, gridLine, asBars: true);

    private static UIElement Create(
        IReadOnlyList<ChartPoint> points, double width, double height,
        Brush stroke, Brush fill, Brush axisText, Brush gridLine, bool asBars)
    {
        var canvas = new Canvas { Width = width, Height = height };
        if (points.Count == 0)
            return canvas;

        var plotWidth = Math.Max(width - AxisGutter, 10);
        var plotHeight = Math.Max(height - LabelStrip, 10);

        var max = points.Max(p => p.Value);
        var min = points.Min(p => p.Value);

        // اشمل الصفر دائماً: بدونه تبدو قيم متقاربة كأنها تتأرجح بعنف،
        // ولا يظهر الفرق بين ربح وخسارة.
        if (max < 0) max = 0;
        if (min > 0) min = 0;
        if (max == min) max = min + 1;

        double Y(decimal value) =>
            plotHeight - (double)((value - min) / (max - min)) * (plotHeight - ValueHeadroom - 3) - 3;

        // شبكة أفقية بثلاثة مستويات مع قيمها — أساس قراءة الرقم من الرسم.
        foreach (var value in new[] { max, min + (max - min) / 2m, min })
        {
            var y = Y(value);
            canvas.Children.Add(Place(new Line
            {
                X1 = AxisGutter, X2 = width, Y1 = y, Y2 = y,
                Stroke = gridLine, StrokeThickness = 1, Opacity = 0.35,
            }));

            canvas.Children.Add(Place(new TextBlock
            {
                Text = Compact(value),
                Foreground = axisText,
                FontSize = 10,
                Width = AxisGutter - 6,
                TextAlignment = TextAlignment.Left,
            }, 0, y - 8));
        }

        // خطّ الصفر أوضح من الشبكة — الفاصل بين ربح وخسارة.
        if (min < 0 && max > 0)
        {
            var zeroY = Y(0m);
            canvas.Children.Add(Place(new Line
            {
                X1 = AxisGutter, X2 = width, Y1 = zeroY, Y2 = zeroY,
                Stroke = axisText, StrokeThickness = 1, Opacity = 0.8,
            }));
        }

        if (asBars)
            DrawBars(canvas, points, AxisGutter, plotWidth, plotHeight, Y, fill, stroke, axisText);
        else
            DrawLine(canvas, points, AxisGutter, plotWidth, plotHeight, Y, fill, stroke, axisText);

        return canvas;
    }

    private static void DrawBars(
        Canvas canvas, IReadOnlyList<ChartPoint> points,
        double left, double plotWidth, double plotHeight,
        Func<decimal, double> y, Brush fill, Brush stroke, Brush axisText)
    {
        var slot = plotWidth / points.Count;
        var barWidth = Math.Max(slot * 0.55, 8);

        for (var i = 0; i < points.Count; i++)
        {
            var p = points[i];
            var x = left + i * slot + (slot - barWidth) / 2;
            var top = Math.Min(y(p.Value), y(0m));
            var barHeight = Math.Max(Math.Abs(y(p.Value) - y(0m)), 1);

            canvas.Children.Add(Place(new Rectangle
            {
                Width = barWidth, Height = barHeight,
                Fill = p.Value < 0 ? stroke : fill,
                Opacity = p.Value < 0 ? 0.55 : 0.9,
                RadiusX = 3, RadiusY = 3,
            }, x, top));

            // القيمة فوق العمود — الغرض من المقارنة قراءة الرقم لا تخمينه.
            canvas.Children.Add(Place(new TextBlock
            {
                Text = Compact(p.Value),
                Foreground = axisText, FontSize = 10, FontWeight = FontWeights.SemiBold,
                Width = slot, TextAlignment = TextAlignment.Center,
            }, left + i * slot, Math.Max(top - 15, 0)));

            canvas.Children.Add(Place(new TextBlock
            {
                Text = p.Label,
                Foreground = axisText, FontSize = 10, Opacity = 0.85,
                Width = slot, TextAlignment = TextAlignment.Center,
            }, left + i * slot, plotHeight + 2));
        }
    }

    private static void DrawLine(
        Canvas canvas, IReadOnlyList<ChartPoint> points,
        double left, double plotWidth, double plotHeight,
        Func<decimal, double> y, Brush fill, Brush stroke, Brush axisText)
    {
        var step = points.Count > 1 ? plotWidth / (points.Count - 1) : 0;
        var line = new PointCollection();
        for (var i = 0; i < points.Count; i++)
            line.Add(new Point(left + i * step, y(points[i].Value)));

        var area = new PointCollection { new Point(left, y(0m)) };
        foreach (var p in line) area.Add(p);
        area.Add(new Point(left + (points.Count - 1) * step, y(0m)));

        canvas.Children.Add(Place(new Polygon
        {
            Points = area, Fill = fill, Opacity = 0.28, StrokeThickness = 0,
        }));

        canvas.Children.Add(Place(new Polyline
        {
            Points = line, Stroke = stroke, StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round,
        }));

        for (var i = 0; i < points.Count; i++)
        {
            var px = left + i * step;
            var py = y(points[i].Value);

            canvas.Children.Add(Place(new Ellipse
            {
                Width = 6, Height = 6, Fill = stroke,
            }, px - 3, py - 3));

            canvas.Children.Add(Place(new TextBlock
            {
                Text = points[i].Label,
                Foreground = axisText, FontSize = 10, Opacity = 0.85,
                Width = Math.Max(step, 34), TextAlignment = TextAlignment.Center,
            }, px - Math.Max(step, 34) / 2, plotHeight + 2));
        }

        // قيمة آخر نقطة — الرقم الذي يهمّ في اتجاه زمنيّ.
        var last = points[^1];
        canvas.Children.Add(Place(new TextBlock
        {
            Text = Compact(last.Value),
            Foreground = axisText, FontSize = 10, FontWeight = FontWeights.SemiBold,
        }, left + (points.Count - 1) * step - 26, Math.Max(y(last.Value) - 16, 0)));
    }

    private static T Place<T>(T element, double? x = null, double? y = null) where T : UIElement
    {
        if (x is double left) Canvas.SetLeft(element, left);
        if (y is double top) Canvas.SetTop(element, top);
        return element;
    }

    /// <summary>صيغة مختصرة تُبقي الرقم مقروءاً في مساحة ضيقة (١٢٣٤٥ → 12.3K).</summary>
    private static string Compact(decimal value)
    {
        var abs = Math.Abs(value);
        var sign = value < 0 ? "-" : "";
        return abs switch
        {
            >= 1_000_000 => $"{sign}{abs / 1_000_000:0.#}M",
            >= 1_000 => $"{sign}{abs / 1_000:0.#}K",
            _ => value.ToString("0.##", CultureInfo.InvariantCulture),
        };
    }
}
