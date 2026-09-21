using Brqosh.Domain.Accounting;
using Brqosh.Domain.Tax;

namespace Brqosh.Desktop.Ui;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٦) <b>ما تعرضه شاشةُ بيع الأصل قبل الترحيل — في موضعٍ يُقاس.</b>
///
/// <para><b>ولِمَ أُخرج من الشاشة:</b> الحسابُ كان داخل معالجِ ضغطٍ يحتاج خيطَ واجهة، <b>فلا
/// يُقاس ما يراه المستخدم إلا بتشغيل التطبيق</b> — وهو السببُ البنيويُّ نفسُه الذي أُخرجت
/// لأجله <see cref="VatReturnRows"/>.</para>
///
/// <para><b>والرقمُ المعروض هو الرقمُ المُرحَّل حرفاً</b>: التقريبُ والقاعدةُ من
/// <c>FixedAssetDisposalPostingService</c>. <b>واختلافُ الشاشة عن القيد أسوأُ من الخطأ
/// نفسِه</b> — يُفقد الثقةَ في كلّ تأكيدٍ بعده.</para>
/// </summary>
public static class FixedAssetDisposalPreview
{
    /// <summary>
    /// <b>معاملةٌ ضريبيّةٌ مختارةٌ من قائمةٍ مغلقة — لا رمزٌ يُكتب بيد</b> (على منوال ح-١١).
    ///
    /// <para><b>والفئةُ ورمزُها يُسافران معاً</b>: فئةٌ صفريّةٌ بلا رمزٍ نظاميٍّ يرفضها
    /// النطاقُ (<c>BR-KSA-CL-06</c>)، ورمزٌ من فئةٍ أخرى يرفضه كذلك. <b>فاقترانُهما في
    /// مدخلٍ واحدٍ يجعل الحالةَ الخاطئةَ غيرَ قابلةٍ للتمثيل</b> بدل أن تُحرَس بفحص.</para>
    ///
    /// <para><b>وهذه الشاشةُ لم تكن تُمرّر فئةً أصلاً</b> — والخدمةُ تقبلها منذ
    /// <c>CEEF-232 · ض-٢</c>، <b>فكانت الأساسيّةُ تُفرض على كلّ بيع</b>.</para>
    /// </summary>
    public sealed record VatTreatment(string Label, VatCategory Category, string? Code)
    {
        public static readonly IReadOnlyList<VatTreatment> Options =
        [
            new("خاضعة 15%", VatCategory.Standard, null),
            new("صفرية — تصدير الأصل خارج المملكة", VatCategory.ZeroRated, "VATEX-SA-32"),
            new("صفرية — وسيلة نقل مؤهَّلة", VatCategory.ZeroRated, "VATEX-SA-34-4"),
        ];

        public override string ToString() => Label;
    }

    /// <summary>أرقامُ المعاينة — وهي أرقامُ القيد نفسُها.</summary>
    public readonly record struct Summary(
        decimal Proceeds, decimal Vat, decimal Received, decimal GainLoss, bool Invoiced);

    /// <summary>
    /// <paramref name="companyIsVatRegistered"/> <b>يُقرأ من حال المنشأة لا يُكتب ثابتاً</b>:
    /// منشأةٌ غير مسجَّلةٍ لا تُصدر ضريبةً <b>ولا فاتورةَ ضريبة</b>، <b>ووعدُ الشاشة لها
    /// بضريبةٍ لن تُقيَّد يُخالف ما في القيد</b>.
    /// </summary>
    public static Summary Compute(
        decimal proceeds, VatTreatment treatment, decimal bookValue, bool companyIsVatRegistered)
    {
        var rounded = Math.Round(proceeds, 2, MidpointRounding.AwayFromZero);
        var category = companyIsVatRegistered ? treatment.Category : VatCategory.OutOfScope;

        var vat = Math.Round(
            rounded * category.RateFor(VatCalculator.SaudiVatRate), 2, MidpointRounding.AwayFromZero);

        // ‏**وشرطُ الفوترة هو شرطُ الخدمة حرفاً**: مقابلٌ موجبٌ وفئةٌ داخل النطاق.
        // ‏فصفريُّ النسبة **يُفوتَر وإن كانت ضريبتُه صفراً** — وهو توريدٌ خاضعٌ لا معفًى.
        var invoiced = rounded > 0 && category != VatCategory.OutOfScope;

        return new Summary(
            rounded, vat, rounded + vat,
            Math.Round(rounded - bookValue, 2, MidpointRounding.AwayFromZero),
            invoiced);
    }

    /// <summary>نصُّ المعاينة — <b>ويعرض المقبوضَ لا المقابلَ وحدَه</b>، فهو ما يُودَع فعلاً.</summary>
    public static string Describe(
        string assetName, decimal acquisitionCost, decimal accumulatedDepreciation,
        decimal bookValue, Summary s)
    {
        var verdict = s.GainLoss >= 0 ? $"ربح متوقّع {s.GainLoss:N2}" : $"خسارة متوقّعة {-s.GainLoss:N2}";
        var invoice = s.Invoiced
            ? "وتُصدَر فاتورة ضريبة إلكترونية بهذا البيع."
            : "ولا تُصدَر فاتورة ضريبة — لا مقابل، أو المنشأة خارج نطاق الضريبة.";

        return $"استبعاد الأصل {assetName}\n\n"
             + $"التكلفة: {acquisitionCost:N2}\n"
             + $"مجمّع الإهلاك: {accumulatedDepreciation:N2}\n"
             + $"القيمة الدفترية: {bookValue:N2}\n"
             + $"عائد البيع (قبل الضريبة): {s.Proceeds:N2}\n"
             + $"ضريبة القيمة المضافة: {s.Vat:N2}\n"
             + $"المقبوض: {s.Received:N2}\n"
             + $"⟵ {verdict}\n{invoice}";
    }
}
