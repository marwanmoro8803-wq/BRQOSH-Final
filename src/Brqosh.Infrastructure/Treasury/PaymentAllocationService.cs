using Brqosh.Infrastructure.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Construction;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Treasury;

public record OpenDocumentBalance(
    PaymentAllocationDocumentType DocumentType,
    long DocumentId,
    string DocumentNumber,
    DateOnly DocumentDate,
    decimal OriginalAmount,
    decimal AllocatedAmount,
    decimal RemainingAmount);

/// <summary>استعلام المتبقي على فواتير/مستخلصات وحفظ تخصيصات السندات.</summary>
public class PaymentAllocationService(BrqoshDbContext db)
{
    public async Task<IReadOnlyList<OpenDocumentBalance>> OpenSalesDocumentsAsync(
        long companyId, long customerId, CancellationToken ct = default)
    {
        var invoices = await db.SalesInvoices
            .Where(i => i.CompanyId == companyId
                        && i.CustomerId == customerId
                        && i.Status == SalesInvoiceStatus.Posted
                        && i.PosShiftId == null)
            .ToListAsync(ct);

        var billings = await db.ProgressBillings
            .Where(b => b.CompanyId == companyId
                        && b.CustomerId == customerId
                        && b.Status == ProgressBillingStatus.Posted)
            .ToListAsync(ct);

        var result = new List<OpenDocumentBalance>();
        foreach (var inv in invoices)
        {
            var remaining = await RemainingAsync(companyId, PaymentAllocationDocumentType.SalesInvoice,
                inv.Id, inv.GrandTotal(), ct);
            if (remaining > 0.009m)
            {
                result.Add(new OpenDocumentBalance(
                    PaymentAllocationDocumentType.SalesInvoice, inv.Id,
                    inv.InvoiceNumber ?? inv.Id.ToString(), inv.InvoiceDate,
                    inv.GrandTotal(), inv.GrandTotal() - remaining, remaining));
            }
        }

        foreach (var b in billings)
        {
            var remaining = await RemainingAsync(companyId, PaymentAllocationDocumentType.ProgressBilling,
                b.Id, b.GrandTotal, ct);
            if (remaining > 0.009m)
            {
                result.Add(new OpenDocumentBalance(
                    PaymentAllocationDocumentType.ProgressBilling, b.Id,
                    b.BillingNumber ?? b.Id.ToString(), b.BillingDate,
                    b.GrandTotal, b.GrandTotal - remaining, remaining));
            }
        }

        return result.OrderBy(r => r.DocumentDate).ThenBy(r => r.DocumentNumber).ToList();
    }

    public async Task<IReadOnlyList<OpenDocumentBalance>> OpenPurchaseDocumentsAsync(
        long companyId, long supplierId, CancellationToken ct = default)
    {
        var invoices = await db.PurchaseInvoices
            .Where(i => i.CompanyId == companyId
                        && i.SupplierId == supplierId
                        && i.Status == PurchaseInvoiceStatus.Posted)
            .ToListAsync(ct);

        var result = new List<OpenDocumentBalance>();
        foreach (var inv in invoices)
        {
            var remaining = await RemainingAsync(companyId, PaymentAllocationDocumentType.PurchaseInvoice,
                inv.Id, inv.GrandTotal(), ct);
            if (remaining > 0.009m)
            {
                result.Add(new OpenDocumentBalance(
                    PaymentAllocationDocumentType.PurchaseInvoice, inv.Id,
                    inv.InvoiceNumber ?? inv.Id.ToString(), inv.InvoiceDate,
                    inv.GrandTotal(), inv.GrandTotal() - remaining, remaining));
            }
        }

        return result.OrderBy(r => r.DocumentDate).ThenBy(r => r.DocumentNumber).ToList();
    }

    public async Task ApplyReceiptAllocationsAsync(
        long companyId, long receiptVoucherId, DateOnly date,
        IReadOnlyList<(PaymentAllocationDocumentType Type, long DocumentId, string Number, decimal Amount)> lines,
        CancellationToken ct = default)
    {
        await ValidateAndAddAsync(companyId, date, lines, receiptVoucherId, null, isReceipt: true, ct);
    }

