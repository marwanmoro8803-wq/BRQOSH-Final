using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Platform;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Platform;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Inventory;

/// <summary>إلغاء حركة مخزون يدوية مرحّلة — عكس القيد واستبعاد الكميات من الأرصدة.</summary>
public class StockMovementCancellationService(BrqoshDbContext db)
{
    private static readonly HashSet<StockMovementType> CancellableTypes =
    [
        StockMovementType.Opening,
        StockMovementType.AdjustmentIn,
        StockMovementType.AdjustmentOut,
        StockMovementType.Transfer,
    ];

    public async Task CancelAsync(long companyId, long movementId, DateOnly cancellationDate,
        string username, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanCancelOrReverse();
        username = SecurityContext.ResolveUsername(username);

        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var movement = await db.StockMovements
                .Include(m => m.Lines)
                .SingleAsync(m => m.CompanyId == companyId && m.Id == movementId, token);

            if (movement.Status != StockMovementStatus.Posted)
                throw new AccountingException("لا تُلغى إلا حركة مخزون مرحّلة.");

            if (!string.IsNullOrWhiteSpace(movement.SourceDocument))
                throw new AccountingException("حركات المستندات المرتبطة (فواتير، إشعارات) تُصحَّح من المستند المصدر.");

            if (!CancellableTypes.Contains(movement.Type))
                throw new AccountingException("نوع الحركة لا يدعم الإلغاء من هذه الشاشة.");

            await new JournalPostingService(db).EnsurePeriodOpenAsync(companyId, cancellationDate);

            if (movement.JournalEntryId is long entryId)
            {
                await new JournalReversalService(db).ReverseAsync(
                    companyId, entryId, cancellationDate, username, fromSourceDocument: true, token);
            }

            movement.MarkCancelled();

            // عكسٌ كامل عبر المسار الموحّد — كمية وتكلفة.
            //
            // كان هنا عكسُ الكمية وحدها، ثم تصفيرُ المتوسط في حالةٍ منحلّة واحدة: أن يصير
            // الرصيد الكليّ صفراً. فإلغاء حركةٍ واردة مع بقاء رصيد كان يترك المتوسط مسموماً
            // بتكلفة حركةٍ أُلغيت، فيفترق الدفتر المساعد عن الأستاذ فرقاً دائماً.
            await new InventoryService(db).ReverseDocumentMovementAsync(movement);
            await db.SaveChangesAsync(token);

            await new AuditService(db).LogAsync(companyId, username, "إلغاء حركة مخزون",
                "StockMovement", movement.MovementNumber ?? movement.Id.ToString(), movement.Description);

            await DomainEventHub.PublishAsync(new DocumentCancelledEvent(
                companyId, "StockMovement", movement.Id, movement.MovementNumber ?? movement.Id.ToString(),
                "إلغاء حركة مخزون"), token);
        }, ct);
    }
}
