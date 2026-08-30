using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Platform;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Companies;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Platform;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Inventory;

/// <summary>ترحيل حركات المخزون اليدوية مع القيود المحاسبية حيث يلزم.</summary>
public class StockMovementPostingService(BrqoshDbContext db)
{
    public async Task<StockMovement> PostAsync(StockMovement movement)
    {
        SecurityContext.EnsureCanPost();
        return await UnitOfWork.ExecuteInTransactionAsync(db, async ct =>
        {
            if (movement.BranchId is null)
                movement.AssignBranch(await new BranchAssignmentService(db)
                .ResolveBranchIdAsync(movement.CompanyId, BranchSessionContext.PreferredBranchId, ct));

            await FinancialPostingSupport.ValidateAsync(
                db, movement.CompanyId, movement.MovementDate, PostingDocumentKind.StockMovement, ct: ct);

            var inventory = await AccountAsync(movement.CompanyId, SaudiChartOfAccounts.SystemCodes.Inventory);
            var inv = new InventoryService(db);

            if (movement.IsOutbound || movement.Type == StockMovementType.Transfer)
            {
                foreach (var line in movement.Lines)
                {
                    var onHand = await inv.GetOnHandAsync(movement.CompanyId, line.ProductId, movement.WarehouseId);
                    if (line.Quantity > onHand)
                    {
                        var p = await db.Products.SingleAsync(x => x.Id == line.ProductId, ct);
                        throw new AccountingException($"رصيد '{p.NameAr}' غير كافٍ — المتوفر {onHand:N2}.");
                    }
                }
            }

            var entry = await BuildJournalEntryAsync(movement, inventory, inv);

            if (movement.IsInbound)
            {
                var costing = new InventoryCostingService(db);
                foreach (var line in movement.Lines)
                {
                    await costing.OnReceiptAsync(movement.CompanyId, line.ProductId, movement.WarehouseId,
                        movement.MovementDate, line.Quantity, line.UnitCost,
                        movement.SourceDocument ?? movement.Description ?? "StockMovement");
                }
            }

            await inv.ApplyMovementBalancesAsync(movement);

            var number = await DocumentNumberGenerator.NextAsync(db, 
                db.StockMovements.Where(m => m.CompanyId == movement.CompanyId).Select(m => m.MovementNumber),
                "SM", movement.MovementDate.Year, movement.CompanyId,
                db.StockMovements.Local.Where(m => m.CompanyId == movement.CompanyId).Select(m => m.MovementNumber));
            movement.MarkPosted(number);

            if (entry is not null)
            {
                var entryNumber = await new JournalEntryNumberGenerator(db)
                    .NextAsync(movement.CompanyId, movement.MovementDate);
                entry.Post(entryNumber);
                db.JournalEntries.Add(entry);
                await db.SaveChangesAsync(ct);
                movement.LinkJournalEntry(entry.Id);
            }

            db.StockMovements.Add(movement);
            await db.SaveChangesAsync(ct);

            await DomainEventHub.PublishAsync(new DocumentPostedEvent(
                movement.CompanyId, "StockMovement", movement.Id, movement.MovementNumber!), ct);
            if (entry is not null)
            {
                await DomainEventHub.PublishAsync(new JournalEntryPostedEvent(
                    movement.CompanyId, entry.Id, entry.EntryNumber!, movement.MovementDate), ct);
            }

            await FinancialPostingSupport.AuditPostAsync(
                db, movement.CompanyId, "StockMovement", movement.MovementNumber!,
                reason: "ترحيل حركة مخزون", ct: ct);

            return movement;
        });
    }

