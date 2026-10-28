using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;

using Brqosh.Domain.Inventory;

using Brqosh.Domain.Retail;

using Brqosh.Domain.Sales;

using Brqosh.Domain.Zatca;

using Brqosh.Infrastructure.Accounting;

using Brqosh.Infrastructure.Companies;

using Brqosh.Infrastructure.Inventory;

using Brqosh.Domain.Platform;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Treasury;
using Brqosh.Infrastructure.Platform;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Zatca;

using Microsoft.EntityFrameworkCore;



namespace Brqosh.Infrastructure.Sales;



/// <summary>

/// ترحيل فاتورة المبيعات: قيد إيراد + ضريبة، وتكلفة مبيعات + صرف مخزون للأصناف المخزنية.

/// </summary>

public class SalesInvoicePostingService(BrqoshDbContext db)

{

    public async Task<SalesInvoice> PostAsync(SalesInvoice invoice)
    {
        SecurityContext.EnsureCanPost();
        await UnitOfWork.ExecuteInTransactionAsync(db, async ct =>
        {
            // (CEEF-230 · ط6-٧) ‏**والتوليدُ نزل إلى `PostCoreAsync`** فصار كلُّ قناةٍ
            // ‏تُصيبه، وارتفع من هنا. ولو بقي في الموضعين لرمى المولّدُ «مستند ZATCA
            // ‏موجود مسبقاً» على **كلّ** فاتورةٍ تُرحَّل من هذا الباب.
            await PostCoreAsync(invoice, ct);
            return invoice;
        });

        await ZatcaPostingOrchestrator.SubmitAfterCommitAsync(
            db, invoice.CompanyId, ZatcaDocumentType.SalesInvoice, invoice.Id);

        return invoice;
    }

