using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Platform;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Security;
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
    public async Task<DebitNote> PostAsync(DebitNote note, CancellationToken ct = default, bool acceptBelowCost = false)
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
            // ‏(جاهزيّةُ الإطلاق · ح-١٠٧ · ‏`found#7`) ‏**وعملةُ الإشعار عملةُ فاتورته وسعرُها** — وإلّا قورنت قيمةٌ بالدولار بطبقاتٍ بالريال.
            if (note.CurrencyCode != invoice.CurrencyCode || note.ExchangeRate != invoice.ExchangeRate)
                throw new AccountingException(
                    $"عملةُ الإشعار ({note.CurrencyCode} @ {note.ExchangeRate}) لا تطابق فاتورته ({invoice.CurrencyCode} @ {invoice.ExchangeRate}).");

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

            // (جاهزيّةُ الإطلاق · ح-٣١) ‏**الرقمُ يُولَد قبل أن يُستعمل مفتاحاً.**
            //
            // ‏كان توليدُه بعد حلقةِ استهلاكِ الطبقات، والحلقةُ تُمرّر
            // ‏`$"DebitNote:{note.NoteNumber}"` — **و`NoteNumber` عدمٌ وقتَها**، لا يُسنَد
            // ‏إلّا داخل `DebitNote.Post`. **والاستيفاءُ يُصيّر العدمَ فراغاً ولا يرمي**،
            // ‏فيخرج المفتاحُ `DebitNote:` — **واحداً لكلّ إشعارٍ تُصدرُه المنشأةُ ما بقيت**،
            // ‏بينما الحركةُ المخزنيّةُ أسفلَ تُرحّل تحت `DebitNote:{الرقم المولّد}`.
            //
            // ‏**فلا يلتقي دفترُ الاستهلاك بالحركة، ولا يجد `RestoreConsumedLayersAsync`
            // ‏شيئاً يُرمّمُه.** **وأشدُّ من ذلك**: مفتاحٌ فارغٌ مشترَكٌ ليس «مفتاحاً
            // ‏ناقصاً» بل **مفتاحُ مستندٍ آخر** — والترميمُ به يردُّ طبقاتَ إشعارٍ سواه.
            var noteNumber = await DocumentNumberGenerator.NextAsync(db, 
                db.DebitNotes.Where(n => n.CompanyId == note.CompanyId).Select(n => n.NoteNumber),
                "DN", note.NoteDate.Year, note.CompanyId);

            var inv = new InventoryService(db);
            var products = await inv.LoadProductsForLinesAsync(note.CompanyId, note.Lines.Select(l => l.ProductId));
            decimal inventoryReduction = 0;
            var issueLines = new List<(long WarehouseId, long ProductId, decimal Qty, decimal UnitCost, string? BatchNumber, DateOnly? ExpiryDate)>();
            var costing = new InventoryCostingService(db);
            var defaultWarehouse = await inv.GetDefaultWarehouseAsync(note.CompanyId);
            // ‏(جاهزيّةُ الإطلاق · ح-١٠٥ · ‏`order-to-cash-procure-to-pay-inventory#7`) ‏**المتتبَّعُ يخرج من الدفعات التي دخل بها.**
            // ‏وكان سطرُ المرتجع بلا هويّة، والمَخنَقُ يرفض كلَّ سطرٍ متتبَّعٍ بلاها — فلا يُرجَع دواءٌ ولا غذاءٌ متتبَّعٌ إلى مورّده أبداً.
            var received = await ReceivedBatchesStillHeldAsync(invoice, products, token);
            // ‏(جاهزيّةُ الإطلاق · ح-١١١ · ‏`order-to-cash-procure-to-pay-inventory#12`) ‏**المرتجعُ يخرج من المستودع الذي دخلت إليه بضاعةُ الفاتورة** —
            // ‏المتتبَّعُ من مستودع دفعته، وغيرُ المتتبَّع من أوّل مستودعٍ استلمه، وإلا الافتراضيّ. وتغييرُ الافتراضيّ بعد الشراء لا يُحوِّله.
            var receiptWarehouses = await ReceiptWarehousesAsync(invoice, token);
            // ‏(ح-١٠٧) ‏**وتحت FIFO تُستهلك طبقاتُ هذه الفاتورة وإذوناتِها أوّلاً** — وكانت أقدمُ طبقات المستودع، فيُقاس فرقُ سعرٍ لم يقع.
            var receiptSources = await ReceiptSourcesAsync(invoice, token);

            foreach (var line in note.Lines)
            {
                if (line.ProductId is long pid && products.TryGetValue(pid, out var p) && p.IsStockable())
                {
                    // ‏**وتُحسم الدفعاتُ قبل استهلاك الطبقات** — فمرتجعٌ يُرفض لا يمسّ التكلفة.
                    var draws = p.IsTracked
                        ? DrawReturnedBatches(p, line.Quantity, line.BatchNumber, received)
                        : null;
                    // ‏**استهلاكٌ لكلّ مستودعٍ يخرج منه السطر** (واحدٌ لغير المتتبَّع) — ومنطقُ ح-١٠٧ كما هو داخل كلٍّ منها.
                    var perWarehouse = draws is null
                        ? [(WarehouseId: receiptWarehouses.GetValueOrDefault(pid, defaultWarehouse.Id), Quantity: line.Quantity)]
                        : draws.GroupBy(d => d.WarehouseId).Select(g => (WarehouseId: g.Key, Quantity: g.Sum(d => d.Quantity))).ToList();
                    foreach (var (whId, whQty) in perWarehouse)
                    {
                        var (total, unit) = await costing.ConsumeForIssueAsync(
                            note.CompanyId, pid, whId, whQty,
                            $"DebitNote:{noteNumber}", token, preferredSources: receiptSources);
                        inventoryReduction += total;
                        if (draws is null)
                            issueLines.Add((whId, pid, whQty, unit, null, null));
                        else
                            foreach (var draw in draws.Where(d => d.WarehouseId == whId))
                                issueLines.Add((whId, pid, draw.Quantity, unit, draw.Batch, draw.Expiry));
                    }
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

            // (جاهزيّةُ الإطلاق · ح-١٠٧) ‏**المرتجعُ المُسعَّر دون التكلفة يحتاج حسابَ الفرق** — يُوفَّر
            // ‏عند الحاجة فقط كي لا يُنشأ لمن لا يستعمله.
            note.RecalculateTotals();
            var credited = invoice.VatDeductible ? note.Subtotal : note.GrandTotal();
            // ‏(ح-١٠٧) ‏**وما دون التكلفة لا يُرحَّل إلا بتأكيدٍ صريح** — فالرفضُ القديمُ كان الحارسَ الوحيدَ على خطأ كتابة السعر،
            // ‏والفرقُ بلا تأكيدٍ خسارةٌ صامتة. والرسالةُ تُسمّي المبالغَ الثلاثة كي يُقرَّر بها.
            if (inventoryReduction > credited && !acceptBelowCost)
                throw new AccountingException(
                    $"يُدائَن المرتجعُ بـ{credited:N2} وتكلفتُه {inventoryReduction:N2} — والفرقُ {inventoryReduction - credited:N2} "
                    + "خسارةٌ في «فروق أسعار المشتريات». راجِع السعر، أو أكِّد المرتجعَ دون التكلفة إن كان مقصوداً.");
            // ‏(جاهزيّةُ الإطلاق · found#7 · قرارُ المالك 2026-10-18) ‏**والتأكيدُ بالخسارة للمدير** — وكان يملكه كلُّ مرحِّل، فالحارسُ
            // ‏الوحيدُ على خطأ كتابة السعر يُرفع بخانةٍ يضعها من أخطأ. ولا دورَ «مدير قسم» في المنتج: المديرُ هو مديرُ النظام.
            if (inventoryReduction > credited && SecurityContext.CurrentRole != UserRole.Administrator)
                throw new AccountingException(
                    $"المرتجعُ دون التكلفة بخسارة {inventoryReduction - credited:N2} يتطلّب تأكيدَ مدير النظام.");
            var variance = inventoryReduction > credited
                ? await PurchaseAccountProvisioning.PriceVarianceAsync(db, note.CompanyId, token)
                : null;

            var entry = note.Post(noteNumber, purchases, vatInput, payable, inventoryReduction, inventory, purchases,
                invoice.VatDeductible, variance);
            var entryNumber = await new JournalEntryNumberGenerator(db).NextAsync(note.CompanyId, note.NoteDate, token);
            entry.Post(entryNumber);

            db.JournalEntries.Add(entry);
            // (CEEF-230 · ط2-٠) ‏يُضاف إن لم يكن محفوظاً — واصطلاحُ `JournalPostingService` نفسُه.
            // ‏وكان الإضافةُ بلا شرطٍ في تسع خدمات، **وأسقطت شاشةَ الرواتب بـ`23505`** حين
            // ‏حفظت مسودّتَها ثمّ رحّلتها. والشرطُ يجعل المسارين مقبولين معاً، **فيُغلق
            // ‏صنفُ العطل لا حالتُه**: شاشةٌ تُكتب غداً بالاصطلاح الآخر لا تسقط.
            if (note.Id == 0)
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
                // ‏حركةٌ لكلّ مستودع، بمفتاح المستند نفسِه — والإلغاءُ يعكس كلَّ حركاته.
                foreach (var group in issueLines.GroupBy(l => l.WarehouseId))
                {
                    await inv.PostDocumentMovementAsync(note.CompanyId, group.Key,
                        StockMovementType.Issue, note.NoteDate,
                        $"صرف من إشعار مدين {noteNumber}", $"DebitNote:{noteNumber}",
                        group.Select(l => (l.ProductId, l.Qty, l.UnitCost, l.BatchNumber, l.ExpiryDate)).ToList(),
                        // ‏**والمنتهيةُ تعود إلى مورّدها** — وهو أشيعُ مرتجعات الصيدليّة؛ والبيعُ يبقى يرفضها.
                        expiredMayLeave: true);
                }
                await db.SaveChangesAsync(token);
            }

            return note;
        }, ct);
    }

    /// <summary>‏مصادرُ ما دخل لهذه الفاتورة: إدخالُها نفسُه وإذونُ استلام أمر الشراء المرحَّلة.</summary>
    private async Task<List<string>> ReceiptSourcesAsync(PurchaseInvoice invoice, CancellationToken ct)
    {
        var receiptSources = new List<string>();
        if (!string.IsNullOrWhiteSpace(invoice.InvoiceNumber))
            receiptSources.Add($"PurchaseInvoice:{invoice.InvoiceNumber}");
        if (invoice.PurchaseOrderId is long poId)
        {
            var receiptNumbers = await db.GoodsReceipts.AsNoTracking()
                .Where(g => g.CompanyId == invoice.CompanyId && g.PurchaseOrderId == poId
                            && g.Status == GoodsReceiptStatus.Posted && g.ReceiptNumber != null)
                .Select(g => g.ReceiptNumber!)
                .ToListAsync(ct);
            receiptSources.AddRange(receiptNumbers.Select(n => $"GoodsReceipt:{n}"));
        }

        return receiptSources;
    }

    /// <summary>
    /// (ح-١١١) <b>المستودعُ الذي دخل إليه كلُّ صنفٍ لهذه الفاتورة</b> — أوّلُ مستودعٍ بترتيب الاستلام؛ وما لم يدخل بحركةٍ
    /// (قبل أن تُسجَّل) لا مفتاحَ له فيرجع الاستدعاءُ إلى الافتراضيّ.
    /// </summary>
    internal async Task<Dictionary<long, long>> ReceiptWarehousesAsync(PurchaseInvoice invoice, CancellationToken ct)
    {
        var sources = await ReceiptSourcesAsync(invoice, ct);
        var rows = await db.StockMovements.AsNoTracking()
            .Where(m => m.CompanyId == invoice.CompanyId && sources.Contains(m.SourceDocument!)
                        && m.Status == StockMovementStatus.Posted && m.Type == StockMovementType.Receipt)
            .SelectMany(m => m.Lines, (m, l) => new { MovementId = m.Id, m.WarehouseId, l.ProductId })
            .OrderBy(x => x.MovementId)
            .ToListAsync(ct);
        var first = new Dictionary<long, long>();
        foreach (var r in rows)
            first.TryAdd(r.ProductId, r.WarehouseId);
        return first;
    }

    /// <summary>‏دفعةٌ استُلمت لفاتورةٍ، وما يصلح إرجاعُه منها الآن.</summary>
    internal sealed class ReceivedBatch(long productId, string? batchNumber, DateOnly? expiryDate, long warehouseId)
    {
        public long ProductId { get; } = productId;
        /// <summary>‏(ح-١١١) المستودعُ الذي دخلت إليه الدفعة — ومنه يخرج مرتجعُها.</summary>
        public long WarehouseId { get; } = warehouseId;
        public string? BatchNumber { get; } = batchNumber;
        public DateOnly? ExpiryDate { get; } = expiryDate;
        public decimal Received { get; private set; }

        /// <summary>‏ما يصلح إرجاعُه: المستلَمُ ناقصَ المُرجَع سلفاً، ومقيَّداً بما بقي ماديّاً في المستودع.</summary>
        public decimal Left { get; set; }

        public void Arrived(decimal quantity) => Received += quantity;
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-١٠٥) <b>ما استُلم من كلّ دفعةٍ ولا يزال يصلح للإرجاع</b> — من سطور حركات الاستلام نفسِها
    /// (إذونُ استلام أمر الشراء الذي تُفوتَر به الفاتورة، وإدخالُ الفاتورة نفسِها)، <b>بترتيب الاستلام</b>، ناقصاً ما أرجعته
    /// الإشعاراتُ المرحَّلة سلفاً على أمر الشراء نفسِه (فالإذنُ يخدم فواتيرَ عدّة)، <b>ثمّ مقيَّداً بما بقي ماديّاً</b> من
    /// الدفعة في المستودع الذي يخرج منه المرتجع — <b>فبعضُها قد بيع</b>، وليس كالمرتجع من العميل الذي يعود فيه ما خرج.
    /// وغيرُ المتتبَّع لا يُقرأ هنا.
    /// </summary>
    internal async Task<List<ReceivedBatch>> ReceivedBatchesStillHeldAsync(PurchaseInvoice invoice,
        IReadOnlyDictionary<long, Product> products, CancellationToken ct)
    {
        var tracked = products.Values.Where(p => p.IsTracked).Select(p => p.Id).ToHashSet();
        var received = new List<ReceivedBatch>();
        if (tracked.Count == 0)
            return received;

        var receiptSources = await ReceiptSourcesAsync(invoice, ct);

        // ‏**وترتيبُ الاستلام مُصرَّحٌ به لا حادث** — السطورُ مملوكةٌ بلا عمود ترتيب، وEF لا يُرتّب السطورَ داخل الحركة،
        // ‏فتُرتَّب هنا بمفتاح الحركة ثمّ بمفتاح السطر الظلّيّ (ووصلٌ مسطّحٌ لأنّ SQLite لا يدعم `APPLY`).
        var receipts = await db.StockMovements.AsNoTracking()
            .Where(m => m.CompanyId == invoice.CompanyId && receiptSources.Contains(m.SourceDocument!)
                        && m.Status == StockMovementStatus.Posted && m.Type == StockMovementType.Receipt)
            .SelectMany(m => m.Lines, (m, l) => new { MovementId = m.Id, m.WarehouseId, LineId = EF.Property<long>(l, "id"), Line = l })
            .OrderBy(x => x.MovementId).ThenBy(x => x.LineId)
            .ToListAsync(ct);
        foreach (var entry in receipts.Where(x => tracked.Contains(x.Line.ProductId)))
        {
            var line = entry.Line;
            var row = received.FirstOrDefault(r => r.ProductId == line.ProductId && r.BatchNumber == line.BatchNumber
                                                   && r.WarehouseId == entry.WarehouseId);
            if (row is null)
                received.Add(row = new ReceivedBatch(line.ProductId, line.BatchNumber, line.ExpiryDate, entry.WarehouseId));
            row.Arrived(line.Quantity);
        }

        var invoiceIds = invoice.PurchaseOrderId is long order
            ? await db.PurchaseInvoices.AsNoTracking()
                .Where(i => i.CompanyId == invoice.CompanyId && i.PurchaseOrderId == order)
                .Select(i => i.Id).ToListAsync(ct)
            : [invoice.Id];
        var returnNumbers = await db.DebitNotes.AsNoTracking()
            .Where(n => n.CompanyId == invoice.CompanyId && invoiceIds.Contains(n.OriginalPurchaseInvoiceId)
                        && n.Status == DebitNoteStatus.Posted && n.NoteNumber != null)
            .Select(n => n.NoteNumber!)
            .ToListAsync(ct);
        var returnSources = returnNumbers.Select(n => $"DebitNote:{n}").ToList();
        var returns = await db.StockMovements.AsNoTracking()
            .Where(m => m.CompanyId == invoice.CompanyId && returnSources.Contains(m.SourceDocument!)
                        && m.Status == StockMovementStatus.Posted && m.Type == StockMovementType.Issue)
            .ToListAsync(ct);
        var returned = returns.SelectMany(m => m.Lines.Select(l => (m.WarehouseId, Line: l)))
            .Where(x => tracked.Contains(x.Line.ProductId))
            .GroupBy(x => (x.Line.ProductId, x.Line.BatchNumber, x.WarehouseId))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Line.Quantity));

        var held = await db.StockBatches.AsNoTracking()
            .Where(b => b.CompanyId == invoice.CompanyId && tracked.Contains(b.ProductId))
            .ToListAsync(ct);
        foreach (var row in received)
        {
            var notReturned = row.Received - returned.GetValueOrDefault((row.ProductId, row.BatchNumber, row.WarehouseId));
            // ‏وما دخل بلا دفعةٍ (قبل التتبّع) يبقى مرشَّحاً بكمّيته كي يُرفض باسمه عند بلوغه، لا أن يُسكَت عنه.
            var onHand = row.BatchNumber is null
                ? notReturned
                : held.Where(b => b.ProductId == row.ProductId && b.BatchNumber == row.BatchNumber
                                && b.WarehouseId == row.WarehouseId).Sum(b => b.Quantity);
            row.Left = Math.Max(0m, Math.Min(notReturned, onHand));
        }

        return received;
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-١٠٥) <b>يُقسّم كمّيةَ المرتجع على الدفعات المستلَمة</b> بترتيب الاستلام، وينقص ما خرج منها فلا
    /// يخرج من دفعةٍ أكثرُ ممّا بقي فيها ولو تكرّر الصنفُ في الإشعار. <b>والدفعةُ التي يسمّيها السطرُ يخرج منها وحدَها.</b>
    /// <b>وما استُلم قبل أن يُتتبَّع الصنفُ لا تُعرف دفعتُه — فيُرفض باسمه</b> ولا يُخمَّن. وما لا تغطّيه الدفعاتُ الباقيةُ
    /// يُرفض باسمه كذلك.
    /// </summary>
    private static List<(string? Batch, DateOnly? Expiry, decimal Quantity, long WarehouseId)> DrawReturnedBatches(
        Product product, decimal quantity, string? namedBatch, List<ReceivedBatch> received)
    {
        var draws = new List<(string? Batch, DateOnly? Expiry, decimal Quantity, long WarehouseId)>();
        var remaining = quantity;
        var candidates = received.Where(r => r.ProductId == product.Id && r.Left > 0m);
        if (!string.IsNullOrWhiteSpace(namedBatch))
        {
            // ‏(ح-١١١) ‏**والمسمّاةُ تُؤخذ من كلّ مستودعٍ استُلمت فيه** — وكان أوّلُ صفوفها وحدَه، فلا يُرجَع منها فوق ما في مستودعها الأوّل.
            var named = candidates.Where(r => r.BatchNumber == namedBatch).ToList();
            if (named.Sum(r => r.Left) < quantity)
                throw new AccountingException(
                    $"لا يبقى من الدفعة '{namedBatch}' من '{product.NameAr}' المستلَمة في هذه الفاتورة ما يستوعب مرتجعاً بـ{quantity:N2}.");
            candidates = named;
        }

        // ‏**وصفُّ ما قبل التتبّع لا يحجب ما تغطّيه الدفعات** — يُتخطّى، ولا يُرفض باسمه إلا إن بقي ما لا تغطّيه؛
        // ‏فالتتبّعُ لا يُفعَّل إلا والرصيدُ صفر، وما دخل قبله لا يكون قائماً.
        var pretracked = false;
        foreach (var row in candidates)
        {
            if (remaining <= 0m) break;
            if (row.BatchNumber is null)
            {
                pretracked = true;
                continue;
            }
            var take = Math.Min(remaining, row.Left);
            draws.Add((row.BatchNumber, row.ExpiryDate, take, row.WarehouseId));
            row.Left -= take;
            remaining -= take;
        }

        if (remaining > 0m && pretracked)
            throw new AccountingException(
                $"استُلم '{product.NameAr}' لهذه الفاتورة قبل أن يُتتبَّع بالدفعة — فلا يُعرف من أيّ دفعةٍ يخرج المرتجع.");
        if (remaining > 0m)
            throw new AccountingException(
                $"لا يبقى من دفعات '{product.NameAr}' المستلَمة في هذه الفاتورة ما يستوعب مرتجعاً بـ{quantity:N2}.");
        return draws;
    }
}
