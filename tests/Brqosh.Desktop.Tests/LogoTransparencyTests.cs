using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (CEEF-227) تفريغ خلفية الشعار — قياسٌ على بكسلات فعلية لا ثقة بالنيّة.
///
/// المطلب: تختفي الخلفية المتّصلة بالحواف، **ويبقى البياض المحاط بالرسم**. استبدالُ
/// كلّ بكسل فاتح — وهو الحلّ الساذج — يثقب عين الحرف وفراغَ الشعار الداخليّ.
/// لذلك يبني الاختبار صورة فيها الحالتان معاً: إطار أبيض خارجيّ، ومربّع أسود،
/// وداخله بقعة بيضاء **محبوسة**. ثم يفحص الثلاثة.
/// </summary>
public class LogoTransparencyTests
{
    private static readonly Dispatcher UiDispatcher = StartUiThread();

    private static Dispatcher StartUiThread()
    {
        Dispatcher? dispatcher = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            if (System.Windows.Application.Current is null)
                _ = new System.Windows.Application();
            dispatcher = Dispatcher.CurrentDispatcher;
            ready.Set();
            Dispatcher.Run();
        })
        { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(ready.Wait(TimeSpan.FromSeconds(30)), "خيط الواجهة لم يبدأ.");
        return dispatcher!;
    }

    private static void OnUiThread(Action body) => UiDispatcher.Invoke(body);

    /// <summary>شعار اصطناعي: خلفية بيضاء · مربّع أسود · بقعة بيضاء محبوسة داخله.</summary>
    private static string WriteSampleLogo()
    {
        const int size = 60;
        var stride = size * 4;
        var px = new byte[size * stride];

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var i = y * stride + x * 4;
                var inSquare = x >= 15 && x < 45 && y >= 15 && y < 45;
                var inHole = x >= 25 && x < 35 && y >= 25 && y < 35;
                var white = !inSquare || inHole;

                px[i] = px[i + 1] = px[i + 2] = white ? (byte)255 : (byte)0;
                px[i + 3] = 255;
            }
        }

        var src = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, px, stride);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(src));

        var path = Path.Combine(Path.GetTempPath(), $"brqosh-logo-{Guid.NewGuid():N}.png");
        using var fs = File.Create(path);
        encoder.Save(fs);
        return path;
    }

    private static byte AlphaAt(byte[] png, int x, int y)
    {
        using var ms = new MemoryStream(png);
        var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);
        var stride = frame.PixelWidth * 4;
        var buffer = new byte[frame.PixelHeight * stride];
        frame.CopyPixels(buffer, stride, 0);
        return buffer[y * stride + x * 4 + 3];
    }

    [Fact]
    public void EdgeBackground_BecomesTransparent()
    {
        OnUiThread(() =>
        {
            var path = WriteSampleLogo();
            try
            {
                var png = LogoImageProcessor.MakeBackgroundTransparent(path);

                Assert.NotNull(png);
                Assert.Equal(0, AlphaAt(png!, 2, 2));          // ركن الصورة
                Assert.Equal(0, AlphaAt(png!, 30, 5));         // أعلى المنتصف
            }
            finally { File.Delete(path); }
        });
    }

    /// <summary>الحارس الحاسم: البياض المحبوس داخل الرسم **لا يُثقب**.</summary>
    [Fact]
    public void WhiteEnclosedByArtwork_StaysOpaque()
    {
        OnUiThread(() =>
        {
            var path = WriteSampleLogo();
            try
            {
                var png = LogoImageProcessor.MakeBackgroundTransparent(path);

                Assert.NotNull(png);
                Assert.Equal(255, AlphaAt(png!, 30, 30));      // البقعة المحبوسة
                Assert.Equal(255, AlphaAt(png!, 20, 20));      // جسم الشعار الأسود
            }
            finally { File.Delete(path); }
        });
    }

    /// <summary>ملف غير صالح لا يرمي — يعيد null فيُحفظ الأصل بدل أن يضيع الشعار.</summary>
    [Fact]
    public void UnreadableFile_ReturnsNullInsteadOfThrowing()
    {
        OnUiThread(() =>
        {
            var path = Path.Combine(Path.GetTempPath(), $"brqosh-bad-{Guid.NewGuid():N}.png");
            File.WriteAllText(path, "ليست صورة");
            try
            {
                Assert.Null(LogoImageProcessor.MakeBackgroundTransparent(path));
            }
            finally { File.Delete(path); }
        });
    }
}
