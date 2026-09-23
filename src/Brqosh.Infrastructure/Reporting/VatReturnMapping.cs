using Brqosh.Application.Reporting;
using Brqosh.Domain.Tax;

namespace Brqosh.Infrastructure.Reporting;

/// <summary>
/// (CEEF-229 · م1-١٠ / Z-12) <b>عبورُ الإقرار من الاستعلام إلى الحدّ — وهو ما كان مقطوعاً.</b>
///
/// <para><c>VatReturnQuery</c> يحسب الصناديقَ بالفئة منذ A-15، و<b>صفرُ قارئٍ لها خارج ملفّه</b>.
/// فالمستخدمُ يرى مجموعاً كلّياً بنسبةٍ مخلوطة — ربعٌ فيه مبيعاتٌ محلية 1,000,000 وصادراتٌ
/// صفرية 400,000 يعرض <b>10.71%</b>، نسبةٌ لا وجود لها في أيّ نظام ضريبيّ، وتعارضٌ آليٌّ
/// فوريّ على بوابة الهيئة.</para>
///
/// <para><b>ولماذا صنفٌ مستقلٌّ لا سطرٌ داخل المُحوِّل:</b> المُحوِّل <c>file sealed</c> فلا
/// يُقاس من خارج ملفّه — <b>والوصلةُ التي لا تُقاس هي بالضبط ما انقطع هنا</b>. فأُخرج العبورُ
/// إلى موضعٍ يُستدعى في اختبار.</para>
/// </summary>
public static class VatReturnMapping
{
    public static VatReturnDto From(VatReturnResult r) =>
        new(r.FromDate, r.ToDate,
            r.SalesSubtotal, r.OutputVat,
            r.ProgressBillingsSubtotal, r.ProgressBillingsVat,
            r.CreditNotesSubtotal, r.CreditNotesVat,
            r.PurchasesSubtotal, r.DeductibleInputVat,
            r.DebitNotesSubtotal, r.DebitNotesVat, r.DebitNotesDeductibleVat,
            // ‏(جاهزيّةُ الإطلاق · ح-٤٠) **والحدودُ الأربعةُ تعبر الحدّ** — وكانت تُحسب في
            // ‏الإقرار وتسقط هنا، فتقول الشاشةُ صافياً غيرَ المُقدَّم.
            CancelledOutputVat: r.CancelledOutputVat,
            CancelledInputVat: r.CancelledInputVat,
            JournalOutputVat: r.JournalOutputVat,
            JournalInputVat: r.JournalInputVat,
            SalesBaseNet: r.SalesBaseNet,
            PurchasesBaseNet: r.PurchasesBaseNet,
            GlVatNetPayableBalance: r.GlVatNetPayableBalance,
            Map(r.SalesByCategory),
            Map(r.PurchasesByCategory));

    private static IReadOnlyList<VatReturnCategoryDto>? Map(IReadOnlyList<VatReturnBucket>? buckets) =>
        buckets?.Select(b => new VatReturnCategoryDto(
                b.Category.UblCode(), b.TaxableAmount, b.VatAmount))
            .ToList();

}
