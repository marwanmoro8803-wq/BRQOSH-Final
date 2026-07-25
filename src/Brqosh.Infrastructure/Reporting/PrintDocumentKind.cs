namespace Brqosh.Infrastructure.Reporting;

/// <summary>Print layout kind — drives title, party labels, and ZATCA compliance strip.</summary>
public enum PrintDocumentKind
{
    SimplifiedSalesInvoice = 1,
    StandardSalesInvoice = 2,
    PurchaseInvoice = 3,
    CreditNote = 4,
    PurchaseDebitNote = 5,
    ProgressBilling = 6,
}
