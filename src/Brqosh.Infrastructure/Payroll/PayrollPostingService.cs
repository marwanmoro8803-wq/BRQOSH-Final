using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Payroll;
using Brqosh.Domain.Platform;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Companies;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Platform;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Payroll;

public class PayrollPostingService(BrqoshDbContext db)
{
    public async Task<PayrollRun> PostAsync(PayrollRun run, string username, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPostAccounting();
        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            await BranchAssignmentHelper.EnsureAssignedAsync(run, db, ct: token);

            var salaries = await AccountAsync(run.CompanyId, SaudiChartOfAccounts.SystemCodes.SalariesExpense, token);
            var gosiExp = await AccountAsync(run.CompanyId, "5203", token);
            var salariesPayable = await AccountAsync(run.CompanyId, "2108", token);
            var gosiPayable = await AccountAsync(run.CompanyId, "2109", token);

            await FinancialPostingSupport.ValidateAsync(
                db, run.CompanyId, run.PayDate, PostingDocumentKind.Payroll,
                accountIds: [salaries.Id, gosiExp.Id, salariesPayable.Id, gosiPayable.Id], ct: token);

            var entry = run.Post(salaries, gosiExp, salariesPayable, gosiPayable);
            var entryNumber = await new JournalEntryNumberGenerator(db).NextAsync(run.CompanyId, run.PayDate);
            entry.Post(entryNumber);

            db.JournalEntries.Add(entry);
            db.PayrollRuns.Add(run);
            await db.SaveChangesAsync(token);

            run.LinkJournal(entry.Id);
            await db.SaveChangesAsync(token);

            await FinancialPostingSupport.AuditPostAsync(
                db, run.CompanyId, "PayrollRun", run.PeriodCode,
                reason: "ترحيل رواتب", details: $"صافي: {run.TotalNet:N2}", ct: token);

            await DomainEventHub.PublishAsync(new DocumentPostedEvent(
                run.CompanyId, "PayrollRun", run.Id, run.PeriodCode), token);
            await DomainEventHub.PublishAsync(new JournalEntryPostedEvent(
                run.CompanyId, entry.Id, entry.EntryNumber!, run.PayDate), token);

            return run;
        }, ct);
    }

    private async Task<Account> AccountAsync(long companyId, string code, CancellationToken ct) =>
        await db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == code, ct);
}
