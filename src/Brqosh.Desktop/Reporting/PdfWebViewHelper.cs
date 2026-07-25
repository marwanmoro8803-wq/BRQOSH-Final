using System.IO;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Brqosh.Desktop.Reporting;

/// <summary>WebView2 helpers — embedded PDF preview and system print dialog.</summary>
internal static class PdfWebViewHelper
{
    public static string UserDataFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BRQOSH",
        "webview2");

    public static async Task InitializeForPdfAsync(WebView2 webView, string pdfFilePath, CancellationToken ct = default)
    {
        Directory.CreateDirectory(UserDataFolder);

        var env = await CoreWebView2Environment.CreateAsync(null, UserDataFolder);
        await webView.EnsureCoreWebView2Async(env);

        var uri = new Uri(Path.GetFullPath(pdfFilePath)).AbsoluteUri;
        webView.CoreWebView2!.Navigate(uri);
    }

    public static async Task WaitForNavigationAsync(WebView2 webView, CancellationToken ct = default)
    {
        if (webView.CoreWebView2 is null)
            return;

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            webView.CoreWebView2!.NavigationCompleted -= Handler;
            tcs.TrySetResult(e.IsSuccess);
        }

        webView.CoreWebView2.NavigationCompleted += Handler;
        ct.Register(() => tcs.TrySetCanceled(ct));

        if (!await tcs.Task)
            throw new InvalidOperationException("تعذّر تحميل ملف PDF في المعاينة.");
    }

    public static void ShowSystemPrintDialog(WebView2 webView)
    {
        if (webView.CoreWebView2 is null)
            throw new InvalidOperationException("المعاينة غير جاهزة للطباعة بعد.");

        webView.CoreWebView2.ShowPrintUI(CoreWebView2PrintDialogKind.System);
    }
}
