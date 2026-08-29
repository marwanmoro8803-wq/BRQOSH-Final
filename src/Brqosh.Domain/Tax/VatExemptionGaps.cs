using Brqosh.Domain.Sales;

namespace Brqosh.Domain.Tax;

/// <summary>
/// (CEEF-229 · م1-١ب الشقّ الثاني) <b>ما ينقص سطراً غيرَ خاضعٍ للنسبة الأساسية — باسمه.</b>
///
/// <para>ثلاثةُ أعطالٍ مختلفة تُرفض كلُّها عند الهيئة، ويجب أن تُقال مفرَّقةً لا مجموعة:</para>
/// <list type="number">
///   <item><b>سببٌ غائب</b> — فئةٌ <c>Z</c>/<c>E</c>/<c>O</c> بلا رمز (BR-KSA-CL-06).</item>
///   <item><b>رمزٌ خارج القائمة</b> أو <b>رمزُ فئةٍ أخرى</b> — كـ<c>VATEX-SA-32</c> (تصدير،
///     فئة Z) على سطرٍ معفى. يمرّ على فحص الوجود ويسقط عند الهيئة.</item>
///   <item><b>رمزان مختلفان في فئةٍ واحدة</b> — والبنيةُ لا تحتملهما: الهيئةُ تقرأ السببَ من
///     <c>cac:TaxSubtotal</c> وهو <b>وعاءٌ واحدٌ لكلّ فئة</b>. فسطرُ تصديرِ سلعٍ وسطرُ تصديرِ
///     خدماتٍ في فاتورةٍ واحدة يُنتجان وعاءً واحداً لا يسع رمزين — ولو أُصدرت لَحملت
///     أحدَهما صامتةً وكذبت على الآخر.</item>
/// </list>
///
/// <para>والفئةُ <c>O</c> سببُها نصٌّ حرّ يكتبه المكلَّف — فرمزُها واحدٌ ثابت
/// (<c>VATEX-SA-OOS</c>) ونصُّها مطلوبٌ منه هو.</para>
/// </summary>
public static class VatExemptionGaps
{
    /// <summary>النقصُ مسمّى سطراً سطراً — وقائمةٌ فارغة تعني «تُصدَر».</summary>
    public static IReadOnlyList<string> For(IReadOnlyList<SalesInvoiceLine> lines)
    {
        var gaps = new List<string>();

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (!VatExemptionReason.IsRequiredFor(line.VatCategory))
                continue;

            var label = $"السطر {i + 1} («{line.Description}»)";
            var known = VatExemptionReason.Find(line.TaxExemptionReasonCode);

            if (known is null)
            {
                gaps.Add(string.IsNullOrWhiteSpace(line.TaxExemptionReasonCode)
                    ? $"{label} بفئة «{line.VatCategory.UblCode()}» بلا سبب إعفاء."
                    : $"{label}: رمز «{line.TaxExemptionReasonCode}» ليس من قائمة الهيئة.");
                continue;
            }

            if (known.Category != line.VatCategory)
                gaps.Add($"{label}: رمز «{known.Code}» يخصّ فئة «{known.Category.UblCode()}» "
                         + $"لا فئة «{line.VatCategory.UblCode()}».");

            // ‏نصُّ «خارج النطاق» من المكلَّف وحده — والقائمةُ لا تملك له نصّاً.
            if (known.Code == VatExemptionReason.OutOfScopeCode
                && string.IsNullOrWhiteSpace(line.TaxExemptionReason))
                gaps.Add($"{label}: فئة «خارج نطاق الضريبة» تتطلّب سبباً مكتوباً.");
        }

        foreach (var group in lines
                     .Where(l => VatExemptionReason.IsRequiredFor(l.VatCategory))
                     .GroupBy(l => l.VatCategory))
        {
            var codes = group
                .Select(l => l.TaxExemptionReasonCode?.Trim())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (codes.Count > 1)
                gaps.Add($"فئة «{group.Key.UblCode()}» تحمل سببين مختلفين ({string.Join("، ", codes)}) "
                         + "— والوعاء الضريبيّ واحد لكلّ فئة، فلا يسع إلا سبباً واحداً.");
        }

        return gaps;
    }
}
