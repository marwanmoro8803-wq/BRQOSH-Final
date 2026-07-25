using System.IO;
using System.Windows;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Security;
using Microsoft.Win32;

namespace Brqosh.Desktop.Reporting;

public readonly record struct DocumentDeliverOptions(
    bool Preview = true,
    bool OfferSaveAs = true,
    bool AutoPrint = false)
{
    public static DocumentDeliverOptions Standard => new(Preview: true, OfferSaveAs: true);

    public static DocumentDeliverOptions Resolve(DocumentDeliverOptions options) =>
        options == default ? Standard : options;
}

/// <summary>Desktop delivery — WebView2 preview, Save As, system print dialog.</summary>
public static class DocumentPrintHelper
{
    public static async Task DeliverAsync(
        PrintResult result,
        Window? owner = null,
        DocumentDeliverOptions options = default)
    {
        var opts = DocumentDeliverOptions.Resolve(options);
        if (opts.Preview)
        {
            new DocumentPdfPreviewWindow(result, opts.AutoPrint) { Owner = owner }.ShowDialog();
        }
        else
        {
            var path = await WriteTempAsync(result);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }

        if (opts.OfferSaveAs &&
            MessageBox.Show(owner, "هل تريد حفظ نسخة من الملف؟", "BRQOSH",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            TrySaveAs(result, owner);
        }
    }

    public static async Task ExportAndDeliverAsync(
        Func<DocumentPrintService, Task<PrintResult>> export,
        Window? owner = null,
        DocumentDeliverOptions options = default)
    {
        using var facade = new DesktopAccountingFacade();
        var result = await export(facade.GetService<DocumentPrintService>());
        await DeliverAsync(result, owner, options);
    }

    public static async Task ExportAndPrintAsync(
        Func<DocumentPrintService, Task<PrintResult>> export,
        Window? owner = null)
    {
        await ExportAndDeliverAsync(
            export,
            owner,
            new DocumentDeliverOptions(Preview: true, OfferSaveAs: false, AutoPrint: true));
    }

    public static async Task<string> WriteTempAsync(PrintResult result)
    {
        var path = Path.Combine(Path.GetTempPath(), result.SuggestedFileName);
        await File.WriteAllBytesAsync(path, result.PdfBytes);
        return path;
    }

    public static bool TrySaveAs(PrintResult result, Window? owner)
    {
        var dialog = new SaveFileDialog
        {
            FileName = result.SuggestedFileName,
            Filter = "PDF (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
        };
        if (dialog.ShowDialog(owner) != true)
            return false;

        File.WriteAllBytes(dialog.FileName, result.PdfBytes);
        MessageBox.Show(owner, "تم الحفظ بنجاح.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
        return true;
    }

    public static async Task DeliverReportAsync(
        byte[] pdfBytes,
        string suggestedFileName,
        string reportType,
        string periodKey,
        Window? owner = null,
        DocumentDeliverOptions options = default)
    {
        using var facade = new DesktopAccountingFacade();
        var service = facade.GetService<DocumentPrintService>();
        await service.LogReportExportAsync(
            AppServices.CompanyId,
            SecurityContext.ResolveUsername(),
            reportType,
            periodKey);
        await DeliverAsync(new PrintResult(pdfBytes, suggestedFileName), owner, options);
    }
}
