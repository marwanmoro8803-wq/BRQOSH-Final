using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Construction;
using Brqosh.Domain.Platform;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Companies;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Platform;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Zatca;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Construction;

public class ProgressBillingPostingService(BrqoshDbContext db)
{
    public async Task<ProgressBilling> PostAsync(ProgressBilling billing, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPost();
        var posted = await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            await FinancialPostingSupport.ValidateAsync(
                db, billing.CompanyId, billing.BillingDate, PostingDocumentKind.ProgressBilling, ct: token);
            await BranchAssignmentHelper.EnsureAssignedAsync(billing, db, ct: token);

            var billingNumber = await DocumentNumberGenerator.NextAsync(db, 
                db.ProgressBillings.Where(b => b.CompanyId == billing.CompanyId).Select(b => b.BillingNumber),
                "PB", billing.BillingDate, billing.CompanyId);

            var receivable = await SystemAccountAsync(billing.CompanyId, SaudiChartOfAccounts.SystemCodes.AccountsReceivable, token);
            var retention = await SystemAccountAsync(billing.CompanyId, SaudiChartOfAccounts.SystemCodes.RetentionReceivable, token);
            var revenue = await SystemAccountAsync(billing.CompanyId, SaudiChartOfAccounts.SystemCodes.ContractRevenue, token);
            var vatOutput = await SystemAccountAsync(billing.CompanyId, SaudiChartOfAccounts.SystemCodes.VatOutput, token);
            var deferred = await SystemAccountAsync(billing.CompanyId, SaudiChartOfAccounts.SystemCodes.AdvancesFromCustomers, token);

            var (recognizedRevenue, _, _) = await ProgressBillingIfrs15.ComputeAsync(db, billing, token);

            var entry = billing.Post(
                billingNumber, receivable, retention, revenue, vatOutput,
                recognizedRevenue, deferred);
            var entryNumber = await new JournalEntryNumberGenerator(db)
                .NextAsync(billing.CompanyId, billing.BillingDate);
            entry.Post(entryNumber);

            db.JournalEntries.Add(entry);
            // ‏يُضاف إن لم يكن محفوظاً — واصطلاحُ `JournalPostingService` نفسُه.
            // ‏وكان الإضافةُ بلا شرطٍ في تسع خدمات، **وأسقطت شاشةَ الرواتب بـ`23505`** حين
            // ‏حفظت مسودّتَها ثمّ رحّلتها. والشرطُ يجعل المسارين مقبولين معاً، **فيُغلق
            // ‏صنفُ العطل لا حالتُه**: شاشةٌ تُكتب غداً بالاصطلاح الآخر لا تسقط.
            if (billing.Id == 0)
                db.ProgressBillings.Add(billing);
            await db.SaveChangesAsync(token);

            billing.LinkJournalEntry(entry.Id);
            await db.SaveChangesAsync(token);

            // ‏ ‏**والمستخلصُ يُعاد إلى موضعه من الإنجاز** — ما فُوتر فوق المكتسب التزامُ عقد، وما سبقه أصلُ عقد.
            await new ContractRevenueTrueUp(db).ApplyAsync(billing.CompanyId, billing.ProjectId, billing.BillingDate, token);

            await DomainEventHub.PublishAsync(new DocumentPostedEvent(
                billing.CompanyId, "ProgressBilling", billing.Id, billing.BillingNumber!), token);
            await DomainEventHub.PublishAsync(new JournalEntryPostedEvent(
                billing.CompanyId, entry.Id, entry.EntryNumber!, billing.BillingDate), token);

            await FinancialPostingSupport.AuditPostAsync(
                db, billing.CompanyId, "ProgressBilling", billing.BillingNumber!,
                reason: "ترحيل فاتورة تقدم أعمال", ct: token);

            await ZatcaPostingOrchestrator.GenerateInsideTransactionAsync(
                db, billing.CompanyId,
                () => new ZatcaEInvoiceGenerator(db).TryGenerateForProgressBillingAsync(billing.Id),
                token);

            return billing;
        }, ct);

        await ZatcaPostingOrchestrator.SubmitAfterCommitAsync(
            db, posted.CompanyId, ZatcaDocumentType.ProgressBilling, posted.Id);

        return posted;
    }

    private async Task<Account> SystemAccountAsync(long companyId, string code, CancellationToken ct) =>
        await db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == code, ct);
}
