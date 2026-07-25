using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Construction;

public enum RetentionReleaseType
{
    CustomerRelease = 0,
    SubcontractRelease = 1,
}

/// <summary>
/// إفراج محتجز — قيد محاسبي: تحويل من حساب المحتجز إلى ذمم مدينة/دائنة.
/// </summary>
public class RetentionReleaseService(BrqoshDbContext db)
{
    public async Task<JournalEntry> ReleaseAsync(
        long companyId, long projectId, RetentionReleaseType type,
        decimal amount, DateOnly releaseDate, string description, string username,
        CancellationToken ct = default) =>
        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            SecurityContext.EnsureCanPostAccounting();
            if (amount <= 0)
                throw new AccountingException("مبلغ الإفراج يجب أن يكون أكبر من صفر.");

            var available = await new RetentionSummaryQuery(db).AvailableAsync(companyId, projectId, type, token);
            if (amount > available)
                throw new AccountingException(
                    $"مبلغ الإفراج ({amount:N2}) يتجاوز المحتجز المتاح ({available:N2}).");

            var project = await db.ConstructionProjects.SingleAsync(
                p => p.Id == projectId && p.CompanyId == companyId, token);

            await new JournalPostingService(db).EnsurePeriodOpenAsync(companyId, releaseDate);

            var entry = JournalEntry.CreateDraft(
                companyId, releaseDate,
                $"إفراج محتجز — {project.NameAr}: {description}",
                $"RetentionRelease:{projectId}:{(int)type}");

            if (type == RetentionReleaseType.CustomerRelease)
            {
                var retention = await AccountAsync(companyId, SaudiChartOfAccounts.SystemCodes.RetentionReceivable, token);
                var receivable = await AccountAsync(companyId, SaudiChartOfAccounts.SystemCodes.AccountsReceivable, token);
                entry.AddLine(receivable, amount, 0m, "تحويل محتجز إلى ذمم مدينة");
                entry.AddLine(retention, 0m, amount, "إفراج محتجز عميل");
            }
            else
            {
                var retentionPayable = await AccountAsync(companyId, SaudiChartOfAccounts.SystemCodes.RetentionPayableSubcontractors, token);
                var payable = await AccountAsync(companyId, SaudiChartOfAccounts.SystemCodes.AccountsPayable, token);
                entry.AddLine(retentionPayable, amount, 0m, "إفراج محتجز مقاول باطن");
                entry.AddLine(payable, 0m, amount, "تحويل إلى ذمم دائنة");
            }

            var entryNumber = await new JournalEntryNumberGenerator(db).NextAsync(companyId, releaseDate);
            entry.Post(entryNumber);
            db.JournalEntries.Add(entry);
            await db.SaveChangesAsync(token);

            await new AuditService(db).LogAsync(companyId, username, "إفراج محتجز",
                "ConstructionProject", project.Code, $"{type}: {amount:N2}");

            return entry;
        }, ct);

    private async Task<Account> AccountAsync(long companyId, string code, CancellationToken ct) =>
        await db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == code, ct);
}