    internal async Task PostCoreAsync(SalesInvoice invoice, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPost();

        if (invoice.BranchId is null)
            invoice.AssignBranch(await new BranchAssignmentService(db)
                .ResolveBranchIdAsync(invoice.CompanyId, BranchSessionContext.PreferredBranchId, ct));

        var posting = new JournalPostingService(db);
        await FinancialPostingSupport.ValidateAsync(
            db, invoice.CompanyId, invoice.InvoiceDate, PostingDocumentKind.SalesInvoice,
            documentKey: invoice.InvoiceNumber);

        var inv = new InventoryService(db);



        var products = await inv.LoadProductsForLinesAsync(invoice.CompanyId, invoice.Lines.Select(l => l.ProductId));
        InventoryService.EnsureTradable(products, "بيع");

        var stockLines = invoice.Lines

            .Where(l => !invoice.StockIssuedByDelivery   // ‏صرفه إذنُ التسليم
                        && l.ProductId is long pid && products.TryGetValue(pid, out var p) && p.IsStockable())

            .Select(l => (l.ProductId!.Value, l.Quantity, l.BatchNumber, l.UnitCode))

            .ToList();



        // (CEEF-229 · م0-٣) **الرقمُ يُخصَّص قبل استهلاك الطبقات** — فهو مفتاحُ سجلّ
        // الاستهلاك الذي يُرمَّم منه عند الإلغاء. ولو خُصّص بعده لَسُجّل الاستهلاكُ
        // باسمٍ فارغ، فلا يجده الإلغاءُ ولا تعود الطبقات — عطلٌ صامتٌ تماماً.
        var invoiceNumber = await NextInvoiceNumberAsync(invoice.CompanyId, invoice.InvoiceDate);

        // ‏(جاهزيّةُ الإطلاق · ح-٩٥ · order-to-cash#13) **حدُّ ائتمان العميل** — قبل استهلاك الطبقات، و**بعد** قفل التسلسل
        // ‏الاستشاريّ (`NextInvoiceNumberAsync`): فحصٌ قبل القفل يدع فاتورتين متزامنتين تقرآن الرصيدَ نفسَه فتتجاوزان الحدَّ معاً.
        // ‏والرفضُ هنا يُرجِع المعاملةَ كلَّها فلا يُستهلك الرقم. والبيعُ الآجلُ وحدَه: بيعُ نقطة البيع نقدٌ أو بطاقة لا يمسّ الذمّة.
        if (invoice.PosShiftId is null || invoice.PosPaymentMethod is null)
            await EnsureWithinCreditLimitAsync(invoice, ct);

        decimal cogsAmount = 0;
        var costByProduct = new Dictionary<long, decimal>();
        var issueLines = new List<(long ProductId, decimal Qty, decimal UnitCost,
            string? BatchNumber, DateOnly? ExpiryDate)>();
        var costing = new InventoryCostingService(db);
        // ‏(جاهزيّةُ الإطلاق · ح-١١٠ · ‏`order-to-cash-procure-to-pay-inventory#12`) ‏**البيعُ يُصرف من مستودع الفاتورة — لا الافتراضيِّ أبداً.**
        // ‏والفارغُ هو الافتراضيّ كما كانت كلُّ فاتورةٍ قبله؛ **والمستودعُ يُسأل هنا قبل أيّ صرف** — فمستودعٌ أُوقف بعد
        // ‏اختياره يُرفض باسمه ولا تُرحَّل حركةٌ ولا قيد. والإلغاءُ يعكس كلَّ حركةٍ في مستودعها هي.
        var warehouse = await ResolveWarehouseAsync(inv, invoice, ct);

        foreach (var (productId, entered, batchNumber, unitCode) in stockLines)
        {
            // (CEEF-230 · ط1-١) ‏يُحوَّل **قبل** فحص الرصيد: بيعُ كرتونٍ من مئةِ حبّةٍ
            // ‏يجب أن يُقارَن باثنتَي عشرة لا بواحدة، **وإلا مرّ بيعٌ لا رصيدَ له**.
            var qty = await new UnitConversionService(db).ToProductUnitAsync(
                invoice.CompanyId, products[productId], entered, unitCode, ct);

            var onHand = await inv.GetOnHandAsync(invoice.CompanyId, productId, warehouse.Id);
            if (qty > onHand)
            {
                var p = products[productId];
                throw new AccountingException(
                    $"رصيد '{p.NameAr}' غير كافٍ للبيع — المتوفر {onHand:N2}.");
            }

            // ‏والمحجوزُ لأوامر بيعٍ مؤكَّدة لا يُباع لغيرها (أمرُ هذه الفاتورة أُفرج عن حجزه قبل ترحيلها).
            var reserved = await new SalesOrderService(db).ReservedQuantityAsync(invoice.CompanyId, productId, ct);
            if (reserved > 0m)
            {
                var available = await inv.GetCompanyOnHandAsync(invoice.CompanyId, productId) - reserved;
                if (qty > available)
                    throw new AccountingException(
                        $"'{products[productId].NameAr}' محجوزٌ لأوامر بيع مؤكَّدة — المتاح للبيع {Math.Max(available, 0m):N2}.");
            }

            var (totalCost, unitCost) = await costing.ConsumeForIssueAsync(
                invoice.CompanyId, productId, warehouse.Id, qty,
                $"SalesInvoice:{invoiceNumber}", ct);
            cogsAmount += totalCost;
            costByProduct[productId] = costByProduct.GetValueOrDefault(productId) + totalCost;
            issueLines.Add((productId, qty, unitCost, batchNumber, null));
        }

        var receivable = await DebitAccountForSaleAsync(invoice);
        var revenue = await SystemAccountAsync(invoice.CompanyId, SaudiChartOfAccounts.SystemCodes.SalesRevenue);
        var vatOutput = await SystemAccountAsync(invoice.CompanyId, SaudiChartOfAccounts.SystemCodes.VatOutput);
        Account? cogs = cogsAmount > 0
            ? await SystemAccountAsync(invoice.CompanyId, SaudiChartOfAccounts.SystemCodes.CostOfGoodsSold)
            : null;
        Account? inventory = cogsAmount > 0
            ? await SystemAccountAsync(invoice.CompanyId, SaudiChartOfAccounts.SystemCodes.Inventory)
            : null;

        // ‏حساباتُ الصنف ← الفئة ← العامّ. ولا توزيعَ إن لم يُعرَّف حسابٌ خاصّ — فالقيدُ كما كان.
        // ‏والمخزونُ كذلك — فالشراءُ والاستلامُ والتسوياتُ والمرتجعاتُ تقيّده على الحساب نفسِه.
        var itemAccounts = await ItemAccountResolver.ResolveAsync(db, invoice.CompanyId, products.Values, ct);
        List<(Account, decimal)>? revenueSplit = null;
        List<(Account, Account, decimal)>? cogsSplit = null;
        if (itemAccounts.Values.Any(a => a.Revenue != null || a.Cogs != null || a.Inventory != null))
        {
            var accountCache = new Dictionary<long, Account>();
            async Task<Account> AccountByIdAsync(long id)
            {
                if (accountCache.TryGetValue(id, out var a)) return a;
                a = await db.Accounts.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == invoice.CompanyId, ct)
                    ?? throw new AccountingException($"حساب الصنف/الفئة رقم {id} غير موجود في دليل حسابات المنشأة.");
                return accountCache[id] = a;
            }

            // ‏الإيراد: نصيبُ كلّ سطرٍ من الصافي بعد خصم المستند، والفرقُ التقريبيّ على آخر سطر.
            var weights = invoice.Lines.Select(l => (Line: l, W: l.LineTotal)).ToList();
            var totalW = weights.Sum(x => x.W);
            var shares = new List<(long? AccId, decimal Amount)>();
            decimal allocated = 0m;
            for (var i = 0; i < weights.Count; i++)
            {
                var (line, w) = weights[i];
                var amount = i == weights.Count - 1 || totalW == 0m
                    ? (i == weights.Count - 1 ? invoice.Subtotal - allocated : 0m)
                    : Math.Round(invoice.Subtotal * w / totalW, 2, MidpointRounding.AwayFromZero);
                allocated += amount;
                long? accId = line.ProductId is { } pid && itemAccounts.TryGetValue(pid, out var ia) ? ia.Revenue : null;
                shares.Add((accId, amount));
            }
            revenueSplit = new();
            foreach (var g in shares.GroupBy(s => s.AccId))
                revenueSplit.Add((g.Key is { } id ? await AccountByIdAsync(id) : revenue, g.Sum(s => s.Amount)));

            if (cogsAmount > 0)
            {
                cogsSplit = new();
                foreach (var g in costByProduct.GroupBy(kv => itemAccounts.TryGetValue(kv.Key, out var ia) ? (ia.Cogs, ia.Inventory) : (null, null)))
                    cogsSplit.Add((g.Key.Cogs is { } cid ? await AccountByIdAsync(cid) : cogs!,
                        g.Key.Inventory is { } iid ? await AccountByIdAsync(iid) : inventory!, g.Sum(kv => kv.Value)));
            }
        }

