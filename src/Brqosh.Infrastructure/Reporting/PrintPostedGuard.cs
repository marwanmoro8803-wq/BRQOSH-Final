using Brqosh.Domain.Accounting;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;

namespace Brqosh.Infrastructure.Reporting;

/// <summary>AUDIT-PRT-001 — منع طباعة مستندات غير مرحّلة.</summary>
public static class PrintPostedGuard
{
    public static void EnsureSalesInvoicePosted(SalesInvoice invoice)
    {
        if (invoice.Status != SalesInvoiceStatus.Posted)
            throw new AccountingException("لا يمكن طباعة فاتورة غير مرحّلة.");
    }

    public static void EnsurePurchaseInvoicePosted(PurchaseInvoice invoice)
    {
        if (invoice.Status != PurchaseInvoiceStatus.Posted)
            throw new AccountingException("لا يمكن طباعة فاتورة شراء غير مرحّلة.");
    }

    public static void EnsureCreditNotePosted(CreditNote note)
    {
        if (note.Status != CreditNoteStatus.Posted)
            throw new AccountingException("لا يمكن طباعة إشعار دائن غير مرحّل.");
    }

    public static void EnsureDebitNotePosted(DebitNote note)
    {
        if (note.Status != DebitNoteStatus.Posted)
            throw new AccountingException("لا يمكن طباعة إشعار مدين غير مرحّل.");
    }

    public static void EnsureReceiptVoucherPosted(ReceiptVoucher voucher)
    {
        if (voucher.Status != ReceiptVoucherStatus.Posted)
            throw new AccountingException("لا يمكن طباعة سند قبض غير مرحّل.");
    }

    public static void EnsurePaymentVoucherPosted(PaymentVoucher voucher)
    {
        if (voucher.Status != PaymentVoucherStatus.Posted)
            throw new AccountingException("لا يمكن طباعة سند صرف غير مرحّل.");
    }

    public static void EnsureJournalEntryPosted(JournalEntry entry)
    {
        if (entry.Status != JournalEntryStatus.Posted)
            throw new AccountingException("لا يمكن طباعة قيد غير مرحّل.");
    }
}
