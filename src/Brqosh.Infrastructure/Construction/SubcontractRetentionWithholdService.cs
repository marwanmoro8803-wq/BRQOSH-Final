using Brqosh.Domain.Accounting;
using Brqosh.Domain.Construction;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Construction;

/// <summary>تسجيل محتجز مستحق لمقاول باطن — دائن 2110 / مدين ذمم دائنة.</summary>
public class SubcontractRetentionWithholdService(BrqoshDbContext db)
{
    public async Task<JournalEntry> WithholdAsync(
        long companyId, long agreementId, decimal amount, DateOnly withholdDate,
        string description, string username, CancellationToken ct = default) =>
        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            SecurityContext.EnsureCanPostAccounting();
            if (amount <= 0)
                throw new AccountingException("مبلغ المحتجز يجب أن يكون أكبر من صفر.");

            var agreement = await db.SubcontractAgreements.SingleAsync(
                a => a.CompanyId == companyId && a.Id == agreementId, token);

            if (agreement.Status != SubcontractAgreementStatus.Active)
                throw new AccountingException("لا يُسجَّل محتجز إلا لعقد باطن نشط.");

            await new JournalPostingService(db).EnsurePeriodOpenAsync(companyId, withholdDate);

            var payable = await db.Accounts.SingleAsync(
                a => a.CompanyId == companyId && a.Code == SaudiChartOfAccounts.SystemCodes.AccountsPayable, token);
            var retention = await db.Accounts.SingleAsync(
                a => a.CompanyId == companyId && a.Code == SaudiChartOfAccounts.SystemCodes.RetentionPayableSubcontractors, token);

            var entry = JournalEntry.CreateDraft(
                companyId, withholdDate,
                $"محتجز باطن — {agreement.AgreementNumber}: {description}",
                $"SubcontractRetentionHold:{agreement.ProjectId}:{agreementId}");

            entry.AddLine(payable, amount, 0m, "تخفيض ذمة المقاول");
            entry.AddLine(retention, 0m, amount, "محتجز مستحق لمقاول الباطن");

            var entryNumber = await new JournalEntryNumberGenerator(db).NextAsync(companyId, withholdDate);
            entry.Post(entryNumber);
            db.JournalEntries.Add(entry);
            await db.SaveChangesAsync(token);

            await new AuditService(db).LogAsync(companyId, username, "تسجيل محتجز باطن",
                "SubcontractAgreement", agreement.AgreementNumber, $"{amount:N2}");

            return entry;
        }, ct);
}
