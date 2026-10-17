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
            // ‏(جاهزيّةُ الإطلاق · ح-١١١ · ‏`order-to-cash-procure-to-pay-inventory#12`) ‏**الشراءُ المباشر يدخل مستودعَ الفاتورة — لا الافتراضيَّ أبداً.**
            // ‏يُسأل المستودعُ هنا قبل أيّ قيد: موقوفٌ أو مفقودٌ يُرفض باسمه فلا يُرحَّل شيء. والفارغُ هو الافتراضيّ كما كان.
            var receiptWarehouse = invoice.WarehouseId is null
                ? null
                : await ResolveWarehouseAsync(db, inv, invoice.CompanyId, invoice.WarehouseId, ct);

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

            // ‏(جاهزيّةُ الإطلاق · ح-١٤) ‏**فاتورةٌ بلا أمرِ شراءٍ لبضاعةٍ وصلت بإذنِ استلام
            // ‏تُدخلها المخزونَ مرّةً ثانية.**
            //
            // ‏مسارُ «بلا إذن» أدناه يُضيف الكمّيةَ إلى المستودع. **وهو الصوابُ لشراءٍ مباشر**
            // ‏— نقداً من السوق بلا أمرٍ ولا إذن. **لكنّ البضاعةَ التي دخلت بإذنِ استلامٍ
            // ‏دخلت المخزونَ يومَها**، فإن فُوتِرت بفاتورةٍ غيرِ مربوطةٍ بأمرها:
            // ‏**تُضاف كمّيتُها ثانيةً، ويبقى `2112` دائناً إلى الأبد** — كمّيةٌ وهميّةٌ
            // ‏في الجرد والتزامٌ لا يُقفله شيء. **والقيدُ متوازنٌ فلا يشكو الميزان.**
            //
            // ‏**ولا سبيلَ للخدمة أن تعرف النيّةَ من الفاتورة وحدَها** — لا رابطَ فيها.
            // ‏فيُسأل الدفترُ: أللمورِّد استحقاقٌ مفتوحٌ على هذا الصنف؟ فإن كان، **فالفاتورةُ
            // ‏غالباً له**، ويُطلب ربطُها بأمرها بدل أن تُخمَّن النيّة.
            if (invoice.PurchaseOrderId is null)
                await EnsureNoOpenAccrualForUnlinkedInvoiceAsync(invoice, products, ct);
            decimal inventorySubtotal = 0;
            // ‏(جاهزيّةُ الإطلاق · ح-١٠٨ · ‏`order-to-cash-procure-to-pay-inventory#7`) ‏**الدفعةُ وانتهاؤها** تسافران مع السطر إلى حركة الاستلام —
            // ‏وكان التحميلُ القديمُ يُسقطهما فيرفض المَخنَقُ الصنفَ المتتبَّع.
            var receiptLines = new List<(long ProductId, decimal Qty, decimal UnitCost, string? BatchNumber, DateOnly? ExpiryDate)>();

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

            // (جاهزيّةُ الإطلاق · ح-٤) ‏**سطورُ المستند بعملته، والدفترُ بالريال.**
            //
            // ‏`RecalculateTotals` يحسب `ForeignSubtotal` من السطور **ثمّ** يضربه في السعر
            // ‏ليُخرج `Subtotal` بالريال. **وهذه الحلقةُ كانت تجمع `LineTotal` خامّاً** —
            // ‏أي بالعملة الأجنبيّة — **وتُمرّره حيث يُنتظر الريال**.
            //
            // ‏فاتورةُ ١٬٠٠٠ دولارٍ بسعر ٣٫٧٥: `Subtotal` ٣٬٧٥٠ ريالاً، و`inventorySubtotal`
            // ‏كان **١٬٠٠٠**. فيُمدَّن المخزونُ بألفٍ، **ويذهب الفرقُ ٢٬٧٥٠ إلى مصروف
            // ‏المشتريات** — فالقيدُ متوازنٌ والمخزونُ مُقيَّمٌ بربع قيمته، **وتنكسر الهوية
            // ‏هـ-٣** بين الدفتر المساعد و1107، ويخرج كلُّ بيعٍ لاحقٍ بتكلفةٍ خاطئة.
            //
            // ‏**وفرقُ السعر كان يخلط العملتين كذلك**: `relief` بالريال (فالاستحقاقُ سُجِّل
            // ‏بالريال يوم الاستلام) و`LineTotal` بالأجنبيّة — فالطرحُ بين مقدارين لا
            // ‏يُطرح أحدُهما من الآخر.
            //
            // ‏والتحويلُ لكلّ سطرٍ على حدة، **وبسعرٍ واحدٍ هو سعرُ يوم الفاتورة**. وحين تكون
            // ‏العملةُ الريالَ فالسعرُ ١ والنتيجةُ مطابقةٌ حرفاً لِما كان.
            var rate = invoice.ExchangeRate;

            foreach (var line in invoice.Lines)
            {
                if (line.ProductId is not long pid
                    || !products.TryGetValue(pid, out var product) || !product.IsStockable())
                    continue;

                var lineTotalSar = Math.Round(line.LineTotal * rate, 2, MidpointRounding.AwayFromZero);

                if (!hasPostedGrn)
                {
                    inventorySubtotal += lineTotalSar;
                    receiptLines.Add((pid, line.Quantity,
                        Math.Round(line.UnitPrice * rate, 2, MidpointRounding.AwayFromZero),
                        line.BatchNumber, line.ExpiryDate));
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
                priceVariance += lineTotalSar - relief;
            }

            // ‏(ح-١٤٤ · ‏`sector-modules#9`) ‏**فاتورةُ مشروعٍ تُدين تكاليفَ المشاريع لا المشتريات** — بجزئها غير
            // ‏المخزنيّ وحدَه؛ فالمخزنيُّ يدخل المخزونَ ويُحمَّل على المشروع حين يُصرف إليه، لا مرّتين.
            var project = invoice.ProjectId is long projectId
                ? await ProjectForCostAsync(invoice.CompanyId, projectId, ct)
                : null;
            // ‏(جاهزيّةُ الإطلاق · ح-١٥٨ · ‏`treasury-payroll-assets#12`) ‏**فاتورةُ أصلٍ تُدين حسابَ الأصل لا المشتريات** — وكلُّها له:
            // ‏لا صنفَ مخزنيّاً فيها، وصافيها (أو إجماليُّها لغير القابل للخصم) تكلفتُه المسجَّلة.
            var asset = invoice.FixedAssetId is long assetId
                ? await AssetForInvoiceAsync(invoice, assetId, project, inventorySubtotal, grniRelief, receiptLines.Count, ct)
                : null;
            var purchases = asset is not null
                ? await db.Accounts.SingleAsync(a => a.Id == asset.GlAssetAccountId && a.CompanyId == invoice.CompanyId, ct)
                : await SystemAccountAsync(invoice.CompanyId, project is null ? "5102" : ProjectCostAccountCode);
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
            if (asset is not null)
            {
                var booked = entry.Lines.Where(l => l.AccountId == asset.GlAssetAccountId).Sum(l => l.Debit - l.Credit);
                if (booked != asset.AcquisitionCost)
                    throw new AccountingException(
                        $"تكلفةُ الأصل {asset.Code} المسجَّلة {asset.AcquisitionCost:N2} وفاتورتُه تُدينه {booked:N2} — وحِّدهما قبل الترحيل.");
            }
            var entryNumber = await new JournalEntryNumberGenerator(db).NextAsync(invoice.CompanyId, invoice.InvoiceDate);

            // (CEEF-230 · ط1-٧) ‏مركزُ تكلفة المستند يُختم على سطور المصروف — **وقبل
            // ‏الترحيل**، فالقيدُ المرحَّل لا يُعدَّل. ويُتحقَّق من المركز أوّلاً: مرجعٌ
            // ‏لمركزٍ محذوفٍ يُنتج تقريراً يعرض «غير مُصنَّف» ويُقرأ نقصَ تصنيف.
            await FinancialPostingSupport.EnsureCostCenterAsync(db, invoice.CompanyId, invoice.CostCenterId, ct);
            entry.StampCostCenter(invoice.CostCenterId);
            entry.Post(entryNumber);

            db.JournalEntries.Add(entry);
            // (CEEF-230 · ط2-٠) ‏يُضاف إن لم يكن محفوظاً — واصطلاحُ `JournalPostingService` نفسُه.
            // ‏وكان الإضافةُ بلا شرطٍ في تسع خدمات، **وأسقطت شاشةَ الرواتب بـ`23505`** حين
            // ‏حفظت مسودّتَها ثمّ رحّلتها. والشرطُ يجعل المسارين مقبولين معاً، **فيُغلق
            // ‏صنفُ العطل لا حالتُه**: شاشةٌ تُكتب غداً بالاصطلاح الآخر لا تسقط.
            if (invoice.Id == 0)
                db.PurchaseInvoices.Add(invoice);
            await db.SaveChangesAsync(ct);

            invoice.LinkJournalEntry(entry.Id);
            asset?.AcquireByInvoice(entry.Id, invoice.InvoiceDate);
            await db.SaveChangesAsync(ct);

            if (project is not null)
                await RecordProjectCostAsync(invoice, project, entry, ct);

            await DomainEventHub.PublishAsync(new DocumentPostedEvent(
                invoice.CompanyId, "PurchaseInvoice", invoice.Id, invoice.InvoiceNumber!), ct);
            await DomainEventHub.PublishAsync(new JournalEntryPostedEvent(
                invoice.CompanyId, entry.Id, entry.EntryNumber!, invoice.InvoiceDate), ct);

            await FinancialPostingSupport.AuditPostAsync(
                db, invoice.CompanyId, "PurchaseInvoice", invoice.InvoiceNumber!,
                reason: "ترحيل فاتورة مشتريات", ct: ct);

            if (receiptLines.Count > 0)
            {
                var warehouse = receiptWarehouse ?? await inv.GetDefaultWarehouseAsync(invoice.CompanyId);
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
    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-١٤) <b>يمنع فاتورةً غيرَ مربوطةٍ أن تُدخل بضاعةً دخلت بإذنِ استلام.</b>
    ///
    /// <para><b>والحكمُ من الدفتر لا من النيّة:</b> يُسأل عن استحقاقٍ مفتوحٍ لهذا المورِّد
    /// على هذا الصنف — أي بضاعةٍ استُلمت منه ولم تُفوتَر بعد. <b>فإن وُجد، فالفاتورةُ
    /// غالباً لها</b>، وربطُها بأمرها يُقفل الاستحقاقَ ولا يُضاعف المخزون.</para>
    ///
    /// <para><b>ولا يُمنع الشراءُ المباشر</b>: مورِّدٌ بلا إذنِ استلامٍ مفتوحٍ على الصنف
    /// يمرّ كما كان — <b>وهو الحالةُ الشائعةُ في المتاجر الصغيرة</b>، وحظرُها كان سيُغلق
    /// باباً سليماً لأجل بابٍ معطوب.</para>
    /// </summary>
    private async Task EnsureNoOpenAccrualForUnlinkedInvoiceAsync(
        PurchaseInvoice invoice,
        IReadOnlyDictionary<long, Domain.Inventory.Product> products,
        CancellationToken ct)
    {
        var stockableIds = invoice.Lines
            .Where(l => l.ProductId is long pid
                        && products.TryGetValue(pid, out var p) && p.IsStockable())
            .Select(l => l.ProductId!.Value)
            .Distinct()
            .ToList();

        if (stockableIds.Count == 0) return;

        var received = await db.GoodsReceipts
            .Where(g => g.CompanyId == invoice.CompanyId
                        && g.SupplierId == invoice.SupplierId
                        && g.Status == GoodsReceiptStatus.Posted)
            .SelectMany(g => g.Lines)
            .Where(l => l.ProductId != null && stockableIds.Contains(l.ProductId!.Value))
            .Select(l => new { ProductId = l.ProductId!.Value, l.ReceivedQuantity })
            .ToListAsync(ct);

        if (received.Count == 0) return;

        var invoiced = (await db.PurchaseInvoices
                .Where(i => i.CompanyId == invoice.CompanyId
                            && i.SupplierId == invoice.SupplierId
                            && i.PurchaseOrderId != null
                            && i.Status == PurchaseInvoiceStatus.Posted)
                .SelectMany(i => i.Lines)
                .Where(l => l.ProductId != null && stockableIds.Contains(l.ProductId!.Value))
                .Select(l => new { ProductId = l.ProductId!.Value, l.Quantity })
                .ToListAsync(ct))
            .GroupBy(l => l.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

        var open = received
            .GroupBy(l => l.ProductId)
            .Where(g => g.Sum(l => l.ReceivedQuantity) - invoiced.GetValueOrDefault(g.Key) > 0m)
            .Select(g => g.Key)
            .ToList();

        if (open.Count == 0) return;

        var names = open
            .Select(id => products.TryGetValue(id, out var p) ? p.NameAr : id.ToString())
            .ToList();

        throw new AccountingException(
            "أصنافٌ استُلمت من هذا المورّد بإذنِ استلامٍ ولم تُفوتَر بعد: "
            + string.Join("، ", names)
            + " — واربط الفاتورةَ بأمر الشراء، وإلّا دخلت البضاعةُ المخزونَ مرّتين "
            + "وبقي استحقاقُ الاستلام مفتوحاً.");
    }

    private async Task<Dictionary<long, AccrualState>> OpenAccrualByProductAsync(
        PurchaseInvoice invoice, CancellationToken ct)
    {
        var poId = invoice.PurchaseOrderId!.Value;

        // (جاهزيّةُ الإطلاق · ح-١٧) ‏**والترتيبُ جزءٌ من الجواب لا زينةٌ في الاستعلام.**
        //
        // ‏كلُّ سطرِ إذنٍ **طبقةٌ بقيمتها التي قُيّدت في `2112` يومَها**، والفوترةُ تُحرِّرها
        // ‏بالترتيب (الأقدمُ أوّلاً). **ولولا الترتيبُ لَما كان للتحرير معنًى محدَّد**: أيُّ
        // ‏طبقةٍ تُحرَّر أوّلاً يُغيّر ما يُقيَّد فرقَ سعر.
        var received = await db.GoodsReceipts
            .Where(g => g.CompanyId == invoice.CompanyId
                        && g.PurchaseOrderId == poId
                        && g.Status == GoodsReceiptStatus.Posted)
            .OrderBy(g => g.ReceiptDate).ThenBy(g => g.Id)
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
            // (جاهزيّةُ الإطلاق · ح-١٧) ‏**طبقةٌ لكلّ سطرِ إذن — لا متوسّطٌ لكلّ الأذون.**
            //
            // ‏كان المتبقّي يُحسب بمتوسّطٍ مُرجَّح: **مجموعُ قيمة ما استُلم كلِّه** مضروباً
            // ‏في نسبة الكمّية المتبقّية. **وهو صحيحٌ ما دامت كلُّ الأذون بسعرٍ واحد**، فإن
            // ‏اختلفت الأسعارُ بين دفعةٍ وأخرى — **وذاك أمرٌ يوميّ على أمرٍ واحد** — صار
            // ‏المُحرَّرُ غيرَ المُقيَّد.
            //
            // ‏**مثالٌ مقيس:** ستٌّ بعشرةٍ ثمّ فاتورتُها (يُحرَّر ٦٠٫٠٠ فيصفر الحساب)، ثمّ
            // ‏أربعٌ بعشرةٍ وعُشر (يُقيَّد ٤٠٫٤٠). فالمتوسّطُ يُحرِّر للأربع
            // ‏`١٠٠٫٤٠ × ٤ ÷ ١٠ = ٤٠٫١٦` — **فيبقى ٠٫٢٤ في `2112` وقد فُوتِر كلُّ ما استُلم**،
            // ‏وهو نقضُ الهوية هـ-٨ بعينه: **التزامٌ في الميزانية لا يُقفله شيءٌ أبداً**،
            // ‏ويتراكم أمرَ شراءٍ بعد آخر ولا يشكو منه ميزانُ المراجعة.
            //
            // ‏**والطبقاتُ تُغلق الحساب بالبناء**: مجموعُ قيم الطبقات هو عينُ ما قُيّد
            // ‏دائناً في `2112`، فاستهلاكُها كلِّها يُحرِّره إلى الصفر حرفاً.
            var layers = group
                .Where(l => l.ReceivedQuantity > 0)
                .Select(l => new AccrualLayer(
                    l.ReceivedQuantity,
                    Math.Round(l.ReceivedQuantity * l.UnitCost, 2, MidpointRounding.AwayFromZero)))
                .ToList();

            if (layers.Count == 0) continue;

            var state = new AccrualState(layers);

            // ‏**وما فُوتِر سابقاً يُستهلَك من المقدّمة** — فالحالةُ تُحسب من الدفتر في كلّ
            // ‏مرّة ولا تُحفظ، وترتيبُ الاستهلاك هو ترتيبُ الأذون.
            state.Consume(invoicedQuantity.GetValueOrDefault(group.Key));

            open[group.Key] = state;
        }

        return open;
    }

    /// <summary>طبقةُ استحقاقٍ واحدة — سطرُ إذنٍ بكمّيته وقيمته التي قُيّدت في 2112.</summary>
    private sealed class AccrualLayer(decimal quantity, decimal value)
    {
        public decimal Quantity { get; set; } = quantity;
        public decimal Value { get; set; } = value;
    }

    /// <summary>
    /// ما بقي من استحقاق صنفٍ — <b>طبقاتٍ مرتَّبةً بترتيب الأذون</b>، لا كمّيةً وقيمةً
    /// مُتوسَّطتَين. والقيمةُ تتبع الكمية لا سعرَ الفاتورة.
    /// </summary>
    private sealed class AccrualState(List<AccrualLayer> layers)
    {
        private readonly List<AccrualLayer> _layers = layers;
        private int _head;

        /// <summary>
        /// ‏يستهلك من الاستحقاق ما يقابل <paramref name="quantityInvoiced"/> <b>من أقدم
        /// الطبقات</b>، ويُعيد القيمةَ المُحرَّرة.
        ///
        /// <para><b>والطبقةُ التي تُستنفد تُحرَّر كاملةً</b> لا بحاصل الضرب: التقريبُ إلى
        /// هللتين على كلّ فاتورةٍ يترك كسراً، <b>وكسرٌ في حساب استحقاقٍ لا يُقفله شيءٌ
        /// بعده</b>. فالشرطُ يضمن الهوية <b>بالبناء</b>: فُوتِر كلُّ ما استُلم ⟵ 2112
        /// صفرٌ تماماً.</para>
        /// </summary>
        public decimal Consume(decimal quantityInvoiced)
        {
            var released = 0m;
            var remaining = quantityInvoiced;

            while (remaining > 0 && _head < _layers.Count)
            {
                var layer = _layers[_head];
                if (layer.Quantity <= 0) { _head++; continue; }

                var take = Math.Min(remaining, layer.Quantity);

                // ‏استنفادُ الطبقة يُحرِّر باقيَها كلَّه — فلا يبقى كسرٌ في `2112`.
                var amount = take == layer.Quantity
                    ? layer.Value
                    : Math.Round(take * layer.Value / layer.Quantity, 2, MidpointRounding.AwayFromZero);

                layer.Quantity -= take;
                layer.Value -= amount;
                released += amount;
                remaining -= take;

                if (layer.Quantity <= 0) _head++;
            }

            return released;
        }
    }

    /// <summary>
    /// (ح-١١١) <b>المستودعُ المختار إن وُجد — في المنشأة وغيرُ موقوف — وإلا الافتراضيّ.</b> والرفضُ يُسمّي المستودع.
    /// </summary>
    internal static async Task<Warehouse> ResolveWarehouseAsync(
        BrqoshDbContext db, InventoryService inv, long companyId, long? warehouseId, CancellationToken ct)
    {
        if (warehouseId is not long id)
            return await inv.GetDefaultWarehouseAsync(companyId);

        var warehouse = await db.Warehouses.SingleOrDefaultAsync(w => w.CompanyId == companyId && w.Id == id, ct)
            ?? throw new AccountingException("المستودع المختار لهذه الفاتورة غير موجود في المنشأة.");
        if (!warehouse.IsActive)
            throw new AccountingException(
                $"المستودع «{warehouse.NameAr}» موقوف — فعِّله من شاشة المستودعات، أو اختر مستودعاً آخر للفاتورة.");

        return warehouse;
    }

    private async Task<Account> SystemAccountAsync(long companyId, string code) =>
        await db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == code);

    /// <summary>تكاليفُ المشاريع — الحسابُ نفسُه الذي يُدينه حوارُ تكلفة المشروع (<c>ProjectCostService</c>).</summary>
    internal const string ProjectCostAccountCode = "5105";

    /// <summary>
    /// (ح-١٤٤) المشروعُ الذي تُحمَّل عليه الفاتورة — <b>من المنشأة نفسِها، ولم يُكمَل ولم يُلغَ</b>.
    /// </summary>
    /// <summary>(ح-١٥٨) الأصلُ الذي تقتنيه الفاتورة: في المنشأة، ينتظر فاتورتَه، والفاتورةُ كلُّها له.</summary>
    private async Task<Domain.Assets.FixedAsset> AssetForInvoiceAsync(
        PurchaseInvoice invoice, long assetId, Domain.Construction.ConstructionProject? project,
        decimal inventorySubtotal, decimal grniRelief, int receiptLines, CancellationToken ct)
    {
        if (project is not null)
            throw new AccountingException("الفاتورةُ تشتري أصلاً أو تُحمَّل على مشروع — لا الاثنين.");
        var asset = await db.FixedAssets
            .SingleOrDefaultAsync(a => a.Id == assetId && a.CompanyId == invoice.CompanyId, ct)
            ?? throw new AccountingException("الأصل المحدَّد على الفاتورة غير موجود في هذه المنشأة.");
        if (!asset.AwaitingInvoice || asset.CancelledOn is not null)
            throw new AccountingException($"الأصل {asset.Code} لا ينتظر فاتورةَ مورّد — اقتُني أو أُلغي.");
        if (inventorySubtotal > 0m || grniRelief > 0m || receiptLines > 0)
            throw new AccountingException("فاتورةُ الأصل لا تحمل أصنافاً مخزنيّة — افصلها في فاتورةٍ أخرى.");
        return asset;
    }

    private async Task<Domain.Construction.ConstructionProject> ProjectForCostAsync(
        long companyId, long projectId, CancellationToken ct)
    {
        var project = await db.ConstructionProjects
            .SingleOrDefaultAsync(p => p.Id == projectId && p.CompanyId == companyId, ct)
            ?? throw new AccountingException("المشروع المحدَّد على الفاتورة غير موجود في هذه المنشأة.");

        if (project.Status is Domain.Construction.ConstructionProjectStatus.Completed
            or Domain.Construction.ConstructionProjectStatus.Cancelled)
            throw new AccountingException(
                $"المشروع {project.Code} {Domain.Construction.ConstructionProject.StatusLabel(project.Status)} — لا تُحمَّل عليه تكاليفُ جديدة.");

        return project;
    }

    /// <summary>
    /// (ح-١٤٤) <b>تكلفةُ المشروع من قيد الفاتورة نفسِه</b> — لا قيدَ ثانٍ يُكرّرها.
    /// </summary>
    /// <remarks>
    /// المبلغُ هو ما أُدين على 5105 في القيد: الوعاءُ غيرُ المخزنيّ بعد الضريبة القابلة للخصم،
    /// <b>وشاملاً الضريبةَ حين لا تُخصم</b> (مورّدٌ غيرُ مسجَّل) — فالمشروعُ يحمل تكلفتَه الحقيقيّة.
    /// ثمّ تُسوّى نسبةُ إنجاز العقد كما يفعل حوارُ التكلفة.
    /// </remarks>
    private async Task RecordProjectCostAsync(PurchaseInvoice invoice,
        Domain.Construction.ConstructionProject project, JournalEntry entry, CancellationToken ct)
    {
        var amount = entry.Lines
            .Where(l => l.AccountCode == ProjectCostAccountCode)
            .Sum(l => l.Debit - l.Credit);
        if (amount <= 0m)
            return;

        var cost = Domain.Construction.ProjectCostEntry.Create(invoice.CompanyId, project.Id, invoice.InvoiceDate,
            $"فاتورة مشتريات {invoice.InvoiceNumber} — مرجع المورد {invoice.SupplierInvoiceReference}",
            amount, $"PurchaseInvoice:{invoice.InvoiceNumber}");
        cost.LinkJournalEntry(entry.Id);
        if (invoice.BranchId is long branchId)
            cost.AssignBranch(branchId);
        db.ProjectCostEntries.Add(cost);
        await db.SaveChangesAsync(ct);

        await new Construction.ContractRevenueTrueUp(db).ApplyAsync(invoice.CompanyId, project.Id, invoice.InvoiceDate, ct);
    }
}
