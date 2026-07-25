using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Brqosh.Desktop.Ui;

/// <summary>رسم sparkline/min-chart خفيف للوحة التنفيذية.</summary>
public static class SparklineBuilder
{
    public static Canvas Create(IReadOnlyList<decimal> values, double width, double height, Brush stroke, Brush? fill = null)
    {
        var canvas = new Canvas { Width = width, Height = height, ClipToBounds = true };
        if (values.Count < 2)
            return canvas;

        var max = values.Max();
        var min = values.Min();
        var range = max - min;
        if (range == 0) range = 1;

        var step = width / (values.Count - 1);
        var points = new PointCollection();
        for (var i = 0; i < values.Count; i++)
        {
            var x = i * step;
            var y = height - (double)((values[i] - min) / range) * (height - 4) - 2;
            points.Add(new Point(x, y));
        }

        if (fill is not null)
        {
            var area = new PointCollection { new Point(0, height) };
            foreach (var p in points)
                area.Add(p);
            area.Add(new Point(width, height));
            canvas.Children.Add(new Polygon
            {
                Points = area,
                Fill = fill,
                StrokeThickness = 0,
                Opacity = 0.35,
            });
        }

        canvas.Children.Add(new Polyline
        {
            Points = points,
            Stroke = stroke,
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round,
        });

        return canvas;
    }
}
