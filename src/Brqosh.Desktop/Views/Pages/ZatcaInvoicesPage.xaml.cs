using System.IO;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Tax;
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
            // (جاهزيّةُ الإطلاق · ح-١٥) ‏**والاسمُ يُسمّى، ولا يُنفى عنه «فاتورة».**
            // ‏أربعةٌ من الستّة كانت تُعرض «إشعار دائن» — ومنها **مستخلصُ الأعمال**.
            ZatcaDocumentPresentation.TypeLabel(d.DocumentType),
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

        // (جاهزيّةُ الإطلاق · ح-١٥) ‏**المصدرُ يُسأل عنه، ولا يُستنتج بنفيِ «فاتورة».**
        //
        // ‏كان السؤالُ ثنائيّاً — أهي فاتورةُ مبيعات؟ **وإلّا فإشعارٌ دائن** — والأنواعُ ستّة
        // ‏ومصادرُها ثلاثة. فمستخلصٌ ومُلغيان **تُقرأ من `credit_notes` بمعرّفٍ ليس لها**،
        // ‏وفضاءاتُ المعرّفات مستقلّةٌ تبدأ كلُّها من واحد: **فتُعرض سطورُ مستندٍ آخرَ ومبالغُه
        // ‏وضريبتُه** تحت عنوان «إشعار دائن»، ولا شيءَ يقول إنّه غيرُه. انظر
        // ‏<see cref="ZatcaDocumentPresentation"/>.
        var sourceKind = ZatcaDocumentPresentation.SourceOf(row.DocumentType);
        var title = ZatcaDocumentPresentation.SourceTitle(row.DocumentType);

        // ‏**وترويسةُ النافذة تفصل المستندَ عن مصدره** — فهما رقمان مختلفان، وخلطُهما
        // ‏هو أصلُ العطل.
        string Header(string? sourceNumber, DateOnly date, string? extra = null) =>
            $"مستند ZATCA: {row.InvoiceNumber} ({row.TypeAr})  |  "
            + $"المصدر: {sourceNumber ?? "—"}  |  التاريخ: {date:yyyy-MM-dd}"
            + (extra is null ? "" : $"  |  {extra}");

        void Missing() => UiDialog.Show(
            ZatcaDocumentPresentation.SourceMissingMessage(row.DocumentType),
            "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);

        switch (sourceKind)
        {
            case ZatcaDocumentPresentation.SourceKind.SalesInvoice:
            {
                var invoice = await db.SalesInvoices.SingleOrDefaultAsync(i => i.Id == row.SourceDocumentId);
                if (invoice is null) { Missing(); return; }

                new DocumentLinesViewDialog(title,
                    Header(invoice.InvoiceNumber, invoice.InvoiceDate), invoice.Lines.ToList(),
                    invoice.Subtotal, invoice.VatAmount, invoice.GrandTotal()) { Owner = owner }.ShowDialog();
                return;
            }

            case ZatcaDocumentPresentation.SourceKind.CreditNote:
            {
                var note = await db.CreditNotes.SingleOrDefaultAsync(n => n.Id == row.SourceDocumentId);
                if (note is null) { Missing(); return; }

                new DocumentLinesViewDialog(title,
                    Header(note.NoteNumber, note.NoteDate, $"السبب: {note.Reason}"), note.Lines.ToList(),
                    note.Subtotal, note.VatAmount, note.GrandTotal()) { Owner = owner }.ShowDialog();
                return;
            }

            case ZatcaDocumentPresentation.SourceKind.ProgressBilling:
            {
                var billing = await db.ProgressBillings.SingleOrDefaultAsync(b => b.Id == row.SourceDocumentId);
                if (billing is null) { Missing(); return; }

                // ‏**والسطرُ هو نفسُه الذي أرسله المولّد** إلى الهيئة — وصفُ الفترة بكمّيةٍ
                // ‏واحدةٍ بسعر المبلغ الخاضع — فما على الشاشة هو ما في المستند لا تقريبٌ له.
                var lines = new List<SalesInvoiceLine>
                {
                    new(billing.PeriodDescription, 1m, billing.TaxableAmount, null),
                };
                new DocumentLinesViewDialog(title,
                    Header(billing.BillingNumber, billing.BillingDate), lines,
                    billing.TaxableAmount, billing.VatAmount, billing.TotalAmount) { Owner = owner }.ShowDialog();
                return;
            }

            case ZatcaDocumentPresentation.SourceKind.FixedAssetDisposal:
            {
                var asset = await db.FixedAssets.IgnoreQueryFilters()
                    .SingleOrDefaultAsync(a => a.Id == row.SourceDocumentId);
                if (asset?.DisposalProceeds is not decimal proceeds) { Missing(); return; }

                // ‏**والسطرُ هو نفسُه الذي أرسله المولّد** — انظر `TryGenerateForAssetDisposalAsync`.
                var category = asset.DisposalVatCategory ?? VatCategory.Standard;
                var assetLines = new List<SalesInvoiceLine>
                {
                    new($"بيع أصل ثابت {asset.Code} — {asset.NameAr}", 1m, proceeds, null, category),
                };
                var assetVat = Math.Round(
                    proceeds * category.RateFor(VatCalculator.SaudiVatRate), 2, MidpointRounding.AwayFromZero);

                // ‏**وتاريخُ الفاتورة تاريخُ قيد الاستبعاد** لا تاريخُ توليد المستند — وهو
                // ‏ما كتبه المولّدُ في `cbc:IssueDate`، فيُقرأ من حيث كُتب.
                var disposalDate = asset.DisposalJournalEntryId is long jid
                    ? await db.JournalEntries.Where(e => e.Id == jid).Select(e => e.EntryDate).SingleAsync()
                    : default;

                new DocumentLinesViewDialog(title,
                    Header(asset.DisposalInvoiceNumber, disposalDate, $"الأصل: {asset.Code}"),
                    assetLines, proceeds, assetVat, proceeds + assetVat) { Owner = owner }.ShowDialog();
                return;
            }

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(sourceKind), sourceKind, "مصدرُ مستندٍ إلكترونيٍّ غيرُ معروف.");
        }
    }
}
