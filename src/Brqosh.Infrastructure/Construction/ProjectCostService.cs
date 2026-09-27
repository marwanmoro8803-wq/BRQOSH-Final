using Brqosh.Domain.Accounting;
using Brqosh.Domain.Construction;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Construction;

/// <summary>تسجيل تكلفة مشروع مع قيد اختياري (مدين 5105 / دائن صندوق) وتتبع IFRS 15.</summary>
public class ProjectCostService(BrqoshDbContext db)
{
    public async Task<ProjectCostEntry> RecordAsync(
        long companyId, long projectId, DateOnly costDate, string description, decimal amount,
        string username, bool postJournal = true, string sourceDocument = "",
        CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPostAccounting();
        username = SecurityContext.ResolveUsername(username);

        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            _ = await db.ConstructionProjects.SingleAsync(
                p => p.Id == projectId && p.CompanyId == companyId, token);

            var entry = ProjectCostEntry.Create(companyId, projectId, costDate, description, amount, sourceDocument);
            db.ProjectCostEntries.Add(entry);
            await db.SaveChangesAsync(token);

            if (postJournal)
            {
                await new JournalPostingService(db).EnsurePeriodOpenAsync(companyId, costDate);
                var expense = await db.Accounts.SingleAsync(
                    a => a.CompanyId == companyId && a.Code == "5105", token);
                var cash = await db.Accounts.SingleAsync(
                    a => a.CompanyId == companyId && a.Code == SaudiChartOfAccounts.SystemCodes.CashOnHand, token);

                var je = JournalEntry.CreateDraft(companyId, costDate,
                    $"تكلفة مشروع — {description}",
                    string.IsNullOrWhiteSpace(sourceDocument) ? $"ProjectCost:{entry.Id}" : sourceDocument);
                je.AddLine(expense, amount, 0m, description);
                je.AddLine(cash, 0m, amount, "صرف تكلفة مشروع");
                var num = await new JournalEntryNumberGenerator(db).NextAsync(companyId, costDate, token);
                je.Post(num);
                db.JournalEntries.Add(je);
                await db.SaveChangesAsync(token);
                entry.LinkJournalEntry(je.Id);
                await db.SaveChangesAsync(token);
            }

            // ‏(جاهزيّةُ الإطلاق · ح-٨٠) ‏**والتكلفةُ تُغيّر نسبةَ الإنجاز فيُسوّى إيرادُ العقد** — قُيّدت هنا أو جاءت من مستندٍ آخر.
            await new ContractRevenueTrueUp(db).ApplyAsync(companyId, projectId, costDate, token);

            await new AuditService(db).LogAsync(companyId, username, "تسجيل تكلفة مشروع",
                "ConstructionProject", projectId.ToString(), $"{amount:N2} — {description}");

            return entry;
        }, ct);
    }

    public async Task<decimal> IncurredCostAsync(long companyId, long projectId, CancellationToken ct = default) =>
        await db.ProjectCostEntries
            .Where(c => c.CompanyId == companyId && c.ProjectId == projectId && !c.IsReversed)
            .SumAsync(c => (decimal?)c.Amount, ct) ?? 0m;
}
