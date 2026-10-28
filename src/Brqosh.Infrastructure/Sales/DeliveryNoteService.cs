using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Sales;

/// <summary>
/// ‏إذنُ التسليم من أمر البيع: يصرف المخزونَ ويقيّد «تكلفة المبيعات / المخزون» (بحساب الصنف ← الفئة ← العامّ)،
/// ‏ويُفرج عن الحجز. ‏والفاتورةُ اللاحقةُ تُفوتر المسلَّمَ بلا صرفٍ ثانٍ.
/// </summary>
public class DeliveryNoteService(BrqoshDbContext db) : Brqosh.Application.Accounting.IDeliveryNoteService
{
    /// <param name="quantities">كمّيةٌ لكلّ رقم سطر (من الصفر)، و<c>null</c> = كلُّ المتبقّي من الأصناف المخزنيّة.</param>
    /// <param name="batchNumbers">‏رقمُ الدفعة لكلّ سطرٍ صنفُه متتبَّع — يُصرف منها ويُفحص رصيدُها وصلاحيتُها.</param>
    public async Task<DeliveryNote> DeliverFromOrderAsync(long companyId, long orderId, DateOnly deliveryDate,
        IReadOnlyDictionary<int, decimal>? quantities = null, long? warehouseId = null,
        IReadOnlyDictionary<int, string>? batchNumbers = null, CancellationToken ct = default) =>
        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            SecurityContext.EnsureCanPost();
            await DocumentSequenceLock.AcquireAsync(db.Database, companyId, "SOI", (int)(orderId % int.MaxValue));
            await new JournalPostingService(db).EnsurePeriodOpenAsync(companyId, deliveryDate);

            var order = await db.SalesOrders.SingleAsync(o => o.CompanyId == companyId && o.Id == orderId, token);
            if (order.Status is not (SalesOrderStatus.Confirmed or SalesOrderStatus.PartInvoiced))
                throw new AccountingException("يُسلَّم من أمر بيع مؤكد فقط.");

            var inv = new InventoryService(db);
            var products = await inv.LoadProductsForLinesAsync(companyId, order.Lines.Select(l => l.ProductId));
            var invoiced = await db.SalesOrderInvoiceLines.AsNoTracking()
                .Where(l => l.CompanyId == companyId && l.SalesOrderId == orderId && !l.IsCancelled)
                .ToListAsync(token);
            var deliveredUninvoiced = await SalesOrderService.DeliveredUninvoicedAsync(db, companyId, order, token);

            var take = new decimal[order.Lines.Count];
            for (var i = 0; i < take.Length; i++)
            {
                var line = order.Lines[i];
                var stockable = line.ProductId is long pid && products.TryGetValue(pid, out var p) && p.IsStockable();
                var open = line.Quantity - invoiced.Where(x => x.LineIndex == i).Sum(x => x.Quantity) - deliveredUninvoiced[i];
                if (quantities is null)
                    take[i] = stockable ? Math.Max(open, 0m) : 0m;
                else if (quantities.TryGetValue(i, out var q) && q != 0m)
                {
                    if (!stockable)
                        throw new AccountingException($"سطر أمر البيع رقم {i + 1} ليس صنفاً مخزنياً — لا يُسلَّم بإذن.");
                    if (q < 0m || q > open)
                        throw new AccountingException($"كمية السطر {i + 1} ({q:N2}) تتجاوز المتبقّي للتسليم ({open:N2}).");
                    take[i] = q;
                }
            }
            if (quantities is not null && quantities.Keys.Any(k => k < 0 || k >= take.Length))
                throw new AccountingException("سطر أمر البيع في إذن التسليم غير موجود.");
            if (take.All(q => q <= 0m))
                throw new AccountingException("لا كمية متبقّية للتسليم في أمر البيع.");

            var warehouse = warehouseId is long chosen
                ? await db.Warehouses.SingleOrDefaultAsync(w => w.CompanyId == companyId && w.Id == chosen, token)
                    ?? throw new AccountingException("المستودع المختار لهذه الفاتورة غير موجود في المنشأة.")
                : await inv.GetDefaultWarehouseAsync(companyId);

            var number = await DocumentNumberGenerator.NextAsync(db,
                db.DeliveryNotes.Where(n => n.CompanyId == companyId).Select(n => (string?)n.Number),
                "DLV", deliveryDate, companyId);
            var source = $"DeliveryNote:{number}";
            var note = DeliveryNote.Create(companyId, orderId, order.CustomerId, warehouse.Id, deliveryDate, number);

            var costing = new InventoryCostingService(db);
            var issueLines = new List<(long ProductId, decimal Qty, decimal UnitCost, string? BatchNumber, DateOnly? ExpiryDate)>();
            var costByProduct = new List<(long ProductId, decimal Amount)>();
            decimal totalCost = 0m;
            for (var i = 0; i < take.Length; i++)
            {
                if (take[i] <= 0m) continue;
                var pid = order.Lines[i].ProductId!.Value;
                var onHand = await inv.GetOnHandAsync(companyId, pid, warehouse.Id);
                if (take[i] > onHand)
                    throw new AccountingException($"رصيد '{products[pid].NameAr}' غير كافٍ للتسليم — المتوفر {onHand:N2}.");
                var (lineCost, unitCost) = await costing.ConsumeForIssueAsync(companyId, pid, warehouse.Id, take[i], source, token);
                totalCost += lineCost;
                costByProduct.Add((pid, lineCost));
                issueLines.Add((pid, take[i], unitCost,
                    batchNumbers is not null && batchNumbers.TryGetValue(i, out var batch) && !string.IsNullOrWhiteSpace(batch) ? batch.Trim() : null,
                    null));
                note.AddLine(i, pid, take[i], unitCost);
            }