    private async Task<JournalEntry?> BuildJournalEntryAsync(StockMovement movement, Account inventory, InventoryService inv)
    {
        // (CEEF-229 · م2-٢١) حركةٌ قيمتُها صفر **لا قيدَ لها** — لا قيدٌ بأصفار.
        //
        // كان القيد يُبنى دائماً، فيمرّ على `AddLine(inventory, 0m, 0m)` ويرمي «سطر القيد يجب
        // أن يحمل مبلغاً مديناً أو دائناً». وذلك يقع في **اليوم الأول** من حياة كل منشأة:
        // تُدخل دليل أصنافها ثم تجري جرداً لإثبات الأرصدة الافتتاحية، وكلُّ صنفٍ لم يُشترَ بعدُ
        // متوسطُ تكلفته صفر — فيرتدّ الجرد **بأكمله** برسالةٍ لا علاقة لها بما فعل المستخدم،
        // وعمودُ تكلفة الوحدة في الشاشة للقراءة فقط فلا يستطيع تصحيحه.
        //
        // والامتناع عن القيد ليس التفافاً بل الصواب المحاسبيّ: حركةٌ بلا قيمة لا أثر لها في
        // الأستاذ. والكمية تُسجَّل، وهوية المخزون (رصيد الحساب == Σ الكمية × المتوسط) تبقى
        // صادقة لأن الطرفين صفر. ويظلّ الجردُ المختلط يُرحَّل قيدَ ما له تكلفة وحده.
        // الحارس داخل كل بانٍ لا هنا: `AdjustmentOut` يستهلك طبقات التكلفة قبل أن يعرف
        // مجموعه، فالخروج المبكر هنا كان سيتخطّى الاستهلاك ويترك طبقاتٍ معلّقة.
        return movement.Type switch
        {
            StockMovementType.Opening => await OpeningEntryAsync(movement, inventory),
            StockMovementType.AdjustmentIn => await AdjustmentInEntryAsync(movement, inventory),
            StockMovementType.AdjustmentOut => await AdjustmentOutEntryAsync(movement, inventory, inv),
            StockMovementType.Transfer => null,
            _ => throw new AccountingException("نوع الحركة غير مدعوم."),
        };
    }

    private async Task<JournalEntry?> OpeningEntryAsync(StockMovement movement, Account inventory)
    {
        if (movement.TotalValue == 0m) return null;

        var retained = await AccountAsync(movement.CompanyId, SaudiChartOfAccounts.SystemCodes.RetainedEarnings);

        var entry = JournalEntry.CreateDraft(movement.CompanyId, movement.MovementDate,
            movement.Description, "StockOpening");
        entry.AddLine(inventory, movement.TotalValue, 0m, "رصيد افتتاحي للمخزون");
        entry.AddLine(retained, 0m, movement.TotalValue, "رصيد افتتاحي — حقوق ملكية");
        return entry;
    }

    private async Task<JournalEntry?> AdjustmentInEntryAsync(StockMovement movement, Account inventory)
    {
        if (movement.TotalValue == 0m) return null;

        var equity = await AccountAsync(movement.CompanyId, SaudiChartOfAccounts.SystemCodes.RetainedEarnings);

        var entry = JournalEntry.CreateDraft(movement.CompanyId, movement.MovementDate,
            movement.Description, "StockAdjustmentIn");
        entry.AddLine(inventory, movement.TotalValue, 0m, "زيادة مخزون — جرد");
        entry.AddLine(equity, 0m, movement.TotalValue, "فائض جرد — حقوق ملكية");
        return entry;
    }

    private async Task<JournalEntry?> AdjustmentOutEntryAsync(StockMovement movement, Account inventory, InventoryService inv)
    {
        var costing = new InventoryCostingService(db);
        decimal total = 0;
        foreach (var line in movement.Lines)
        {
            var (lineCost, _) = await costing.ConsumeForIssueAsync(
                movement.CompanyId, line.ProductId, movement.WarehouseId, line.Quantity);
            total += lineCost;
        }
        if (total == 0m) return null;

        var misc = await AccountAsync(movement.CompanyId, "5215");
        var entry = JournalEntry.CreateDraft(movement.CompanyId, movement.MovementDate,
            movement.Description, "StockAdjustmentOut");
        entry.AddLine(misc, total, 0m, "نقص مخزون — جرد");
        entry.AddLine(inventory, 0m, total, "تخفيض المخزون");
        return entry;
    }

    private async Task<Account> AccountAsync(long companyId, string code) =>
        await db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == code);
}
