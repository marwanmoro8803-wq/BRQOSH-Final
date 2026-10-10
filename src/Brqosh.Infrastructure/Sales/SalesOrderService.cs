using Brqosh.Domain.Inventory;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Sales;

public class SalesOrderService(BrqoshDbContext db)
{
    public async Task<SalesOrder> ConfirmAsync(SalesOrder order, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPost();
        if (order.Lines.Count == 0)
            throw new Domain.Accounting.AccountingException("أضف سطراً واحداً على الأقل.");

        var number = await DocumentNumberGenerator.NextAsync(db, 
            db.SalesOrders.Where(o => o.CompanyId == order.CompanyId).Select(o => o.OrderNumber),
            "SO", order.OrderDate.Year, order.CompanyId);

        order.Confirm(number);
        db.SalesOrders.Add(order);
        await db.SaveChangesAsync(ct);
        return order;
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٣٣) <b>الفاتورةُ تُؤرّخ يومَ صدورها لا يومَ الأمر،
    /// وتقع هي ووسمُ الأمر في معاملةٍ واحدة.</b>
    ///
    /// <para><b>وتاريخُ الفاتورة حقلٌ ضريبيٌّ لا وصفٌ</b>: تقع به في إقرارٍ
    /// دون إقرار، <b>وتأريخٌ إلى الوراء يُدخل توريداً في فترةٍ قد تكون
    /// قُدِّمت إقرارُها</b>. وكان يُؤخذ <c>order.OrderDate</c> — <b>وأمرُ البيع ليس
    /// توريداً</b>، وقد يسبق الفاتورةَ بشهور.
    ///
    /// <para><b>والتوأمُ يفعلُها صواباً</b>: <c>PurchaseOrderService.CreateInvoiceFromOrderAsync</c>
    /// يأخذ <c>invoiceDate</c> وسيطاً <b>ويلُفّ في <c>UnitOfWork.ExecuteInTransactionAsync</c></b>.
    /// <b>فهذا نقلُ نمطٍ قائمٍ لا اختراع.</b></para>
    ///
    /// <para><b>والمعاملةُ ليست زينةً هنا</b>: كان الترحيلُ يقع ثمّ يُحفظ وسمُ
    /// الأمر <b>عمليّتين منفصلتين</b>. فإن سقطت الثانيةُ <b>بقيت فاتورةٌ
    /// ضريبيّةٌ مُرحَّلةٌ والأمرُ يقرأ «غيرَ مُفوتر»</b> — <b>فيُفوتَر ثانيةً
    /// فتصدر فاتورتان ضريبيّتان عن توريدٍ واحد</b>.
    /// <b>ومسارُ التراجُع لا يُفرِّقُه اختبارٌ هنا</b> — يلزمُه حقنُ عطلٍ،
    /// <b>وذلك يُقال ولا يُدّعى خلافُه</b>.</para>
    /// </summary>
    public async Task<SalesInvoice> CreateInvoiceFromOrderAsync(long companyId, long orderId,
        DateOnly invoiceDate, long? warehouseId = null, CancellationToken ct = default) =>
        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            SecurityContext.EnsureCanPost();
            var order = await db.SalesOrders
                .SingleAsync(o => o.CompanyId == companyId && o.Id == orderId, token);

            if (order.Status != SalesOrderStatus.Confirmed)
                throw new Domain.Accounting.AccountingException("يُنشأ الفاتورة من أمر بيع مؤكد فقط.");

            var invoice = SalesInvoice.CreateDraft(companyId, order.CustomerId, invoiceDate);
            // ‏(جاهزيّةُ الإطلاق · ح-١١٠ · ‏`order-to-cash-procure-to-pay-inventory#12`) ‏**الفاتورةُ تُصرف من المستودع المختار** (والفارغُ
            // ‏الافتراضيّ)، **وتخصيصُ الدفعات أدناه من المستودع نفسِه** — وإلا خُصِّصت دفعةٌ من مستودعٍ غير الذي يُنقَص.
            invoice.SetWarehouse(warehouseId);

