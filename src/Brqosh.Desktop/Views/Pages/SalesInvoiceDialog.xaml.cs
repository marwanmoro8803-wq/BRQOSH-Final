using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Ui;
using Brqosh.Desktop.ViewModels;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class SalesInvoiceDialog : Window
{
    public record CustomerOption(long Id, string Display);

    /// <summary>‏(ح-١١٠) ‏خيارُ مستودع — <c>Id</c> الفارغُ هو الافتراضيّ.</summary>
    public record WarehouseOption(long? Id, string Display);

    private const string DefaultWarehouseChoice = "(المستودع الافتراضي)";

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

        /// <summary>‏(جاهزيّةُ الإطلاق · ح-١٠٢) ‏دفعةُ السطر — يملؤها المرتجعُ من سطر الفاتورة، فيعود إليها بعينها.</summary>
        public string? BatchNumber { get; set; }

        /// <summary>‏(جاهزيّةُ الإطلاق · ح-١٠٨) ‏انتهاءُ الدفعة المشتراة — لشاشة الشراء المباشر وحدَها.</summary>
        public DateTime? ExpiryDate { get; set; }

        /// <summary>
        /// ‏(جاهزيّةُ الإطلاق · <c>tax-zatca#8</c>) ‏وحدةُ السطر — يملؤها نسخُ الفاتورة المرفوضة وحدَه؛ <b>وبلاها تصير «٢ كرتون»
        /// ‏«٢ حبّة»</b>. وفارغةً هي وحدةُ الصنف كما كانت كلُّ فاتورةٍ تُدخَل هنا.
        /// </summary>
        public string? UnitCode { get; set; }

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
    private readonly SalesInvoiceDialogViewModel _vm = new();

    public record ProductOption(long Id, string Display, decimal SalePrice, string NameAr);

    public SalesInvoiceDialog()
    {
        InitializeComponent();
        Brqosh.Desktop.Ui.CurrencyChoices.Fill(CurrencyBox, Brqosh.Desktop.Ui.CurrencyLabel.Code);
        Loaded += async (_, _) => Brqosh.Desktop.Ui.CurrencyChoices.Fill(
            CurrencyBox, Brqosh.Desktop.Ui.CurrencyLabel.Code, await _vm.LoadCurrenciesAsync());
        DatePickerBox.SelectedDate = DateTime.Today;
        LinesGrid.ItemsSource = _rows;
        Loaded += async (_, _) =>
        {
            HeroSubtitleText.Text = LocaleManager.T(
                "مسار ذهبي: بيع → ZATCA → تحصيل — Financial Truth First.",
                "Golden path: Sell → ZATCA → Collect — Financial Truth First.");
            await LoadCustomersAsync();
            await LoadWarehousesAsync();
            await LoadProductsAsync();
            await LoadZatcaPanelAsync();
            SelectSeededParties();
        };
    }

    private CorrectedInvoiceSeed? _seed;

    /// <summary>
    /// ‏(جاهزيّةُ الإطلاق · <c>tax-zatca#8</c>) <b>فاتورةٌ جديدةٌ مملوءةٌ ببيانات فاتورةٍ رفضتها الهيئةُ وأُلغيت</b> — يُعدّلها المستخدمُ
    /// ‏ثمّ يُرحّلها بزرّ الترحيل نفسِه، فتأخذ رقماً جديداً ومستنداً إلكترونيّاً جديداً.
    /// </summary>
    public SalesInvoiceDialog(CorrectedInvoiceSeed seed) : this()
    {
        _seed = seed;
        Brqosh.Desktop.Ui.CurrencyChoices.Select(CurrencyBox, seed.CurrencyCode);
        RateBox.Text = seed.ExchangeRate.ToString(System.Globalization.CultureInfo.InvariantCulture);
        DocumentDiscountBox.Text = seed.DocumentDiscountAmount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        foreach (var line in seed.Lines)
            _rows.Add(new LineRow
            {
                ProductId = line.ProductId, Description = line.Description, Quantity = line.Quantity,
                UnitPrice = line.UnitPrice, DiscountPercent = line.DiscountPercent, VatCategory = line.VatCategory,
                TaxExemptionReasonCode = line.TaxExemptionReasonCode, TaxExemptionReason = line.TaxExemptionReason,
                BatchNumber = line.BatchNumber, UnitCode = line.UnitCode,
            });
    }

    /// <summary>العميلُ والمستودعُ يُختاران بعد تحميل قائمتيهما — <b>وعميلٌ أُوقف بعدها لا يُختار</b>، فيختار المستخدمُ غيرَه.</summary>
    private void SelectSeededParties()
    {
        if (_seed is null) return;
        CustomerCombo.SelectedValue = _seed.CustomerId;
        WarehouseCombo.SelectedValue = _seed.WarehouseId;
        UpdateTotals();
    }

    private async Task LoadZatcaPanelAsync()
    {
        var environment = await _vm.LoadZatcaEnvironmentAsync();
        if (environment is not null)
        {
            ZatcaStatusText.Text = LocaleManager.T(
                $"مفعّل · البيئة: {environment}",
                $"Enabled · Environment: {environment}");
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
        _products = await _vm.LoadProductsAsync();
        ProductColumn.ItemsSource = _products;   // (م2-٢٠) بلا هذا يبقى العمود فارغاً
    }

    private async Task LoadWarehousesAsync()
    {
        // ‏(جاهزيّةُ الإطلاق · ح-١١٠ · ‏`order-to-cash-procure-to-pay-inventory#12`) ‏المستودعاتُ القائمة — والأوّلُ «الافتراضيّ».
        await using var db = AppServices.CreateDb();
        var choices = new List<WarehouseOption> { new(null, DefaultWarehouseChoice) };
        choices.AddRange(await db.Warehouses
            .Where(w => w.CompanyId == AppServices.CompanyId && w.IsActive)
            .OrderBy(w => w.Code)
            .Select(w => new WarehouseOption(w.Id, w.Code + " — " + w.NameAr))
            .ToListAsync());
        WarehouseCombo.ItemsSource = choices;
        WarehouseCombo.SelectedIndex = 0;
    }

    private async Task LoadCustomersAsync()
    {
        var customers = await _vm.LoadCustomersAsync();
        CustomerCombo.ItemsSource = customers;
        // (ح-٦١) لا طرفَ يُختار سلفاً — «Enter» وأوّلُ اسمٍ أبجديّاً كانا يُرحّلان لمن لم يُقصد.
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
        NumericInput.TryParsePositive(DocumentDiscountBox.Text, out var value) ? value : 0m;

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٦٠) <b>مجاميعُ الشاشة من الموزِّع الذي يُرحِّل — لا بصيغةٍ ثانية.</b>
    ///
    /// <para><b>ما كان:</b> حوارا البيع والشراء يضربان الوعاءَ كلَّه في النسبة الأساسية
    /// ويتجاهلان فئةَ السطر — <b>فسطرُ تصديرٍ صفريٌّ يُعرض عليه خمسةَ عشرَ بالمئة</b> والمُرحَّلُ
    /// صفر. وخصمُ المستند كان يُطرح من الإجمالي، <b>والنطاقُ يوزّعه على الأوعية</b>. والحوارات
    /// الثلاثة تشترك في <see cref="LineRow"/>، فتشترك هنا في حسابه.</para>
    ///
    /// <para><b>والخصمُ الزائد عن الصافي يُقصّ عند العرض</b>: النطاقُ يرفضه عند الحفظ، وبلا
    /// قصٍّ يأخذ الوعاءُ الأخيرُ باقياً سالباً فتُعرض ضريبةٌ سالبة.</para>
    /// </summary>
    internal static (decimal Subtotal, decimal Vat) TotalsOf(IEnumerable<LineRow> rows, decimal documentDiscount)
    {
        var lines = rows
            .Where(r => r.Quantity != 0m || r.UnitPrice != 0m)
            .Select(r => new SalesInvoiceLine(
                r.Description ?? "", r.Quantity, r.UnitPrice, r.ProductId, r.VatCategory,
                r.TaxExemptionReasonCode, r.TaxExemptionReason, DiscountPercent: r.DiscountPercent))
            .ToList();

        var net = Math.Max(lines.Sum(l => l.LineTotal), 0m);
        return LineDocumentTotalsCalculator.Compute(
            lines, documentDiscount: Math.Clamp(documentDiscount, 0m, net));
    }

    private void UpdateTotals()
    {
        var (subtotal, vat) = TotalsOf(_rows, DocumentDiscount());
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

            await _vm.PostAsync(invoice);

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

        // ‏(ح-١١٠) ‏مستودعُ الصرف — الفارغُ هو الافتراضيّ؛ والمستودعُ يُسأل عند الترحيل.
        invoice.SetWarehouse(WarehouseCombo.SelectedValue as long?);

        foreach (var row in _rows.Where(r =>
                     !string.IsNullOrWhiteSpace(r.Description) || r.Quantity != 0 || r.UnitPrice != 0))
        {
            invoice.AddLine(row.Description ?? "", row.Quantity, row.UnitPrice, row.ProductId,
                row.VatCategory, row.TaxExemptionReasonCode, row.TaxExemptionReason,
                batchNumber: row.BatchNumber, discountPercent: row.DiscountPercent, unitCode: row.UnitCode);
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
        var code = (CurrencyBox.SelectedItem as string ?? Brqosh.Desktop.Ui.CurrencyLabel.Code).Trim().ToUpperInvariant();
        if (code.Length == 0) code = Brqosh.Desktop.Ui.CurrencyLabel.Code;
        if (code == Brqosh.Desktop.Ui.CurrencyLabel.Code) return;

        if (!NumericInput.TryParse(RateBox.Text, out var rate))
            throw new Brqosh.Domain.Accounting.AccountingException(
                $"سعر الصرف غير صالح — ووردَ «{RateBox.Text}».");

        invoice.SetCurrency(code, rate, Brqosh.Desktop.Ui.CurrencyLabel.Code);
    }
}
