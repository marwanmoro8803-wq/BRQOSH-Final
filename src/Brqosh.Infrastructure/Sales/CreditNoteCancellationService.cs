using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Platform;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Platform;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Zatca;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Sales;

/// <summary>إلغاء إشعار دائن مرحّل — عكس القيد وعكس إرجاع المخزون.</summary>
public class CreditNoteCancellationService(BrqoshDbContext db)
{
    public async Task CancelAsync(long companyId, long noteId, DateOnly cancellationDate,
        string username, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanCancelOrReverse();
        username = SecurityContext.ResolveUsername(username);

        var settings = await db.ZatcaSettings.FirstOrDefaultAsync(s => s.CompanyId == companyId, ct);

        Brqosh.Domain.Zatca.ZatcaEInvoiceDocument? zatcaDocument = null;

        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var note = await db.CreditNotes
                .SingleAsync(n => n.CompanyId == companyId && n.Id == noteId, token);

            if (note.Status != CreditNoteStatus.Posted)
                throw new AccountingException("لا يُلغى إلا إشعار دائن مرحّل.");

            if (note.JournalEntryId is not long entryId)
                throw new AccountingException("الإشعار بلا قيد محاسبي مرتبط.");

            await new JournalPostingService(db).EnsurePeriodOpenAsync(companyId, cancellationDate);

            await new JournalReversalService(db).ReverseAsync(
                companyId, entryId, cancellationDate, username, fromSourceDocument: true, token);

            var sourceDoc = $"CreditNote:{note.NoteNumber}";
            var returnMovements = await db.StockMovements
                .Include(m => m.Lines)
                .Where(m => m.CompanyId == companyId && m.SourceDocument == sourceDoc && m.Status == StockMovementStatus.Posted)
                .ToListAsync(token);

            // **يُعكَس الأصلُ — لا تُرحَّل حركةٌ مضادّة.** مرآةُ م0-٣.
            //
            // كان يُرحَّل `Issue` جديدٌ بتاريخ الإلغاء، **وهو يستهلك من رأس طابور FIFO**
            // — أقدمَ طبقةٍ وأرخصَها — ويترك طبقةَ المرتجع قائمة. فتُقلَب تكلفةُ البيعة
            // التالية، وقيمةُ المخزون الإجمالية **لا تكشف ذلك لأنّها متطابقة**.
            //
            // وأظهر القياسُ ما هو أسوأ: `PostDocumentMovementAsync` لا يستهلك طبقاتٍ
            // إطلاقاً، فبقيت الطبقاتُ 200 والرصيدُ 100 — **انشقاقٌ بين الطبقات والأرصدة**.
            var inv = new InventoryService(db);
            foreach (var movement in returnMovements)
            {
                await inv.ReverseDocumentMovementAsync(movement);
                movement.MarkCancelled();
            }

            note.MarkCancelled(cancellationDate);
            await db.SaveChangesAsync(token);

            await new AuditService(db).LogAsync(companyId, username, "إلغاء إشعار دائن",
                "CreditNote", note.NoteNumber ?? note.Id.ToString(), "تم عكس القيد والمخزون");

            await DomainEventHub.PublishAsync(new DocumentCancelledEvent(
                companyId, "CreditNote", note.Id, note.NoteNumber ?? note.Id.ToString(),
                "إلغاء إشعار دائن"), token);

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
                    .TryGenerateCancellationForCreditNoteAsync(noteId);
        }, ct);

        // ‏الإرسالُ بعد الالتزام: فشلُه شبكيٌّ لا يُبطل إلغاءً وقع، والمستندُ محفوظ.
        if (zatcaDocument is not null)
            await ZatcaAutoSubmissionHelper.TrySubmitDocumentAsync(db, companyId, zatcaDocument, ct);
    }
}
