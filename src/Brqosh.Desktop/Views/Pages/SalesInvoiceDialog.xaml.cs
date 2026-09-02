using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class SalesInvoiceDialog : Window
{
    public record CustomerOption(long Id, string Display);

    /// <summary>
    /// (CEEF-229 · م0-١٧) صفٌّ مُشعِر — يُستعمل في فواتير البيع والشراء وإشعارَي الدائن والمدين.
    /// بدون الإشعار كان عمود «الإجمالي» يبقى 0.00 بينما ملخّص الفاتورة صحيح، فيظنّ المستخدم
    /// أن إدخاله لم يُحفَظ.
    /// </summary>
    public class LineRow : Brqosh.Desktop.Ui.NotifyingRow
    {
        private decimal _quantity;
        private decimal _unitPrice;
        private decimal _discountPercent;

        public long? ProductId { get; set; }
        public string? Description { get; set; }

        public decimal Quantity
        {
            get => _quantity;
            set => Set(ref _quantity, value, [nameof(LineTotal)]);
        }

        public decimal UnitPrice
        {
            get => _unitPrice;
            set => Set(ref _unitPrice, value, [nameof(LineTotal)]);
        }

        /// <summary>
        /// (CEEF-231 · ط1-٢ب) نسبةُ خصم السطر — <b>والمحرّكُ مبنيٌّ منذ <c>ط1-٢</c> ولا
        /// حقلَ في شاشةٍ يصل إليه</b>.
        /// </summary>
        /// <remarks>
        /// <b>وإدخالُ السعر مخصوماً بيدٍ ليس بديلاً</b>: تختفي النسبةُ من المستند
        /// الإلكترونيّ (<c>AllowanceCharge</c>) فلا تعرفها الهيئة، <b>وتختفي من كلّ تقريرِ
        /// ربحيّة</b> — فيبدو الصنفُ مبيعاً بسعرٍ أقلَّ لا مخصوماً.
        /// </remarks>
        public decimal DiscountPercent
        {
            get => _discountPercent;
            set => Set(ref _discountPercent, value, [nameof(LineTotal)]);
        }

        /// <summary>الفئة الضريبية للسطر — الافتراضي أساسية 15% (CEEF-208).</summary>
        public Brqosh.Domain.Tax.VatCategory VatCategory { get; set; }
            = Brqosh.Domain.Tax.VatCategory.Standard;
        /// <summary>
        /// (CEEF-229 · م1-١ب/الشقّ الثاني) رمزُ سبب الإعفاء ونصُّه — BR-KSA-CL-06.
        ///
        /// <para>فارغٌ يعني «لم يُختَر»، والنطاق يرفض فئةً غير أساسية بلا سبب. والنصُّ
        /// لفئة «خارج النطاق» وحدها: المعيار يقول إنّه حرٌّ يكتبه المكلَّف حالةً بحالة.</para>
        /// </summary>
        public string? TaxExemptionReasonCode { get; set; }
        public string? TaxExemptionReason { get; set; }

        /// <summary>صافي السطر بعد خصمه — <b>وهو الوعاءُ الذي تُحسب عليه الضريبة</b>.</summary>
        public decimal LineTotal
        {
            get
            {
                var gross = Math.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);
                var discount = Math.Round(gross * DiscountPercent / 100m, 2, MidpointRounding.AwayFromZero);
                return gross - discount;
            }
        }
    }

    private readonly ObservableCollection<LineRow> _rows = [];
    private List<ProductOption> _products = [];

    public record ProductOption(long Id, string Display, decimal SalePrice, string NameAr);

    public SalesInvoiceDialog()
    {
        InitializeComponent();
        DatePickerBox.SelectedDate = DateTime.Today;
        LinesGrid.ItemsSource = _rows;
        Loaded += async (_, _) =>
        {
            HeroSubtitleText.Text = LocaleManager.T(
                "مسار ذهبي: بيع → ZATCA → تحصيل — Financial Truth First.",
                "Golden path: Sell → ZATCA → Collect — Financial Truth First.");
            await LoadCustomersAsync();
            await LoadProductsAsync();
            await LoadZatcaPanelAsync();
        };
    }

    private async Task LoadZatcaPanelAsync()
    {
        await using var db = AppServices.CreateDb();
        var settings = await db.ZatcaSettings.FirstOrDefaultAsync(s => s.CompanyId == AppServices.CompanyId);
        if (settings is { IsEnabled: true })
        {
            ZatcaStatusText.Text = LocaleManager.T(
                $"مفعّل · البيئة: {settings.Environment}",
                $"Enabled · Environment: {settings.Environment}");
            ZatcaHintText.Text = LocaleManager.T(
                "UUID · QR · UBL XML — يُولَّد تلقائياً عند الترحيل.",
                "UUID · QR · UBL XML — generated automatically on post.");
        }
        else
        {
            ZatcaStatusText.Text = LocaleManager.T("ZATCA غير مفعّل", "ZATCA not enabled");
            ZatcaHintText.Text = LocaleManager.T(
                "فعّل من شاشة إعدادات ZATCA للفوترة الإلكترونية.",
                "Enable from ZATCA settings for e-invoicing.");
        }
    }

    private async Task LoadProductsAsync()
    {
        await using var db = AppServices.CreateDb();
        _products = await db.Products.Where(p => p.CompanyId == AppServices.CompanyId && p.IsActive)
            .OrderBy(p => p.Code)
            .Select(p => new ProductOption(p.Id, $"{p.Code} — {p.NameAr}", p.SalePrice, p.NameAr))
            .ToListAsync();
        ProductColumn.ItemsSource = _products;   // (م2-٢٠) بلا هذا يبقى العمود فارغاً
    }

    private async Task LoadCustomersAsync()
    {
        await using var db = AppServices.CreateDb();
        var customers = await db.Customers
            .Where(c => c.CompanyId == AppServices.CompanyId && c.IsActive)
            .OrderBy(c => c.NameAr)
            .Select(c => new CustomerOption(c.Id, c.NameAr))
            .ToListAsync();
        CustomerCombo.ItemsSource = customers;
        if (customers.Count > 0)
            CustomerCombo.SelectedIndex = 0;
    }

    private void OnCellEditEnding(object sender, DataGridCellEditEndingEventArgs e) =>
        Dispatcher.BeginInvoke(UpdateTotals);

    private void OnDocumentDiscountChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        UpdateTotals();
    }

    /// <summary>
    /// خصمُ المستند المُدخَل — <b>وما لا يُقرأ صفرٌ لا رفضٌ هنا</b>.
    /// </summary>
    /// <remarks>
    /// <b>والرفضُ عند الحفظ لا عند كلّ حرف</b>: الحقلُ يُقرأ مع كلّ ضغطة، ورفضُ «1.» أثناء
    /// الكتابة يمنع كتابةَ «1.5». <b>والنطاقُ يحرس عند الترحيل</b> — وهو الموضعُ الذي
    /// لا يُلتفّ عليه.
    /// </remarks>
    private decimal DocumentDiscount() =>
        decimal.TryParse(DocumentDiscountBox.Text, out var value) && value > 0m ? value : 0m;

    private void UpdateTotals()
    {
        var subtotal = _rows.Sum(r => r.LineTotal) - DocumentDiscount();
        if (subtotal < 0m) subtotal = 0m;

        var vat = Math.Round(subtotal * SalesInvoice.SaudiVatRate, 2, MidpointRounding.AwayFromZero);
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
                throw new AccountingException("تاريخ الفاتورة مطلوب.");
            if (CustomerCombo.SelectedValue is not long customerId)
                throw new AccountingException("اختر العميل.");

            var priceWarnings = _rows
                .Where(r => r.ProductId is long pid && _products.FirstOrDefault(p => p.Id == pid) is { SalePrice: > 0 } product
                    && r.UnitPrice != product.SalePrice
                    && Math.Abs(r.UnitPrice - product.SalePrice) / product.SalePrice > 0.01m)
                .Select(r =>
                {
                    var p = _products.First(x => x.Id == r.ProductId);
                    return $"{p.NameAr}: {r.UnitPrice:N2} (الافتراضي {p.SalePrice:N2})";
                })
                .ToList();

            if (priceWarnings.Count > 0 &&
                UiDialog.Show(
                    "أسعار تختلف عن سعر البيع الافتراضي:\n" + string.Join("\n", priceWarnings) + "\n\nمتابعة؟",
                    "BRQOSH", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            var invoice = BuildInvoiceFromRows(
                AppServices.CompanyId, customerId,
                DateOnly.FromDateTime(DatePickerBox.SelectedDate.Value));

            using var facade = new DesktopAccountingFacade();
            await facade.PostSalesInvoiceAsync(invoice);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }


    /// <summary>
    /// (CEEF-229 · م2-٢٥) <b>الوصلة بين الشبكة والنطاق — مُستخرَجةٌ كي تُقاس.</b>
    ///
    /// <para>كان بناءُ المستند مبثوثاً داخل <c>OnPost</c>، وهو <c>async void</c> يفتح صناديق
    /// رسائل ويُغلق النافذة — فلا يُستدعى من اختبار. ولذلك بقيت أخطرُ حلقةٍ في السلسلة بلا
    /// قياس: <b>هل ما تحمله صفوف الشبكة يصل إلى الكيان؟</b> وهي بالضبط الحلقة التي انكسرت
    /// في م2-٢٠ — <c>ProductId</c> يُقرأ هنا ولم يكن أيُّ عمودٍ يكتبه، فمرّ <c>null</c> دائماً
    /// ولم يلحظه أحد.</para>
    ///
    /// <para>الاستخراج لا يغيّر سلوكاً: المنطق نفسه حرفاً بحرف، لكنه صار قابلاً للاستدعاء
    /// من اختبارٍ يُنشئ الحوار ويملأ صفوفه ويقيس ما وصل.</para>
    /// </summary>
    internal SalesInvoice BuildInvoiceFromRows(long companyId, long customerId, DateOnly invoiceDate)
    {
        var invoice = SalesInvoice.CreateDraft(companyId, customerId, invoiceDate);

        // (CEEF-230 · ط4-٦) ‏**والعملةُ تُضبط قبل السطور**: `SetCurrency` يُعيد حسابَ
        // ‏المجاميع، والسطورُ تُحسب بعملة الفاتورة ثمّ تُضرب في السعر. **وضبطُها بعد
        // ‏الإضافة يترك الأساسَ الأجنبيَّ صفراً** — فيُصدَر مستندٌ إلكترونيٌّ بإجماليٍّ صفر.
        //
        // ‏وكانت هذه الميزةُ مبنيّةً في `ط1-٤` **ولا حقلَ في أيّ شاشةٍ يصل إليها**، فيُدخل
        // ‏المصدِّرُ فاتورتَه محوَّلةً بيده وتضيع القيمةُ الأصلية.
        ApplyCurrency(invoice);

        foreach (var row in _rows.Where(r =>
                     !string.IsNullOrWhiteSpace(r.Description) || r.Quantity != 0 || r.UnitPrice != 0))
        {
            invoice.AddLine(row.Description ?? "", row.Quantity, row.UnitPrice, row.ProductId,
                row.VatCategory, row.TaxExemptionReasonCode, row.TaxExemptionReason,
                discountPercent: row.DiscountPercent);
        }

        // (CEEF-231 · ط1-٢ب) ‏**وخصمُ المستند بعد السطور**: النطاقُ يرفض خصماً يتجاوز
        // ‏صافيَها، **وفحصُه قبل إضافتها يقيس صفراً فيرفض كلَّ خصم**.
        //
        // ‏والتوزيعُ على الأوعية بنسبتها يقع في النطاق — **وطرحُه من الإجمالي وحدَه
        // ‏يُصرَّح للهيئة بضريبةٍ على مبلغٍ لم يُدفع**.
        var documentDiscount = DocumentDiscount();
        if (documentDiscount > 0m)
            invoice.ApplyDocumentDiscount(documentDiscount);

        return invoice;
    }

    /// <summary>صفوفُ الشبكة — للاختبار وحده، كي يملأها كما يملؤها المستخدم.</summary>
    internal System.Collections.ObjectModel.ObservableCollection<LineRow> RowsForTests => _rows;

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
    /// <summary>
    /// يضبط عملةَ الفاتورة وسعرَها — <b>ويرفض ما لا يُقرأ بدل أن يبتلعه</b>.
    ///
    /// <para><b>وريالٌ بسعرِ واحدٍ لا يُستدعى له شيء</b>: فالمسارُ القديم يبقى حرفاً،
    /// ولا يتغيّر رقمٌ في منشأةٍ لا تتعامل بالعملات.</para>
    /// </summary>
    private void ApplyCurrency(Brqosh.Domain.Sales.SalesInvoice invoice)
    {
        var code = (CurrencyBox.Text ?? "SAR").Trim().ToUpperInvariant();
        if (code.Length == 0) code = "SAR";
        if (code == "SAR") return;

        if (!decimal.TryParse((RateBox.Text ?? "").Trim(),
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var rate))
            throw new Brqosh.Domain.Accounting.AccountingException(
                $"سعر الصرف غير صالح — ووردَ «{RateBox.Text}».");

        invoice.SetCurrency(code, rate);
    }
}
