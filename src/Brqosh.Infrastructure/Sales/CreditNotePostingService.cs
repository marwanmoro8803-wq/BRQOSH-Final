using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Platform;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Companies;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Platform;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Zatca;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Sales;

/// <summary>
/// ترحيل إشعار الدائن: تحقق من المتبقي، قيد عكسي، وإرجاع مخزني للأصناف المخزنية.
/// </summary>
public class CreditNotePostingService(BrqoshDbContext db)
{
    public async Task<CreditNote> PostAsync(CreditNote note)
    {
        SecurityContext.EnsureCanPost();
        await UnitOfWork.ExecuteInTransactionAsync(db, async ct =>
        {
            await PostCoreAsync(note, ct);
            await ZatcaPostingOrchestrator.GenerateInsideTransactionAsync(
                db, note.CompanyId,
                () => new ZatcaEInvoiceGenerator(db).TryGenerateForCreditNoteAsync(note.Id),
                ct);
            return note;
        });

        await ZatcaPostingOrchestrator.SubmitAfterCommitAsync(
            db, note.CompanyId, ZatcaDocumentType.CreditNote, note.Id);

        return note;
    }

    internal async Task PostCoreAsync(CreditNote note, CancellationToken ct = default)
    {
        await FinancialPostingSupport.ValidateAsync(
            db, note.CompanyId, note.NoteDate, PostingDocumentKind.CreditNote, ct: ct);

        var inv = new InventoryService(db);

        var invoice = await db.SalesInvoices
            .FirstOrDefaultAsync(i => i.Id == note.OriginalInvoiceId && i.CompanyId == note.CompanyId, ct)
            ?? throw new AccountingException("الفاتورة الأصلية غير موجودة.");
        if (invoice.Status != SalesInvoiceStatus.Posted)
            throw new AccountingException("لا يصدر إشعار دائن إلا على فاتورة مرحّلة.");
        if (invoice.CustomerId != note.CustomerId)
            throw new AccountingException("عميل الإشعار لا يطابق عميل الفاتورة الأصلية.");

        await BranchAssignmentHelper.EnsureAssignedAsync(note, db, invoice.BranchId, ct);

        var previousCredits = await db.CreditNotes
            .Where(n => n.CompanyId == note.CompanyId
                        && n.OriginalInvoiceId == note.OriginalInvoiceId
                        && n.Status == CreditNoteStatus.Posted)
            .ToListAsync(ct);

        var remaining = invoice.GrandTotal() - previousCredits.SumGrandTotal();
        if (note.GrandTotal() > remaining)
            throw new AccountingException(
                $"قيمة الإشعار {note.GrandTotal():N2} تتجاوز المتبقي من الفاتورة {remaining:N2}.");

        EnsureReturnedQuantitiesWithinInvoice(note, invoice, previousCredits);

        var products = await inv.LoadProductsForLinesAsync(note.CompanyId, note.Lines.Select(l => l.ProductId));
        var originalCosts = await ResolveOriginalSaleUnitCostsAsync(invoice, ct);
        decimal cogsReversal = 0;
        var returnLines = new List<(long ProductId, decimal Qty, decimal UnitCost)>();

        foreach (var line in note.Lines)
        {
            if (line.ProductId is long pid && products.TryGetValue(pid, out var product) && product.IsStockable())
            {
                var unitCost = originalCosts.TryGetValue(pid, out var original)
                    ? original
                    : product.WeightedAverageCost;
                var cost = Math.Round(line.Quantity * unitCost, 2, MidpointRounding.AwayFromZero);
                cogsReversal += cost;
                returnLines.Add((pid, line.Quantity, unitCost));
            }
        }

        var salesReturns = await SystemAccountAsync(note.CompanyId, SaudiChartOfAccounts.SystemCodes.SalesReturns);
        var vatOutput = await SystemAccountAsync(note.CompanyId, SaudiChartOfAccounts.SystemCodes.VatOutput);
        var receivable = await ReceivableAccountForCreditNoteAsync(note, invoice, ct);
        Account? cogs = cogsReversal > 0
            ? await SystemAccountAsync(note.CompanyId, SaudiChartOfAccounts.SystemCodes.CostOfGoodsSold)
            : null;
        Account? inventory = cogsReversal > 0
            ? await SystemAccountAsync(note.CompanyId, SaudiChartOfAccounts.SystemCodes.Inventory)
            : null;

        var noteNumber = await DocumentNumberGenerator.NextAsync(
            db.CreditNotes.Where(n => n.CompanyId == note.CompanyId).Select(n => n.NoteNumber),
            "CN", note.NoteDate.Year);

        var entry = note.Post(noteNumber, salesReturns, vatOutput, receivable, cogsReversal, cogs, inventory);
        var entryNumber = await new JournalEntryNumberGenerator(db).NextAsync(note.CompanyId, note.NoteDate);
        entry.Post(entryNumber);

        db.JournalEntries.Add(entry);
        db.CreditNotes.Add(note);
        await db.SaveChangesAsync(ct);

        note.LinkJournalEntry(entry.Id);
        await db.SaveChangesAsync(ct);

        await DomainEventHub.PublishAsync(new DocumentPostedEvent(
            note.CompanyId, "CreditNote", note.Id, note.NoteNumber!), ct);
        await DomainEventHub.PublishAsync(new JournalEntryPostedEvent(
            note.CompanyId, entry.Id, entry.EntryNumber!, note.NoteDate), ct);

        await FinancialPostingSupport.AuditPostAsync(
            db, note.CompanyId, "CreditNote", note.NoteNumber!,
            reason: "ترحيل إشعار دائن", ct: ct);

        if (returnLines.Count > 0)
        {
            var warehouse = await inv.GetDefaultWarehouseAsync(note.CompanyId);
            await inv.PostDocumentMovementAsync(note.CompanyId, warehouse.Id,
                StockMovementType.SalesReturn, note.NoteDate,
                $"إرجاع من إشعار دائن {noteNumber}",
                $"CreditNote:{noteNumber}", returnLines);
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// (CEEF-229 · م0-٦) التحقق الكمّي لكل صنف مقابل الفاتورة الأصلية.
    ///
    /// <para>فحص <b>القيمة</b> وحده لا يكفي: فاتورة فيها حبةٌ من صنفٍ بـ1,000 ومئةُ حبةٍ من صنفٍ
    /// بريال واحد إجماليها 1,100 — وإشعارٌ بمئة حبة من الصنف الغالي إجماليه 1,100 أيضاً، فيمرّ
    /// فحص القيمة ويُدخل إلى المخزون <b>مئة حبة لم تُبَع قط</b> بتكلفتها الأصلية.</para>
    ///
    /// <para>ولهذا يُلازم هذا الفحصُ تمريرَ <c>ProductId</c> من الشاشة في التزام واحد: قبل التمرير
    /// كان الخطر منزوع الصاعق لأن لا سطر يحمل صنفاً أصلاً — وتمريرُ الصنف وحده يُفجّره.</para>
    /// </summary>
    private static void EnsureReturnedQuantitiesWithinInvoice(
        CreditNote note, SalesInvoice invoice, IReadOnlyList<CreditNote> previousCredits)
    {
        var invoiced = invoice.Lines
            .Where(l => l.ProductId is not null)
            .GroupBy(l => l.ProductId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

        var alreadyReturned = previousCredits
            .SelectMany(n => n.Lines)
            .Where(l => l.ProductId is not null)
            .GroupBy(l => l.ProductId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

        // تُجمع سطور الإشعار بالصنف أولاً: صنفٌ مكرّر على سطرين يجب أن يُحاسَب مجموعُه،
        // وإلا مرّ التجاوز بتقسيمه على سطرين.
        var requested = note.Lines
            .Where(l => l.ProductId is not null)
            .GroupBy(l => l.ProductId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

        foreach (var (productId, quantity) in requested)
        {
            if (!invoiced.TryGetValue(productId, out var soldQuantity))
                throw new AccountingException(
                    "لا يمكن إرجاع صنف لم يرد في الفاتورة الأصلية.");

            var returnable = soldQuantity - alreadyReturned.GetValueOrDefault(productId);
            if (quantity > returnable)
                throw new AccountingException(
                    $"الكمية المرتجعة {quantity:N2} تتجاوز المتاح للإرجاع {returnable:N2} من هذا الصنف.");
        }
    }

    private async Task<Dictionary<long, decimal>> ResolveOriginalSaleUnitCostsAsync(
        SalesInvoice invoice, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(invoice.InvoiceNumber))
            return [];

        var sourceDoc = $"SalesInvoice:{invoice.InvoiceNumber}";
        var movements = await db.StockMovements
            .Where(m => m.CompanyId == invoice.CompanyId
                        && m.SourceDocument == sourceDoc
                        && m.Status == StockMovementStatus.Posted
                        && m.Type == StockMovementType.Issue)
            .ToListAsync(ct);

        var costs = new Dictionary<long, decimal>();
        foreach (var movement in movements)
        {
            foreach (var line in movement.Lines)
                costs[line.ProductId] = line.UnitCost;
        }

        return costs;
    }

    private async Task<Account> ReceivableAccountForCreditNoteAsync(
        CreditNote note, SalesInvoice invoice, CancellationToken ct)
    {
        if (invoice.PosShiftId is not null && invoice.PosPaymentMethod is { } method)
        {
            var code = method switch
            {
                Domain.Retail.PosPaymentMethod.Card => SaudiChartOfAccounts.SystemCodes.CashAtBanks,
                _ => SaudiChartOfAccounts.SystemCodes.CashOnHand,
            };
            return await SystemAccountAsync(note.CompanyId, code);
        }

        return await SystemAccountAsync(note.CompanyId, SaudiChartOfAccounts.SystemCodes.AccountsReceivable);
    }

    private async Task<Account> SystemAccountAsync(long companyId, string code) =>
        await db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == code);
}
