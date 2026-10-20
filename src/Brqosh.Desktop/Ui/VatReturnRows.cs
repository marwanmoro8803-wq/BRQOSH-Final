using Brqosh.Application.Reporting;

namespace Brqosh.Desktop.Ui;

/// <summary>
/// (CEEF-229 · م1-١٠ / Z-12) <b>صفوفُ الإقرار الضريبيّ — مبنيّةٌ في موضعٍ يُقاس.</b>
///
/// <para><b>ولماذا أُخرجت من الشاشة:</b> الشاشةُ كانت تبني صفوفَها داخل معالجِ ضغطٍ يحتاج
/// قاعدةَ بيانات وخيطَ واجهة — <b>فما تعرضه لا يُقاس إلا بتشغيل التطبيق</b>. وهذا هو
/// السببُ البنيويّ لبقاء «الوصلة» مقطوعةً: التفكيكُ محسوبٌ منذ A-15 ولم يره أحد.</para>
///
/// <para>فصار بناءُ الصفوف دالّةً صافية من <see cref="VatReturnDto"/> إلى قائمة سطور —
/// تُستدعى من الشاشة وتُقاس في اختبار.</para>
/// </summary>
public static class VatReturnRows
{
    public sealed record Row(string Label, decimal Value, bool Bold = false, bool Indented = false);

