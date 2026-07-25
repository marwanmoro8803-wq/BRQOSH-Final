using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Treasury;

/// <summary>عكس سند قبض أو صرف مرحّل — عكس القيد المرتبط.</summary>
public class VoucherReversalService(BrqoshDbContext db)
{
    public async Task ReversePaymentAsync(long companyId, long voucherId, DateOnly reversalDate,
        string username, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanCancelOrReverse();
        username = SecurityContext.ResolveUsername(username);

        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var voucher = await db.PaymentVouchers
                .SingleAsync(v => v.CompanyId == companyId && v.Id == voucherId, token);

            if (voucher.Status != PaymentVoucherStatus.Posted)
                throw new Domain.Accounting.AccountingException("لا يُعكس إلا سند صرف مرحّل.");

            if (voucher.JournalEntryId is not long entryId)
                throw new Domain.Accounting.AccountingException("السند بلا قيد محاسبي مرتبط.");

            await new JournalReversalService(db).ReverseAsync(
                companyId, entryId, reversalDate, username, fromSourceDocument: true, token);

            voucher.MarkReversed();
            await new PaymentAllocationService(db).ReverseVoucherAllocationsAsync(
                companyId, receiptVoucherId: null, paymentVoucherId: voucher.Id, token);
            await db.SaveChangesAsync(token);

            await new AuditService(db).LogAsync(companyId, username, "عكس سند صرف",
                "PaymentVoucher", voucher.VoucherNumber ?? voucher.Id.ToString(), "");
        }, ct);
    }

    public async Task ReverseReceiptAsync(long companyId, long voucherId, DateOnly reversalDate,
        string username, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanCancelOrReverse();
        username = SecurityContext.ResolveUsername(username);

        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var voucher = await db.ReceiptVouchers
                .SingleAsync(v => v.CompanyId == companyId && v.Id == voucherId, token);

            if (voucher.Status != ReceiptVoucherStatus.Posted)
                throw new Domain.Accounting.AccountingException("لا يُعكس إلا سند قبض مرحّل.");

            if (voucher.JournalEntryId is not long entryId)
                throw new Domain.Accounting.AccountingException("السند بلا قيد محاسبي مرتبط.");

            await new JournalReversalService(db).ReverseAsync(
                companyId, entryId, reversalDate, username, fromSourceDocument: true, token);

            voucher.MarkReversed();
            await new PaymentAllocationService(db).ReverseVoucherAllocationsAsync(
                companyId, receiptVoucherId: voucher.Id, paymentVoucherId: null, token);
            await db.SaveChangesAsync(token);

            await new AuditService(db).LogAsync(companyId, username, "عكس سند قبض",
                "ReceiptVoucher", voucher.VoucherNumber ?? voucher.Id.ToString(), "");
        }, ct);
    }
}