            var movement = await inv.PostDocumentMovementAsync(companyId, warehouse.Id, StockMovementType.Issue,
                deliveryDate, $"إذن تسليم {number}", source, issueLines);
            db.DeliveryNotes.Add(note);
            await db.SaveChangesAsync(token);
            note.LinkStockMovement(movement.Id);

            if (totalCost > 0m)
            {
                var cogs = await db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == SaudiChartOfAccounts.SystemCodes.CostOfGoodsSold, token);
                var inventory = await db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == SaudiChartOfAccounts.SystemCodes.Inventory, token);
                var entry = JournalEntry.CreateDraft(companyId, deliveryDate, $"إذن تسليم {number}", sourceDocument: source);
                entry.AddLine(cogs, totalCost, 0m, "تكلفة البضاعة المسلَّمة");
                entry.AddLine(inventory, 0m, totalCost, "صرف من المخزون بإذن تسليم");
                await ItemAccountResolver.RerouteAsync(db, companyId, entry, cogs, costByProduct, a => a.Cogs, debitSide: true, token);
                await ItemAccountResolver.RerouteAsync(db, companyId, entry, inventory, costByProduct, a => a.Inventory, debitSide: false, token);
                entry.Post(await new JournalEntryNumberGenerator(db).NextAsync(companyId, deliveryDate, token));
                db.JournalEntries.Add(entry);
                await db.SaveChangesAsync(token);
                note.LinkJournalEntry(entry.Id);
            }

            // ‏والمسلَّمُ يخرج من الحجز.
            var reservations = await db.StockReservations
                .Where(r => r.CompanyId == companyId && r.SalesOrderId == orderId).ToListAsync(token);
            for (var i = 0; i < take.Length; i++)
                if (take[i] > 0m)
                {
                    var toRelease = take[i];
                    foreach (var r in reservations.Where(r => r.ProductId == order.Lines[i].ProductId))
                        toRelease -= r.Release(toRelease);
                }

            await db.SaveChangesAsync(token);
            return note;
        }, ct);

    /// <summary>
    /// ‏إلغاءُ إذن تسليمٍ لم تُفوتَر كمّيتُه: يعكس حركةَ المخزون (ويُرمّم طبقاتِ التكلفة بعينها) وقيدَ التكلفة،
    /// ‏ويُعيد الكمّيةَ محجوزةً لأمرها. ‏وما فُوتر منه لا يُلغى قبل إلغاء فاتورته — وإلّا بقيت فاتورةٌ بلا صرف.
    /// </summary>
    public async Task<DeliveryNote> CancelAsync(long companyId, long deliveryNoteId, DateOnly cancellationDate,
        CancellationToken ct = default) =>
        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            SecurityContext.EnsureCanPost();
            var note = await db.DeliveryNotes.Include(n => n.Lines)
                .SingleOrDefaultAsync(n => n.CompanyId == companyId && n.Id == deliveryNoteId, token)
                ?? throw new AccountingException("إذن التسليم غير موجود.");
            await DocumentSequenceLock.AcquireAsync(db.Database, companyId, "SOI", (int)(note.SalesOrderId % int.MaxValue));
            await new JournalPostingService(db).EnsurePeriodOpenAsync(companyId, cancellationDate);
            if (note.Status == DeliveryNoteStatus.Cancelled)
                throw new AccountingException("إذن التسليم ملغى مسبقاً.");

            var order = await db.SalesOrders.SingleAsync(o => o.CompanyId == companyId && o.Id == note.SalesOrderId, token);
            var deliveredUninvoiced = await SalesOrderService.DeliveredUninvoicedAsync(db, companyId, order, token);
            foreach (var g in note.Lines.GroupBy(l => l.OrderLineIndex))
                if (g.Key >= deliveredUninvoiced.Length || deliveredUninvoiced[g.Key] < g.Sum(l => l.Quantity))
                    throw new AccountingException("لا يُلغى إذن تسليم فُوتر ما سلّمه — ألغِ الفاتورة أولاً.");

            if (note.JournalEntryId is long entryId)
                await new JournalReversalService(db).ReverseAsync(
                    companyId, entryId, cancellationDate, SecurityContext.CurrentUsername ?? "", fromSourceDocument: true, token);

            if (note.StockMovementId is long movementId)
            {
                var movement = await db.StockMovements.SingleAsync(m => m.CompanyId == companyId && m.Id == movementId, token);
                if (movement.Status == StockMovementStatus.Posted)
                {
                    await new InventoryService(db).ReverseDocumentMovementAsync(movement);
                    movement.MarkCancelled();
                }
            }

            note.Cancel();

            // ‏والكمّيةُ العائدةُ إلى أمرٍ قائمٍ تعود محجوزة.
            if (order.Status is SalesOrderStatus.Confirmed or SalesOrderStatus.PartInvoiced)
            {
                var reservations = await db.StockReservations
                    .Where(r => r.CompanyId == companyId && r.SalesOrderId == order.Id).ToListAsync(token);
                foreach (var line in note.Lines)
                    reservations.FirstOrDefault(r => r.ProductId == line.ProductId)?.Restore(line.Quantity);
            }

            await db.SaveChangesAsync(token);
            return note;
        }, ct);
}
