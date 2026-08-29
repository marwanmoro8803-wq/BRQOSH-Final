using Brqosh.Domain.Accounting;

namespace Brqosh.Domain.Sales;

public enum SalesQuotationStatus
{
    Draft = 0,
    Sent = 1,
    Accepted = 2,
    Rejected = 3,
    Expired = 4,
}

/// <summary>عرض سعر — LIFECYCLE-31 (مبيعات مبكرة).</summary>
public class SalesQuotation : ILineDocumentTotals
{
    private readonly List<SalesInvoiceLine> _lines = [];

    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long CustomerId { get; private set; }
    public string? QuotationNumber { get; private set; }
    public DateOnly QuotationDate { get; private set; }
    public DateOnly ValidUntil { get; private set; }
    public SalesQuotationStatus Status { get; private set; } = SalesQuotationStatus.Draft;
    public decimal Subtotal { get; private set; }
    public decimal VatAmount { get; private set; }
    public IReadOnlyList<SalesInvoiceLine> Lines => _lines.AsReadOnly();

    private SalesQuotation() { }

    private SalesQuotation(long companyId, long customerId, DateOnly quotationDate, DateOnly validUntil)
    {
        CompanyId = companyId;
        CustomerId = customerId;
        QuotationDate = quotationDate;
        ValidUntil = validUntil;
    }

    public static SalesQuotation CreateDraft(long companyId, long customerId, DateOnly quotationDate, int validDays = 30)
    {
        if (validDays < 1)
            throw new AccountingException("مدة صلاحية العرض يجب أن تكون يوماً واحداً على الأقل.");
        return new SalesQuotation(companyId, customerId, quotationDate, quotationDate.AddDays(validDays));
    }

    public void AddLine(string description, decimal quantity, decimal unitPrice, long? productId = null)
    {
        if (Status != SalesQuotationStatus.Draft)
            throw new AccountingException("لا تُعدَّل عروض الأسعار بعد الإرسال.");
        DocumentLineScale.EnsureUnitPriceHalala(unitPrice);
        var line = new SalesInvoiceLine(description.Trim(), quantity, unitPrice, productId);
        _lines.Add(line);
        RecalculateTotals();
    }

    public void MarkSent(string number)
    {
        if (Status != SalesQuotationStatus.Draft)
            throw new AccountingException("العرض ليس مسودة.");
        QuotationNumber = number;
        Status = SalesQuotationStatus.Sent;
    }

    public void Accept() => Status = SalesQuotationStatus.Accepted;

    private void RecalculateTotals()
    {
        Subtotal = Math.Round(_lines.Sum(l => l.LineTotal), 2, MidpointRounding.AwayFromZero);
        VatAmount = Math.Round(Subtotal * SalesInvoice.SaudiVatRate, 2, MidpointRounding.AwayFromZero);
    }
}
