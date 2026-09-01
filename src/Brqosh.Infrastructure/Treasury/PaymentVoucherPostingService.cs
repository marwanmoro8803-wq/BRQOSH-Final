using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Platform;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Companies;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Platform;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Workflow;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Treasury;

/// <summary>
/// ترحيل سند الصرف مع تخصيص اختياري على فواتير مشتريات.
/// </summary>
public class PaymentVoucherPostingService(BrqoshDbContext db)
{
    public Task<PaymentVoucher> PostAsync(PaymentVoucher voucher) =>
        PostAsync(voucher, Array.Empty<(PaymentAllocationDocumentType, long, string, decimal)>());

    public async Task<PaymentVoucher> PostAsync(
        PaymentVoucher voucher,
        IReadOnlyList<(PaymentAllocationDocumentType Type, long DocumentId, string Number, decimal Amount)> allocations)
    {
        SecurityContext.EnsureCanPostAccounting();
        return await UnitOfWork.ExecuteInTransactionAsync(db, async ct =>
        {
            if (voucher.BranchId is null)
                voucher.AssignBranch(await new BranchAssignmentService(db)
                .ResolveBranchIdAsync(voucher.CompanyId, BranchSessionContext.PreferredBranchId, ct));

            await FinancialPostingSupport.ValidateAsync(
                db, voucher.CompanyId, voucher.VoucherDate, PostingDocumentKind.PaymentVoucher, ct: ct);

            await new PaymentVoucherWorkflowService(db, new WorkflowEngine(db))
                .EnsureWorkflowApprovedAsync(voucher, ct);

            if (allocations.Count > 0)
            {
                var sum = allocations.Sum(a => a.Amount);
                if (Math.Abs(sum - voucher.Amount) > 0.01m)
                    throw new AccountingException(
                        $"مجموع التخصيصات {sum:N2} يجب أن يساوي مبلغ السند {voucher.Amount:N2}.");
            }

            var payable = await db.Accounts.SingleAsync(a =>
                a.CompanyId == voucher.CompanyId && a.Code == SaudiChartOfAccounts.SystemCodes.AccountsPayable, ct);
            var cashOrBank = await ResolveCashOrBankAccountAsync(voucher.CompanyId, voucher.Method, voucher.BankAccountId);

            var voucherNumber = await DocumentNumberGenerator.NextAsync(db, 
                db.PaymentVouchers.Where(v => v.CompanyId == voucher.CompanyId).Select(v => v.VoucherNumber),
                "PV", voucher.VoucherDate.Year, voucher.CompanyId);

            var entry = voucher.Post(voucherNumber, payable, cashOrBank);
            var entryNumber = await new JournalEntryNumberGenerator(db).NextAsync(voucher.CompanyId, voucher.VoucherDate);

            // (CEEF-230 · ط1-٧) ‏مركزُ تكلفة المستند يُختم على سطور المصروف — **وقبل
            // ‏الترحيل**، فالقيدُ المرحَّل لا يُعدَّل. ويُتحقَّق من المركز أوّلاً: مرجعٌ
            // ‏لمركزٍ محذوفٍ يُنتج تقريراً يعرض «غير مُصنَّف» ويُقرأ نقصَ تصنيف.
            await FinancialPostingSupport.EnsureCostCenterAsync(db, voucher.CompanyId, voucher.CostCenterId, ct);
            entry.StampCostCenter(voucher.CostCenterId);
            entry.Post(entryNumber);

            db.JournalEntries.Add(entry);
            if (voucher.Id == 0)
                // (CEEF-230 · ط2-٠) ‏يُضاف إن لم يكن محفوظاً — واصطلاحُ `JournalPostingService` نفسُه.
                // ‏وكان الإضافةُ بلا شرطٍ في تسع خدمات، **وأسقطت شاشةَ الرواتب بـ`23505`** حين
                // ‏حفظت مسودّتَها ثمّ رحّلتها. والشرطُ يجعل المسارين مقبولين معاً، **فيُغلق
                // ‏صنفُ العطل لا حالتُه**: شاشةٌ تُكتب غداً بالاصطلاح الآخر لا تسقط.
                if (voucher.Id == 0)
                    db.PaymentVouchers.Add(voucher);
            await db.SaveChangesAsync(ct);

            voucher.LinkJournalEntry(entry.Id);
            await db.SaveChangesAsync(ct);

            if (allocations.Count > 0)
            {
                await new PaymentAllocationService(db).ApplyPaymentAllocationsAsync(
                    voucher.CompanyId, voucher.Id, voucher.VoucherDate, allocations, ct);
                await db.SaveChangesAsync(ct);
            }

            await DomainEventHub.PublishAsync(new DocumentPostedEvent(
                voucher.CompanyId, "PaymentVoucher", voucher.Id, voucher.VoucherNumber!), ct);
            await DomainEventHub.PublishAsync(new JournalEntryPostedEvent(
                voucher.CompanyId, entry.Id, entry.EntryNumber!, voucher.VoucherDate), ct);

            await FinancialPostingSupport.AuditPostAsync(
                db, voucher.CompanyId, "PaymentVoucher", voucher.VoucherNumber!,
                reason: "ترحيل سند صرف", ct: ct);

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
