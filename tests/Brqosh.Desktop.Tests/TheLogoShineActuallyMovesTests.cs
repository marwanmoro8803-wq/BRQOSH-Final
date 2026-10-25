using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Brqosh.Desktop.Views.Controls;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (حركةُ الشعار) <b>الحركةُ تُقاس لا تُفترض.</b> التقاطُ إطاراتٍ من التطبيق الحيّ أثبت أنّ الدخولَ (ظهورٌ وتكبير) يعمل وأنّ اللمعةَ لا تتحرّك —
/// فهذا الاختبارُ يُشغّل الشعارَ على خيط واجهةٍ حقيقيّ ويقرأ قيمَ اللمعة بعد انقضاء وقتها: يجب أن تُزاح الفرشاةُ ويظهر الغطاء.
/// </summary>
public class TheLogoShineActuallyMovesTests
{
    /// <summary>يُدير حلقةَ الرسائل مرّةً: بلا ذلك لا تتقدّم حركاتُ WPF (تعتمد على نبضات العرض لا على Thread.Sleep).</summary>
    private static void PumpOnce()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    [Fact]
    public void TheShine_IsVisibleAndMoved_WhileItsSweepRuns()
    {
        var (opacities, shifts, entered) = WpfTestHost.Invoke(() =>
        {
            var logo = new BrandLogo { Animate = true, MarkHeight = 96 };
            var window = new Window
            {
                Content = logo, Width = 300, Height = 300, ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.Manual, Left = -4000, Top = 0,
            };
            window.Show();

            var shine = (Path)logo.FindName("Shine");
            var scale = (ScaleTransform)logo.FindName("MarkScale");
            var opacities = new List<double>();
            var shifts = new List<double>();
            var started = DateTime.UtcNow;
            while ((DateTime.UtcNow - started).TotalSeconds < 3.4)
            {
                PumpOnce();
                opacities.Add(shine.Opacity);
                var brush = (LinearGradientBrush)shine.Fill;
                shifts.Add(((TranslateTransform)brush.RelativeTransform).X);
                Thread.Sleep(40);
            }

            var entered = scale.ScaleX;
            window.Close();
            return (opacities, shifts, entered);
        });

        Assert.True(entered > 0.99, $"دخولُ الشعار لم يكتمل: المقياس {entered}.");
        Assert.True(opacities.Max() > 0.5, $"اللمعةُ لم تظهر قطّ — أعلى شفافيّةٍ {opacities.Max():0.00}.");
        Assert.True(shifts.Max() - shifts.Min() > 1.0, $"فرشاةُ اللمعة لم تُزَح — المدى {shifts.Max() - shifts.Min():0.00}.");
    }
}