    public static IReadOnlyList<Row> Build(VatReturnDto r)
    {
        var rows = new List<Row>
        {
            new(LocaleManager.T("مبيعات خاضعة (وعاء)", "Taxable sales (base)"), r.SalesSubtotal),
            new(LocaleManager.T("ضريبة المخرجات — مبيعات", "Output VAT — sales"), r.OutputVat),
        };

        rows.Add(new Row(LocaleManager.T("مستخلصات أعمال (وعاء)", "Progress billings (base)"), r.ProgressBillingsSubtotal));
        rows.Add(new Row(LocaleManager.T("ضريبة المخرجات — مستخلصات", "Output VAT — progress billings"), r.ProgressBillingsVat));
        rows.Add(new Row(LocaleManager.T("إشعارات دائن (وعاء)", "Credit notes (base)"), r.CreditNotesSubtotal));
        rows.Add(new Row(LocaleManager.T("ضريبة مرتجعة (إشعارات دائن)", "VAT returned (credit notes)"), r.CreditNotesVat));

        // (جاهزيّةُ الإطلاق · ح-٤٠) **لكلّ حدٍّ في الصافي صفٌّ يراه المالك.**
        //
        // كان الصافي أدناه بلا هذين الحدّين أصلاً — `VatReturnDto` يشتقّه بمعادلةٍ أقدمَ من
        // الإقرار. **فلمّا صار يحملهما لزم أن يُعرَضا**: صافٍ لا تجمع إليه السطورُ فوقه
        // **رقمٌ لا يُراجَع**، ولا يعرف المالكُ من أين جاءه فرقُ بيع الأصل.
        rows.Add(new Row(LocaleManager.T("تسوية إلغاء مستندات فترات سابقة (تُطرح)", "Prior-period cancellations (deducted)"), r.CancelledOutputVat));
        rows.Add(new Row(LocaleManager.T("ضريبة مخرجات مقيّدة بلا مستند (تُضاف)", "Output VAT posted without a document (added)"), r.JournalOutputVat));
        // ‏(جاهزيّةُ الإطلاق · tax-zatca#12) ‏حدٌّ في الصافي فله صفّ — ومقابلُه في المدخلات القابلة للخصم.
        rows.Add(new Row(LocaleManager.T("ضريبة الاحتساب العكسي (تُضاف، وتُخصم في المدخلات)", "Reverse-charge VAT (added, and deducted in input VAT)"), r.ReverseChargeVat));
        rows.Add(new Row(LocaleManager.T("صافي ضريبة المخرجات", "Net output VAT"), r.NetOutputVat, Bold: true));

        // (CEEF-229 · م1-١٠) **التفكيكُ تحت الإجمالي مباشرةً — لا في تقريرٍ آخر.**
        // نموذجُ الإقرار السعوديّ يفصل الأساسيَّ عن الصفريّ عن المعفى، ومجموعٌ كلّيٌّ وحده
        // يُنتج نسبةً مخلوطةً لا وجود لها في نظامٍ ضريبيّ.
        //
        // (جاهزيّةُ الإطلاق · ح-٤١) **وتحت الأبِ الذي تجمع إليه فعلاً.** كانت الأوعيةُ تُعرض
        // «منها» تحت وعاء **الفواتير وحدَها قبل الإشعارات**، وهي **صافيةٌ من الإشعارات** —
        // فلا تجمع إليه متى وُجد إشعارٌ واحد. **والأبُ الآن الصافي**، والضريبةُ المقيّدة بلا
        // مستندٍ «منها» بلا فئة: فهي في الصافي ولا وعاءَ لها يُخترع.
        AddBreakdown(rows, null, r.NetOutputVat, r.SalesByCategory, r.JournalOutputVat, taxable: false);
        if (r.SalesByCategory is not null && r.ReverseChargeVat != 0m)
            rows.Add(new Row(LocaleManager.T("منها احتساب عكسي — الخانة 9", "of which reverse charge — box 9"), r.ReverseChargeVat, Indented: true));
        AddBreakdown(rows, LocaleManager.T("صافي وعاء المبيعات (من المستندات)", "Net sales base (from documents)"), r.SalesBaseNet,
            r.SalesByCategory, r.JournalOutputVat, taxable: true);

        rows.Add(new Row(LocaleManager.T("مشتريات (وعاء)", "Purchases (base)"), r.PurchasesSubtotal));
        rows.Add(new Row(LocaleManager.T("ضريبة مدخلات قابلة للخصم", "Deductible input VAT"), r.DeductibleInputVat));
        rows.Add(new Row(LocaleManager.T("إشعارات مدينة (وعاء)", "Debit notes (base)"), r.DebitNotesSubtotal));
        rows.Add(new Row(LocaleManager.T("ضريبة مدخلات مُخفّضة (إشعارات مدينة)", "Reduced input VAT (debit notes)"), r.DebitNotesDeductibleVat));
        rows.Add(new Row(LocaleManager.T("تسوية إلغاء مشتريات فترات سابقة (تُطرح)", "Prior-period purchase cancellations (deducted)"), r.CancelledInputVat));
        rows.Add(new Row(LocaleManager.T("ضريبة مدخلات مقيّدة بلا مستند (تُضاف)", "Input VAT posted without a document (added)"), r.JournalInputVat));
        rows.Add(new Row(LocaleManager.T("صافي ضريبة المدخلات", "Net input VAT"), r.NetDeductibleInputVat, Bold: true));
        AddBreakdown(rows, null, r.NetDeductibleInputVat, r.PurchasesByCategory, r.JournalInputVat, taxable: false);
        AddBreakdown(rows, LocaleManager.T("صافي وعاء المشتريات القابلة للخصم (من المستندات)", "Net deductible purchases base (from documents)"), r.PurchasesBaseNet,
            r.PurchasesByCategory, r.JournalInputVat, taxable: true);

        rows.Add(new Row(LocaleManager.T("صافي الضريبة المستحقة/المستردة", "Net VAT payable / refundable"), r.NetVatPayable, Bold: true));

        AddOfficialForm(rows, r);
        return rows;
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-١٦٥ · tax-zatca#12) <b>خاناتُ نموذج الإقرار كما في بوّابة الهيئة</b> — يُنقل منها المالكُ
    /// إلى البوّابة خانةً خانة: المبلغُ والتعديلاتُ والضريبة. <b>وضريبةٌ قُيّدت بلا مستندٍ محسوبٍ لا خانةَ لها هنا</b>
    /// فتُعرض صفّاً مستقلّاً — ولا يُخترع لها وعاء.
    /// </summary>
    private static void AddOfficialForm(List<Row> rows, VatReturnDto r)
    {
        if (r.SalesBoxes is null || r.PurchaseBoxes is null)
            return;

        AddBoxes(rows, r.SalesBoxes, Brqosh.Domain.Tax.VatReturnBox.TotalSales);
        // ‏(sector-modules#6) ‏ضريبةُ العرابين قيدٌ لا مستندٌ محسوب — لكنّ لها فاتورةَ دفعةٍ (386) فبلغت الخانة 1؛ فلا تُعرض هنا مرّتين.
        var outputVatWithoutBox = r.JournalOutputVat - r.JournalOutputVatInBoxes;
        if (outputVatWithoutBox != 0m)
            rows.Add(new Row(LocaleManager.T("ضريبة مخرجات بلا مستند محسوب — لا خانة لها هنا", "Output VAT without a counted document — no box here"), outputVatWithoutBox, Indented: true));
        AddBoxes(rows, r.PurchaseBoxes, Brqosh.Domain.Tax.VatReturnBox.TotalPurchases);
        // ‏(tax-zatca#12) ‏ضريبةُ الخانة 9 مستحقّةٌ وقابلةٌ للخصم معاً — يحتسبها النموذجُ بنفسه (دليلُ الاستيراد §6.3).
        if (r.ReverseChargeVat != 0m)
            rows.Add(new Row(LocaleManager.T("ضريبة الخانة 9 مستحقة وقابلة للخصم معاً — أثرها في الصافي صفر", "Box 9 VAT is both due and deductible — no net effect"), r.ReverseChargeVat, Indented: true));
        if (r.JournalInputVat != 0m)
            rows.Add(new Row(LocaleManager.T("ضريبة مدخلات بلا مستند محسوب — لا خانة لها هنا", "Input VAT without a counted document — no box here"), r.JournalInputVat, Indented: true));
    }

    private static void AddBoxes(List<Row> rows, IReadOnlyList<VatReturnBoxDto> boxes, int totalBox)
    {
        foreach (var b in boxes)
        {
            rows.Add(new Row(BoxLabel(b.Box), b.Amount));
            if (b.Adjustments != 0m)
                rows.Add(new Row(LocaleManager.T("التعديلات", "Adjustments"), b.Adjustments, Indented: true));
            rows.Add(new Row(LocaleManager.T("ضريبة القيمة المضافة", "VAT amount"), b.VatAmount + b.VatAdjustments, Indented: true));
        }

        rows.Add(new Row(BoxLabel(totalBox), boxes.Sum(b => b.Amount + b.Adjustments), Bold: true));
        rows.Add(new Row(LocaleManager.T("ضريبة القيمة المضافة", "VAT amount"), boxes.Sum(b => b.VatAmount + b.VatAdjustments), Indented: true));
    }

    /// <summary>(ح-١٦٥) «خانة N — عنوانها» بلغة الواجهة.</summary>
    private static string BoxLabel(int box) => LocaleManager.T("خانة ", "Box ") + $"{box} — {BoxTitle(box)}";

    /// <summary>(ح-١٦٥) عنوانُ الخانة — والإنجليزيُّ حرفُ النموذج (<c>VatReturnBox.EnglishTitle</c>).</summary>
    private static string BoxTitle(int box) => box switch
    {
        1 => LocaleManager.T("المبيعات الخاضعة للنسبة الأساسية", Brqosh.Domain.Tax.VatReturnBox.EnglishTitle(box)),
        2 => LocaleManager.T("المبيعات التي تتحمل الدولة ضريبتها", Brqosh.Domain.Tax.VatReturnBox.EnglishTitle(box)),
        3 => LocaleManager.T("المبيعات المحلية الخاضعة للنسبة الصفرية", Brqosh.Domain.Tax.VatReturnBox.EnglishTitle(box)),
        4 => LocaleManager.T("الصادرات", Brqosh.Domain.Tax.VatReturnBox.EnglishTitle(box)),
        5 => LocaleManager.T("المبيعات المعفاة", Brqosh.Domain.Tax.VatReturnBox.EnglishTitle(box)),
        6 => LocaleManager.T("إجمالي المبيعات", Brqosh.Domain.Tax.VatReturnBox.EnglishTitle(box)),
        7 => LocaleManager.T("المشتريات المحلية الخاضعة للنسبة الأساسية", Brqosh.Domain.Tax.VatReturnBox.EnglishTitle(box)),
        8 => LocaleManager.T("الاستيرادات الخاضعة للضريبة المدفوعة في الجمارك", Brqosh.Domain.Tax.VatReturnBox.EnglishTitle(box)),
        9 => LocaleManager.T("الاستيرادات الخاضعة للضريبة بآلية الاحتساب العكسي", Brqosh.Domain.Tax.VatReturnBox.EnglishTitle(box)),
        10 => LocaleManager.T("المشتريات الخاضعة للنسبة الصفرية", Brqosh.Domain.Tax.VatReturnBox.EnglishTitle(box)),
        11 => LocaleManager.T("المشتريات المعفاة", Brqosh.Domain.Tax.VatReturnBox.EnglishTitle(box)),
        12 => LocaleManager.T("إجمالي المشتريات", Brqosh.Domain.Tax.VatReturnBox.EnglishTitle(box)),
        _ => box.ToString(),
    };

    /// <summary>
    /// (ح-٤١) <b>أوعيةُ الفئة «منها» تحت أبٍ تجمع إليه.</b> الوعاءُ تحت صافي الوعاء (سطرٌ
    /// يُضاف هنا)؛ <b>والضريبةُ تحت صفّ الصافي القائم فوقها مباشرةً</b> (<paramref name="parent"/>
    /// عدمٌ)، ومعها «بلا فئة» لِما قُيّد بلا مستند متى وُجد.
    /// </summary>
    private static void AddBreakdown(List<Row> rows, string? parent, decimal parentValue,
        IReadOnlyList<VatReturnCategoryDto>? buckets, decimal uncategorised, bool taxable)
    {
        if (buckets is null)
            return;

        if (parent is not null)
            rows.Add(new Row(parent, parentValue));

        foreach (var b in buckets)
        {
            var value = taxable ? b.TaxableAmount : b.VatAmount;
            if (!taxable && value == 0m)
                continue;
            rows.Add(new Row(LocaleManager.T("منها ", "of which ") + $"{LabelFor(b.Code)} ({b.Code})", value, Indented: true));
        }

        if (!taxable && uncategorised != 0m)
            rows.Add(new Row(LocaleManager.T("منها بلا فئة — مقيّدة بلا مستند", "of which uncategorised — posted without a document"), uncategorised, Indented: true));
    }

    /// <summary>
    /// (CEEF-229 · م1-١٠) تسميةُ الفئة — <b>في طبقة العرض عمداً</b>.
    ///
    /// <para>بوّابةُ الترجمة تمسح <c>src/Brqosh.Desktop</c> وحدها. فتسميةٌ في طبقةٍ أدنى
    /// <b>تُفلت منها وتبقى عربيةً عند التبديل إلى الإنجليزية</b> — والحدُّ يحمل الرمزَ وحده.</para>
    /// </summary>
    private static string LabelFor(string code) => code switch
    {
        "S" => LocaleManager.T("خاضعة بالنسبة الأساسية", "Standard-rated"),
        "Z" => LocaleManager.T("خاضعة بنسبة صفر", "Zero-rated"),
        "E" => LocaleManager.T("معفاة", "Exempt"),
        "O" => LocaleManager.T("خارج نطاق الضريبة", "Out of scope"),
        _ => code,
    };
}
