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

        return rows;
    }

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
