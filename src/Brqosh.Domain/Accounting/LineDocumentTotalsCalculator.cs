using Brqosh.Domain.Sales;
using Brqosh.Domain.Tax;

namespace Brqosh.Domain.Accounting;

/// <summary>وعاء ضريبيّ واحد: أسطر تشترك في الفئة والنسبة (cac:TaxSubtotal في UBL).</summary>
public record VatSubtotal(VatCategory Category, decimal Rate, decimal TaxableAmount, decimal VatAmount,
    string? ExemptionReasonCode = null, string? ExemptionReasonText = null);

/// <summary>
/// حساب إجماليات مستندات السطور — Subtotal و VatAmount مُخزَّنان في DB.
/// GrandTotal محسوب في الذاكرة فقط عبر <see cref="LineDocumentTotalsExtensions.GrandTotal"/>.
/// في استعلامات EF استخدم <c>SumGrandTotalAsync</c> من Infrastructure — لا تُجمِع GrandTotal في LINQ.
/// </summary>
public static class LineDocumentTotalsCalculator
{
    /// <summary>
    /// الأوعية الضريبية مجمَّعة بالفئة (CEEF-207).
    ///
    /// التقريب على مستوى **الوعاء** لا المستند ولا السطر — وهو ما تفرضه دلالة
    /// cac:TaxSubtotal لدى الهيئة. وحين تكون كل الأسطر أساسية (الحالة السائدة)
    /// يصير الوعاء واحداً فتطابق النتيجة السلوك السابق حرفياً — لا انزياح في أرقام قائمة.
    /// </summary>
    public static IReadOnlyList<VatSubtotal> ComputeSubtotals(
        IEnumerable<SalesInvoiceLine> lines,
        decimal standardRate = SalesInvoice.SaudiVatRate,
        decimal documentDiscount = 0m)
    {
        var groups = lines
            .GroupBy(l => l.VatCategory)
            .OrderBy(g => g.Key)
            .ToList();

        // (CEEF-230 · ط1-٢) ‏**توزيعُ خصم المستند على الأوعية — والفلسُ الأخير مقصود.**
        //
        // ‏الخصمُ يُوزَّع بنسبة كلّ وعاءٍ من الصافي. والتقريبُ لكلّ وعاءٍ على حدةٍ يُنتج
        // ‏مجموعاً يفترق عن الخصم بفلسٍ أو فلسين — **فيصير مجموعُ الأوعية مخالفاً
        // ‏لإجمالي المستند، وهو ما ترفضه الهيئة حساباً لا شكلاً**.
        //
        // ‏فالوعاءُ الأخير يأخذ **الباقي** لا حصّتَه المقرَّبة. وهو المذهبُ نفسُه الذي
        // ‏يفرضه توزيعُ أيّ مبلغٍ على حصص: **الفرقُ يُسكَن في طرفٍ واحدٍ مُعلَن**، لا يُترك
        // ‏يتسرّب في كلّ الحصص.
        var netTotal = groups.Sum(g => g.Sum(l => l.LineTotal));
        var shares = new decimal[groups.Count];

        if (documentDiscount > 0m && netTotal > 0m)
        {
            decimal allocated = 0m;
            for (var i = 0; i < groups.Count; i++)
            {
                if (i == groups.Count - 1)
                {
                    shares[i] = documentDiscount - allocated;
                    break;
                }

                shares[i] = Math.Round(
                    documentDiscount * groups[i].Sum(l => l.LineTotal) / netTotal,
                    2, MidpointRounding.AwayFromZero);
                allocated += shares[i];
            }
        }

        return groups
            .Select((g, i) =>
            {
                var rate = g.Key.RateFor(standardRate);
                var taxable = g.Sum(l => l.LineTotal) - shares[i];
                return new VatSubtotal(
                    g.Key,
                    rate,
                    taxable,
                    Math.Round(taxable * rate, 2, MidpointRounding.AwayFromZero),
                    g.Select(l => l.TaxExemptionReasonCode)
                     .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)),
                    // ‏والنصُّ دالّةٌ في الرمز: سطرٌ قديمٌ خُزّن قبل هذه الدفعة يحمل رمزاً بلا
                    // ‏نصّ، فيُستكمل من القائمة بدل أن يخرج المستندُ ناقصاً وترفضه الهيئة.
                    g.Select(l => l.TaxExemptionReason)
                     .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t))
                    ?? VatExemptionReason.Find(
                           g.Select(l => l.TaxExemptionReasonCode)
                            .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)))?.EnglishText);
            })
            .ToList();
    }

    /// <summary>
    /// (CEEF-230 · ط1-٢) ‏حصّةُ كلّ وعاءٍ من خصم المستند — تُعرض في UBL عنصرَ
    /// <c>AllowanceCharge</c> مستقلّاً بفئته الضريبية، **فيُقرأ التوزيعُ في المستند نفسِه**
    /// لا يُستنتج من فرق الأرقام.
    /// </summary>
    public static IReadOnlyList<(VatCategory Category, decimal Rate, decimal Amount)>
        ComputeDiscountAllocation(
            IEnumerable<SalesInvoiceLine> lines,
            decimal documentDiscount,
            decimal standardRate = SalesInvoice.SaudiVatRate)
    {
        if (documentDiscount <= 0m) return [];

        var withDiscount = ComputeSubtotals(lines, standardRate, documentDiscount);
        var without = ComputeSubtotals(lines, standardRate);

        return withDiscount
            .Select((s, i) => (s.Category, s.Rate, without[i].TaxableAmount - s.TaxableAmount))
            .Where(x => x.Item3 != 0m)
            .ToList();
    }

    public static (decimal Subtotal, decimal VatAmount) Compute(
        IEnumerable<SalesInvoiceLine> lines,
        decimal vatRate = SalesInvoice.SaudiVatRate,
        decimal documentDiscount = 0m)
    {
        var subtotals = ComputeSubtotals(lines, vatRate, documentDiscount);
        return (subtotals.Sum(s => s.TaxableAmount), subtotals.Sum(s => s.VatAmount));
    }
}

public interface ILineDocumentTotals
{
    decimal Subtotal { get; }
    decimal VatAmount { get; }
}

/// <summary>إجمالي شامل الضريبة — للاستخدام بعد تحميل الكيان (لا يُترجم إلى SQL).</summary>
public static class LineDocumentTotalsExtensions
{
    public static decimal GrandTotal(this ILineDocumentTotals document) =>
        document.Subtotal + document.VatAmount;
}