        (Account, decimal)? cardShare = null;
        if (invoice.PosShiftId is not null && invoice.PosPaymentMethod == PosPaymentMethod.Mixed)
            cardShare = (await BankGlAccountResolver.ResolveAsync(db, invoice.CompanyId),
                invoice.PosCardAmount ?? throw new AccountingException("الدفع المختلط يتطلب مبلغ البطاقة."));

        var entry = invoice.Post(invoiceNumber, receivable, revenue, vatOutput, cogsAmount, cogs, inventory,
            revenueSplit, cogsSplit, cardShare);

        var entryNumber = await new JournalEntryNumberGenerator(db).NextAsync(invoice.CompanyId, invoice.InvoiceDate);
        entry.Post(entryNumber);

        db.JournalEntries.Add(entry);
        // (CEEF-230 · ط2-٠) ‏يُضاف إن لم يكن محفوظاً — واصطلاحُ `JournalPostingService` نفسُه.
        // ‏وكان الإضافةُ بلا شرطٍ في تسع خدمات، **وأسقطت شاشةَ الرواتب بـ`23505`** حين
        // ‏حفظت مسودّتَها ثمّ رحّلتها. والشرطُ يجعل المسارين مقبولين معاً، **فيُغلق
        // ‏صنفُ العطل لا حالتُه**: شاشةٌ تُكتب غداً بالاصطلاح الآخر لا تسقط.
        if (invoice.Id == 0)
            db.SalesInvoices.Add(invoice);
        await db.SaveChangesAsync(ct);

