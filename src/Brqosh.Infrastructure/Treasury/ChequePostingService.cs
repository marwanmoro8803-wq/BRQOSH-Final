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

public class ChequePostingService(BrqoshDbContext db)
{
    public async Task<Cheque> RegisterReceivedAsync(Cheque cheque)
    {
        SecurityContext.EnsureCanPostAccounting();
        return await UnitOfWork.ExecuteInTransactionAsync(db, async ct =>
        {
            var posting = new JournalPostingService(db);
            await posting.EnsurePeriodOpenAsync(cheque.CompanyId, cheque.ChequeDate);
            await BranchAssignmentHelper.EnsureAssignedAsync(cheque, db, ct: ct);

            var chequesUnderCollection = await SystemAccountAsync(cheque.CompanyId, SaudiChartOfAccounts.SystemCodes.ChequesUnderCollection);
            var receivable = await SystemAccountAsync(cheque.CompanyId, SaudiChartOfAccounts.SystemCodes.AccountsReceivable);

            var entry = cheque.PostToPortfolio("استلام شيك وارد", chequesUnderCollection, receivable);
            db.Cheques.Add(cheque);
            await PostAndLinkAsync(cheque, entry, linkPortfolio: true, ct);
            return cheque;
        });
    }

    public async Task<Cheque> RegisterIssuedAsync(Cheque cheque)
    {
        SecurityContext.EnsureCanPostAccounting();
        return await UnitOfWork.ExecuteInTransactionAsync(db, async ct =>
        {
            var posting = new JournalPostingService(db);
            await posting.EnsurePeriodOpenAsync(cheque.CompanyId, cheque.ChequeDate);
            await BranchAssignmentHelper.EnsureAssignedAsync(cheque, db, ct: ct);

            var payable = await SystemAccountAsync(cheque.CompanyId, SaudiChartOfAccounts.SystemCodes.AccountsPayable);
            var chequesPayable = await SystemAccountAsync(cheque.CompanyId, SaudiChartOfAccounts.SystemCodes.ChequesPayable);

            var entry = cheque.PostToPortfolio("إصدار شيك صادر", payable, chequesPayable);
            db.Cheques.Add(cheque);
            await PostAndLinkAsync(cheque, entry, linkPortfolio: true, ct);
            return cheque;
        });
    }

    public async Task<Cheque> DepositAsync(long companyId, long chequeId, DateOnly depositDate)
    {
        SecurityContext.EnsureCanPostAccounting();
        return await UnitOfWork.ExecuteInTransactionAsync(db, async ct =>
        {
            var cheque = await LoadChequeAsync(companyId, chequeId);
            var posting = new JournalPostingService(db);
            await posting.EnsurePeriodOpenAsync(cheque.CompanyId, depositDate);

            var bank = await LoadBankAsync(cheque.CompanyId, cheque.BankAccountId);
            var bankGl = await db.Accounts.SingleAsync(a => a.Id == bank.GlAccountId, ct);
            var chequesUnderCollection = await SystemAccountAsync(cheque.CompanyId, SaudiChartOfAccounts.SystemCodes.ChequesUnderCollection);

            var entry = cheque.PostDeposit(depositDate, bankGl, chequesUnderCollection);
            await PostAndLinkAsync(cheque, entry, linkPortfolio: false, ct);
            return cheque;
        });
    }

    public async Task<Cheque> ClearAsync(long companyId, long chequeId, DateOnly clearDate)
    {
        SecurityContext.EnsureCanPostAccounting();
        return await UnitOfWork.ExecuteInTransactionAsync(db, async ct =>
        {
            var cheque = await LoadChequeAsync(companyId, chequeId);
            var posting = new JournalPostingService(db);
            await posting.EnsurePeriodOpenAsync(cheque.CompanyId, clearDate);

            var bank = await LoadBankAsync(cheque.CompanyId, cheque.BankAccountId);
            var bankGl = await db.Accounts.SingleAsync(a => a.Id == bank.GlAccountId, ct);

            var clearingCode = cheque.Type == ChequeType.Received
                ? SaudiChartOfAccounts.SystemCodes.ChequesUnderCollection
                : SaudiChartOfAccounts.SystemCodes.ChequesPayable;
            var clearing = await SystemAccountAsync(cheque.CompanyId, clearingCode);

            var entry = cheque.PostClear(clearDate, bankGl, clearing);
            if (entry is not null)
                await PostAndLinkAsync(cheque, entry, linkPortfolio: false, ct);
            else
            {
                db.Cheques.Update(cheque);
                await db.SaveChangesAsync(ct);

                await DomainEventHub.PublishAsync(new DocumentPostedEvent(
                    cheque.CompanyId, "Cheque", cheque.Id, cheque.ChequeNumber), ct);
            }

            return cheque;
        });
    }

