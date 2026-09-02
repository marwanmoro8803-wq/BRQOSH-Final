using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.RealEstate;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.RealEstate;

/// <summary>
/// (CEEF-230 · ط3-١) <b>تأمينُ المستأجر — أمانةٌ لا إيراد.</b>
///
/// <para><b>وهذه الخدمةُ وُجدت لأنّ الادّعاء كان نثراً</b>: كُتب في العقد أنّ التأمين
/// «أمانةٌ لا إيراد»، <b>ولا شيءَ في النظام يجعله كذلك</b> — رقمٌ في عمودٍ لا يبلغ
/// الأستاذَ أبداً. فالمنشأةُ تقبض مئةَ ألفٍ تأميناتٍ وتُريها الدفاترُ نقداً بلا التزامٍ
/// يقابله، <b>فتبدو أغنى بمالٍ تردُّه</b>. وقيدُها إيراداً أسوأ: يُضخّم الربحَ وتُدفع عنه
/// زكاةٌ وضريبةٌ على مالٍ ليس للمنشأة.</para>
///
/// <para><b>والخصمُ عند الردّ وحده يصير إيراداً</b>: حين يُحتجَز جزءٌ لقاء تلفٍ أو
/// متأخّرات <b>يسقط الالتزامُ بردّه</b>، فيتحوّل من أمانةٍ إلى دخل. والباقي يخرج نقداً.</para>
/// </summary>
public class SecurityDepositService(BrqoshDbContext db)
{
    /// <summary>
    /// يقبض التأمينَ: النقدُ يدخل، <b>والالتزامُ يُسجَّل معه</b>.
    ///
    /// <para><b>ولا يُقبض إلا مرّةً</b> — والحارسُ في العقد لا هنا، فالقيدُ يتبع الحالة.</para>
    /// </summary>
    public async Task<long> ReceiveAsync(long companyId, long leaseId, DateOnly on,
        string username, string cashAccountCode = SaudiChartOfAccounts.SystemCodes.CashAtBanks,
        CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPostAccounting();
        username = SecurityContext.ResolveUsername(username);

        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var lease = await LeaseAsync(companyId, leaseId, token);
            lease.RecordDepositReceived(on);

            var cash = await SystemAccountProvisioning.EnsureAsync(db, companyId, cashAccountCode, token);
            var held = await SystemAccountProvisioning.EnsureAsync(
                db, companyId, SaudiChartOfAccounts.SystemCodes.TenantSecurityDeposits, token);

            await FinancialPostingSupport.ValidateAsync(
                db, companyId, on, PostingDocumentKind.JournalEntry,
                accountIds: [cash.Id, held.Id], ct: token);

            var entry = JournalEntry.CreateDraft(companyId, on,
                $"تأمين عقد الإيجار {lease.ContractNumber}",
                sourceDocument: $"LeaseDeposit:{lease.ContractNumber}");

            entry.AddLine(cash, lease.SecurityDeposit, 0m, "قبض تأمين مستأجر");
            entry.AddLine(held, 0m, lease.SecurityDeposit, "التزام بردّ تأمين المستأجر");

            var number = await new JournalEntryNumberGenerator(db).NextAsync(companyId, on);
            entry.Post(number);
            db.JournalEntries.Add(entry);
            await db.SaveChangesAsync(token);

            await FinancialPostingSupport.AuditPostAsync(
                db, companyId, "LeaseDepositReceived", lease.ContractNumber,
                reason: "قبض تأمين مستأجر",
                details: $"العقد {lease.ContractNumber} · المبلغ {lease.SecurityDeposit:N2}",
                ct: token);

            return entry.Id;
        }, ct);
    }

    /// <summary>
    /// يردّ التأمينَ ويُغلق الالتزام.
    ///
    /// <para><b>والالتزامُ يُغلق كاملاً مهما خُصم</b>: مدينٌ بالتأمين كلِّه، ودائنٌ نقداً
    /// بما خرج وإيراداً بما احتُجز. <b>وإغلاقُه بالمدفوع وحده يترك بقيّةً معلّقةً إلى
    /// الأبد</b> — التزامٌ لمستأجرٍ غادر ولا أحدَ يطالب به.</para>
    /// </summary>
    public async Task<long> ReturnAsync(long companyId, long leaseId, DateOnly on,
        decimal forfeited, string username,
        string cashAccountCode = SaudiChartOfAccounts.SystemCodes.CashAtBanks,
        CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPostAccounting();
        username = SecurityContext.ResolveUsername(username);

        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var lease = await LeaseAsync(companyId, leaseId, token);
            lease.RecordDepositReturned(on, forfeited);

            var refunded = lease.SecurityDeposit - lease.DepositForfeited;

            var cash = await SystemAccountProvisioning.EnsureAsync(db, companyId, cashAccountCode, token);
            var held = await SystemAccountProvisioning.EnsureAsync(
                db, companyId, SaudiChartOfAccounts.SystemCodes.TenantSecurityDeposits, token);
            var income = lease.DepositForfeited > 0m
                ? await SystemAccountProvisioning.EnsureAsync(
                    db, companyId, SaudiChartOfAccounts.SystemCodes.OtherIncome, token)
                : null;

            List<long> accountIds = [cash.Id, held.Id];
            if (income is not null) accountIds.Add(income.Id);

            await FinancialPostingSupport.ValidateAsync(
                db, companyId, on, PostingDocumentKind.JournalEntry,
                accountIds: accountIds, ct: token);

            var entry = JournalEntry.CreateDraft(companyId, on,
                $"ردّ تأمين عقد الإيجار {lease.ContractNumber}",
                sourceDocument: $"LeaseDepositReturn:{lease.ContractNumber}");

            entry.AddLine(held, lease.SecurityDeposit, 0m, "إغلاق التزام تأمين المستأجر");

            // ‏**وردٌّ خُصم كلُّه لا يُخرج نقداً** — فسطرٌ بصفرٍ يُثقل القيدَ بلا معنى.
            if (refunded > 0m)
                entry.AddLine(cash, 0m, refunded, "ردّ تأمين للمستأجر");

            if (income is not null)
                entry.AddLine(income, 0m, lease.DepositForfeited, "مخصوم من تأمين المستأجر");

            var number = await new JournalEntryNumberGenerator(db).NextAsync(companyId, on);
            entry.Post(number);
            db.JournalEntries.Add(entry);
            await db.SaveChangesAsync(token);

            await FinancialPostingSupport.AuditPostAsync(
                db, companyId, "LeaseDepositReturned", lease.ContractNumber,
                reason: "ردّ تأمين مستأجر",
                details: $"المردود {refunded:N2} · المخصوم {lease.DepositForfeited:N2}",
                ct: token);

            return entry.Id;
        }, ct);
    }

    private async Task<LeaseContract> LeaseAsync(long companyId, long leaseId, CancellationToken ct) =>
        await db.LeaseContracts.SingleOrDefaultAsync(
            l => l.Id == leaseId && l.CompanyId == companyId, ct)
        ?? throw new AccountingException("العقد غير موجود.");
}