    public async Task ApplyPaymentAllocationsAsync(
        long companyId, long paymentVoucherId, DateOnly date,
        IReadOnlyList<(PaymentAllocationDocumentType Type, long DocumentId, string Number, decimal Amount)> lines,
        CancellationToken ct = default)
    {
        await ValidateAndAddAsync(companyId, date, lines, null, paymentVoucherId, isReceipt: false, ct);
    }

    private async Task ValidateAndAddAsync(
        long companyId, DateOnly date,
        IReadOnlyList<(PaymentAllocationDocumentType Type, long DocumentId, string Number, decimal Amount)> lines,
        long? receiptId, long? paymentId, bool isReceipt, CancellationToken ct)
    {
        if (lines.Count == 0)
            return;

        foreach (var line in lines)
        {
            if (line.Amount <= 0)
                throw new AccountingException("مبلغ التخصيص يجب أن يكون أكبر من صفر.");

            var original = await OriginalAmountAsync(companyId, line.Type, line.DocumentId, ct);
            var remaining = await RemainingAsync(companyId, line.Type, line.DocumentId, original, ct);
            if (line.Amount > remaining + 0.01m)
                throw new AccountingException(
                    $"التخصيص على {line.Number} ({line.Amount:N2}) يتجاوز المتبقي {remaining:N2}.");

            if (isReceipt)
            {
                db.PaymentAllocationLines.Add(PaymentAllocationLine.ForReceipt(
                    companyId, receiptId!.Value, line.Type, line.DocumentId, line.Number, line.Amount, date));
            }
            else
            {
                db.PaymentAllocationLines.Add(PaymentAllocationLine.ForPayment(
                    companyId, paymentId!.Value, line.Type, line.DocumentId, line.Number, line.Amount, date));
            }
        }
    }

    public async Task ReverseVoucherAllocationsAsync(
        long companyId, long? receiptVoucherId, long? paymentVoucherId, CancellationToken ct = default)
    {
        IQueryable<PaymentAllocationLine> query = db.PaymentAllocationLines.Where(a => a.CompanyId == companyId);
        if (receiptVoucherId is long rid)
            query = query.Where(a => a.ReceiptVoucherId == rid);
        else if (paymentVoucherId is long pid)
            query = query.Where(a => a.PaymentVoucherId == pid);
        else
            return;

        var rows = await query.ToListAsync(ct);
        db.PaymentAllocationLines.RemoveRange(rows);
    }

    private async Task<decimal> RemainingAsync(
        long companyId, PaymentAllocationDocumentType type, long documentId, decimal original, CancellationToken ct)
    {
        var allocated = await db.PaymentAllocationLines
            .Where(a => a.CompanyId == companyId
                        && a.DocumentType == type
                        && a.DocumentId == documentId)
            .SumAsync(a => (decimal?)a.Amount, ct) ?? 0m;

        // خصم إشعارات الدائن من أصل فاتورة المبيعات
        if (type == PaymentAllocationDocumentType.SalesInvoice)
        {
            var credited = await db.CreditNotes
                .Where(n => n.CompanyId == companyId
                            && n.OriginalInvoiceId == documentId
                            && n.Status == CreditNoteStatus.Posted)
                .SumGrandTotalAsync(ct);
            original = Math.Max(0m, original - credited);
        }

        return Math.Round(Math.Max(0m, original - allocated), 2, MidpointRounding.AwayFromZero);
    }

    private async Task<decimal> OriginalAmountAsync(
        long companyId, PaymentAllocationDocumentType type, long documentId, CancellationToken ct) =>
        type switch
        {
            PaymentAllocationDocumentType.SalesInvoice =>
                (await db.SalesInvoices.SingleAsync(i => i.CompanyId == companyId && i.Id == documentId, ct)).GrandTotal(),
            PaymentAllocationDocumentType.PurchaseInvoice =>
                (await db.PurchaseInvoices.SingleAsync(i => i.CompanyId == companyId && i.Id == documentId, ct)).GrandTotal(),
            PaymentAllocationDocumentType.ProgressBilling =>
                (await db.ProgressBillings.SingleAsync(b => b.CompanyId == companyId && b.Id == documentId, ct)).GrandTotal,
            _ => throw new AccountingException("نوع مستند التخصيص غير مدعوم."),
        };
}
