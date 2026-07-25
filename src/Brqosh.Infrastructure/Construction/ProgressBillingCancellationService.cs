using Brqosh.Domain.Accounting;
using Brqosh.Domain.Construction;
using Brqosh.Domain.Platform;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Platform;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Zatca;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Construction;

/// <summary>إلغاء مستخلص أعمال مرحّل — عكس القيد المحاسبي.</summary>
public class ProgressBillingCancellationService(BrqoshDbContext db)
{
    public async Task CancelAsync(long companyId, long billingId, DateOnly cancellationDate,
        string username, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanCancelOrReverse();
        username = SecurityContext.ResolveUsername(username);
        var settings = await db.ZatcaSettings.FirstOrDefaultAsync(s => s.CompanyId == companyId, ct);

        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var billing = await db.ProgressBillings
                .SingleAsync(b => b.CompanyId == companyId && b.Id == billingId, token);

            if (billing.Status != ProgressBillingStatus.Posted)
                throw new AccountingException("لا يُلغى إلا مستخلص مرحّل.");

            if (billing.JournalEntryId is not long entryId)
                throw new AccountingException("المستخلص بلا قيد محاسبي مرتبط.");

            await new JournalPostingService(db).EnsurePeriodOpenAsync(companyId, cancellationDate);

            await new JournalReversalService(db).ReverseAsync(
                companyId, entryId, cancellationDate, username, fromSourceDocument: true, token);

            billing.MarkCancelled();
            await db.SaveChangesAsync(token);

            await new AuditService(db).LogAsync(companyId, username, "إلغاء مستخلص أعمال",
                "ProgressBilling", billing.BillingNumber ?? billing.Id.ToString(), "تم عكس القيد");

            await DomainEventHub.PublishAsync(new DocumentCancelledEvent(
                companyId, "ProgressBilling", billing.Id, billing.BillingNumber ?? billing.Id.ToString(),
                "إلغاء مستخلص أعمال"), token);
        }, ct);

        if (settings?.IsEnabled == true)
        {
            try
            {
                var zatcaDoc = await new ZatcaEInvoiceGenerator(db)
                    .TryGenerateCancellationForProgressBillingAsync(billingId);
                if (zatcaDoc is not null)
                    await ZatcaAutoSubmissionHelper.TrySubmitDocumentAsync(db, companyId, zatcaDoc, ct);
            }
            catch (AccountingException) { /* لا نُوقف الإلغاء */ }
        }
    }
}
