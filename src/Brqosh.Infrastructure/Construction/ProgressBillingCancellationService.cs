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

        Brqosh.Domain.Zatca.ZatcaEInvoiceDocument? zatcaDocument = null;

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

            billing.MarkCancelled(cancellationDate);
            await db.SaveChangesAsync(token);

            // ‏ ‏**وإلغاءُ المستخلص يُعيد العقدَ إلى ما اكتُسب** بتاريخ الإلغاء.
            await new ContractRevenueTrueUp(db).ApplyAsync(companyId, billing.ProjectId, cancellationDate, token);

            await new AuditService(db).LogAsync(companyId, username, "إلغاء مستخلص أعمال",
                "ProgressBilling", billing.BillingNumber ?? billing.Id.ToString(), "تم عكس القيد");

            await DomainEventHub.PublishAsync(new DocumentCancelledEvent(
                companyId, "ProgressBilling", billing.Id, billing.BillingNumber ?? billing.Id.ToString(),
                "إلغاء مستخلص أعمال"), token);

            // **التوليدُ داخل المعاملة — والإرسالُ خارجها.**
            //
            // كان الاثنان بعد إغلاقها، فوقع عطلان:
            //
            // ١) **قفلُ عدّاد ICV ميّت.** `pg_advisory_xact_lock` مربوطٌ بالمعاملة، وخارجَها
            //    يُحرَّر بانتهاء العبارة — فيقرأ متزاحمان العدّادَ نفسَه. وإلغاءُ الفاتورة
            //    كان يستدعيه **داخل** معاملته، فحيَّ هناك وميّتٌ هنا.
            //
            // ٢) **والأخطر: `catch (AccountingException) { }` يبتلع الفشلَ كلَّه.** فمستندُ
            //    الإلغاء لا يُولَّد ولا يُقال شيء — **المستندُ ملغىً في الدفاتر وقائمٌ لدى
            //    الهيئة**، وهو انشقاقٌ لا يُصلَح بأثرٍ رجعيّ. وقيس: مستندان لا ثلاثة، بلا خطأ.
            //
            // **والتمييزُ جوهريّ:** فشلُ **التوليد** محلّيٌّ (هويةٌ ناقصة، شهادةٌ غائبة)
            // فيجب أن يوقف الإلغاء — إلغاءٌ لا يُبلَّغ أسوأُ من إلغاءٍ لم يقع. وفشلُ
            // **الإرسال** شبكيٌّ فيبقى غيرَ موقِف، والمستندُ مُولَّدٌ محفوظٌ يُعاد إرساله.
            if (settings?.IsEnabled == true)
                zatcaDocument = await new ZatcaEInvoiceGenerator(db)
                    .TryGenerateCancellationForProgressBillingAsync(billingId);
        }, ct);

        // ‏الإرسالُ بعد الالتزام: فشلُه شبكيٌّ لا يُبطل إلغاءً وقع، والمستندُ محفوظ.
        if (zatcaDocument is not null)
            await ZatcaAutoSubmissionHelper.TrySubmitDocumentAsync(db, companyId, zatcaDocument, ct);
    }
}