        invoice.LinkJournalEntry(entry.Id);
        await db.SaveChangesAsync(ct);

        await DomainEventHub.PublishAsync(new DocumentPostedEvent(
            invoice.CompanyId, "SalesInvoice", invoice.Id, invoice.InvoiceNumber!), ct);
        await DomainEventHub.PublishAsync(new JournalEntryPostedEvent(
            invoice.CompanyId, entry.Id, entry.EntryNumber!, invoice.InvoiceDate), ct);

        await FinancialPostingSupport.AuditPostAsync(
            db, invoice.CompanyId, "SalesInvoice", invoice.InvoiceNumber!,
            reason: "ترحيل فاتورة مبيعات", ct: ct);

        if (issueLines.Count > 0)
        {
            await inv.PostDocumentMovementAsync(invoice.CompanyId, warehouse.Id,
                StockMovementType.Issue, invoice.InvoiceDate,
                $"صرف من فاتورة مبيعات {invoiceNumber}",
                $"SalesInvoice:{invoiceNumber}", issueLines);
            await db.SaveChangesAsync(ct);
        }

        // (CEEF-230 · ط3-٣ج) ‏**وبيعُ طبقٍ مرتبطٍ بوصفةٍ يصرف مكوّناتِه** — ومن هنا لأنّ
        // ‏كلَّ بيعٍ يمرّ بهذا المسار: نقطةُ البيع والفاتورة معاً. **فالعطلُ يُغلق صنفاً
        // ‏لا حالةً**، ولا يعتمد على أن يتذكّر أحدٌ ضغطَ زرّ.
        //
        // ‏والأطباقُ **أصنافٌ خدمية**، فلا تدخل `issueLines` أعلاه ولا تُصرف مرّتين.
        await new Hospitality.KitchenIssueService(db).IssueForSoldProductsAsync(
            invoice.CompanyId, invoice.InvoiceDate,
            [.. invoice.Lines.Where(l => l.ProductId is not null)
                .Select(l => (ProductId: l.ProductId!.Value, Quantity: l.Quantity))],
            $"SalesInvoice:{invoiceNumber}", warehouseId: warehouse.Id, ct: ct);

