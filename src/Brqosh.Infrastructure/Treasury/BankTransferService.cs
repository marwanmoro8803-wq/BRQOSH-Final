using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Platform;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Companies;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Platform;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Treasury;

/// <summary>تحويل بين حسابين بنكيين — LIFECYCLE-65–66.</summary>
public class BankTransferService(BrqoshDbContext db)
{
    private const string SourcePrefix = "BankTransfer:";

    public async Task<JournalEntry> TransferAsync(
        long companyId, long fromBankAccountId, long toBankAccountId,
        decimal amount, DateOnly transferDate, string? reference = null,
        CancellationToken ct = default)
    {
        if (amount <= 0)
            throw new AccountingException("مبلغ التحويل يجب أن يكون موجباً.");
        if (fromBankAccountId == toBankAccountId)
            throw new AccountingException("لا يمكن التحويل لنفس الحساب.");

        SecurityContext.EnsureCanPostAccounting();
        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var from = await db.BankAccounts.SingleAsync(
                b => b.CompanyId == companyId && b.Id == fromBankAccountId && b.IsActive, token);
            var to = await db.BankAccounts.SingleAsync(
                b => b.CompanyId == companyId && b.Id == toBankAccountId && b.IsActive, token);

            var fromGl = await db.Accounts.SingleAsync(a => a.Id == from.GlAccountId, token);
            var toGl = await db.Accounts.SingleAsync(a => a.Id == to.GlAccountId, token);

            await FinancialPostingSupport.ValidateAsync(
                db, companyId, transferDate, PostingDocumentKind.Other,
                accountIds: [fromGl.Id, toGl.Id], ct: token);

            var description = $"تحويل بنكي {from.Code} → {to.Code}";
            var entry = JournalEntry.CreateDraft(companyId, transferDate, description,
                $"{SourcePrefix}{reference ?? Guid.NewGuid().ToString("N")}");
            entry.AssignBranch(await new BranchAssignmentService(db)
                .ResolveBranchIdAsync(companyId, BranchSessionContext.PreferredBranchId, token));
            entry.AddLine(toGl, amount, 0m, description);
            entry.AddLine(fromGl, 0m, amount, description);

            var posted = await new JournalPostingService(db).PostAsync(entry);

            await FinancialPostingSupport.AuditPostAsync(
                db, companyId, "BankTransfer", posted.EntryNumber!,
                reason: "تحويل بين حسابات بنكية", ct: token);

            await DomainEventHub.PublishAsync(new DocumentPostedEvent(
                companyId, "BankTransfer", fromBankAccountId, posted.EntryNumber!), token);

            return posted;
        }, ct);
    }

    /// <summary>
    /// <b>عكسُ تحويلٍ بنكيّ</b> — وكان لا يُعكس أبداً: العكسُ العامُّ
    /// يرفض قيداً له مستند، ولا خدمةَ تعكسه. <b>والقائمُ وحدَه يُعكس</b> (<c>WhereStanding</c>: لا المعكوسُ ولا قيدُ العكس نفسُه)،
    /// <b>والمُسوّى مع البنك يُرفض حتى تُلغى تسويتُه، وسطرُ الكشف المطابَقُ له يُحرَّر</b> — وهما في <c>JournalReversalService</c> لكلّ
    /// عكسٍ منذ ح-٩٠، لا هنا وحدَه.
    /// </summary>
    public async Task<JournalEntry> ReverseAsync(
        long companyId, long journalEntryId, DateOnly reversalDate, string username, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanCancelOrReverse();
        username = SecurityContext.ResolveUsername(username);
        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var isStandingTransfer = await db.JournalEntries
                .Where(e => e.CompanyId == companyId && e.Id == journalEntryId
                            && e.SourceDocument != null && e.SourceDocument.StartsWith(SourcePrefix))
                .WhereStanding(db.JournalEntries)
                .AnyAsync(token);
            if (!isStandingTransfer)
                throw new AccountingException("لا تحويلَ بنكيّاً قائماً بهذا القيد يُعكس.");

            return await new JournalReversalService(db).ReverseAsync(
                companyId, journalEntryId, reversalDate, username, fromSourceDocument: true, token);
        }, ct);
    }
}
