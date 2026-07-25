using System.IO;
using System.Windows;
using Brqosh.Infrastructure.Reporting;

namespace Brqosh.Desktop.Reporting;

public partial class DocumentPdfPreviewWindow : Window
{
    private readonly PrintResult _result;
    private readonly bool _autoPrint;
    private string? _tempPath;
    private bool _previewReady;

    public DocumentPdfPreviewWindow(PrintResult result, bool autoPrint = false)
    {
        _result = result;
        _autoPrint = autoPrint;
        InitializeComponent();
        TitleText.Text = result.SuggestedFileName;
        SizeText.Text = $"الحجم: {result.PdfBytes.Length / 1024.0:N1} ك.ب";
        Loaded += OnLoadedAsync;
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoadedAsync;
        try
        {
            _tempPath = await DocumentPrintHelper.WriteTempAsync(_result);
            await PdfWebViewHelper.InitializeForPdfAsync(PdfWebView, _tempPath);
            await PdfWebViewHelper.WaitForNavigationAsync(PdfWebView);
            _previewReady = true;

            if (_autoPrint)
                PdfWebViewHelper.ShowSystemPrintDialog(PdfWebView);
        }
        catch (Exception ex)
        {
            _previewReady = false;
            PrintBtn.IsEnabled = false;
            PdfWebView.Visibility = Visibility.Collapsed;
            FallbackPanel.Visibility = Visibility.Visible;
            FallbackText.Text =
                $"تعذّر تحميل المعاينة المضمّنة (WebView2): {ex.Message}\nيمكنك الفتح في العارض الافتراضي أو الحفظ.";
        }
    }

    private void OnPrint(object sender, RoutedEventArgs e)
    {
        if (!_previewReady)
        {
            MessageBox.Show(this, "المعاينة غير جاهزة — استخدم «فتح خارجياً» ثم اطبع.", "BRQOSH",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            PdfWebViewHelper.ShowSystemPrintDialog(PdfWebView);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnSaveAs(object sender, RoutedEventArgs e) => DocumentPrintHelper.TrySaveAs(_result, this);

    private async void OnOpenExternal(object sender, RoutedEventArgs e) => await OpenExternalAsync();

    private async Task OpenExternalAsync()
    {
        _tempPath ??= await DocumentPrintHelper.WriteTempAsync(_result);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_tempPath) { UseShellExecute = true });
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
