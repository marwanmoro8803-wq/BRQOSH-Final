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
using Brqosh.Infrastructure.Treasury;
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
            // (CEEF-230 · ط6-٧) ‏التوليدُ نزل إلى `PostCoreAsync` — انظر الشرحَ هناك.
            await PostCoreAsync(note, ct);
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
        var returnLines = new List<(long ProductId, decimal Qty, decimal UnitCost, string? BatchNumber, DateOnly? ExpiryDate)>();
        // ‏(جاهزيّةُ الإطلاق · ح-١٠٢ · ‏`order-to-cash-procure-to-pay-inventory#7`) ‏**المتتبَّعُ يعود إلى دفعاته.**
        // ‏وكان سطرُ المرتجع بلا هويّة، والمَخنَقُ يرفض كلَّ سطرٍ متتبَّعٍ بلاها — فلا يُرجَع دواءٌ ولا غذاءٌ بإشعارٍ دائنٍ أبداً.
        var soldOut = await SoldBatchesStillOutAsync(invoice, previousCredits, products, ct);

        foreach (var line in note.Lines)
        {
            if (line.ProductId is long pid && products.TryGetValue(pid, out var product) && product.IsStockable())
            {
                var unitCost = originalCosts.TryGetValue(pid, out var original)
                    ? original
                    : product.WeightedAverageCost;
                if (!product.IsTracked)
                {
                    cogsReversal += Math.Round(line.Quantity * unitCost, 2, MidpointRounding.AwayFromZero);
                    returnLines.Add((pid, line.Quantity, unitCost, null, null));
                    continue;
                }

                // ‏**وكلُّ دفعةٍ تعود بتكلفتها هي** — وكانت تكلفةُ آخر سطرٍ للصنف تُعكس على الكلّ، وسطورُ الصنف
                // ‏المتتبَّع سطرٌ لكلّ دفعةٍ بطبقاتٍ مختلفة: مرتجعُ أربعٍ بعشرةٍ يُعكس بثمانيةٍ وأربعين لا بأربعين.
                foreach (var draw in DrawReturnedBatches(product, line.Quantity, line.BatchNumber, soldOut))
                {
                    cogsReversal += Math.Round(draw.Quantity * draw.UnitCost, 2, MidpointRounding.AwayFromZero);
                    returnLines.Add((pid, draw.Quantity, draw.UnitCost, draw.Batch, draw.Expiry));
                }
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

        var noteNumber = await DocumentNumberGenerator.NextAsync(db, 
            db.CreditNotes.Where(n => n.CompanyId == note.CompanyId).Select(n => n.NoteNumber),
            "CN", note.NoteDate.Year, note.CompanyId);

        var entry = note.Post(noteNumber, salesReturns, vatOutput, receivable, cogsReversal, cogs, inventory);
        var entryNumber = await new JournalEntryNumberGenerator(db).NextAsync(note.CompanyId, note.NoteDate);
        entry.Post(entryNumber);

        db.JournalEntries.Add(entry);
        // (CEEF-230 · ط2-٠) ‏يُضاف إن لم يكن محفوظاً — واصطلاحُ `JournalPostingService` نفسُه.
        // ‏وكان الإضافةُ بلا شرطٍ في تسع خدمات، **وأسقطت شاشةَ الرواتب بـ`23505`** حين
        // ‏حفظت مسودّتَها ثمّ رحّلتها. والشرطُ يجعل المسارين مقبولين معاً، **فيُغلق
        // ‏صنفُ العطل لا حالتُه**: شاشةٌ تُكتب غداً بالاصطلاح الآخر لا تسقط.
        if (note.Id == 0)
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

        // (CEEF-230 · ط6-٧) ‏**والمستندُ الإلكترونيُّ من المسار المشترك** — كما في فاتورة
        // ‏المبيعات، وللسبب نفسِه. **وللإشعار قناةٌ واحدةٌ اليوم** لا خمس، لكنّ موضعَ
        // ‏التوليد يُوحَّد على الاصطلاح: **حيث يقع الترحيلُ يقع التوليد**. فمَن يبني قناةَ
        // ‏إشعارٍ غداً — إلغاءُ مطالبةٍ أو تسويةُ تأمين — لا يحتاج أن يعلم بهذا السطر.
        await ZatcaPostingOrchestrator.GenerateInsideTransactionAsync(
            db, note.CompanyId,
            () => new ZatcaEInvoiceGenerator(db).TryGenerateForCreditNoteAsync(note.Id),
            ct);
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

    /// <summary>‏دفعةٌ بيع منها صنفٌ متتبَّع، وما بقي منها عند العميل بعد ما أُرجع سلفاً.</summary>
    private sealed class SoldBatch(long productId, string? batchNumber, DateOnly? expiryDate)
    {
        public long ProductId { get; } = productId;
        public string? BatchNumber { get; } = batchNumber;
        public DateOnly? ExpiryDate { get; } = expiryDate;
        public decimal Out { get; set; }
        public decimal Sold { get; private set; }
        private decimal _cost;

        /// <summary>‏تكلفةُ ما خرج من هذه الدفعة — متوسّطاً موزوناً إن تعدّدت سطورُها.</summary>
        public decimal UnitCost => Sold == 0m ? 0m : _cost / Sold;

        public void Issued(decimal quantity, decimal unitCost)
        {
            Sold += quantity;
            Out += quantity;
            _cost += quantity * unitCost;
        }
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-١٠٢) <b>ما بيع من كلّ دفعةٍ ولم يُرجَع بعد</b> — من سطور حركة البيع نفسِها (وهي تحمل الدفعة
    /// منذ CEEF-230) ناقصاً سطورَ مرتجعات الإشعارات المرحّلة قبله على الفاتورة نفسِها؛ <b>بترتيب البيع</b>، فيعود المرتجعُ
    /// إلى الدفعات بالترتيب الذي خرج به. وغيرُ المتتبَّع لا يُقرأ هنا.
    /// </summary>
    private async Task<List<SoldBatch>> SoldBatchesStillOutAsync(SalesInvoice invoice,
        IReadOnlyList<CreditNote> previousCredits, IReadOnlyDictionary<long, Product> products, CancellationToken ct)
    {
        var tracked = products.Values.Where(p => p.IsTracked).Select(p => p.Id).ToHashSet();
        var sold = new List<SoldBatch>();
        if (tracked.Count == 0 || string.IsNullOrWhiteSpace(invoice.InvoiceNumber))
            return sold;

        var saleSource = $"SalesInvoice:{invoice.InvoiceNumber}";
        // ‏**وترتيبُ البيع مُصرَّحٌ به لا حادث** — السطورُ مملوكةٌ بلا عمود ترتيب، وEF لا يُرتّب السطورَ داخل الحركة،
        // ‏فتُرتَّب هنا بمفتاح الحركة ثمّ بمفتاح السطر الظلّيّ.
        var sales = await db.StockMovements.AsNoTracking()
            .Where(m => m.CompanyId == invoice.CompanyId && m.SourceDocument == saleSource
                        && m.Status == StockMovementStatus.Posted && m.Type == StockMovementType.Issue)
            .SelectMany(m => m.Lines, (m, l) => new { MovementId = m.Id, LineId = EF.Property<long>(l, "id"), Line = l })
            .OrderBy(x => x.MovementId).ThenBy(x => x.LineId)
            .ToListAsync(ct);
        foreach (var line in sales.Select(x => x.Line).Where(l => tracked.Contains(l.ProductId)))
        {
            var row = sold.FirstOrDefault(s => s.ProductId == line.ProductId && s.BatchNumber == line.BatchNumber);
            if (row is null)
                sold.Add(row = new SoldBatch(line.ProductId, line.BatchNumber, line.ExpiryDate));
            row.Issued(line.Quantity, line.UnitCost);
        }

        var returnSources = previousCredits.Where(n => n.NoteNumber is not null)
            .Select(n => $"CreditNote:{n.NoteNumber}").ToList();
        var returns = await db.StockMovements.AsNoTracking()
            .Where(m => m.CompanyId == invoice.CompanyId && returnSources.Contains(m.SourceDocument!)
                        && m.Status == StockMovementStatus.Posted && m.Type == StockMovementType.SalesReturn)
            .ToListAsync(ct);
        foreach (var line in returns.SelectMany(m => m.Lines).Where(l => tracked.Contains(l.ProductId)))
        {
            var row = sold.FirstOrDefault(s => s.ProductId == line.ProductId && s.BatchNumber == line.BatchNumber);
            if (row is not null)
                row.Out -= line.Quantity;
        }

        return sold;
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-١٠٢) <b>يُقسّم كمّيّةَ المرتجع على دفعات البيع</b>، وينقص ما خرج منها فلا يُرجَع إلى دفعةٍ
    /// أكثرُ ممّا بيع منها ولو تكرّر الصنفُ في الإشعار. <b>والدفعةُ التي يسمّيها سطرُ الإشعار يعود إليها وحدَها</b> — فالشاشةُ
    /// تنسخها من سطر الفاتورة، ومرتجعُ الدفعة القريبة الانتهاء لا يُقيَّد على البعيدة؛ <b>وبترتيب البيع حين لا يُسمّي</b>.
    /// <b>وما بيع قبل أن يُتتبَّع الصنفُ لا يُعرف إلى أيّ دفعةٍ يعود — فيُرفض باسمه</b> ولا يُخمَّن.
    /// </summary>
    private static List<(string? Batch, DateOnly? Expiry, decimal Quantity, decimal UnitCost)> DrawReturnedBatches(
        Product product, decimal quantity, string? namedBatch, List<SoldBatch> soldOut)
    {
        var draws = new List<(string? Batch, DateOnly? Expiry, decimal Quantity, decimal UnitCost)>();
        var remaining = quantity;
        var candidates = soldOut.Where(s => s.ProductId == product.Id && s.Out > 0m);
        if (!string.IsNullOrWhiteSpace(namedBatch))
        {
            var named = candidates.FirstOrDefault(s => s.BatchNumber == namedBatch);
            if (named is null || named.Out < quantity)
                throw new AccountingException(
                    $"لا يبقى من الدفعة '{namedBatch}' من '{product.NameAr}' المبيعة في هذه الفاتورة ما يستوعب مرتجعاً بـ{quantity:N2}.");
            candidates = [named];
        }

        foreach (var row in candidates)
        {
            if (remaining <= 0m) break;
            if (row.BatchNumber is null)
                throw new AccountingException(
                    $"بيع '{product.NameAr}' في هذه الفاتورة قبل أن يُتتبَّع بالدفعة — فلا يُعرف إلى أيّ دفعةٍ يعود المرتجع.");
            var take = Math.Min(remaining, row.Out);
            draws.Add((row.BatchNumber, row.ExpiryDate, take, row.UnitCost));
            row.Out -= take;
            remaining -= take;
        }

        if (remaining > 0m)
            throw new AccountingException(
                $"لا يبقى من دفعات '{product.NameAr}' المبيعة في هذه الفاتورة ما يستوعب مرتجعاً بـ{quantity:N2}.");
        return draws;
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
            // (CEEF-229 · م2-١٥أ) المرتجع يخرج من **الحساب الذي دخلت فيه البيعة**.
            //
            // كان يُعيد 1102 (الأب) بينما البيعة تنزل على ابن البنك (110201) عبر
            // BankGlAccountResolver — فبيعةُ مدى ومرتجعُها لا يُلغي أحدهما الآخر: يبقى
            // الابنُ منتفخاً والأبُ سالباً بالمقدار نفسه إلى الأبد. والتسويةُ البنكية تقرأ
            // الابن وحده فتُقارَن بكشفٍ لا يحوي المرتجع، فيظهر فرقٌ دائم لا يُغلق.
            //
            // ولا تكشفه هوية التدفقات النقدية لأنها تُطابق النقد بالبادئة "1102" فتضمّ
            // الأب وابنه معاً: المجموع صفرٌ سليماً كان أو منشقّاً — والخلل في التوزيع.
            //
            // وهذا نصفُ إصلاحٍ من م2-١٥: هُوجر الأبُ في المبيعات وسندَي القبض والصرف
            // ونُسي المرتجع، والتعليقُ في SalesInvoicePostingService يصف هذا العطل بعينه.
            if (method == Domain.Retail.PosPaymentMethod.Card)
                return await BankGlAccountResolver.ResolveAsync(db, note.CompanyId, ct: ct);

            return await SystemAccountAsync(note.CompanyId, SaudiChartOfAccounts.SystemCodes.CashOnHand);
        }

        return await SystemAccountAsync(note.CompanyId, SaudiChartOfAccounts.SystemCodes.AccountsReceivable);
    }

    private async Task<Account> SystemAccountAsync(long companyId, string code) =>
        await db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == code);
}