    public async Task<Cheque> BounceReceivedAsync(long companyId, long chequeId, DateOnly bounceDate)
    {
        SecurityContext.EnsureCanPostAccounting();
        return await UnitOfWork.ExecuteInTransactionAsync(db, async ct =>
        {
            var cheque = await LoadChequeAsync(companyId, chequeId);
            if (cheque.Type != ChequeType.Received)
                throw new AccountingException("الارتجاع للشيكات الواردة فقط.");

            var posting = new JournalPostingService(db);
            await posting.EnsurePeriodOpenAsync(cheque.CompanyId, bounceDate);

            var receivable = await SystemAccountAsync(cheque.CompanyId, SaudiChartOfAccounts.SystemCodes.AccountsReceivable);
            var bank = await LoadBankAsync(cheque.CompanyId, cheque.BankAccountId);
            var bankGl = await db.Accounts.SingleAsync(a => a.Id == bank.GlAccountId, ct);
            var chequesUnderCollection = await SystemAccountAsync(cheque.CompanyId, SaudiChartOfAccounts.SystemCodes.ChequesUnderCollection);

            var creditAccount = cheque.Status is ChequeStatus.Deposited or ChequeStatus.Cleared
                ? bankGl
                : chequesUnderCollection;

            var useSecondary = cheque.SettlementJournalEntryId is not null;
            var entry = cheque.PostBounceOrCancel(bounceDate, receivable, creditAccount, isCancel: false);
            await PostAndLinkAsync(cheque, entry, linkPortfolio: false, ct, useSecondary);
            return cheque;
        });
    }

    public async Task<Cheque> CancelIssuedAsync(long companyId, long chequeId, DateOnly cancelDate)
    {
        SecurityContext.EnsureCanPostAccounting();
        return await UnitOfWork.ExecuteInTransactionAsync(db, async ct =>
        {
            var cheque = await LoadChequeAsync(companyId, chequeId);
            if (cheque.Type != ChequeType.Issued)
                throw new AccountingException("الإلغاء للشيكات الصادرة فقط.");

            var posting = new JournalPostingService(db);
            await posting.EnsurePeriodOpenAsync(cheque.CompanyId, cancelDate);

            var chequesPayable = await SystemAccountAsync(cheque.CompanyId, SaudiChartOfAccounts.SystemCodes.ChequesPayable);
            var payable = await SystemAccountAsync(cheque.CompanyId, SaudiChartOfAccounts.SystemCodes.AccountsPayable);

            var entry = cheque.PostBounceOrCancel(cancelDate, chequesPayable, payable, isCancel: true);
            await PostAndLinkAsync(cheque, entry, linkPortfolio: false, ct);
            return cheque;
        });
    }

    private async Task PostAndLinkAsync(Cheque cheque, JournalEntry entry, bool linkPortfolio, CancellationToken ct, bool useSecondary = false)
    {
        // (جاهزيّةُ الإطلاق · ح-٣٢) ‏**والحارسُ يقيس تاريخَ القيد نفسِه.**
        //
        // ‏هذا الموضعُ لا يمرّ بـ`JournalPostingService.PostAsync` — **يكتب في
        // ‏`JournalEntries` رأساً** فلا يبلغُه `PostingOrchestrator` الذي يفحص
        // ‏بـ`entry.EntryDate`. **والخدمةُ تفحص بتاريخِ المستخدم أعلاه**،
        // ‏فإن افترق عن تاريخ القيد لم يُفحص ما دخل الدفترَ أصلاً.
        //
        // ‏**فيُسأل عن تاريخ القيد عند كتابته** — وهذا حارسٌ يقيس ما يحرس،
        // ‏فلا يعود النفاذُ إن افترق تاريخان ثانيةً. **وهو لا يُغني عن
        // ‏تمرير التاريخ ولا يُغني التمريرُ عنه**: ذاك يُصحّح التاريخ،
        // ‏وهذا يمنع عودةَ الصنف.
        await new JournalPostingService(db)
            .EnsurePeriodOpenAsync(cheque.CompanyId, entry.EntryDate);

        var entryNumber = await new JournalEntryNumberGenerator(db).NextAsync(cheque.CompanyId, entry.EntryDate);
        entry.Post(entryNumber);

        if (!db.Cheques.Local.Contains(cheque))
            db.Cheques.Update(cheque);

        db.JournalEntries.Add(entry);
        await db.SaveChangesAsync(ct);

        if (linkPortfolio)
            cheque.LinkPortfolioJournal(entry.Id);
        else if (useSecondary)
            cheque.LinkSecondarySettlementJournal(entry.Id);
        else
            cheque.LinkSettlementJournal(entry.Id);

        await db.SaveChangesAsync(ct);

        await FinancialPostingSupport.AuditPostAsync(
            db, cheque.CompanyId, "Cheque", cheque.ChequeNumber,
            reason: "ترحيل شيك", ct: ct);

        await DomainEventHub.PublishAsync(new DocumentPostedEvent(
            cheque.CompanyId, "Cheque", cheque.Id, cheque.ChequeNumber), ct);
        await DomainEventHub.PublishAsync(new JournalEntryPostedEvent(
            cheque.CompanyId, entry.Id, entry.EntryNumber!, entry.EntryDate), ct);
    }

    private async Task<Cheque> LoadChequeAsync(long companyId, long chequeId) =>
        await db.Cheques.SingleAsync(c => c.CompanyId == companyId && c.Id == chequeId);

    private async Task<BankAccount> LoadBankAsync(long companyId, long bankAccountId) =>
        await db.BankAccounts.SingleAsync(b => b.CompanyId == companyId && b.Id == bankAccountId && b.IsActive);

    private async Task<Account> SystemAccountAsync(long companyId, string code) =>
        await db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == code);
}
