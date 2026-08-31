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
            InventoryService.EnsureTradable(products, "شراء");
            decimal inventorySubtotal = 0;
            var receiptLines = new List<(long ProductId, decimal Qty, decimal UnitCost)>();

            // (CEEF-229 · م1-١٩) **مع إذنِ استلامٍ لا يُمَسّ 1107 إطلاقاً.**
            //
            // وجودُ الإذن يمنع الحركةَ المخزنية، فمتوسطُ التكلفة لا يُعاد حسابه — وأيُّ ريالٍ
            // يُضاف إلى 1107 هنا بلا مقابلٍ في الدفتر المساعد **يكسر الهوية هـ-٣ كسراً دائماً**.
            // فالاستحقاقُ يُقفَل بمقدار ما استُلم، والفرقُ يذهب مصروفاً.
            decimal grniRelief = 0m;
            decimal priceVariance = 0m;
            var openAccrual = hasPostedGrn
                ? await OpenAccrualByProductAsync(invoice, ct)
                : [];

            foreach (var line in invoice.Lines)
            {
                if (line.ProductId is not long pid
                    || !products.TryGetValue(pid, out var product) || !product.IsStockable())
                    continue;

                if (!hasPostedGrn)
                {
                    inventorySubtotal += line.LineTotal;
                    receiptLines.Add((pid, line.Quantity, line.UnitPrice));
                    continue;
                }

                // (CEEF-229 · م1-٢٠) ‏الاستهلاكُ بالكمية، والقيمةُ المُحرَّرة تتبعها.
                // ‏والفرقُ = ما تقوله الفاتورة ناقصَ ما حُرّر — **موجباً أو سالباً**:
                // ‏موجبٌ حين تُفوتَر البضاعةُ بأغلى ممّا استُلمت، وسالبٌ حين بأرخص.
                // ‏وكان يُحسب بالقيمة فيصير صفراً في حالة الانخفاض، فيبقى الفرقُ التزاماً
                // ‏وهمياً في 2112 لا يُقفله شيءٌ أبداً.
                var relief = openAccrual.TryGetValue(pid, out var accrual)
                    ? accrual.Consume(line.Quantity)
                    : 0m;

                grniRelief += relief;
                priceVariance += line.LineTotal - relief;
            }

            var purchases = await SystemAccountAsync(invoice.CompanyId, "5102");
            var inventoryAccount = inventorySubtotal > 0
                ? await SystemAccountAsync(invoice.CompanyId, SaudiChartOfAccounts.SystemCodes.Inventory)
                : null;
            var vatInput = await SystemAccountAsync(invoice.CompanyId, SaudiChartOfAccounts.SystemCodes.VatInput);
            var payable = await SystemAccountAsync(invoice.CompanyId, SaudiChartOfAccounts.SystemCodes.AccountsPayable);

            var invoiceNumber = await DocumentNumberGenerator.NextAsync(db, 
                db.PurchaseInvoices.Where(i => i.CompanyId == invoice.CompanyId).Select(i => i.InvoiceNumber),
                "PI", invoice.InvoiceDate.Year, invoice.CompanyId);

            var grniAccount = grniRelief > 0
                ? await PurchaseAccountProvisioning.GoodsReceivedNotInvoicedAsync(db, invoice.CompanyId, ct)
                : null;
            var varianceAccount = priceVariance != 0m
                ? await PurchaseAccountProvisioning.PriceVarianceAsync(db, invoice.CompanyId, ct)
                : null;

            var entry = invoice.Post(invoiceNumber, purchases, vatInput, payable,
                inventorySubtotal, inventoryAccount, grniRelief, grniAccount, priceVariance, varianceAccount);
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

    /// <summary>
    /// (CEEF-229 · م1-٢٠ / I-8) الاستحقاقُ المفتوح لكلّ صنف — <b>كمّيةً وقيمةً معاً</b>:
    /// ما استُلم ناقصَ ما <b>فوترته</b> فواتيرُ سابقة على أمر الشراء نفسه.
    ///
    /// <para><b>وكان يُتابَع بالقيمة وحدها، فيُخطئ متى اختلف سعرُ الفاتورة عن سعر الاستلام.</b>
    /// الطرحُ كان <c>Σ(كمية الفاتورة × سعرها)</c> — أيْ ما <b>دفعته</b> الفاتورة لا ما
    /// <b>استهلكته</b> من البضاعة. فالبضاعةُ تخرج من الاستحقاق بكمّيتها، والمالُ يخرج بسعره،
    /// والاثنان لا يتساويان.</para>
    ///
    /// <para><b>مقيسٌ:</b> استُلمت 10 بـ10 (استحقاق 100) وفُوتِرت 10 بـ9.9 ⟵ <b>يبقى ريالٌ
    /// في 2112 إلى الأبد</b>. وجزئياً: 4 من 10 بسعر 9.9 ⟵ المتبقّي <c>60.40</c> لا 60،
    /// <b>فينحرف الرصيدُ فاتورةً بعد فاتورة</b>.</para>
    ///
    /// <para><b>وحدودُ الأثر مقيسة</b>: المطابقةُ الثلاثية ترفض انحرافاً يتجاوز ٢٪ وترفض
    /// فوترةَ أكثرَ ممّا استُلم. فالخطأُ محصورٌ في التسامح — <b>ريالٌ أو ريالان على كلّ أمر
    /// شراء، لكنّهما لا يُقفلان أبداً ويتراكمان</b>. والتسامحُ معاملٌ قابلٌ للرفع.</para>
    /// </summary>
    private async Task<Dictionary<long, AccrualState>> OpenAccrualByProductAsync(
        PurchaseInvoice invoice, CancellationToken ct)
    {
        var poId = invoice.PurchaseOrderId!.Value;

        var received = await db.GoodsReceipts
            .Where(g => g.CompanyId == invoice.CompanyId
                        && g.PurchaseOrderId == poId
                        && g.Status == GoodsReceiptStatus.Posted)
            .SelectMany(g => g.Lines)
            .Where(l => l.ProductId != null)
            .Select(l => new { ProductId = l.ProductId!.Value, l.ReceivedQuantity, l.UnitCost })
            .ToListAsync(ct);

        var invoicedQuantity = (await db.PurchaseInvoices
                .Where(i => i.CompanyId == invoice.CompanyId
                            && i.PurchaseOrderId == poId
                            && i.Status == PurchaseInvoiceStatus.Posted)
                .SelectMany(i => i.Lines)
                .Where(l => l.ProductId != null)
                .Select(l => new { ProductId = l.ProductId!.Value, l.Quantity })
                .ToListAsync(ct))
            .GroupBy(l => l.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

        var open = new Dictionary<long, AccrualState>();
        foreach (var group in received.GroupBy(l => l.ProductId))
        {
            var receivedQuantity = group.Sum(l => l.ReceivedQuantity);
            if (receivedQuantity <= 0) continue;

            var receivedValue = group.Sum(l =>
                Math.Round(l.ReceivedQuantity * l.UnitCost, 2, MidpointRounding.AwayFromZero));

            var consumed = invoicedQuantity.GetValueOrDefault(group.Key);
            var remaining = Math.Max(0m, receivedQuantity - consumed);

            // ‏القيمةُ المتبقّية تُشتقّ من الكمية المتبقّية بمتوسط سعر الاستلام — فما خرج
            // ‏من البضاعة خرج من قيمتها بالنسبة نفسِها، مهما اختلف سعرُ الفاتورة.
            open[group.Key] = new AccrualState(
                remaining,
                Math.Round(receivedValue * remaining / receivedQuantity, 2, MidpointRounding.AwayFromZero));
        }

        return open;
    }

    /// <summary>ما بقي من استحقاق صنفٍ: كمّيةً وقيمةً. والقيمةُ تتبع الكمية لا سعرَ الفاتورة.</summary>
    private sealed class AccrualState(decimal quantity, decimal value)
    {
        public decimal Quantity { get; private set; } = quantity;
        public decimal Value { get; private set; } = value;

        /// <summary>
        /// ‏يستهلك من الاستحقاق ما يقابل <paramref name="quantityInvoiced"/>، ويُعيد القيمةَ
        /// المُحرَّرة.
        ///
        /// <para><b>وآخرُ ما بقي يُحرَّر كاملاً</b> لا بحاصل الضرب: التقريبُ إلى هللتين على
        /// كلّ فاتورةٍ يترك كسراً، وكسرٌ في حساب استحقاقٍ لا يُقفله شيءٌ بعده. فالشرطُ
        /// يضمن الهوية <b>بالبناء</b>: فُوتِر كلُّ ما استُلم ⟵ 2112 صفرٌ تماماً.</para>
        /// </summary>
        public decimal Consume(decimal quantityInvoiced)
        {
            var consumed = Math.Min(quantityInvoiced, Quantity);
            if (consumed <= 0) return 0m;

            var released = consumed == Quantity
                ? Value
                : Math.Round(consumed * Value / Quantity, 2, MidpointRounding.AwayFromZero);

            Quantity -= consumed;
            Value -= released;
            return released;
        }
    }

    private async Task<Account> SystemAccountAsync(long companyId, string code) =>
        await db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == code);
}