        // (CEEF-230 · ط6-٧) ‏**والمستندُ الإلكترونيُّ يُولَّد من هنا لا من فوق** — فكلُّ
        // ‏فاتورةِ ضريبةٍ في النظام تلتقي في هذا المسار: الفاتورةُ المباشرة وأمرُ البيع
        // ‏ونقطةُ البيع **والزيارةُ الطبّية وحسابُ النزيل والفوترةُ الدورية**. وكانت
        // ‏الثلاثُ الأخيرةُ تُصدر فاتورةَ ضريبةٍ **بلا مستندٍ إلكترونيّ البتّة** — مخالفةُ
        // ‏الطور الثاني بعينها: فاتورةٌ تُسلَّم للعميل ولا تعرفها الهيئةُ أبداً.
        //
        // ‏**والعطلُ يُغلق صنفاً لا حالةً**: قناةٌ تُكتب غداً تُصيب التوليدَ بمجرّد أن
        // ‏ترحّل، ولا تعتمد على أن يتذكّر كاتبُها سطراً.
        //
        // ‏**وموضعُ التوليد واحدٌ لأنّ المولّدَ يرمي على المكرَّر** ولا يُعيد الموجود.
        // ‏والرميةُ صوابٌ يُحتفظ به — مستندان لفاتورةٍ واحدةٍ يُبلَّغان برقمَي عدّادٍ
        // ‏مختلفين فتنكسر سلسلةُ البصمات — **فيُنقل التوليدُ لا يُنسخ**.
        //
        // ‏**والإرسالُ يبقى فوق**: هذا داخل المعاملة، والنداءُ الشبكيُّ لا يجري إلّا بعد
        // ‏الـcommit، وهو عرفُ هذا المستودع منذ أوّله.
        await ZatcaPostingOrchestrator.GenerateInsideTransactionAsync(
            db, invoice.CompanyId,
            () => new ZatcaEInvoiceGenerator(db).TryGenerateForSalesInvoiceAsync(invoice.Id),
            ct);
    }



    /// <summary>
    /// (ح-١١٠) <b>مستودعُ الفاتورة إن اختير — في المنشأة وغيرُ موقوف — وإلا الافتراضيّ.</b> والرفضُ يُسمّي المستودع:
    /// فالمالكُ يعرف أيَّ مستودعٍ أوقفه، ولا يقرأ «رصيدٌ غير كافٍ» عن مستودعٍ لم يقصده.
    /// </summary>
    private async Task<Warehouse> ResolveWarehouseAsync(InventoryService inv, SalesInvoice invoice, CancellationToken ct)
    {
        if (invoice.WarehouseId is not long id)
            return await inv.GetDefaultWarehouseAsync(invoice.CompanyId);

        var warehouse = await db.Warehouses.SingleOrDefaultAsync(w => w.CompanyId == invoice.CompanyId && w.Id == id, ct)
            ?? throw new AccountingException("المستودع المختار لهذه الفاتورة غير موجود في المنشأة.");
        if (!warehouse.IsActive)
            throw new AccountingException(
                $"المستودع «{warehouse.NameAr}» موقوف — فعِّله من شاشة المستودعات، أو اختر مستودعاً آخر للفاتورة.");

        return warehouse;
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٩٥ · <c>order-to-cash-procure-to-pay-inventory#13</c>) <b>البيعُ الآجلُ لا يتجاوز حدَّ ائتمان العميل</b> — المفتوحُ
    /// عليه (<c>CustomerBalanceQuery</c>: الفواتيرُ والمستخلصاتُ بعد الإشعارات والمقبوض) زائداً إجماليَّ هذه الفاتورة بضريبتها.
    /// <b>وكان لا حدَّ في الشجرة كلِّها</b> — عميلٌ متعثّرٌ يُباع له بلا سقف. <b>وبلا حدٍّ ⟵ كما كان.</b>
    /// </summary>
    private async Task EnsureWithinCreditLimitAsync(SalesInvoice invoice, CancellationToken ct)
    {
        var limit = await db.Customers
            .Where(c => c.CompanyId == invoice.CompanyId && c.Id == invoice.CustomerId)
            .Select(c => c.CreditLimit)
            .SingleOrDefaultAsync(ct);
        if (limit is not decimal cap)
            return;

        var open = (await new CustomerBalanceQuery(db).ForCustomerAsync(invoice.CompanyId, invoice.CustomerId)).Balance;
        var total = invoice.GrandTotal();
        if (open + total > cap)
            throw new AccountingException(
                $"تتجاوز الفاتورةُ حدَّ ائتمان العميل: المفتوحُ {open:N2} والفاتورةُ {total:N2} والحدُّ {cap:N2}.");
    }

    private async Task<Account> DebitAccountForSaleAsync(SalesInvoice invoice)

    {

        // بيع POS نقدي/بطاقة — يُقيَّد على الصندوق/البنك مباشرة لا على ذمم العملاء

        if (invoice.PosShiftId is not null && invoice.PosPaymentMethod is { } method)

        {

            // (CEEF-206) الدفع المختلط لا يُرحَّل: لا يوجد في النموذج أي حقل يحمل تقسيم
            // المبلغ بين النقد والشبكة، وكان يُقيَّد بالكامل على الصندوق — فتخالف عهدة
            // الصندوق الدفترية الفعلية وتفشل مطابقة الوردية، ولا تظهر حصة الشبكة في البنك.
            // ترحيل قيد نعلم أنه خاطئ أسوأ من رفضه. البديل التشغيلي: فاتورتان منفصلتان.
            // ‏الدفع المختلط: المدينُ الأوّل النقد، وحصّةُ البطاقة سطرٌ ثانٍ (انظر `secondDebit`).

            // (CEEF-229 · م2-١٥) بيعُ البطاقة يذهب إلى **حساب البنك نفسه** لا إلى الأب 1102.
            //
            // كان يحلّ الكود الحرفيّ 1102، وكلُّ مسارٍ آخر يحلّ `bank.GlAccountId` أي الابن
            // (110201). و1102 قابلٌ للترحيل في القالب، فالأب وابنُه يتقاسمان نقد البنوك:
            // إيرادُ الشبكة يتراكم على الأب ولا ينزل، والمدفوعات تُخصم من الابن حتى يصير
            // رصيدُه سالباً؛ والتسويةُ البنكية تقرأ الابن وحده فتُقارَن بكشفٍ يحوي تحصيلات
            // الشبكة، فيظهر فرقٌ دائم لا يُغلق. ولا تكشفه هوية قائمة التدفقات لأن المجاميع
            // تحت البادئة "1102" تبقى سليمة — الخلل في التوزيع لا في الإجمالي.
            if (method == PosPaymentMethod.Card)
                return await BankGlAccountResolver.ResolveAsync(db, invoice.CompanyId);

            return await SystemAccountAsync(invoice.CompanyId, SaudiChartOfAccounts.SystemCodes.CashOnHand);

        }



        return await SystemAccountAsync(invoice.CompanyId, SaudiChartOfAccounts.SystemCodes.AccountsReceivable);

    }



    private async Task<Account> SystemAccountAsync(long companyId, string code) =>

        await db.Accounts.SingleOrDefaultAsync(a => a.CompanyId == companyId && a.Code == code)

        ?? throw new AccountingException(
            $"الحساب النظامي {code} غير موجود في دليل حسابات المنشأة — أضفه من شاشة دليل الحسابات ثم أعد الترحيل.");



    private async Task<string> NextInvoiceNumberAsync(long companyId, DateOnly date)

    {

        var fiscalYear = await Accounting.FiscalYearResolver.StartYearForAsync(db, companyId, date);
        var prefix = $"INV-{fiscalYear}-";

        // (CEEF-229 · م1-١٤) **قبل القراءة** — وهذا المسارُ خاصٌّ بالفواتير ولا يمرّ
        // بـ`DocumentNumberGenerator`، فلا يكفي إلزامُ ذاك بالسياق. وقياسُ العطل قبل
        // الإصلاح: ستّةُ كتّابٍ متزامنين ⟵ **نجح ٣ وأخفق ٣** بـ`23505` على
        // `IX_sales_invoices_CompanyId_InvoiceNumber` — أي أنّ كاشيرين يبيعان معاً
        // فتموت إحدى البيعتين برسالةٍ إنجليزية خام.
        await DocumentSequenceLock.AcquireAsync(db.Database, companyId, "INV", fiscalYear);

        var last = await db.SalesInvoices

            .Where(i => i.CompanyId == companyId && i.InvoiceNumber != null && i.InvoiceNumber.StartsWith(prefix))

            // (CEEF-206) الترتيب بالطول أولاً ثم نصياً: التسلسل مصفوف بست خانات، فبعد
            // تجاوز 999,999 يصبح الترتيب النصي وحده خاطئاً ("INV-2026-999999" أكبر من
            // "INV-2026-1000000") فيتكرر الرقم أو يقفز — والمنشآت عالية الحجم تبلغه.
            .OrderByDescending(i => i.InvoiceNumber!.Length)

            .ThenByDescending(i => i.InvoiceNumber)

            .Select(i => i.InvoiceNumber)

            .FirstOrDefaultAsync();



        var next = 1;

        if (last is not null && int.TryParse(last[prefix.Length..], out var seq))

            next = seq + 1;

        return $"{prefix}{next:D6}";

    }

}


