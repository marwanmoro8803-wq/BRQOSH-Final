using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Brqosh.Desktop.Ui;

/// <summary>
/// (CEEF-227) تفريغ خلفية شعار المنشأة قبل تخزينه.
///
/// أغلب الشعارات تصل بخلفية بيضاء صلبة، فتظهر على الفاتورة مستطيلاً أبيض فوق ورقة
/// بيضاء — حدٌّ مرئيّ قبيح، وأسوأ إن كانت الترويسة ملوّنة.
///
/// **لماذا الملء من الحواف لا الاستبدال الشامل؟** استبدال كل بكسل فاتح يثقب
/// المناطق البيضاء **داخل** الشعار (عين حرف، فراغ شعار، نصّ أبيض على دائرة).
/// الملء يبدأ من إطار الصورة ولا ينتشر إلا في المتّصل به — فيزيل الخلفية وحدها
/// ويُبقي البياض المحاط بالرسم.
///
/// وحافة ناعمة: البكسلات الملاصقة للحدّ تُخفَّف تدريجياً بدل قطعها حادّاً، فلا
/// يظهر «سنّ منشار» حول الشعار عند التكبير للطباعة.
/// </summary>
public static class LogoImageProcessor
{
    /// <summary>عتبة القرب من الخلفية (0–255 لكل قناة).</summary>
    private const int ToleranceDefault = 32;

    /// <summary>
    /// يقرأ صورة ويعيد PNG بخلفية شفافة. يُعيد <c>null</c> إن تعذّر الفكّ —
    /// فيبقى الملف الأصليّ كما هو بدل أن يُفقد.
    /// </summary>
    public static byte[]? MakeBackgroundTransparent(string sourcePath, int tolerance = ToleranceDefault)
    {
        try
        {
            var decoded = new BitmapImage();
            decoded.BeginInit();
            decoded.UriSource = new Uri(sourcePath);
            decoded.CacheOption = BitmapCacheOption.OnLoad;
            decoded.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            decoded.EndInit();
            decoded.Freeze();

            var bgra = new FormatConvertedBitmap(decoded, PixelFormats.Bgra32, null, 0);
            bgra.Freeze();

            var width = bgra.PixelWidth;
            var height = bgra.PixelHeight;
            if (width <= 0 || height <= 0 || (long)width * height > 40_000_000L)
                return null;

            var stride = width * 4;
            var pixels = new byte[height * stride];
            bgra.CopyPixels(pixels, stride, 0);

            var background = SampleBackground(pixels, width, height, stride);
            FloodFillFromEdges(pixels, width, height, stride, background, tolerance);
            SoftenEdges(pixels, width, height, stride);

            var output = BitmapSource.Create(
                width, height, bgra.DpiX, bgra.DpiY, PixelFormats.Bgra32, null, pixels, stride);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(output));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            return stream.ToArray();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>لون الخلفية = الأكثر تكراراً بين بكسلات الإطار، لا افتراض «أبيض».</summary>
    private static (byte B, byte G, byte R) SampleBackground(byte[] px, int w, int h, int stride)
    {
        var counts = new Dictionary<int, int>();
        void Tally(int x, int y)
        {
            var i = y * stride + x * 4;
            var key = (px[i] << 16) | (px[i + 1] << 8) | px[i + 2];
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        for (var x = 0; x < w; x++) { Tally(x, 0); Tally(x, h - 1); }
        for (var y = 0; y < h; y++) { Tally(0, y); Tally(w - 1, y); }

        var top = counts.OrderByDescending(kv => kv.Value).First().Key;
        return ((byte)(top >> 16), (byte)((top >> 8) & 0xFF), (byte)(top & 0xFF));
    }

    private static void FloodFillFromEdges(
        byte[] px, int w, int h, int stride, (byte B, byte G, byte R) bg, int tolerance)
    {
        var visited = new bool[w * h];
        var queue = new Queue<int>();

        void Seed(int x, int y)
        {
            var id = y * w + x;
            if (!visited[id]) { visited[id] = true; queue.Enqueue(id); }
        }

        for (var x = 0; x < w; x++) { Seed(x, 0); Seed(x, h - 1); }
        for (var y = 0; y < h; y++) { Seed(0, y); Seed(w - 1, y); }

        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            var x = id % w;
            var y = id / w;
            var i = y * stride + x * 4;

            if (Math.Abs(px[i] - bg.B) > tolerance ||
                Math.Abs(px[i + 1] - bg.G) > tolerance ||
                Math.Abs(px[i + 2] - bg.R) > tolerance)
                continue;

            px[i + 3] = 0;   // شفاف

            if (x > 0) Seed(x - 1, y);
            if (x < w - 1) Seed(x + 1, y);
            if (y > 0) Seed(x, y - 1);
            if (y < h - 1) Seed(x, y + 1);
        }
    }

    /// <summary>يخفّف البكسلات الملاصقة للشفاف فلا تظهر حافة مسنّنة عند التكبير.</summary>
    private static void SoftenEdges(byte[] px, int w, int h, int stride)
    {
        var alpha = new byte[w * h];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                alpha[y * w + x] = px[y * stride + x * 4 + 3];

        for (var y = 1; y < h - 1; y++)
        {
            for (var x = 1; x < w - 1; x++)
            {
                if (alpha[y * w + x] == 0) continue;

                var clearNeighbours =
                    (alpha[(y - 1) * w + x] == 0 ? 1 : 0) +
                    (alpha[(y + 1) * w + x] == 0 ? 1 : 0) +
                    (alpha[y * w + x - 1] == 0 ? 1 : 0) +
                    (alpha[y * w + x + 1] == 0 ? 1 : 0);

                if (clearNeighbours > 0)
                    px[y * stride + x * 4 + 3] = (byte)(255 - clearNeighbours * 48);
            }
        }
    }
}
