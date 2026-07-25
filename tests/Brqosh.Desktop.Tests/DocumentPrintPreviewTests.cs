using System.IO;
using Brqosh.Desktop.Reporting;

namespace Brqosh.Desktop.Tests;

public class DocumentPrintPreviewTests
{
    private static string DesktopRoot()
    {
        var dir = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "..", "..", "src", "Brqosh.Desktop"));
    }

    [Fact]
    public void PreviewWindow_UsesEmbeddedWebView2()
    {
        var xaml = File.ReadAllText(Path.Combine(DesktopRoot(), "Reporting", "DocumentPdfPreviewWindow.xaml"));
        Assert.Contains("WebView2", xaml);
        Assert.Contains("طباعة", xaml);
    }

    [Fact]
    public void DocumentPrintHelper_DefaultsToPreviewAndSaveOffer()
    {
        var options = DocumentDeliverOptions.Standard;
        Assert.True(options.Preview);
        Assert.True(options.OfferSaveAs);
        Assert.False(options.AutoPrint);

        var resolved = DocumentDeliverOptions.Resolve(default);
        Assert.True(resolved.Preview);
        Assert.True(resolved.OfferSaveAs);
    }
}
