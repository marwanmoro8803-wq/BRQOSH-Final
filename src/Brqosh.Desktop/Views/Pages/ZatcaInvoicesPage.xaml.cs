using System.IO;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Tax;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Zatca;
using Brqosh.Infrastructure.Sales;
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
            d.TransmissionMode == ZatcaTransmissionMode.Clearance
                ? LocaleManager.T("اعتماد", "Clearance")
                : LocaleManager.T("إبلاغ", "Reporting"),
            StatusLabel(d.ComplianceStatus),
            d.GeneratedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
            d.QrTlvBase64.Length > 24 ? d.QrTlvBase64[..24] + "…" : d.QrTlvBase64)).ToList();
        Grid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(Grid, rows.Count);

        var (submitted, pending, simulated) = SummaryCounts([.. docs.Select(d => d.ComplianceStatus)]);
        ZatcaSummaryText.Text =
            $"مستندات: {docs.Count} · مُرسل/معتمد: {submitted} · بانتظار الإرسال: {pending} · محاكاة لم تُرسل: {simulated}";
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-١٨) ‏<b>وخلايا الجدول تُترجَم هنا لا في السجلّ.</b>
    ///
    /// <para>‏<c>XamlUiLocalizer</c> يترجم <b>عناوينَ الأعمدة</b> (<c>col.Header</c>)
    /// ولا يبلغ <b>قيمَ الخلايا</b> — فهي تأتي من الكائن المربوط لا من خاصّيّةٍ على عنصر.
    /// <b>فإدخالُ هذه النصوص في <c>ui-overrides.json</c> يُخضِّر البوّابةَ والنصُّ عربيٌّ
    /// كما هو</b> — أي يجعل الحارسَ يكذب، وهو ما يحذّر منه <c>I18nCoverageTests</c> نصّاً
    /// في شأن <c>MessageBox</c>.</para>
    /// </summary>
    private static string StatusLabel(ZatcaComplianceStatus s)
    {
        var (arabic, english) = StatusNames(s);
        return LocaleManager.T(arabic, english);
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٥٧ · ui-ux-i18n-printing#4) ‏<b>العربيُّ من <c>NameAr()</c> — والشاشةُ لا تشتقّه.</b>
    ///
    /// <para>‏<b>وكانت تشتقّه</b>: سبعةُ فروعٍ وفرعٌ افتراضيٌّ «مُولَّد» <b>يبتلع <c>SimulatedLocally</c></b> —
    /// فالمحاكاةُ التي لم تصل الهيئةَ تُعرض «مُولَّداً» والمطبوعُ نفسُه يقول «محاكاة محلية — لم يُرسل للهيئة».
    /// <b>والإنجليزيُّ هنا</b> لأنّ الكيانَ لا يملكه؛ ولكلّ حالةٍ اسمُها — لا فرعَ يجمع اثنتين.</para>
    /// </summary>
    internal static (string Arabic, string English) StatusNames(ZatcaComplianceStatus s) =>
        (s.NameAr(), s switch
        {
            ZatcaComplianceStatus.Generated => "Generated",
            ZatcaComplianceStatus.ReadyToReport => "Ready to Report",
            ZatcaComplianceStatus.ReadyToClear => "Ready to Clear",
            ZatcaComplianceStatus.Reported => "Reported",
            ZatcaComplianceStatus.Cleared => "Cleared",
            ZatcaComplianceStatus.Accepted => "Accepted",
            ZatcaComplianceStatus.Rejected => "Rejected",
            ZatcaComplianceStatus.SimulatedLocally => "Simulated locally — not sent to ZATCA",
            _ => s.ToString(),
        });

    /// <summary>
    /// (ح-٥٧) ‏<b>الملخّصُ يعدّ بما تملكه الشجرة</b> — «مُرسل» هو <c>IsAcknowledgedByAuthority()</c>،
    /// <b>والمحاكاةُ تُعدّ وحدَها</b>: كانت لا مُرسلةً ولا منتظِرةً فتختفي من العدّ كلِّه.
    /// </summary>
    internal static (int Submitted, int Pending, int Simulated) SummaryCounts(
        IReadOnlyCollection<ZatcaComplianceStatus> statuses) =>
        (statuses.Count(s => s.IsAcknowledgedByAuthority()),
         statuses.Count(s => s is ZatcaComplianceStatus.ReadyToReport
             or ZatcaComplianceStatus.ReadyToClear
             or ZatcaComplianceStatus.Generated),
         statuses.Count(s => s is ZatcaComplianceStatus.SimulatedLocally));

    /// <summary>(ح-٥٧) ‏ما وقع بعد الإرسال — <b>من الحالة التي عاد بها المستند</b>، لا من أنّ الاستدعاءَ لم يرمِ.</summary>
    internal enum SubmitOutcome { Acknowledged, Simulated, Rejected, NotYetReceived }

    /// <summary>
    /// (ح-٥٧) ‏<b>وكان كلُّ إرسالٍ لم يرمِ «تم الإرسال بنجاح»</b> — والمحاكاةُ لا ترمي، فتنتهي
    /// <c>SimulatedLocally</c> ويُقال للمستعمل إنّها أُرسلت. <b>والمقبولُ عند الهيئة وحدَه نجاح.</b>
    /// </summary>
    internal static SubmitOutcome OutcomeOf(ZatcaComplianceStatus status) =>
        status.IsAcknowledgedByAuthority()
            ? SubmitOutcome.Acknowledged
            : status switch
            {
                ZatcaComplianceStatus.SimulatedLocally => SubmitOutcome.Simulated,
                ZatcaComplianceStatus.Rejected => SubmitOutcome.Rejected,
                _ => SubmitOutcome.NotYetReceived,
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
            var doc = await facade.GetService<ZatcaSubmissionService>().SubmitAsync(row.Id);
            await ReloadAsync();
            // ‏(ح-٥٧) **الرسالةُ من الحالة التي عاد بها المستند** — وكانت «تم الإرسال بنجاح» لكلّ ما لم يرمِ.
            switch (OutcomeOf(doc.ComplianceStatus))
            {
                case SubmitOutcome.Acknowledged:
                    UiDialog.Show("تم الإرسال — استلمت الهيئة المستند.", "BRQOSH",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                case SubmitOutcome.Simulated:
                    UiDialog.Show("محاكاة محلية — لم يُرسل المستند للهيئة، فالتكامل مضبوط على بيئة المحاكاة.", "BRQOSH",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    break;
                case SubmitOutcome.Rejected:
                    UiDialog.Show("رفضت الهيئة المستند — ورد الهيئة محفوظ مع المستند.", "BRQOSH",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    break;
                default:
                    UiDialog.Show("لم تستلم الهيئة المستند بعد.", "BRQOSH",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    break;
            }
        }
        catch (Exception ex)
        {
            Warn(ex);
        }
    }

    /// <summary>
    /// ‏(جاهزيّةُ الإطلاق · ح-١٢١ · ‏`tax-zatca#8`) <b>إعادةُ إرسال كلِّ معلَّقٍ دفعةً واحدة</b> — بترتيب السلسلة، وتقف عند أوّل فشل.
    /// ‏والرسائلُ حرفيّةٌ تُترجَم من السجلّ؛ وسببُ كلِّ فشلٍ مسجَّلٌ على مستنده في الجدول.
    /// </summary>
    private async void OnResubmitPending(object sender, RoutedEventArgs e) =>
        await UiFeedback.RunSafeAsync(ResubmitPendingAsync);

    private async Task ResubmitPendingAsync()
    {
        using var facade = new DesktopAccountingFacade();
        var result = await facade.GetService<ZatcaSubmissionService>().ResubmitPendingAsync(AppServices.CompanyId);
        await ReloadAsync();

        if (result.Submitted == 0 && result.StillPending == 0)
            UiDialog.Show("لا مستندات معلَّقة.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
        else if (result.StillPending == 0)
            UiDialog.Show("أُرسلت المستندات المعلَّقة كلُّها.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
        else
            UiDialog.Show("بقيت مستندات معلَّقة — وسببُ التوقّف مسجَّلٌ على المستند في الجدول. أعد المحاولة لاحقاً.", "BRQOSH",
                MessageBoxButton.OK, MessageBoxImage.Warning);
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

    /// <summary>
    /// (جاهزيّةُ الإطلاق · tax-zatca#8) <b>فاتورةٌ رفضتها الهيئة: تُلغى وتُفتح فاتورةٌ جديدةٌ بسطورها</b> — والرفضُ يُتحقَّق منه قبل الإلغاء
    /// في الخدمة نفسِها، والقياسيّةُ المرفوضةُ تُلغى بلا إشعارٍ دائنٍ إلكترونيّ لأنّها لم تصدر نظاماً.
    /// </summary>
    private async void OnReissueRejected(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row || row.DocumentType != ZatcaDocumentType.SalesInvoice)
        {
            UiDialog.Show("اختر فاتورة مبيعات رفضتها الهيئة.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (UiDialog.Show("ستُلغى الفاتورة المرفوضة بعكس قيدها، ثم تُفتح فاتورة جديدة بسطورها لتصحّحها وترحّلها. متابعة؟", "BRQOSH",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            CorrectedInvoiceSeed seed;
            using (var facade = new DesktopAccountingFacade())
                seed = await facade.GetService<RejectedInvoiceReissueService>().CancelRejectedAsync(
                    AppServices.CompanyId, row.SourceDocumentId, DateOnly.FromDateTime(DateTime.Today), SecurityContext.ResolveUsername());

            new SalesInvoiceDialog(seed) { Owner = Window.GetWindow(this) }.ShowDialog();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            Warn(ex);
        }
    }

    /// <summary>رسالةُ الخطأ كما رفعها النطاق — والغلافُ يترجمها في الوضع الإنجليزيّ (<c>UiMessages.Translate</c>).</summary>
    private static void Warn(Exception ex) =>
        UiDialog.Show(ex.Message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);

    private async void OnViewXml(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row)
        {
            UiDialog.Show("اختر مستنداً من الجدول.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await using var db = AppServices.CreateDb();
        var doc = await db.ZatcaEInvoiceDocuments.SingleAsync(d => d.Id == row.Id);
        // ‏(جاهزيّةُ الإطلاق · ح-٥٣) **ما يُعرض ويُنسخ للمشتري هو المعتمدُ متى اعتُمد** — وكان
        // ‏نسختَنا الموقَّعةَ أبداً. والمعتمدُ إن ضاع لا تحلّ نسختُنا محلَّه صامتةً.
        var path = ZatcaHandOverCopy.XmlPathFor(doc);

        if (path is null)
        {
            if (doc.ComplianceStatus == ZatcaComplianceStatus.Cleared)
                UiDialog.Show("النسخة المعتمدة من الهيئة غير موجودة على القرص — استعِدها قبل تسليمها للمشتري.",
                    "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
            else
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