            // ‏(جاهزيّةُ الإطلاق · ح-١٠٤ · `order-to-cash-procure-to-pay-inventory#7`) ‏**الصنفُ المتتبَّعُ يُخصَّص له الأقدمُ
            // ‏انتهاءً عند التحويل** — وكان السطرُ يُبنى بلا دفعةٍ فيرفضه المَخنَقُ، فلا يُفوتَر دواءٌ ولا غذاءٌ من أمرِ بيع.
            // ‏**والتخصيصُ لكلّ صنفٍ مرّةً بمجموع كمّياته** ثمّ يُوزَّع على سطوره بالترتيب — فسطران لصنفٍ واحدٍ لا يسحبان الدفعةَ مرّتين.
            // ‏ونقصُ الساري يرميه المُخصِّصُ نفسُه باسم الصنف ولا يُلتقط: فلا فاتورةَ ولا وسمَ على الأمر (المعاملةُ تتراجع).
            // ‏**والمرشِّحُ مرشِّحُ الترحيل نفسُه** (`IsStockable() && IsTracked`) — فخدمةٌ موسومةٌ بالتتبّع لا تُخصَّص ولا تُرفض.
            var inv = new InventoryService(db);
            var products = await inv.LoadProductsForLinesAsync(companyId, order.Lines.Select(l => l.ProductId));
            var trackedIds = order.Lines
                .Where(l => l.ProductId is long pid && products.TryGetValue(pid, out var p) && p.IsStockable() && p.IsTracked)
                .Select(l => l.ProductId!.Value).Distinct().ToList();
            var draws = new Dictionary<long, List<BatchDraw>>();
            if (trackedIds.Count > 0)
            {
                // ‏المستودعُ هو الذي يصرف منه `SalesInvoicePostingService` — وإلا خُصِّصت دفعةٌ من مستودعٍ غير الذي يُنقَص.
                var warehouse = warehouseId is long chosen
                    ? await db.Warehouses.SingleOrDefaultAsync(w => w.CompanyId == companyId && w.Id == chosen, token)
                        ?? throw new Domain.Accounting.AccountingException("المستودع المختار لهذه الفاتورة غير موجود في المنشأة.")
                    : await inv.GetDefaultWarehouseAsync(companyId);
                var allocator = new BatchAllocationService(db);
                foreach (var trackedId in trackedIds)
                {
                    var wanted = order.Lines.Where(l => l.ProductId == trackedId).Sum(l => l.Quantity);
                    draws[trackedId] = (await allocator.AllocateAsync(
                        companyId, trackedId, warehouse.Id, wanted, invoiceDate, token)).ToList();
                }
            }

            // ‏(م-٥ · ن-٥٢) الثلاثيُّ الضريبيُّ يعبر التحويل — انظر `PurchaseOrderService`.
            foreach (var line in order.Lines)
            {
                if (line.ProductId is not long productId || !draws.TryGetValue(productId, out var available))
                {
                    invoice.AddLine(line.Description, line.Quantity, line.UnitPrice, line.ProductId,
                        line.VatCategory, line.TaxExemptionReasonCode, line.TaxExemptionReason);
                    continue;
                }

                // ‏سطرٌ للفاتورة لكلّ دفعةٍ يسحب منها هذا السطر، وما بقي من دفعةٍ يُترك لسطر الأمر التالي.
                var remaining = line.Quantity;
                while (remaining > 0m)
                {
                    // ‏المُخصِّصُ يقبل نقصاً دون ٠٫٠٠٠١ — فيُرفض هنا باسم الصنف لا بخطأ فهرسٍ خامّ.
                    if (available.Count == 0)
                        throw new Domain.Accounting.AccountingException(
                            $"لا يبقى من دفعات '{products[productId].NameAr}' السارية ما يستوعب أمرَ البيع.");
                    var draw = available[0];
                    var take = Math.Min(remaining, draw.Quantity);
                    invoice.AddLine(line.Description, take, line.UnitPrice, line.ProductId,
                        line.VatCategory, line.TaxExemptionReasonCode, line.TaxExemptionReason,
                        batchNumber: draw.BatchNumber);
                    remaining -= take;
                    if (take >= draw.Quantity) available.RemoveAt(0);
                    else available[0] = draw with { Quantity = draw.Quantity - take };
                }
            }

            await new SalesInvoicePostingService(db).PostAsync(invoice);
            order.MarkInvoiced(invoice.Id);
            await db.SaveChangesAsync(token);
            return invoice;
        });

    public async Task<SalesOrder> CreateFromQuotationAsync(long companyId, long quotationId, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPost();
        var quote = await db.SalesQuotations
            .SingleAsync(q => q.CompanyId == companyId && q.Id == quotationId, ct);

        if (quote.Status is not (SalesQuotationStatus.Sent or SalesQuotationStatus.Accepted))
            throw new Domain.Accounting.AccountingException("يُحوَّل عرض سعر مُرسَل أو مقبول فقط.");
        // ‏(ح-١١٥) ‏**العرضُ يُحوَّل مرّةً**: نقرتان على «تحويل» كانتا أمرَين مؤكَّدَين يُفوتَران مرّتين.
        // ‏والأمرُ الملغى لا يُحتسب — فيُعاد التحويلُ بعده.
        if (await db.SalesOrders.AnyAsync(o => o.CompanyId == companyId && o.SalesQuotationId == quotationId
                && o.Status != SalesOrderStatus.Cancelled, ct))
            throw new Domain.Accounting.AccountingException("حُوِّل عرض السعر هذا إلى أمر بيع سلفاً.");

        var order = SalesOrder.CreateDraft(companyId, quote.CustomerId, DateOnly.FromDateTime(DateTime.Today), quotationId);
        // ‏(م-٥ · ن-٥٢) **وهذا الموضعُ صار حيّاً بهذا التغيير نفسِه**: عرضُ السعر لم يكن
        // ‏يقبل فئةً ضريبيّةً أصلاً، فلم يكن ثَمّ ما يسقط. ومتى قبِلها وجب أن تعبر — وإلّا
        // ‏خُرق مبدأُ سجلّ السطر: «ولا تُتاح إلا حيث تُحوَّل».
        foreach (var line in quote.Lines)
            order.AddLine(line.Description, line.Quantity, line.UnitPrice, line.ProductId,
                line.VatCategory, line.TaxExemptionReasonCode, line.TaxExemptionReason);

        return await ConfirmAsync(order, ct);
    }
}
