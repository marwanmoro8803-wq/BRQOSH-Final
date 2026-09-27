using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Platform;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Platform;
using Brqosh.Infrastructure.RealEstate;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Zatca;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Sales;

/// <summary>إلغاء فاتورة مبيعات مرحّلة — عكس القيد وإرجاع المخزون.</summary>
public class SalesInvoiceCancellationService(BrqoshDbContext db)
{
    public async Task CancelAsync(long companyId, long invoiceId, DateOnly cancellationDate,
        string username, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanCancelOrReverse();
        username = SecurityContext.ResolveUsername(username);

        var settings = await db.ZatcaSettings.FirstOrDefaultAsync(s => s.CompanyId == companyId, ct);

        Brqosh.Domain.Zatca.ZatcaEInvoiceDocument? pendingZatcaCancellation = null;

        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var invoice = await db.SalesInvoices
                .SingleAsync(i => i.CompanyId == companyId && i.Id == invoiceId, token);

            if (invoice.Status != SalesInvoiceStatus.Posted)
                throw new AccountingException("لا يُلغى إلا فاتورة مرحّلة.");

            if (invoice.JournalEntryId is not long entryId)
                throw new AccountingException("الفاتورة بلا قيد محاسبي مرتبط.");

            var hasPostedCreditNotes = await db.CreditNotes.AnyAsync(n =>
                n.CompanyId == companyId && n.OriginalInvoiceId == invoiceId
                && n.Status == CreditNoteStatus.Posted, token);
            if (hasPostedCreditNotes)
                throw new AccountingException(
                    "لا يُلغى فاتورة لها إشعار دائن مرحّل — عكس الإشعار أولاً أو استخدم إشعار دائن لتصحيح الفاتورة.");

            var posting = new JournalPostingService(db);
            await posting.EnsurePeriodOpenAsync(companyId, cancellationDate);

            await new JournalReversalService(db).ReverseAsync(
                companyId, entryId, cancellationDate, username, fromSourceDocument: true, token);

            // ‏(جاهزيّةُ الإطلاق · ح-٧٩) ‏**وفاتورةُ إيجارٍ مؤجَّلٍ يُعكس تأجيلُها واعترافاتُها معها** — وإلّا بقي المقبوضُ
            // ‏مقدّماً سالباً والإيرادُ يُكتسب عن فاتورةٍ لم تعد قائمة.
            await new LeaseRentRecognition(db).WithdrawForCancelledInvoiceAsync(
                companyId, invoiceId, cancellationDate, username, token);

            var sourceDoc = $"SalesInvoice:{invoice.InvoiceNumber}";
            var issueMovements = await db.StockMovements
                .Where(m => m.CompanyId == companyId && m.SourceDocument == sourceDoc && m.Status == StockMovementStatus.Posted)
                .ToListAsync(token);

            // (CEEF-229 · م0-٣ / I-2) **يُعكَس الأصلُ — لا تُرحَّل حركةٌ مضادّة.**
            //
            // كان يُرحَّل `SalesReturn` جديدٌ **بتاريخ الإلغاء**، فتُنشأ طبقةُ تكلفةٍ في
            // **آخر طابور FIFO** وتبقى الطبقةُ الأصلية عند صفر. والكميةُ تعود صحيحةً
            // فيمرّ كلُّ فحص — **والتكلفةُ تنقلب**: قيس أنّ البيعةَ نفسَها بعد الإلغاء
            // تكلّف **2,000 بدل 1,000**، فمجملُ الربح ينهار إلى النصف.
            //
            // والمسارُ الموحّد يعكس الرصيد ويُرمّم الطبقاتِ المستهلَكة بعينها.
            var inv = new InventoryService(db);
            foreach (var movement in issueMovements)
            {
                await inv.ReverseDocumentMovementAsync(movement);
                movement.MarkCancelled();
            }

            invoice.MarkCancelled(cancellationDate);
            await db.SaveChangesAsync(token);

            await new AuditService(db).LogAsync(companyId, username, "إلغاء فاتورة مبيعات",
                "SalesInvoice", invoice.InvoiceNumber ?? invoice.Id.ToString(), "تم عكس القيد وإرجاع المخزون");

            await DomainEventHub.PublishAsync(new DocumentCancelledEvent(
                companyId, "SalesInvoice", invoice.Id, invoice.InvoiceNumber ?? invoice.Id.ToString(),
                "إلغاء فاتورة مبيعات"), token);

            // (CEEF-210) التوليد داخل المعاملة (يكتب سجلات يجب أن ترتدّ مع الإلغاء)،
            // والإرسال بعدها. كان الإرسال هنا أيضاً — نداء شبكة داخل معاملة يطيل أقفال
            // القاعدة، وفشلُه يُفشل الإلغاء المحاسبي كله، وقد يبلغ إشعارُ الإلغاء الهيئةَ
            // ثم يرتدّ القيد محلياً فتتباعد سجلاتها عن الدفاتر. وهي سياسة المشروع نفسها
            // الموثّقة في ZatcaPostingOrchestrator: «الإرسال بعد الـcommit فقط».
            if (settings?.IsEnabled == true)
            {
                pendingZatcaCancellation = await new ZatcaEInvoiceGenerator(db)
                    .TryGenerateCancellationForSalesInvoiceAsync(invoiceId);
            }
        }, ct);

        // بعد الالتزام: الإلغاء المحاسبي محفوظ، وفشل الإرسال لا يعكسه.
        if (pendingZatcaCancellation is not null)
            await ZatcaAutoSubmissionHelper.TrySubmitDocumentAsync(
                db, companyId, pendingZatcaCancellation, ct);
    }
}
