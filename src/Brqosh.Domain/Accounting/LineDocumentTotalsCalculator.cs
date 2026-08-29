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
        decimal standardRate = SalesInvoice.SaudiVatRate) =>
        lines
            .GroupBy(l => l.VatCategory)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var rate = g.Key.RateFor(standardRate);
                var taxable = g.Sum(l => l.LineTotal);
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

    public static (decimal Subtotal, decimal VatAmount) Compute(
        IEnumerable<SalesInvoiceLine> lines,
        decimal vatRate = SalesInvoice.SaudiVatRate)
    {
        var subtotals = ComputeSubtotals(lines, vatRate);
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
