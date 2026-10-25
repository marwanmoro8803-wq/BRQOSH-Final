using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Brqosh.Domain.Companies;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (علمُ كلّ عملة) كلُّ عملةٍ في الكتالوج لها رمزُ دولةٍ وعلمٌ مضمَّنٌ في البرنامج، وقائمةُ اختيار العملة تعرض [علم] SAR SA فعلاً (يُقاس بالرسم).
/// </summary>
public class EveryCurrencyHasAFlagTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("جذرُ المستودع غير موجود.");
    }

    [Fact]
    public void EveryCatalogCurrency_HasACountryAndAFlagFile()
    {
        var flags = Path.Combine(Root(), "assets", "flags");
        var missing = new List<string>();
        foreach (var code in CurrencyCatalog.Codes)
        {
            var country = CurrencyCatalog.CountryCode(code);
            if (country.Length != 2 || !File.Exists(Path.Combine(flags, country.ToLowerInvariant() + ".png")))
                missing.Add($"{code}→'{country}'");
        }

        Assert.True(missing.Count == 0, "عملاتٌ بلا علم: " + string.Join(", ", missing));
        Assert.Equal("SA", CurrencyCatalog.CountryCode("SAR"));
        Assert.Equal("EU", CurrencyCatalog.CountryCode("EUR"));
        Assert.Equal("US", CurrencyCatalog.CountryCode("usd"));
        Assert.Equal("", CurrencyCatalog.CountryCode("ABC"));
    }

    [Fact]
    public void TheFlagsAreEmbeddedAsResources()
    {
        var project = File.ReadAllText(Path.Combine(Root(), "src", "Brqosh.Desktop", "Brqosh.Desktop.csproj"));
        Assert.Contains(@"<Resource Include=""..\..\assets\flags\*.png""", project, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCurrencyPickerShowsTheFlagTheCodeAndTheCountryCode()
    {
        var bitmap = WpfTestHost.Invoke(() =>
        {
            var combo = new ComboBox
            {
                ItemsSource = CurrencyCatalog.Codes, SelectedItem = "SAR", Width = 360, FontSize = 14, Padding = new Thickness(10, 8, 10, 8),
                ItemTemplate = (DataTemplate)System.Windows.Application.Current.Resources["CurrencyFlagItem"],
            };
            var host = new Border { Background = Brushes.White, Width = 420, Height = 80, Child = combo, Padding = new Thickness(20) };
            host.Measure(new Size(420, 80));
            host.Arrange(new Rect(0, 0, 420, 80));
            host.UpdateLayout();
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
            host.UpdateLayout();

            var rtb = new RenderTargetBitmap(420, 80, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(host);
            rtb.Freeze();
            return (BitmapSource)rtb;
        });

        var stride = bitmap.PixelWidth * 4;
        var data = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(data, stride, 0);

        // ‏علمُ السعوديّة أخضرُ داكن (≈ #006C35): يجب أن توجد نقاطٌ خضراء في موضع العلم من المربّع.
        var green = 0;
        for (var y = 20; y < 60; y++)
            for (var x = 20; x < 120; x++)
            {
                var i = y * stride + x * 4;
                if (data[i + 1] > 70 && data[i + 1] < 160 && data[i + 2] < 60 && data[i] < 90) green++;
            }

        var dir = Environment.GetEnvironmentVariable("BRQOSH_RENDER_DIR");
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(dir, "currency-picker.png"));
            encoder.Save(file);
        }

        Assert.True(green > 150, $"علمُ السعوديّة لا يظهر في قائمة العملة ({green} نقطة خضراء).");
    }
}
