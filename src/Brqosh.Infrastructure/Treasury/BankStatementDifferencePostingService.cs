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

public sealed record BankDifferencePostingResult(
    long JournalEntryId,
    string EntryNumber,
    int LineIndex);

/// <summary>ترحيل سطر كشف غير مطابق بقيد تسوية — M4.7 / LIFECYCLE-68.</summary>
public sealed class BankStatementDifferencePostingService(BrqoshDbContext db)
{
    private const string DifferenceSource = "BankStatementDifference";

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٨٩ · <c>treasury-payroll-assets#10</c>) <b>عكسُ قيد فرق سطر الكشف</b> — وكان لا يُعكس أبداً، ففرقٌ رُحِّل
    /// على حسابٍ خطأٍ يبقى. <b>يُعكس القيدُ القائمُ وحدَه، وتُلغى تسويتُه، ويُحرَّر السطرُ</b> فيُرحَّل من جديد أو يُطابَق؛
    /// <b>والسطرُ المطابَقُ لغير قيد فروقٍ يُرفض</b> — فقيدُ السند يُصحَّح بعكس السند وتخصيصاته لا من هنا.
    /// </summary>
    public Task ReverseAsync(
        long companyId, long bankStatementId, int lineIndex, DateOnly reversalDate, string username,
        CancellationToken ct = default)
    {
        SecurityContext.EnsureCanCancelOrReverse();
        username = SecurityContext.ResolveUsername(username);
        return UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var statement = await db.BankStatements
                .Include(s => s.Lines)
                .SingleAsync(s => s.CompanyId == companyId && s.Id == bankStatementId, token);
            if (lineIndex < 0 || lineIndex >= statement.Lines.Count)
                throw new AccountingException("سطر الكشف غير موجود.");

            var stmtLine = statement.Lines.ElementAt(lineIndex);
            if (stmtLine.MatchedJournalEntryId is not long entryId
                || !await db.JournalEntries
                    .Where(e => e.CompanyId == companyId && e.Id == entryId && e.SourceDocument == DifferenceSource)
                    .WhereStanding(db.JournalEntries)
                    .AnyAsync(token))
                throw new AccountingException("السطرُ بلا قيد فروقٍ قائمٍ يُعكس — والمطابَقُ لسندٍ يُصحَّح بعكس السند.");

            // ‏(ح-٩٠) تُلغى تسويتُه أوّلاً — فهو مُسوًّى منذ ترحيله، وحارسُ الدفتر يرفض عكسَ المُسوّى.
            await new BankReconciliationQuery(db).UnreconcileAsync(companyId, statement.BankAccountId, entryId);
            await new JournalReversalService(db).ReverseAsync(
                companyId, entryId, reversalDate, username, fromSourceDocument: true, token);
            stmtLine.Unmatch();
            await db.SaveChangesAsync(token);
        }, ct);
    }

    public Task<BankDifferencePostingResult> PostUnmatchedLineAsync(
        long companyId,
        long bankStatementId,
        int lineIndex,
        string? offsetAccountCode = null,
        CancellationToken ct = default) =>
        UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            SecurityContext.EnsureCanPostAccounting();

            var statement = await db.BankStatements
                .Include(s => s.Lines)
                .SingleAsync(s => s.CompanyId == companyId && s.Id == bankStatementId, token);

            if (lineIndex < 0 || lineIndex >= statement.Lines.Count)
                throw new AccountingException("سطر الكشف غير موجود.");

            var stmtLine = statement.Lines.ElementAt(lineIndex);
            if (stmtLine.IsMatched)
                throw new AccountingException("السطر مطابق مسبقاً — لا يُنشأ قيد فروق.");

            var bank = await db.BankAccounts
                .SingleAsync(b => b.CompanyId == companyId && b.Id == statement.BankAccountId, token);
            var bankGl = await db.Accounts.SingleAsync(a => a.Id == bank.GlAccountId, token);

            var code = offsetAccountCode?.Trim() ?? DefaultOffsetCode(stmtLine.SignedAmount);
            var offset = await db.Accounts
                .SingleAsync(a => a.CompanyId == companyId && a.Code == code, token);

            await FinancialPostingSupport.ValidateAsync(
                db, companyId, stmtLine.TransactionDate, PostingDocumentKind.BankStatementDifference,
                accountIds: [bankGl.Id, offset.Id], ct: token);

            var description = $"فروق بنكية — {stmtLine.Description}";
            var entry = JournalEntry.CreateDraft(
                companyId, stmtLine.TransactionDate, description, DifferenceSource);

            if (entry.BranchId is null)
                entry.AssignBranch(await new BranchAssignmentService(db)
                    .ResolveBranchIdAsync(companyId, BranchSessionContext.PreferredBranchId, token));

            var amount = Math.Abs(stmtLine.SignedAmount);
            if (stmtLine.SignedAmount > 0)
            {
                entry.AddLine(bankGl, amount, 0, stmtLine.Description);
                entry.AddLine(offset, 0, amount, stmtLine.Description);
            }
            else
            {
                entry.AddLine(offset, amount, 0, stmtLine.Description);
                entry.AddLine(bankGl, 0, amount, stmtLine.Description);
            }

            var posting = new JournalPostingService(db);
            var entryNumber = await new JournalEntryNumberGenerator(db)
                .NextAsync(companyId, stmtLine.TransactionDate);
            entry.Post(entryNumber);
            db.JournalEntries.Add(entry);
            await db.SaveChangesAsync(token);

            stmtLine.Match(entry.Id);
            await new BankReconciliationQuery(db)
                .ReconcileAsync(companyId, statement.BankAccountId, entry.Id, stmtLine.TransactionDate);

            await DomainEventHub.PublishAsync(new JournalEntryPostedEvent(
                companyId, entry.Id, entry.EntryNumber!, stmtLine.TransactionDate), token);
            await DomainEventHub.PublishAsync(new DocumentPostedEvent(
                companyId, "BankStatementDifference", bankStatementId, $"{bankStatementId}:{lineIndex}"), token);

            await FinancialPostingSupport.AuditPostAsync(
                db, companyId, "BankStatementDifference", entry.EntryNumber!,
                reason: "ترحيل فروق بنكية", ct: token);

            return new BankDifferencePostingResult(entry.Id, entry.EntryNumber!, lineIndex);
        }, ct);

    internal static string DefaultOffsetCode(decimal signedAmount) =>
        signedAmount > 0 ? "4106" : "5213";
}
