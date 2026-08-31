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
            new("مبيعات خاضعة (وعاء)", r.SalesSubtotal),
        };

        // (CEEF-229 · م1-١٠) **التفكيكُ تحت الإجمالي مباشرةً — لا في تقريرٍ آخر.**
        //
        // نموذجُ الإقرار السعوديّ يفصل المبيعاتِ الخاضعة بالنسبة الأساسية عن الصادرات
        // الصفرية عن المعفاة. ومجموعٌ كلّيٌّ وحده يُنتج نسبةً مخلوطة — ربعٌ فيه 1,000,000
        // محلية و400,000 صادرات يقول **10.71%**، ولا وجود لها في نظامٍ ضريبيّ.
        foreach (var bucket in r.SalesByCategory ?? [])
            rows.Add(new Row($"منها {LabelFor(bucket.Code)} ({bucket.Code})", bucket.TaxableAmount, Indented: true));

        rows.Add(new Row("ضريبة المخرجات — مبيعات", r.OutputVat));

        foreach (var bucket in (r.SalesByCategory ?? []).Where(c => c.VatAmount != 0m))
            rows.Add(new Row($"منها {LabelFor(bucket.Code)} ({bucket.Code})", bucket.VatAmount, Indented: true));

        rows.Add(new Row("مستخلصات أعمال (وعاء)", r.ProgressBillingsSubtotal));
        rows.Add(new Row("ضريبة المخرجات — مستخلصات", r.ProgressBillingsVat));
        rows.Add(new Row("إشعارات دائن (وعاء)", r.CreditNotesSubtotal));
        rows.Add(new Row("ضريبة مرتجعة (إشعارات دائن)", r.CreditNotesVat));
        rows.Add(new Row("صافي ضريبة المخرجات", r.NetOutputVat, Bold: true));

        rows.Add(new Row("مشتريات (وعاء)", r.PurchasesSubtotal));
        foreach (var bucket in r.PurchasesByCategory ?? [])
            rows.Add(new Row($"منها {LabelFor(bucket.Code)} ({bucket.Code})", bucket.TaxableAmount, Indented: true));

        rows.Add(new Row("ضريبة مدخلات قابلة للخصم", r.DeductibleInputVat));
        rows.Add(new Row("إشعارات مدينة (وعاء)", r.DebitNotesSubtotal));
        rows.Add(new Row("ضريبة مدخلات مُخفّضة (إشعارات مدينة)", r.DebitNotesDeductibleVat));
        rows.Add(new Row("صافي ضريبة المدخلات", r.NetDeductibleInputVat, Bold: true));
        rows.Add(new Row("صافي الضريبة المستحقة/المستردة", r.NetVatPayable, Bold: true));

        return rows;
    }

    /// <summary>
    /// (CEEF-229 · م1-١٠) تسميةُ الفئة — <b>في طبقة العرض عمداً</b>.
    ///
    /// <para>بوّابةُ الترجمة تمسح <c>src/Brqosh.Desktop</c> وحدها. فتسميةٌ في طبقةٍ أدنى
    /// <b>تُفلت منها وتبقى عربيةً عند التبديل إلى الإنجليزية</b> — والحدُّ يحمل الرمزَ وحده.</para>
    /// </summary>
    private static string LabelFor(string code) => code switch
    {
        "S" => "خاضعة بالنسبة الأساسية",
        "Z" => "خاضعة بنسبة صفر",
        "E" => "معفاة",
        "O" => "خارج نطاق الضريبة",
        _ => code,
    };
}
