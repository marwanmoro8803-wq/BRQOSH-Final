using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Platform;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Companies;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Platform;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Treasury;

/// <summary>
/// ترحيل سند القبض مع تخصيص اختياري على فواتير/مستخلصات.
/// </summary>
public class ReceiptVoucherPostingService(BrqoshDbContext db)
{
    public Task<ReceiptVoucher> PostAsync(ReceiptVoucher voucher) =>
        PostAsync(voucher, Array.Empty<(PaymentAllocationDocumentType, long, string, decimal)>());

    public async Task<ReceiptVoucher> PostAsync(
        ReceiptVoucher voucher,
        IReadOnlyList<(PaymentAllocationDocumentType Type, long DocumentId, string Number, decimal Amount)> allocations)
    {
        SecurityContext.EnsureCanPostAccounting();
        return await UnitOfWork.ExecuteInTransactionAsync(db, async ct =>
        {
            if (voucher.BranchId is null)
                voucher.AssignBranch(await new BranchAssignmentService(db)
                .ResolveBranchIdAsync(voucher.CompanyId, BranchSessionContext.PreferredBranchId, ct));

            await FinancialPostingSupport.ValidateAsync(
                db, voucher.CompanyId, voucher.VoucherDate, PostingDocumentKind.ReceiptVoucher, ct: ct);

            if (allocations.Count > 0)
            {
                var sum = allocations.Sum(a => a.Amount);
                if (Math.Abs(sum - voucher.Amount) > 0.01m)
                    throw new AccountingException(
                        $"مجموع التخصيصات {sum:N2} يجب أن يساوي مبلغ السند {voucher.Amount:N2}.");
            }

            var cashOrBank = await ResolveCashOrBankAccountAsync(voucher.CompanyId, voucher.Method, voucher.BankAccountId);
            var receivable = await db.Accounts.SingleAsync(a =>
                a.CompanyId == voucher.CompanyId && a.Code == SaudiChartOfAccounts.SystemCodes.AccountsReceivable, ct);

            var voucherNumber = await DocumentNumberGenerator.NextAsync(
                db.ReceiptVouchers.Where(v => v.CompanyId == voucher.CompanyId).Select(v => v.VoucherNumber),
                "RV", voucher.VoucherDate.Year);

            var entry = voucher.Post(voucherNumber, cashOrBank, receivable);
            var entryNumber = await new JournalEntryNumberGenerator(db).NextAsync(voucher.CompanyId, voucher.VoucherDate);
            entry.Post(entryNumber);

            db.JournalEntries.Add(entry);
            db.ReceiptVouchers.Add(voucher);
            await db.SaveChangesAsync(ct);

            voucher.LinkJournalEntry(entry.Id);
            await db.SaveChangesAsync(ct);

            if (allocations.Count > 0)
            {
                await new PaymentAllocationService(db).ApplyReceiptAllocationsAsync(
                    voucher.CompanyId, voucher.Id, voucher.VoucherDate, allocations, ct);
                await db.SaveChangesAsync(ct);
            }

            await DomainEventHub.PublishAsync(new DocumentPostedEvent(
                voucher.CompanyId, "ReceiptVoucher", voucher.Id, voucher.VoucherNumber!), ct);
            await DomainEventHub.PublishAsync(new JournalEntryPostedEvent(
                voucher.CompanyId, entry.Id, entry.EntryNumber!, voucher.VoucherDate), ct);

            await FinancialPostingSupport.AuditPostAsync(
                db, voucher.CompanyId, "ReceiptVoucher", voucher.VoucherNumber!,
                reason: "ترحيل سند قبض", ct: ct);

            return voucher;
        });
    }

    private async Task<Account> ResolveCashOrBankAccountAsync(long companyId, ReceiptMethod method, long? bankAccountId)
    {
        // (CEEF-229 · م2-١٥) الحلّ من مكانٍ واحد — وكان السقوط هنا يُقيّد على الأب 1102
        // بينما البنوك تُقيَّد على أبنائه، فينشقّ نقد البنوك. انظر BankGlAccountResolver.
        if (method == ReceiptMethod.Cash)
            return await db.Accounts.SingleAsync(
                a => a.CompanyId == companyId && a.Code == SaudiChartOfAccounts.SystemCodes.CashOnHand);

        return await BankGlAccountResolver.ResolveAsync(db, companyId, bankAccountId);
    }
}
