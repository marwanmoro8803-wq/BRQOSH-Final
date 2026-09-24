using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class CreditNoteDialog : Window
{
    public record InvoiceOption(long Id, long CustomerId, decimal GrandTotal, string Display);

    private readonly ObservableCollection<SalesInvoiceDialog.LineRow> _rows = [];

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٦) الفاتورةُ الأصليّةُ محفوظةً — <b>ويلزم خصمُ مستندها عند
    /// الترحيل لا عند العرض وحدَه</b>.
    /// </summary>
    private SalesInvoice? _invoice;

    public CreditNoteDialog()
    {
        InitializeComponent();
        DatePickerBox.SelectedDate = DateTime.Today;
        LinesGrid.ItemsSource = _rows;
        Loaded += async (_, _) => await LoadInvoicesAsync();
    }

    private async Task LoadInvoicesAsync()
    {
        await using var db = AppServices.CreateDb();
        var invoices = await db.SalesInvoices
            .Where(i => i.CompanyId == AppServices.CompanyId && i.Status == SalesInvoiceStatus.Posted)
            .OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.Id)
            .Take(200)
            .ToListAsync();

        var customerIds = invoices.Select(i => i.CustomerId).Distinct().ToList();
        var customers = await db.Customers
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.NameAr);

        InvoiceCombo.ItemsSource = invoices.Select(i => new InvoiceOption(
            i.Id, i.CustomerId, i.GrandTotal(),
            $"{i.InvoiceNumber} — {customers.GetValueOrDefault(i.CustomerId, "؟")} — {i.GrandTotal():N2}")).ToList();
    }

    private async void OnInvoiceSelected(object sender, SelectionChangedEventArgs e)
    {
        if (InvoiceCombo.SelectedItem is not InvoiceOption option)
            return;

        await using var db = AppServices.CreateDb();
        var postedNotes = await db.CreditNotes
            .Where(n => n.CompanyId == AppServices.CompanyId
                        && n.OriginalInvoiceId == option.Id
                        && n.Status == CreditNoteStatus.Posted)
            .ToListAsync();

        RemainingText.Text = (option.GrandTotal - postedNotes.SumGrandTotal()).ToString("N2");

        // (CEEF-229 · م0-٦) السطور تُبنى من الفاتورة الأصلية لا يكتبها المستخدم نصّاً حرّاً.
        //
        // كان الحوار يترك المستخدم يكتب وصف الصنف بيده، فيخرج السطر بلا ProductId — و
        // CreditNotePostingService يشترطه لإرجاع البضاعة وعكس تكلفتها. النتيجة أن **كل مرتجع
        // مبيعات كان يُرجع صفر بضاعة ويعكس صفر تكلفة**: الإيراد والذمم ينقصان والبضاعة لا تعود،
        // فيفترق المخزون عن الأستاذ فرقاً دائماً لا يكشفه إلا جرد بعد شهور.
        //
        // والبناء من الفاتورة يُغلق العطل من جذره: لا سطر بلا صنف، ولا صنف ليس على الفاتورة،
        // والكمية الافتراضية هي المتاح للإرجاع فعلاً.
        var invoice = await db.SalesInvoices
            .FirstOrDefaultAsync(i => i.Id == option.Id && i.CompanyId == AppServices.CompanyId);
        if (invoice is null)
            return;

        _invoice = invoice;

        var alreadyReturned = postedNotes
            .SelectMany(n => n.Lines)
            .Where(l => l.ProductId is not null)
            .GroupBy(l => l.ProductId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

        _rows.Clear();
        foreach (var line in invoice.Lines)
        {
            var returnable = line.Quantity
                - (line.ProductId is long pid ? alreadyReturned.GetValueOrDefault(pid) : 0m);
            if (returnable <= 0m)
                continue;

            _rows.Add(new SalesInvoiceDialog.LineRow
            {
                ProductId = line.ProductId,
                Description = line.Description,
                Quantity = returnable,
                UnitPrice = line.UnitPrice,
                VatCategory = line.VatCategory,
                // ‏(جاهزيّةُ الإطلاق · ح-٦) ‏**وخصمُ السطر يُورَّث كما تُورَّث فئتُه.**
                // ‏وبدونه يُبنى المرتجعُ على السعر الخامّ — **فيُعاد للعميل أكثرُ ممّا دفع**
                // ‏وتُنقص ضريبةُ المخرجات أكثرَ ممّا استُحقّ.
                DiscountPercent = line.DiscountPercent,
                // ‏(م1-١ب/الشقّ الثاني) السببُ يُورَّث من الفاتورة الأصلية — فالمرتجعُ يحمل
                // ‏فئتَها، ولو تُرك فارغاً لَرفض النطاقُ إشعاراً دائناً لبيعةٍ صفريةٍ صحيحة.
                TaxExemptionReasonCode = line.TaxExemptionReasonCode,
                TaxExemptionReason = line.TaxExemptionReason,
            });
        }

        LinesGrid.Items.Refresh();
        UpdateTotals();
    }

    private void OnCellEditEnding(object sender, DataGridCellEditEndingEventArgs e) =>
        Dispatcher.BeginInvoke(UpdateTotals);

    /// <summary>حصّةُ المُرتجَع من خصم الفاتورة — والحسابُ في النطاق حيث يُقاس.</summary>
    private decimal DocumentDiscountShare() =>
        _invoice is null
            ? 0m
            : CreditNote.DocumentDiscountShareFor(_invoice, _rows.Sum(r => r.LineTotal));

    private void UpdateTotals()
    {
        // ‏(ح-٦) ‏**والمعروضُ هو المُرحَّل** — وكان يعرض حاصلاً خامّاً بلا خصمِ مستند،
        // ‏فيرى المستخدمُ رقماً ويُرحَّل غيرُه. والحسابُ من الموزِّع نفسِه لا بصيغةٍ ثانية.
        // ‏(ح-٦٠) ‏وصار البناءُ مشتركاً مع حوارَي البيع والشراء — كانا يضربان في النسبة.
        var (subtotal, vat) = SalesInvoiceDialog.TotalsOf(_rows, DocumentDiscountShare());

        SubtotalText.Text = subtotal.ToString("N2");
        VatText.Text = vat.ToString("N2");
        TotalText.Text = (subtotal + vat).ToString("N2");
    }

    private async void OnPost(object sender, RoutedEventArgs e)
    {
        // (CEEF-229 · م0-١٦) ضغطة مزدوجة كانت تُنشئ مستندين — الزرّ لا يُعطَّل أثناء الانتظار.
        using var busy = UiBusyGuard.Enter(sender);
        if (!busy.Acquired) return;

        try
        {
            LinesGrid.CommitEdit(DataGridEditingUnit.Row, true);

            if (DatePickerBox.SelectedDate is null)
                throw new AccountingException("تاريخ الإشعار مطلوب.");
            if (InvoiceCombo.SelectedItem is not InvoiceOption option)
                throw new AccountingException("اختر الفاتورة الأصلية.");

            var note = CreditNote.CreateDraft(
                AppServices.CompanyId, option.CustomerId, option.Id,
                DateOnly.FromDateTime(DatePickerBox.SelectedDate.Value),
                ReasonBox.Text);

            // (جاهزيّةُ الإطلاق · ح-٣٥) ‏**والعملةُ تُورَّث من الفاتورة لا تُختار.**
            //
            // ‏سطورُ الفاتورة الأجنبيّة **مُسعّرةٌ بعملتِها**، وتُنسخ أسفلَه
            // ‏كما هي. **فبلا وراثةِ العملة يصير الأجنبيُّ ريالاً**، ومرتجعٌ
            // ‏كاملٌ يردّ جزءاً ممّا قُبِض (قُيس: ١٬١٥٠ من ٤٬٣١٢٫٥٠).
            //
            // ‏**وبسعرِ الفاتورة لا بسعرِ اليوم**: المرتجعُ يردّ ما قُبِض،
            // ‏**وسعرٌ آخرُ يخلق فرقَ صرفٍ لم يقع**.
            if (_invoice is { IsForeignCurrency: true } src)
                note.SetCurrency(src.CurrencyCode, src.ExchangeRate);

            foreach (var row in _rows.Where(r => !string.IsNullOrWhiteSpace(r.Description) || r.Quantity != 0 || r.UnitPrice != 0))
                note.AddLine(row.Description ?? "", row.Quantity, row.UnitPrice,
                    row.ProductId, row.VatCategory, row.TaxExemptionReasonCode, row.TaxExemptionReason,
                    discountPercent: row.DiscountPercent);

            // ‏(جاهزيّةُ الإطلاق · ح-٦) ‏**وحصّةُ المرتجع من خصم المستند.**
            //
            // ‏خصمُ الفاتورة يخصّ المستندَ كلَّه، **والمرتجعُ قد يكون بعضَه** — فيُؤخذ بنسبة
            // ‏صافي المُرتجَع إلى صافي الفاتورة. ومرتجعٌ كاملٌ يأخذه كلَّه، **فيُغلق الإشعارُ
            // ‏الفاتورةَ تماماً بدل أن يتجاوز متبقّيها ويُرفض**.
            var share = DocumentDiscountShare();
            if (share > 0m)
                note.ApplyDocumentDiscount(share);

            using var facade = new DesktopAccountingFacade();
            await facade.GetService<CreditNotePostingService>().PostAsync(note);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
