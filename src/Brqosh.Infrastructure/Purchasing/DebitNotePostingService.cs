using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Platform;
using Brqosh.Domain.Purchasing;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Companies;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Platform;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Purchasing;

public class DebitNotePostingService(BrqoshDbContext db)
{
    public async Task<DebitNote> PostAsync(DebitNote note, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPost();
        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            await FinancialPostingSupport.ValidateAsync(
                db, note.CompanyId, note.NoteDate, PostingDocumentKind.DebitNote, ct: token);

            var invoice = await db.PurchaseInvoices
                .SingleOrDefaultAsync(i => i.Id == note.OriginalPurchaseInvoiceId && i.CompanyId == note.CompanyId, token)
                ?? throw new AccountingException("فاتورة المشتريات الأصلية غير موجودة.");
            if (invoice.Status != PurchaseInvoiceStatus.Posted)
                throw new AccountingException("لا يصدر إشعار مدين إلا على فاتورة مرحّلة.");
            if (invoice.SupplierId != note.SupplierId)
                throw new AccountingException("مورد الإشعار لا يطابق فاتورة المشتريات.");

            await BranchAssignmentHelper.EnsureAssignedAsync(note, db, invoice.BranchId, token);

            var previous = await db.DebitNotes
                .Where(n => n.CompanyId == note.CompanyId
                            && n.OriginalPurchaseInvoiceId == note.OriginalPurchaseInvoiceId
                            && n.Status == DebitNoteStatus.Posted)
                .ToListAsync(token);
            var remaining = invoice.GrandTotal() - previous.SumGrandTotal();
            if (note.GrandTotal() > remaining)
                throw new AccountingException(
                    $"قيمة الإشعار {note.GrandTotal():N2} تتجاوز المتبقي من الفاتورة {remaining:N2}.");

            var inv = new InventoryService(db);
            var products = await inv.LoadProductsForLinesAsync(note.CompanyId, note.Lines.Select(l => l.ProductId));
            decimal inventoryReduction = 0;
            var issueLines = new List<(long ProductId, decimal Qty, decimal UnitCost)>();
            var costing = new InventoryCostingService(db);
            var warehouse = await inv.GetDefaultWarehouseAsync(note.CompanyId);

            foreach (var line in note.Lines)
            {
                if (line.ProductId is long pid && products.TryGetValue(pid, out var p) && p.IsStockable())
                {
                    var (total, unit) = await costing.ConsumeForIssueAsync(
                        note.CompanyId, pid, warehouse.Id, line.Quantity,
                        $"DebitNote:{note.NoteNumber}", token);
                    inventoryReduction += total;
                    issueLines.Add((pid, line.Quantity, unit));
                }
            }

            var purchases = await db.Accounts.SingleAsync(a => a.CompanyId == note.CompanyId && a.Code == "5102", token);
            var vatInput = await db.Accounts.SingleAsync(a =>
                a.CompanyId == note.CompanyId && a.Code == SaudiChartOfAccounts.SystemCodes.VatInput, token);
            var payable = await db.Accounts.SingleAsync(a =>
                a.CompanyId == note.CompanyId && a.Code == SaudiChartOfAccounts.SystemCodes.AccountsPayable, token);
            Account? inventory = inventoryReduction > 0
                ? await db.Accounts.SingleAsync(a =>
                    a.CompanyId == note.CompanyId && a.Code == SaudiChartOfAccounts.SystemCodes.Inventory, token)
                : null;

            var noteNumber = await DocumentNumberGenerator.NextAsync(db, 
                db.DebitNotes.Where(n => n.CompanyId == note.CompanyId).Select(n => n.NoteNumber),
                "DN", note.NoteDate.Year, note.CompanyId);

            var entry = note.Post(noteNumber, purchases, vatInput, payable, inventoryReduction, inventory, purchases);
            var entryNumber = await new JournalEntryNumberGenerator(db).NextAsync(note.CompanyId, note.NoteDate, token);
            entry.Post(entryNumber);

            db.JournalEntries.Add(entry);
            db.DebitNotes.Add(note);
            await db.SaveChangesAsync(token);
            note.LinkJournalEntry(entry.Id);
            await db.SaveChangesAsync(token);

            await DomainEventHub.PublishAsync(new DocumentPostedEvent(
                note.CompanyId, "DebitNote", note.Id, noteNumber), token);
            await DomainEventHub.PublishAsync(new JournalEntryPostedEvent(
                note.CompanyId, entry.Id, entry.EntryNumber!, note.NoteDate), token);

            await FinancialPostingSupport.AuditPostAsync(
                db, note.CompanyId, "DebitNote", noteNumber,
                reason: "ترحيل إشعار مدين", ct: token);

            if (issueLines.Count > 0)
            {
                await inv.PostDocumentMovementAsync(note.CompanyId, warehouse.Id,
                    StockMovementType.Issue, note.NoteDate,
                    $"صرف من إشعار مدين {noteNumber}", $"DebitNote:{noteNumber}", issueLines);
                await db.SaveChangesAsync(token);
            }

            return note;
        }, ct);
    }
}
