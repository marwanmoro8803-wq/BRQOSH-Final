using Brqosh.Domain.Inventory;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Sales;

public class SalesOrderService(BrqoshDbContext db) : Brqosh.Application.Sales.ISalesOrderService
{
    public async Task<SalesOrder> ConfirmAsync(SalesOrder order, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPost();
        if (order.Lines.Count == 0)
            throw new Domain.Accounting.AccountingException("أضف سطراً واحداً على الأقل.");

        var number = await DocumentNumberGenerator.NextAsync(db, 
            db.SalesOrders.Where(o => o.CompanyId == order.CompanyId).Select(o => o.OrderNumber),
            "SO", order.OrderDate, order.CompanyId);

        order.Confirm(number);
        db.SalesOrders.Add(order);
        await db.SaveChangesAsync(ct);

        // ‏حجزُ المخزون عند التأكيد — للأصناف المخزنيّة وحدها، سطرٌ لكلّ صنف.
        var products = await new InventoryService(db).LoadProductsForLinesAsync(order.CompanyId, order.Lines.Select(l => l.ProductId));
        foreach (var g in order.Lines
                     .Where(l => l.ProductId is long pid && products.TryGetValue(pid, out var p) && p.IsStockable())
                     .GroupBy(l => l.ProductId!.Value))
            db.StockReservations.Add(StockReservation.Create(order.CompanyId, order.Id, g.Key, g.Sum(l => l.Quantity)));
        await db.SaveChangesAsync(ct);
        return order;
    }

