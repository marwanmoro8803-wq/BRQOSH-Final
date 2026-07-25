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

/// <summary>
/// ترحيل فاتورة المشتريات مع مطابقة ثلاثية اختيارية عند ربط أمر شراء.
/// عند وجود GRN مرحّل لنفس الأمر: لا يُضاعَف إدخال المخزون (الاستلام سبق).
/// </summary>
public class PurchaseInvoicePostingService(BrqoshDbContext db)
{
    public async Task<PurchaseInvoice> PostAsync(PurchaseInvoice invoice)
    {
        SecurityContext.EnsureCanPost();
        return await UnitOfWork.ExecuteInTransactionAsync(db, async ct =>
        {
            if (invoice.BranchId is null)
                invoice.AssignBranch(await new BranchAssignmentService(db)
                .ResolveBranchIdAsync(invoice.CompanyId, BranchSessionContext.PreferredBranchId, ct));

            await FinancialPostingSupport.ValidateAsync(
                db, invoice.CompanyId, invoice.InvoiceDate, PostingDocumentKind.PurchaseInvoice, ct: ct);
            var inv = new InventoryService(db);

            var duplicate = await db.PurchaseInvoices.AnyAsync(i =>
                i.CompanyId == invoice.CompanyId
                && i.SupplierId == invoice.SupplierId
                && i.SupplierInvoiceReference == invoice.SupplierInvoiceReference
                && i.Status == PurchaseInvoiceStatus.Posted, ct);
            if (duplicate)
                throw new AccountingException(
                    $"فاتورة المورد بالمرجع '{invoice.SupplierInvoiceReference}' مسجلة مسبقاً لنفس المورد.");

            if (invoice.PurchaseOrderId is not null)
                await new ThreeWayMatchService(db).EnsureMatchOrThrowAsync(invoice, ct);

            var hasPostedGrn = invoice.PurchaseOrderId is long poId && await db.GoodsReceipts.AnyAsync(g =>
                g.CompanyId == invoice.CompanyId
                && g.PurchaseOrderId == poId
                && g.Status == GoodsReceiptStatus.Posted, ct);

            var products = await inv.LoadProductsForLinesAsync(invoice.CompanyId, invoice.Lines.Select(l => l.ProductId));
            decimal inventorySubtotal = 0;
            var receiptLines = new List<(long ProductId, decimal Qty, decimal UnitCost)>();

            foreach (var line in invoice.Lines)
            {
                if (line.ProductId is long pid && products.TryGetValue(pid, out var product) && product.IsStockable())
                {
                    inventorySubtotal += line.LineTotal;
                    if (!hasPostedGrn)
                        receiptLines.Add((pid, line.Quantity, line.UnitPrice));
                }
            }

            var purchases = await SystemAccountAsync(invoice.CompanyId, "5102");
            var inventoryAccount = inventorySubtotal > 0
                ? await SystemAccountAsync(invoice.CompanyId, SaudiChartOfAccounts.SystemCodes.Inventory)
                : null;
            var vatInput = await SystemAccountAsync(invoice.CompanyId, SaudiChartOfAccounts.SystemCodes.VatInput);
            var payable = await SystemAccountAsync(invoice.CompanyId, SaudiChartOfAccounts.SystemCodes.AccountsPayable);

            var invoiceNumber = await DocumentNumberGenerator.NextAsync(
                db.PurchaseInvoices.Where(i => i.CompanyId == invoice.CompanyId).Select(i => i.InvoiceNumber),
                "PI", invoice.InvoiceDate.Year);

            // عند وجود GRN: القيد المالي على المخزون بنفس الإجمالي دون حركة مزدوجة
            var entry = invoice.Post(invoiceNumber, purchases, vatInput, payable, inventorySubtotal, inventoryAccount);
            var entryNumber = await new JournalEntryNumberGenerator(db).NextAsync(invoice.CompanyId, invoice.InvoiceDate);
            entry.Post(entryNumber);

            db.JournalEntries.Add(entry);
            db.PurchaseInvoices.Add(invoice);
            await db.SaveChangesAsync(ct);

            invoice.LinkJournalEntry(entry.Id);
            await db.SaveChangesAsync(ct);

            await DomainEventHub.PublishAsync(new DocumentPostedEvent(
                invoice.CompanyId, "PurchaseInvoice", invoice.Id, invoice.InvoiceNumber!), ct);
            await DomainEventHub.PublishAsync(new JournalEntryPostedEvent(
                invoice.CompanyId, entry.Id, entry.EntryNumber!, invoice.InvoiceDate), ct);

            await FinancialPostingSupport.AuditPostAsync(
                db, invoice.CompanyId, "PurchaseInvoice", invoice.InvoiceNumber!,
                reason: "ترحيل فاتورة مشتريات", ct: ct);

            if (receiptLines.Count > 0)
            {
                var warehouse = await inv.GetDefaultWarehouseAsync(invoice.CompanyId);
                await inv.PostDocumentMovementAsync(invoice.CompanyId, warehouse.Id,
                    Domain.Inventory.StockMovementType.Receipt, invoice.InvoiceDate,
                    $"إدخال من فاتورة مشتريات {invoiceNumber}",
                    $"PurchaseInvoice:{invoiceNumber}", receiptLines);
                await db.SaveChangesAsync(ct);
            }

            return invoice;
        });
    }

    private async Task<Account> SystemAccountAsync(long companyId, string code) =>
        await db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == code);
}
