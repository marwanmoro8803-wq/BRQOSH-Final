using System.IO;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Zatca;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Reporting;
using Brqosh.Desktop.Security;
using Brqosh.Infrastructure.Security;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class ZatcaInvoicesPage : UserControl
{
    public record Row(long Id, long SourceDocumentId, ZatcaDocumentType DocumentType,
        string InvoiceNumber, string TypeAr, string UuidShort, int Icv,
        string ModeAr, string StatusAr, string GeneratedAt, string QrShort);

    public ZatcaInvoicesPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (SubmitBtn, PermissionService.CanPostAccounting));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var docs = await db.ZatcaEInvoiceDocuments
            .Where(d => d.CompanyId == AppServices.CompanyId)
            .OrderByDescending(d => d.GeneratedAtUtc).Take(100).ToListAsync();

        var rows = docs.Select(d => new Row(
            d.Id, d.SourceDocumentId, d.DocumentType, d.InvoiceNumber,
            d.DocumentType == ZatcaDocumentType.SalesInvoice ? "فاتورة" : "إشعار دائن",
            d.InvoiceUuid.ToString()[..8] + "…",
            d.Icv,
            d.TransmissionMode == ZatcaTransmissionMode.Clearance ? "اعتماد" : "إبلاغ",
            StatusLabel(d.ComplianceStatus),
            d.GeneratedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
            d.QrTlvBase64.Length > 24 ? d.QrTlvBase64[..24] + "…" : d.QrTlvBase64)).ToList();
        Grid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(Grid, rows.Count);

        var submitted = docs.Count(d =>
            d.ComplianceStatus is ZatcaComplianceStatus.Reported
                or ZatcaComplianceStatus.Cleared
                or ZatcaComplianceStatus.Accepted);
        var pending = docs.Count(d =>
            d.ComplianceStatus is ZatcaComplianceStatus.ReadyToReport
                or ZatcaComplianceStatus.ReadyToClear
                or ZatcaComplianceStatus.Generated);
        ZatcaSummaryText.Text =
            $"مستندات: {docs.Count} · مُرسل/معتمد: {submitted} · بانتظار الإرسال: {pending}";
    }

    private static string StatusLabel(ZatcaComplianceStatus s) => s switch
    {
        ZatcaComplianceStatus.ReadyToReport => "جاهز للإبلاغ",
        ZatcaComplianceStatus.ReadyToClear => "جاهز للاعتماد",
        ZatcaComplianceStatus.Reported => "مُبلَّغ",
        ZatcaComplianceStatus.Cleared => "معتمد",
        ZatcaComplianceStatus.Accepted => "مقبول",
        ZatcaComplianceStatus.Rejected => "مرفوض",
        _ => "مُولَّد",
    };

    private async void OnSubmit(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row)
        {
            UiDialog.Show("اختر مستنداً من الجدول.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            using var facade = new DesktopAccountingFacade();
            await facade.GetService<ZatcaSubmissionService>().SubmitAsync(row.Id);
            await ReloadAsync();
            UiDialog.Show("تم الإرسال بنجاح.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            UiDialog.Show(ex.Message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnPrintPdf(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row)
        {
            UiDialog.Show("اختر مستنداً من الجدول.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var username = SecurityContext.ResolveUsername();
        await DocumentPrintHelper.ExportAndDeliverAsync(
            s => s.ExportZatcaDocumentAsync(AppServices.CompanyId, row.Id, username),
            Window.GetWindow(this),
            new DocumentDeliverOptions(Preview: true, OfferSaveAs: true));
    }

    private async void OnViewXml(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row)
        {
            UiDialog.Show("اختر مستنداً من الجدول.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await using var db = AppServices.CreateDb();
        var doc = await db.ZatcaEInvoiceDocuments.SingleAsync(d => d.Id == row.Id);
        var path = !string.IsNullOrEmpty(doc.SignedXmlFilePath) && File.Exists(doc.SignedXmlFilePath)
            ? doc.SignedXmlFilePath
            : doc.XmlFilePath;

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            UiDialog.Show("ملف XML غير موجود.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        new XmlViewerDialog($"فاتورة ZATCA — {row.InvoiceNumber}", path) { Owner = Window.GetWindow(this) }.ShowDialog();
    }

    private async void OnOpenSource(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row)
        {
            UiDialog.Show("اختر مستنداً من الجدول.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await using var db = AppServices.CreateDb();
        var owner = Window.GetWindow(this);

        if (row.DocumentType == ZatcaDocumentType.SalesInvoice)
        {
            var invoice = await db.SalesInvoices.SingleOrDefaultAsync(i => i.Id == row.SourceDocumentId);
            if (invoice is null)
            {
                UiDialog.Show("الفاتورة المصدر غير موجودة.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var header = $"رقم: {invoice.InvoiceNumber ?? "—"}  |  التاريخ: {invoice.InvoiceDate:yyyy-MM-dd}";
            new DocumentLinesViewDialog("فاتورة مبيعات", header, invoice.Lines.ToList(),
                invoice.Subtotal, invoice.VatAmount, invoice.GrandTotal()) { Owner = owner }.ShowDialog();
            return;
        }

        var note = await db.CreditNotes.SingleOrDefaultAsync(n => n.Id == row.SourceDocumentId);
        if (note is null)
        {
            UiDialog.Show("إشعار الدائن المصدر غير موجود.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var noteHeader = $"رقم: {note.NoteNumber ?? "—"}  |  التاريخ: {note.NoteDate:yyyy-MM-dd}  |  السبب: {note.Reason}";
        new DocumentLinesViewDialog("إشعار دائن", noteHeader, note.Lines.ToList(),
            note.Subtotal, note.VatAmount, note.GrandTotal()) { Owner = owner }.ShowDialog();
    }
}