    /// <summary>‏المسلَّمُ غيرُ المفوتَر لكلّ سطر = ما سلّمته أذونُ التسليم − ما فوترته فواتيرُ «من التسليم» قائمة.</summary>
    internal static async Task<decimal[]> DeliveredUninvoicedAsync(BrqoshDbContext db, long companyId, SalesOrder order, CancellationToken ct)
    {
        var delivered = await db.DeliveryNotes.AsNoTracking()
            .Where(n => n.CompanyId == companyId && n.SalesOrderId == order.Id && n.Status == DeliveryNoteStatus.Posted)
            .SelectMany(n => n.Lines)
            .Select(l => new { l.OrderLineIndex, l.Quantity })
            .ToListAsync(ct);
        var result = new decimal[order.Lines.Count];
        if (delivered.Count == 0) return result;
        var links = await db.SalesOrderInvoiceLines.AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.SalesOrderId == order.Id && !l.IsCancelled)
            .ToListAsync(ct);
        var invoiceIds = links.Select(l => l.SalesInvoiceId).Distinct().ToList();
        var fromDeliveryInvoices = (await db.SalesInvoices.AsNoTracking()
            .Where(i => invoiceIds.Contains(i.Id) && i.StockIssuedByDelivery)
            .Select(i => i.Id).ToListAsync(ct)).ToHashSet();
        for (var i = 0; i < result.Length; i++)
            result[i] = delivered.Where(d => d.OrderLineIndex == i).Sum(d => d.Quantity)
                        - links.Where(l => l.LineIndex == i && fromDeliveryInvoices.Contains(l.SalesInvoiceId)).Sum(l => l.Quantity);
        return result;
    }

    /// <summary>المحجوزُ المفتوح للصنف في المنشأة.</summary>
    public async Task<decimal> ReservedQuantityAsync(long companyId, long productId, CancellationToken ct = default) =>
        (await db.StockReservations.AsNoTracking()
            .Where(r => r.CompanyId == companyId && r.ProductId == productId)
            .Select(r => new { r.Quantity, r.ReleasedQuantity })
            .ToListAsync(ct)).Sum(r => r.Quantity - r.ReleasedQuantity);

    /// <summary>المتاحُ للبيع = الرصيد − المحجوز المفتوح.</summary>
    public async Task<decimal> AvailableToSellAsync(long companyId, long productId, CancellationToken ct = default) =>
        await new InventoryService(db).GetCompanyOnHandAsync(companyId, productId)
        - await ReservedQuantityAsync(companyId, productId, ct);

    /// <summary>إلغاءُ أمر بيعٍ غير مفوتر — ويُفرَج عن حجزه.</summary>
    public async Task CancelAsync(long companyId, long orderId, CancellationToken ct = default) =>
        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            SecurityContext.EnsureCanPost();
            var order = await db.SalesOrders.SingleAsync(o => o.CompanyId == companyId && o.Id == orderId, token);
            order.Cancel();
            foreach (var r in await db.StockReservations.Where(r => r.CompanyId == companyId && r.SalesOrderId == orderId).ToListAsync(token))
                r.Release(r.OpenQuantity);
            await db.SaveChangesAsync(token);
            return 0;
        }, ct);

    /// <summary>
    /// <b>الفاتورةُ تُؤرّخ يومَ صدورها لا يومَ الأمر،
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
    public Task<SalesInvoice> CreateInvoiceFromOrderAsync(long companyId, long orderId,
        DateOnly invoiceDate, long? warehouseId = null, CancellationToken ct = default) =>
        CreatePartialInvoiceFromOrderAsync(companyId, orderId, invoiceDate, null, warehouseId, ct);

    /// <summary>
    /// ‏فاتورةٌ بجزءٍ من الأمر: <paramref name="quantities"/> كمّيةٌ لكلّ رقم سطر (من الصفر)، و<c>null</c> = كلُّ المتبقّي.
    /// ‏ولا تُفوتر كمّيةٌ تزيد على المتبقّي — والمتبقّي = المطلوب − ما فُوتر بفواتيرَ غيرِ ملغاة.
    /// </summary>
    public async Task<SalesInvoice> CreatePartialInvoiceFromOrderAsync(long companyId, long orderId,
        DateOnly invoiceDate, IReadOnlyDictionary<int, decimal>? quantities, long? warehouseId = null,
        CancellationToken ct = default, bool postNow = true) =>
        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            SecurityContext.EnsureCanPost();
            // ‏فاتورتان جزئيّتان متزامنتان لا تقرآن المتبقّي نفسَه.
            await DocumentSequenceLock.AcquireAsync(db.Database, companyId, "SOI", (int)(orderId % int.MaxValue));
            var order = await db.SalesOrders
                .SingleAsync(o => o.CompanyId == companyId && o.Id == orderId, token);

            if (order.Status is not (SalesOrderStatus.Confirmed or SalesOrderStatus.PartInvoiced))
                throw new Domain.Accounting.AccountingException("يُنشأ الفاتورة من أمر بيع مؤكد فقط.");

            var invoicedLines = await db.SalesOrderInvoiceLines.AsNoTracking()
                .Where(l => l.CompanyId == companyId && l.SalesOrderId == orderId && !l.IsCancelled)
                .ToListAsync(token);
            var remaining = order.Lines.Select((l, i) =>
                l.Quantity - invoicedLines.Where(x => x.LineIndex == i).Sum(x => x.Quantity)).ToList();
            // ‏إن سُلِّم شيءٌ لم يُفوتر بعدُ فالفاتورةُ تُفوتره هو — ولا تصرف مخزوناً.
            var deliveredUninvoiced = await DeliveredUninvoicedAsync(db, companyId, order, token);
            var fromDelivery = deliveredUninvoiced.Any(q => q > 0m);
            var cap = fromDelivery ? deliveredUninvoiced : remaining.ToArray();
            var take = new decimal[order.Lines.Count];
            if (quantities is null)
                for (var i = 0; i < take.Length; i++) take[i] = cap[i];
            else
                foreach (var (index, qty) in quantities)
                {
                    if (index < 0 || index >= take.Length)
                        throw new Domain.Accounting.AccountingException($"سطر أمر البيع رقم {index + 1} غير موجود.");
                    if (qty < 0m || qty > cap[index])
                        throw new Domain.Accounting.AccountingException(
                            $"كمية السطر {index + 1} ({qty:N2}) تتجاوز المتبقّي للفوترة ({cap[index]:N2}).");
                    take[index] = qty;
                }
            if (take.All(q => q <= 0m))
                throw new Domain.Accounting.AccountingException("لا كمية متبقّية للفوترة في أمر البيع.");

            var invoice = SalesInvoice.CreateDraft(companyId, order.CustomerId, invoiceDate);
            // ‏ ‏**الفاتورةُ تُصرف من المستودع المختار** (والفارغُ
            // ‏الافتراضيّ)، **وتخصيصُ الدفعات أدناه من المستودع نفسِه** — وإلا خُصِّصت دفعةٌ من مستودعٍ غير الذي يُنقَص.
            invoice.SetWarehouse(warehouseId);
            if (fromDelivery)
                invoice.MarkStockIssuedByDelivery();

            // ‏ ‏**الصنفُ المتتبَّعُ يُخصَّص له الأقدمُ
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
            if (trackedIds.Count > 0 && !fromDelivery)
            {
                // ‏المستودعُ هو الذي يصرف منه `SalesInvoicePostingService` — وإلا خُصِّصت دفعةٌ من مستودعٍ غير الذي يُنقَص.
                var warehouse = warehouseId is long chosen
                    ? await db.Warehouses.SingleOrDefaultAsync(w => w.CompanyId == companyId && w.Id == chosen, token)
                        ?? throw new Domain.Accounting.AccountingException("المستودع المختار لهذه الفاتورة غير موجود في المنشأة.")
                    : await inv.GetDefaultWarehouseAsync(companyId);
                var allocator = new BatchAllocationService(db);
                foreach (var trackedId in trackedIds)
                {
                    var wanted = order.Lines.Select((l, i) => (l, i)).Where(x => x.l.ProductId == trackedId).Sum(x => take[x.i]);
                    draws[trackedId] = (await allocator.AllocateAsync(
                        companyId, trackedId, warehouse.Id, wanted, invoiceDate, token)).ToList();
                }
            }

            // ‏(م-٥ · ن-٥٢) الثلاثيُّ الضريبيُّ يعبر التحويل — انظر `PurchaseOrderService`.
            for (var index = 0; index < order.Lines.Count; index++)
            {
                var line = order.Lines[index];
                if (take[index] <= 0m)
                    continue;
                if (line.ProductId is not long productId || !draws.TryGetValue(productId, out var available))
                {
                    invoice.AddLine(line.Description, take[index], line.UnitPrice, line.ProductId,
                        line.VatCategory, line.TaxExemptionReasonCode, line.TaxExemptionReason,
                        discountPercent: line.DiscountPercent);
                    continue;
                }

                // ‏سطرٌ للفاتورة لكلّ دفعةٍ يسحب منها هذا السطر، وما بقي من دفعةٍ يُترك لسطر الأمر التالي.
                var toDraw = take[index];
                while (toDraw > 0m)
                {
                    // ‏المُخصِّصُ يقبل نقصاً دون ٠٫٠٠٠١ — فيُرفض هنا باسم الصنف لا بخطأ فهرسٍ خامّ.
                    if (available.Count == 0)
                        throw new Domain.Accounting.AccountingException(
                            $"لا يبقى من دفعات '{products[productId].NameAr}' السارية ما يستوعب أمرَ البيع.");
                    var draw = available[0];
                    var drawn = Math.Min(toDraw, draw.Quantity);
                    invoice.AddLine(line.Description, drawn, line.UnitPrice, line.ProductId,
                        line.VatCategory, line.TaxExemptionReasonCode, line.TaxExemptionReason,
                        batchNumber: draw.BatchNumber, discountPercent: line.DiscountPercent);
                    toDraw -= drawn;
                    if (drawn >= draw.Quantity) available.RemoveAt(0);
                    else available[0] = draw with { Quantity = draw.Quantity - drawn };
                }
            }

            // ‏عملةُ الأمر وسعرُه، ونصيبُ الفاتورة من خصم المستند (والفاتورةُ المُكمِلةُ تأخذ الباقي فلا يضيع فرقُ تقريب).
            if (order.CurrencyCode != "SAR" || order.ExchangeRate != 1m)
                invoice.SetCurrency(order.CurrencyCode, order.ExchangeRate);
            if (order.DocumentDiscountAmount > 0m)
            {
                var completes = take.Select((q, i) => remaining[i] - q).All(r => r <= 0m);
                decimal share;
                if (completes)
                {
                    var priorIds = invoicedLines.Select(l => l.SalesInvoiceId).Distinct().ToList();
                    var prior = await db.SalesInvoices.AsNoTracking()
                        .Where(i => priorIds.Contains(i.Id)).SumAsync(i => i.DocumentDiscountAmount, token);
                    share = order.DocumentDiscountAmount - prior;
                }
                else
                {
                    var orderNet = order.Lines.Sum(l => l.LineTotal);
                    var thisNet = order.Lines.Select((l, i) => l.Quantity == 0m ? 0m : take[i] * l.LineTotal / l.Quantity).Sum();
                    share = orderNet == 0m ? 0m
                        : Math.Round(order.DocumentDiscountAmount * thisNet / orderNet, 2, MidpointRounding.AwayFromZero);
                }
                share = Math.Clamp(share, 0m, invoice.Lines.Sum(l => l.LineTotal));
                if (share > 0m)
                    invoice.ApplyDocumentDiscount(share);
            }

            // ‏والكمّيةُ المفوترة تخرج من الحجز — قبل الترحيل، فلا يحجب حجزُ الأمر نفسِه بيعَه.
            var reservations = await db.StockReservations
                .Where(r => r.CompanyId == companyId && r.SalesOrderId == order.Id).ToListAsync(token);
            for (var index = 0; index < take.Length && !fromDelivery; index++)   // ‏المسلَّمُ أُفرج عن حجزه عند التسليم
                if (take[index] > 0m && order.Lines[index].ProductId is long reservedPid)
                {
                    var toRelease = take[index];
                    foreach (var r in reservations.Where(r => r.ProductId == reservedPid))
                        toRelease -= r.Release(toRelease);
                }
            await db.SaveChangesAsync(token);

            // ‏‏`postNow: false` يحفظ الفاتورةَ مسودةً قابلةً للتعديل (خصم/ملاحظات) ثمّ تُرحَّل من مسارها المعتاد.
            if (postNow)
                await new SalesInvoicePostingService(db).PostAsync(invoice);
            else
            {
                db.SalesInvoices.Add(invoice);
                await db.SaveChangesAsync(token);
            }

            for (var index = 0; index < take.Length; index++)
                if (take[index] > 0m)
                    db.SalesOrderInvoiceLines.Add(
                        SalesOrderInvoiceLine.Create(companyId, order.Id, invoice.Id, index, take[index]));
            order.RecordInvoice(invoice.Id, fullyInvoiced: take.Select((q, i) => remaining[i] - q).All(r => r <= 0m));
            await db.SaveChangesAsync(token);
            return invoice;
        });

    /// <summary>يُعلِّم العروضَ المُرسَلة المتجاوزة صلاحيتَها «منتهية» — يُستدعى من مهمة دوريّة أو عند فتح الشاشة.</summary>
    public async Task<int> ExpireOverdueQuotationsAsync(long companyId, DateOnly asOf, CancellationToken ct = default)
    {
        var overdue = await db.SalesQuotations
            .Where(q => q.CompanyId == companyId && q.Status == SalesQuotationStatus.Sent && q.ValidUntil < asOf)
            .ToListAsync(ct);
        foreach (var quote in overdue)
            quote.MarkExpired(asOf);
        if (overdue.Count > 0)
            await db.SaveChangesAsync(ct);
        return overdue.Count;
    }

    /// <param name="orderDate">تاريخُ الأمر (والصلاحيةُ تُقاس به) — اليومُ إن لم يُمرَّر.</param>
    public async Task<SalesOrder> CreateFromQuotationAsync(long companyId, long quotationId,
        DateOnly? orderDate = null, CancellationToken ct = default) =>
        // ‏التحويلُ كلُّه في معاملةٍ واحدةٍ تحت قفلٍ استشاريّ: فحصُ «حُوِّل سلفاً» والإنشاءُ ووسمُ العرض مقبولاً
        // ‏إمّا تقع معاً أو لا يقع شيء — ونقرتان متزامنتان لا تُنشئان أمرين.
        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
    {
        var ct = token;
        var date = orderDate ?? DateOnly.FromDateTime(DateTime.Today);
        SecurityContext.EnsureCanPost();
        await DocumentSequenceLock.AcquireAsync(db.Database, companyId, "SOQ", date.Year);
        var quote = await db.SalesQuotations
            .SingleAsync(q => q.CompanyId == companyId && q.Id == quotationId, ct);

        if (quote.Status is not (SalesQuotationStatus.Sent or SalesQuotationStatus.Accepted))
            throw new Domain.Accounting.AccountingException("يُحوَّل عرض سعر مُرسَل أو مقبول فقط.");
        // ‏صلاحيةُ العرض تُطبَّق: المُرسَلُ الذي تجاوز تاريخَ صلاحيته لا يُحوَّل (والمقبولُ ضمنها يبقى قابلاً للتحويل).
        if (quote.IsExpiredOn(date))
            throw new Domain.Accounting.AccountingException(
                $"انتهت صلاحية عرض السعر في {quote.ValidUntil:yyyy-MM-dd} — أصدر عرضاً جديداً أو اطلب قبول العميل قبل التحويل.");
        // ‏(ح-١١٥) ‏**العرضُ يُحوَّل مرّةً**: نقرتان على «تحويل» كانتا أمرَين مؤكَّدَين يُفوتَران مرّتين.
        // ‏والأمرُ الملغى لا يُحتسب — فيُعاد التحويلُ بعده.
        if (await db.SalesOrders.AnyAsync(o => o.CompanyId == companyId && o.SalesQuotationId == quotationId
                && o.Status != SalesOrderStatus.Cancelled, ct))
            throw new Domain.Accounting.AccountingException("حُوِّل عرض السعر هذا إلى أمر بيع سلفاً.");

        var order = SalesOrder.CreateDraft(companyId, quote.CustomerId, date, quotationId);
        // ‏(م-٥ · ن-٥٢) **وهذا الموضعُ صار حيّاً بهذا التغيير نفسِه**: عرضُ السعر لم يكن
        // ‏يقبل فئةً ضريبيّةً أصلاً، فلم يكن ثَمّ ما يسقط. ومتى قبِلها وجب أن تعبر — وإلّا
        // ‏خُرق مبدأُ سجلّ السطر: «ولا تُتاح إلا حيث تُحوَّل».
        foreach (var line in quote.Lines)
            order.AddLine(line.Description, line.Quantity, line.UnitPrice, line.ProductId,
                line.VatCategory, line.TaxExemptionReasonCode, line.TaxExemptionReason);

        // ‏والعرضُ المُرسَل يصير «مقبولاً» بتحويله — فالأمرُ قبولٌ فعليّ.
        if (quote.Status == SalesQuotationStatus.Sent)
            quote.Accept();

        return await ConfirmAsync(order, ct);
    }, ct);
}
