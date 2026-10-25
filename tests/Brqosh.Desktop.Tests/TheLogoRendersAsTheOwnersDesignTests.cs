using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Brqosh.Desktop.Views.Controls;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (تصاميمُ المالك) الشعارُ يُرسَم فعلاً بالدرع والاسم والشريط: يُرسم <see cref="BrandLogo"/> على سطحٍ فاتحٍ وآخرَ داكنٍ ويُقاس أنّ كلَّ شريحةٍ من الشعار
/// (الدرع، الاسم، الشريط) فيها حبرٌ بلون الشعار — فلا يعود «الاسمُ» فارغاً بصمتٍ كما قد يحدث إن انكسر مورد الهندسة. ويُحفظ PNG لمن يعيّن
/// <c>BRQOSH_RENDER_DIR</c> للمعاينة البصريّة.
/// </summary>
public class TheLogoRendersAsTheOwnersDesignTests
{
    private static BitmapSource Render(string surfaceHex, string inkHex, FlowDirection parentFlow = FlowDirection.LeftToRight, bool animate = false, bool inWindow = false)
    {
        return WpfTestHost.Invoke(() =>
        {
            // ‏الفرشُ تُبنى على خيط الواجهة نفسِه — Freezable لا يعبر الخيوط.
            var surface = new SolidColorBrush((Color)ColorConverter.ConvertFromString(surfaceHex));
            var ink = new SolidColorBrush((Color)ColorConverter.ConvertFromString(inkHex));
            var logo = new BrandLogo { MarkHeight = 300, MarkBrush = ink, NameBrush = ink, HorizontalAlignment = HorizontalAlignment.Center, Animate = animate };
            var host = new Border { Background = surface, Width = 480, Height = 640, Child = logo, Padding = new Thickness(0, 30, 0, 0), FlowDirection = parentFlow };
            Window? window = null;
            if (inWindow)
            {
                // ‏كما في نافذة الدخول الحقيقيّة: نافذةٌ RTL تُعرض فعلاً (فيُطلَق Loaded وحركةُ الدخول) والشعارُ فيها.
                window = new Window { Content = host, Width = 520, Height = 700, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -4000, Top = 0, FlowDirection = parentFlow };
                host.FlowDirection = FlowDirection.LeftToRight;
                window.Show();
                for (var i = 0; i < 40; i++) { PumpFrames(); Thread.Sleep(50); }   // ‏حتى تنتهي حركةُ الدخول (٠٫٨ ث)
            }
            else
            {
                host.Measure(new Size(480, 640));
                host.Arrange(new Rect(0, 0, 480, 640));
                host.UpdateLayout();
                PumpFrames();
            }
            host.UpdateLayout();

            var bitmap = new RenderTargetBitmap(480, 640, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(host);
            bitmap.Freeze();
            window?.Close();
            return (BitmapSource)bitmap;
        });
    }

    private static void PumpFrames()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static int InkPixels(BitmapSource bitmap, int top, int bottom, Color ink)
    {
        var stride = bitmap.PixelWidth * 4;
        var data = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(data, stride, 0);
        var count = 0;
        for (var y = top; y < Math.Min(bottom, bitmap.PixelHeight); y++)
            for (var x = 0; x < bitmap.PixelWidth; x++)
            {
                var i = y * stride + x * 4;
                if (Math.Abs(data[i] - ink.B) < 12 && Math.Abs(data[i + 1] - ink.G) < 12 && Math.Abs(data[i + 2] - ink.R) < 12) count++;
            }
        return count;
    }

    [Theory]
    [InlineData("#FFFFFF", "#8B5A2B", "light")]
    [InlineData("#141413", "#F7EFE4", "dark")]
    public void TheCrestTheWordmarkAndTheBarAreAllDrawn(string surfaceHex, string inkHex, string name)
    {
        var surface = (Color)ColorConverter.ConvertFromString(surfaceHex);
        var ink = (Color)ColorConverter.ConvertFromString(inkHex);
        var bitmap = Render(surfaceHex, inkHex);

        // ‏الدرعُ 300 ارتفاعاً من y=30؛ ثمّ الاسمُ (≈ 0.13×300 فجوة، ارتفاع الحرف ≈ 46)؛ ثمّ الشريط.
        var crest = InkPixels(bitmap, 30, 330, ink);
        var word = InkPixels(bitmap, 365, 420, ink);
        var bar = InkPixels(bitmap, 425, 450, ink);

        Assert.True(crest > 5000, $"الدرعُ لم يُرسَم ({crest} نقطة).");
        Assert.True(word > 800, $"اسمُ «BRQOSH» لم يُرسَم ({word} نقطة) — هندسةُ BrandWordmark.xaml غيرُ محمَّلة؟");
        Assert.True(bar > 300, $"الشريطُ تحت الاسم لم يُرسَم ({bar} نقطة).");

        var dir = Environment.GetEnvironmentVariable("BRQOSH_RENDER_DIR");
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(dir, $"logo-{name}.png"));
            encoder.Save(file);
        }
    }

    [Fact]
    public void TheLogoIsNotMirroredInsideAnArabicRightToLeftWindow()
    {
        // ‏نافذةُ الدخول عربيّةٌ RTL: كان الشعارُ (الدرعُ والاسم) يظهر مقلوباً أفقيّاً فيها. اسمُ العلامة لاتينيٌّ ولا يُعكس — فيجب أن يطابق الرسمُ تحت أبٍ RTL الرسمَ تحت أبٍ LTR نقطةً بنقطة.
        var ltr = Render("#FFFFFF", "#8B5A2B", FlowDirection.LeftToRight, animate: false, inWindow: false);
        var rtl = Render("#FFFFFF", "#8B5A2B", FlowDirection.RightToLeft, animate: false, inWindow: false);

        static byte[] Pixels(BitmapSource b)
        {
            var data = new byte[b.PixelWidth * 4 * b.PixelHeight];
            b.CopyPixels(data, b.PixelWidth * 4, 0);
            return data;
        }

        var a = Pixels(ltr);
        var c = Pixels(rtl);
        var differing = 0;
        for (var i = 0; i < a.Length; i += 4)
            if (Math.Abs(a[i] - c[i]) > 24 || Math.Abs(a[i + 1] - c[i + 1]) > 24 || Math.Abs(a[i + 2] - c[i + 2]) > 24) differing++;

        Assert.True(differing < 400, $"الشعارُ تحت RTL يختلف عن LTR في {differing} نقطة — مقلوب؟");
    }
}
