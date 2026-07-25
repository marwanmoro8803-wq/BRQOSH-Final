using Brqosh.Domain.Sales;

namespace Brqosh.Domain.Accounting;

/// <summary>
/// حساب إجماليات مستندات السطور — Subtotal و VatAmount مُخزَّنان في DB.
/// GrandTotal محسوب في الذاكرة فقط عبر <see cref="LineDocumentTotalsExtensions.GrandTotal"/>.
/// في استعلامات EF استخدم <c>SumGrandTotalAsync</c> من Infrastructure — لا تُجمِع GrandTotal في LINQ.
/// </summary>
public static class LineDocumentTotalsCalculator
{
    public static (decimal Subtotal, decimal VatAmount) Compute(
        IEnumerable<SalesInvoiceLine> lines,
        decimal vatRate = SalesInvoice.SaudiVatRate)
    {
        var subtotal = lines.Sum(l => l.LineTotal);
        var vat = Math.Round(subtotal * vatRate, 2, MidpointRounding.AwayFromZero);
        return (subtotal, vat);
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
