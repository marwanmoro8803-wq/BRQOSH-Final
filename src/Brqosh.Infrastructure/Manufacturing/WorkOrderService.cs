using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Manufacturing;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Manufacturing;

public class WorkOrderService(BrqoshDbContext db)
{
    public async Task<WorkOrder> ReleaseAsync(long companyId, long workOrderId, CancellationToken ct = default) =>
        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            // (CEEF-229 · م2-١) جارتاها في هذا الملف محروستان (CompleteAsync بـ EnsureCanPost
            // و CancelAsync بـ EnsureCanCancelOrReverse) وهي وحدها سائبة — رغم أنها **تستهلك
            // رقماً من متسلسلة المستندات**، وفجوةُ الترقيم أثرٌ لا يُردم.
            SecurityContext.EnsureCanPost();

            var order = await db.WorkOrders.SingleAsync(w => w.CompanyId == companyId && w.Id == workOrderId, token);
            // ‏(جاهزيّةُ الإطلاق · ح-٧٧) ‏**والوصفةُ تُجمَّد عند الإصدار** — فتُقرأ القائمةُ قبل حجز الرقم،
            // ‏وقائمةٌ لا تُوجد تُسقط الإصدارَ قبل أن يُستهلك رقمٌ من المتسلسلة.
            var bom = await db.BillsOfMaterials.SingleAsync(b => b.CompanyId == companyId && b.Id == order.BomId, token);
            var number = await DocumentNumberGenerator.NextAsync(db, 
                db.WorkOrders.Where(w => w.CompanyId == companyId).Select(w => w.OrderNumber),
                "WO", order.OrderDate.Year, companyId);
            order.Release(number, bom);
            await db.SaveChangesAsync(token);
            return order;
        }, ct);

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٣٤) <b>الإكمالُ يُؤرّخ يومَ وقوعه لا يومَ
    /// مسودّة الأمر.</b>
    ///
    /// <para>كانت <b>سبعةُ مواضعَ</b> تأخذ <c>order.OrderDate</c>: حركتا المخزون،
    /// وحارسُ الفترة، والقيدان، وترقيمُهما. <b>وأمرٌ يُسودّ في ديسمبر
    /// ويُكمَل في يناير يُدخل إنتاجَ ينايرَ في ديسمبر</b> — فتكلفةُ
    /// المبيعات وقيمةُ المخزون تقعان في السنة الخطأ.
    ///
    /// <para><b>وبعد ح-١ صار أشدّ</b>: أمرٌ من شهرٍ أُقفِل <b>لا يُكمَل أبداً</b>
    /// — يرفضُه حارسُ الفترة بحقّ، <b>والخطأُ في التاريخ المُغذّى إليه</b>.
    /// <b>فتبقى بضاعةٌ مُنتَجةٌ لا تدخل الدفاتر وموادُ لا تُصرَف.</b></para>
    ///
    /// <para><b>والنمطُ الصحيحُ في هذا الملفّ نفسِه</b>: <c>CancelAsync</c> أسفلَها
    /// تأخذ <c>DateOnly? cancellationDate = null</c> ثمّ <c>?? DateOnly.FromDateTime(DateTime.Today)</c>.
    /// <b>فهذا نقلُ نمطٍ قائمٍ لا اختراع</b> — واختيارُ الوسيط الاختياريّ
    /// <b>يُبقي كلّ موضعِ نداءٍ قائماً ويُصحّح سلوكَه معاً</b>.</para>
    /// </summary>
    /// <param name="outputBatchNumber">(جاهزيّةُ الإطلاق · ح-١٠١ · <c>manufacturing#6</c>) دفعةُ الإنتاج — <b>مطلوبةٌ لمنتجٍ تامٍّ
    /// متتبَّع</b> ومرفوضةٌ لغيره.</param>
    /// <param name="outputExpiry">صلاحيةُ دفعة الإنتاج إن كانت لها صلاحية.</param>
    public async Task<WorkOrder> CompleteAsync(long companyId, long workOrderId, decimal producedQuantity,
        string username, DateOnly? completionDate = null, string? outputBatchNumber = null,
        DateOnly? outputExpiry = null, CancellationToken ct = default, decimal scrapQuantity = 0m,
        bool closeOrder = true, IReadOnlyList<(long ProductId, decimal Quantity, decimal UnitValue)>? byProducts = null)
    {
        SecurityContext.EnsureCanPost();
        username = SecurityContext.ResolveUsername(username);
        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var order = await db.WorkOrders.SingleAsync(w => w.CompanyId == companyId && w.Id == workOrderId, token);
            var completedOn = completionDate ?? DateOnly.FromDateTime(DateTime.Today);
            var bom = await db.BillsOfMaterials.SingleAsync(b => b.Id == order.BomId, token);

            // ‏(جاهزيّةُ الإطلاق · ح-٧٧ · ‏`manufacturing#5`) ‏**الوصفةُ وصفةُ الأمر يومَ صدوره — لا وصفةُ القائمة اليوم.**
            //
            // ‏كان هذا يقرأ `bom.Lines` و`bom.LabourCostPerUnit` و`bom.OverheadRatePercent` **وقتَ الإكمال**؛
            // ‏فقائمةٌ تُعدَّل بين الإصدار والإكمال **تُغيّر ما يُصرَف من المخزون وبكم يدخل التامُّ** لأمرٍ لم
            // ‏يُطلب له ذلك. **والقائمةُ هنا للمنتَج وكمّيّة الدفعة وحدَهما** — وكلاهما لا يتغيّر بعد إنشائها.
            //
            // ‏**وأمرٌ صادرٌ بلا وصفةٍ مجمَّدة يُرفض** ولا يُستكمَل من قائمة اليوم: المُرقِّعُ يُجمّد عند الإقلاع
            // ‏كلَّ أمرٍ صدر قبل الترقية، فبقاءُ واحدٍ بلا وصفةٍ خللٌ يُقال لا يُغطّى.
            if (order.Status == WorkOrderStatus.Released && !order.RecipeFrozen)
                throw new AccountingException(
                    $"أمر الإنتاج {order.OrderNumber} صادرٌ بلا وصفةٍ مجمَّدة — ألغِه وأصدره من جديد.");

            var inv = new InventoryService(db);
            // ‏(جاهزيّةُ الإطلاق · ح-٩٧ · ‏`manufacturing#7`) ‏**مستودعا الأمر — لا الافتراضيُّ أبداً.**
            //
            // ‏كان هذا `GetDefaultWarehouseAsync` للصرف والاستلام معاً؛ فمصنعٌ يحفظ خاماته في غير الافتراضيّ **لا يُكمل أمراً**
            // ‏(الاستهلاكُ يطلب طبقاتِ الخامة في الافتراضيّ فلا يجدها)، والتامُّ يدخل الافتراضيَّ ولو كان له مستودع.
            // ‏**والفارغُ هو الافتراضيّ** كما كان كلُّ أمرٍ قبله؛ **والمستودعُ يُسأل هنا قبل أيّ صرف** — فمستودعٌ أُوقف بعد
            // ‏الإصدار يُرفض باسمه ولا تُرحَّل حركةٌ ولا قيد.
            var materialsWarehouseId = await WarehouseForAsync(inv, companyId, order.MaterialsWarehouseId, "مستودع الموادّ", token);
            var outputWarehouseId = await WarehouseForAsync(inv, companyId, order.OutputWarehouseId, "مستودع الإنتاج", token);

            // ‏(جاهزيّةُ الإطلاق · ح-١٠١ · ‏`manufacturing#6`) ‏**التامُّ المتتبَّعُ يدخل بدفعته — ويُسأل عنها قبل أيّ صرف.**
            // ‏وكان كلُّ أمرٍ لصنفٍ متتبَّعٍ — خامةً أو تامّاً — يُرفض عند المَخنَق «لا يحمل هذا السطرُ هويّةً»: فلا يمرّ أمرٌ واحد.
            var finished = await db.Products.IgnoreQueryFilters().SingleAsync(p => p.Id == bom.FinishedProductId, token);
            // ‏والفارغُ والمسافاتُ لا دفعة؛ والتشذيبُ في `StockMovement.AddLine` نفسِه (تشذيبٌ ثانٍ هنا لم يعضّ فأُزيل).
            var outputBatch = string.IsNullOrWhiteSpace(outputBatchNumber) ? null : outputBatchNumber;
            if (finished.IsTracked && outputBatch is null)
                throw new AccountingException(
                    $"المنتج التامّ «{finished.NameAr}» يُتتبَّع بالدفعة — أدخِل رقمَ دفعة الإنتاج لإكمال الأمر {order.OrderNumber}.");
            // ‏ودفعةٌ لتامٍّ لا يُتتبَّع يرفضها المَخنَقُ نفسُه باسمه (`ApplyBatchDeltaAsync`) — فلا حارسَ ثانٍ هنا.

            // ‏(جاهزيّةُ الإطلاق · ح-١٤٧ · ‏`manufacturing#4`) ‏**أمرٌ صُرف له فعلاً يُكمَل بما صُرف — لا بالوصفة.**
            // ‏فالموادُّ في الإنتاج تحت التشغيل منذ صرفها، ولا يُخصم من المخزون شيءٌ ثانٍ عند الإكمال؛ **والفرقُ عن الوصفة
            // ‏(هدرٌ أو وفر) يظهر في تكلفة الوحدة**. ‏وأمرٌ بلا صرفٍ يُخصم بالوصفة كما كان حرفاً.
            decimal materialCost;
            List<(long ProductId, decimal Qty, decimal UnitCost, string? BatchNumber, DateOnly? ExpiryDate)> issueLines;
            if (order.UsesActualIssues)
            {
                // ‏(ح-١٤٨) ‏**نصيبُ هذه الدفعة** من الباقي في WIP — والإغلاقُ يأخذ الباقيَ كلَّه.
                materialCost = order.IssuedCostToRelieve(producedQuantity, closeOrder);
                issueLines = [];
            }
            else
            {
                var scale = producedQuantity / bom.OutputQuantity;
                (materialCost, issueLines) = await ConsumeMaterialsAsync(companyId, order,
                    order.Materials.Select(m => (m.ComponentProductId,
                        Math.Round(m.Quantity * scale, 4, MidpointRounding.AwayFromZero))),
                    materialsWarehouseId, completedOn, token);
            }

            // ‏(جاهزيّةُ الإطلاق · ح-١٣) ‏**وتكاليفُ التحويل تدخل التكلفة — وكانت الموادُّ وحدَها.**
            //
            // ‏معيارُ المخزون يقضي أن تشمل تكلفةُ المنتَج التامّ **الأجرَ المباشرَ ونصيباً
            // ‏منهجيّاً من التكاليف الصناعيّة**. وبدونهما يدخل التامُّ المخزونَ بأقلَّ من
            // ‏كلفته: **مخزونٌ مُقوَّمٌ بأدنى من حقيقته، وربحٌ مُعلَنٌ أعلى من حقيقته** —
            // ‏ويظهر الوهمُ في كلّ بيعةٍ لاحقةٍ لأنّ تكلفةَ المبيعات تخرج ناقصة.
            //
            // ‏**والنصيبُ على (الموادّ + الأجر)** لا على الموادّ وحدَها، فالأجرُ جزءٌ من
            // ‏وعاء التحميل. **وصفرٌ يُبقي الحسابَ كما كان حرفاً.**
            var labourCost = Math.Round(
                order.RecipeLabourCostPerUnit * producedQuantity, 2, MidpointRounding.AwayFromZero);
            var overheadCost = Math.Round(
                (materialCost + labourCost) * order.RecipeOverheadRatePercent / 100m,
                2, MidpointRounding.AwayFromZero);

            var conversionCost = labourCost + overheadCost;
            var totalProductionCost = materialCost + conversionCost;

            // ‏(جاهزيّةُ الإطلاق · ح-١٤٩ · ‏`manufacturing#4`) ‏**المنتجُ الثانويّ يدخل المخزونَ بقيمته المعلنة، وتُطرح من تكلفة الرئيسيّ**
            // ‏(طريقةُ صافي القيمة القابلة للتحقّق للمنتج الثانويّ) — فلا تُحمَّل نخالةُ الطحين أو قصاصةُ الحديد على وحدة الرئيسيّ،
            // ‏ولا يدخل المخزونَ صنفٌ بلا قيمة. ‏والمجموعُ لا يتغيّر: ما يخرج من WIP يدخل المخزونَ كلُّه.
            var byProductLines = await ByProductLinesAsync(companyId, bom.FinishedProductId, byProducts, token);
            var byProductValue = byProductLines.Sum(l => Math.Round(l.Qty * l.UnitCost, 2, MidpointRounding.AwayFromZero));
            if (byProductValue > totalProductionCost)
                throw new AccountingException(
                    $"قيمةُ المنتجات الثانويّة ({byProductValue:N2}) تتجاوز تكلفةَ الدفعة ({totalProductionCost:N2}) — والرئيسيُّ لا يدخل بتكلفةٍ سالبة.");

            var unitCost = producedQuantity > 0
                ? Math.Round((totalProductionCost - byProductValue) / producedQuantity, 4, MidpointRounding.AwayFromZero)
                : 0m;

            if (issueLines.Count > 0)
                await inv.PostDocumentMovementAsync(companyId, materialsWarehouseId, StockMovementType.Issue,
                    completedOn, $"صرف مواد — أمر {order.OrderNumber}", $"WorkOrder:{order.Id}", issueLines);

            await inv.PostDocumentMovementAsync(companyId, outputWarehouseId, StockMovementType.Receipt,
                completedOn, $"إنتاج منتج — أمر {order.OrderNumber}", $"WorkOrder:{order.Id}",
                [(bom.FinishedProductId, producedQuantity, unitCost, outputBatch, outputBatch is null ? null : outputExpiry),
                 .. byProductLines]);

            var posting = new JournalPostingService(db);
            await posting.EnsurePeriodOpenAsync(companyId, completedOn);

            // ‏(جاهزيّةُ الإطلاق · ح-٩٨ · ‏`manufacturing#8`) ‏**تحت التشغيل حسابُ المصنع — لا أصلُ عقود المقاولات (1113).**
            // ‏ويُنشأ من القالب لمنشأةٍ قائمةٍ لا تحمله، **وحسابٌ بالرمز نفسِه ليس أصلاً قابلاً للترحيل يُرفض باسمه** — فرمزٌ
            // ‏أنشأه المالكُ لغرضٍ آخر لا يُحمَّل بإنتاج المصنع صامتاً.
            var wip = await SystemAccountProvisioning.EnsureAsync(
                db, companyId, SaudiChartOfAccounts.SystemCodes.ProductionWorkInProcess, token);
            if (wip.IsGroup || wip.Type != AccountType.Asset)
                throw new AccountingException(
                    $"الحساب {wip.Code} «{wip.NameAr}» ليس حسابَ أصلٍ قابلاً للترحيل — وهو رمزُ الإنتاج تحت التشغيل؛ صحّحه من دليل الحسابات.");
            var inventory = await AccountAsync(companyId, SaudiChartOfAccounts.SystemCodes.Inventory, token);
            // ‏(ح-١٤٨) ‏مفتاحا الدفعة: الأولى بالمفتاحين القديمين (فلا يتغيّر شيءٌ لأمرٍ بدفعةٍ واحدة)، والتاليةُ بترتيبها.
            var completion = order.CompletionCount + 1;
            var entry = JournalEntry.CreateDraft(companyId, completedOn,
                $"إكمال أمر إنتاج {order.OrderNumber}", CompleteSource(order.Id, completion));
            // ‏(ح-١٣) ‏**وWIP يُحمَّل بالثلاثة**: الموادُّ من المخزون، والأجرُ والتكاليفُ
            // ‏الصناعيّةُ من حسابَيهما — **فتُنقَل تكلفةٌ وقعت، لا تُخترع**. وحسابا 5106
            // ‏و5107 موجودان في الدليل منذ نشأته **ولا يكتبهما أحد**؛ وبعد هذا يحملان
            // ‏المُحمَّلَ دائناً، **ورصيدُهما الباقي هو فرقُ التحميل** يُقرأ في قائمة الدخل.
            // ‏(ح-١٤٧) ‏والموادُّ المصروفةُ فعلاً في WIP سلفاً — فلا يُحمَّل هنا إلا التحويل.
            var chargedNow = order.UsesActualIssues ? conversionCost : totalProductionCost;
            if (chargedNow > 0m)
                entry.AddLine(wip, chargedNow, 0m, "تحميل WIP");
            if (!order.UsesActualIssues)
                entry.AddLine(inventory, 0m, materialCost, "صرف مواد للإنتاج");

            if (labourCost > 0m)
            {
                var directLabour = await AccountAsync(companyId, "5106", token);
                entry.AddLine(directLabour, 0m, labourCost, "تحميل أجور عمالة مباشرة");
            }

            if (overheadCost > 0m)
            {
                var overhead = await AccountAsync(companyId, "5107", token);
                entry.AddLine(overhead, 0m, overheadCost, "تحميل تكاليف صناعية غير مباشرة");
            }

            var entry2 = JournalEntry.CreateDraft(companyId, completedOn,
                $"استلام منتج تام — {order.OrderNumber}", ReceiptSource(order.Id, completion));
            entry2.AddLine(inventory, totalProductionCost, 0m, "منتج تام");
            entry2.AddLine(wip, 0m, totalProductionCost, "إغلاق WIP");

            if (entry.Lines.Count > 0)
            {
                var num1 = await new JournalEntryNumberGenerator(db).NextAsync(companyId, completedOn);
                entry.Post(num1);
                db.JournalEntries.Add(entry);
            }

            var num2 = await new JournalEntryNumberGenerator(db).NextAsync(companyId, completedOn);
            entry2.Post(num2);
            db.JournalEntries.Add(entry2);
            await db.SaveChangesAsync(token);

            var relieved = order.UsesActualIssues ? materialCost : 0m;
            if (closeOrder)
                order.Complete(producedQuantity, scrapQuantity, relieved);
            else
                order.CompletePartially(producedQuantity, relieved, scrapQuantity);
            order.LinkCompletionJournal(entry2.Id);
            await db.SaveChangesAsync(token);

            await new AuditService(db).LogAsync(companyId, username, "إكمال أمر إنتاج",
                "WorkOrder", order.OrderNumber ?? order.Id.ToString(),
                $"كمية: {producedQuantity:N2}" + (scrapQuantity > 0m ? $" — تالف: {scrapQuantity:N2}" : ""));

            return order;
        }, ct);
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-١٤٧ · <c>manufacturing#4</c>) <b>صرفُ موادّ فعليٌّ للأمر قبل إكماله</b> — بالكميّة التي خرجت.
    /// </summary>
    /// <remarks>
    /// يُخصم من مستودع موادّ الأمر بتكلفة المخزون، ويُدان الإنتاجُ تحت التشغيل (1119) ويُدان المخزون —
    /// <b>فيحمل WIP رصيداً بين الصرف والإكمال</b> (<c>manufacturing#8</c>). والصنفُ يجب أن يكون من وصفة الأمر
    /// المجمَّدة: البديلُ قرارُ وصفةٍ لا صرف. وصرفٌ ثانٍ وثالثٌ يُقبلان — لكلٍّ قيدُه.
    /// </remarks>
    public async Task<decimal> IssueMaterialsAsync(long companyId, long workOrderId,
        IReadOnlyList<(long ProductId, decimal Quantity)> issues, string username,
        DateOnly? issueDate = null, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPost();
        username = SecurityContext.ResolveUsername(username);
        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var order = await db.WorkOrders.SingleAsync(w => w.CompanyId == companyId && w.Id == workOrderId, token);
            if (order.Status != WorkOrderStatus.Released || !order.RecipeFrozen)
                throw new AccountingException($"لا يُصرف إلا لأمرٍ صادرٍ بوصفةٍ مجمَّدة — الأمر {order.OrderNumber ?? order.Id.ToString()}.");
            if (issues.Count == 0 || issues.Any(i => i.Quantity <= 0m))
                throw new AccountingException("أدخِل صنفاً واحداً على الأقلّ بكميّةٍ موجبة.");

            var recipe = order.Materials.Select(m => m.ComponentProductId).ToHashSet();
            var foreign = issues.Select(i => i.ProductId).Where(id => !recipe.Contains(id)).Distinct().ToList();
            if (foreign.Count > 0)
            {
                var names = await db.Products.IgnoreQueryFilters().Where(p => foreign.Contains(p.Id))
                    .Select(p => p.NameAr).ToListAsync(token);
                throw new AccountingException(
                    $"ليس في وصفة الأمر {order.OrderNumber}: {string.Join("، ", names)} — والبديلُ يُقرَّر في الوصفة لا عند الصرف.");
            }

            var on = issueDate ?? DateOnly.FromDateTime(DateTime.Today);
            await new JournalPostingService(db).EnsurePeriodOpenAsync(companyId, on);
            var inv = new InventoryService(db);
            var warehouseId = await WarehouseForAsync(inv, companyId, order.MaterialsWarehouseId, "مستودع الموادّ", token);

            var (cost, lines) = await ConsumeMaterialsAsync(companyId, order,
                issues.Select(i => (i.ProductId, Math.Round(i.Quantity, 4, MidpointRounding.AwayFromZero))),
                warehouseId, on, token);
            if (cost <= 0m)
                throw new AccountingException("الموادّ المصروفة بلا تكلفة — راجع أرصدة المستودع وتكلفتها.");

            await inv.PostDocumentMovementAsync(companyId, warehouseId, StockMovementType.Issue,
                on, $"صرف مواد فعليّ — أمر {order.OrderNumber}", $"WorkOrder:{order.Id}", lines);

            var wip = await SystemAccountProvisioning.EnsureAsync(
                db, companyId, SaudiChartOfAccounts.SystemCodes.ProductionWorkInProcess, token);
            if (wip.IsGroup || wip.Type != AccountType.Asset)
                throw new AccountingException(
                    $"الحساب {wip.Code} «{wip.NameAr}» ليس حسابَ أصلٍ قابلاً للترحيل — وهو رمزُ الإنتاج تحت التشغيل؛ صحّحه من دليل الحسابات.");
            var inventory = await AccountAsync(companyId, SaudiChartOfAccounts.SystemCodes.Inventory, token);

            var entry = JournalEntry.CreateDraft(companyId, on,
                $"صرف مواد لأمر إنتاج {order.OrderNumber}", IssueSource(order.Id, order.IssueCount + 1));
            entry.AddLine(wip, cost, 0m, "موادّ مصروفة للإنتاج تحت التشغيل");
            entry.AddLine(inventory, 0m, cost, "صرف مواد للإنتاج");
            entry.Post(await new JournalEntryNumberGenerator(db).NextAsync(companyId, on));
            db.JournalEntries.Add(entry);

            order.RecordIssue(cost);
            await db.SaveChangesAsync(token);

            await new AuditService(db).LogAsync(companyId, username, "صرف مواد لأمر إنتاج",
                "WorkOrder", order.OrderNumber ?? order.Id.ToString(), $"{cost:N2}");
            return cost;
        }, ct);
    }

    /// <summary>
    /// (ح-١٤٩) سطورُ المنتجات الثانويّة — <b>صنفٌ مخزنيٌّ من المنشأة غيرُ الرئيسيّ، بكميّةٍ موجبةٍ وقيمةٍ غير سالبة</b>؛
    /// والمتتبَّعُ بالدفعة يُرفض باسمه: دفعتُه تُسأل في شاشةٍ لم تُبنَ لها.
    /// </summary>
    private async Task<List<(long ProductId, decimal Qty, decimal UnitCost, string? BatchNumber, DateOnly? ExpiryDate)>>
        ByProductLinesAsync(long companyId, long finishedProductId,
            IReadOnlyList<(long ProductId, decimal Quantity, decimal UnitValue)>? byProducts, CancellationToken token)
    {
        var lines = new List<(long ProductId, decimal Qty, decimal UnitCost, string? BatchNumber, DateOnly? ExpiryDate)>();
        if (byProducts is null || byProducts.Count == 0)
            return lines;

        var ids = byProducts.Select(b => b.ProductId).Distinct().ToList();
        var products = await db.Products.Where(p => p.CompanyId == companyId && ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, token);
        foreach (var (productId, quantity, unitValue) in byProducts)
        {
            if (!products.TryGetValue(productId, out var product))
                throw new AccountingException("المنتج الثانويّ غير موجود في هذه المنشأة.");
            if (productId == finishedProductId)
                throw new AccountingException($"«{product.NameAr}» هو المنتجُ الرئيسيّ نفسُه — لا يكون ثانويّاً لأمره.");
            if (!product.IsStockable())
                throw new AccountingException($"المنتج الثانويّ «{product.NameAr}» ليس صنفاً مخزنيّاً.");
            if (product.IsTracked)
                throw new AccountingException($"المنتج الثانويّ «{product.NameAr}» يُتتبَّع بالدفعة — ولا تُسأل دفعتُه هنا بعد.");
            if (quantity <= 0m || unitValue < 0m)
                throw new AccountingException($"كميّةُ «{product.NameAr}» موجبةٌ وقيمتُه غيرُ سالبة.");
            lines.Add((productId, Math.Round(quantity, 4, MidpointRounding.AwayFromZero),
                Math.Round(unitValue, 4, MidpointRounding.AwayFromZero), null, null));
        }

        return lines;
    }

    /// <summary>(ح-١٤٨) مفتاحُ قيد إكمال الدفعة <paramref name="n"/> — الأولى بالمفتاح القديم.</summary>
    internal static string CompleteSource(long workOrderId, int n) =>
        n <= 1 ? $"WorkOrderComplete:{workOrderId}" : $"WorkOrderComplete:{workOrderId}:{n}";

    /// <summary>(ح-١٤٨) مفتاحُ قيد استلام الدفعة <paramref name="n"/> — الأولى بالمفتاح القديم.</summary>
    internal static string ReceiptSource(long workOrderId, int n) =>
        n <= 1 ? $"WorkOrderReceipt:{workOrderId}" : $"WorkOrderReceipt:{workOrderId}:{n}";

    /// <summary>مفتاحُ قيد الصرف رقم <paramref name="n"/> للأمر — ويقرؤه الإلغاءُ ليعكسه.</summary>
    internal static string IssueSource(long workOrderId, int n) => $"WorkOrderIssue:{workOrderId}:{n}";

    /// <summary>
    /// استهلاكُ موادّ الأمر من مستودعها بتكلفة المخزون — <b>للخصم العكسيّ عند الإكمال وللصرف الفعليّ معاً</b>،
    /// فلا يختلف المساران في مفتاح الاستهلاك ولا في تخصيص الدفعات.
    /// </summary>
    private async Task<(decimal Cost, List<(long ProductId, decimal Qty, decimal UnitCost, string? BatchNumber, DateOnly? ExpiryDate)> Lines)>
        ConsumeMaterialsAsync(long companyId, WorkOrder order, IEnumerable<(long ProductId, decimal Qty)> quantities,
            long materialsWarehouseId, DateOnly on, CancellationToken token)
    {
        var wanted = quantities.ToList();
        var issueLines = new List<(long ProductId, decimal Qty, decimal UnitCost, string? BatchNumber, DateOnly? ExpiryDate)>();
        decimal materialCost = 0;
        var costing = new InventoryCostingService(db);
        var allocator = new BatchAllocationService(db);
        var materialIds = wanted.Select(m => m.ProductId).Distinct().ToList();
        var trackedMaterials = (await db.Products.IgnoreQueryFilters()
                .Where(p => materialIds.Contains(p.Id) && p.Tracking != InventoryTracking.None)
                .Select(p => p.Id)
                .ToListAsync(token))
            .ToHashSet();

        foreach (var (productId, qty) in wanted)
        {
            // (جاهزيّةُ الإطلاق · ح-٣٠) ‏**مفتاحُ الاستهلاك هو مفتاحُ الحركة بعينه** (`WorkOrder:{Id}`) — فيُرمّم الإلغاءُ ما استُهلك.
            var (totalCost, componentUnitCost) = await costing.ConsumeForIssueAsync(
                companyId, productId, materialsWarehouseId, qty, $"WorkOrder:{order.Id}", token);
            materialCost += totalCost;
            if (!trackedMaterials.Contains(productId))
            {
                issueLines.Add((productId, qty, componentUnitCost, null, null));
                continue;
            }

            // ‏(ح-١٠١) ‏**والخامةُ المتتبَّعةُ تُصرف بالأقدم انتهاءً — سطرٌ لكلّ دفعة**.
            foreach (var draw in await allocator.AllocateAsync(companyId, productId, materialsWarehouseId, qty, on, token))
                issueLines.Add((productId, draw.Quantity, componentUnitCost, draw.BatchNumber, draw.ExpiryDate));
        }

        return (materialCost, issueLines);
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-١٠١) <b>هل منتجُ الأمر التامُّ متتبَّع</b> — فتسأل الشاشةُ عن دفعة الإنتاج قبل الإكمال.
    /// </summary>
    public async Task<bool> OutputIsTrackedAsync(long companyId, long workOrderId, CancellationToken ct = default)
    {
        var bomId = await db.WorkOrders.Where(w => w.CompanyId == companyId && w.Id == workOrderId)
            .Select(w => w.BomId).SingleAsync(ct);
        var productId = await db.BillsOfMaterials.Where(b => b.CompanyId == companyId && b.Id == bomId)
            .Select(b => b.FinishedProductId).SingleAsync(ct);
        return await db.Products.IgnoreQueryFilters().Where(p => p.Id == productId)
            .Select(p => p.Tracking != InventoryTracking.None).SingleAsync(ct);
    }

    public async Task CancelAsync(long companyId, long workOrderId, string username,
        DateOnly? cancellationDate = null, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanCancelOrReverse();
        username = SecurityContext.ResolveUsername(username);
        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var order = await db.WorkOrders.SingleAsync(w => w.CompanyId == companyId && w.Id == workOrderId, token);
            var cancelDate = cancellationDate ?? DateOnly.FromDateTime(DateTime.Today);

            // ‏(ح-١٤٨) ‏**وأمرٌ صادرٌ أُكمل جزئيّاً يُعكس كالمكتمل** — استلاماتُه وصرفُه وحركاتُه — ثمّ يُلغى.
            if (order.Status == WorkOrderStatus.Released && order.CompletionCount > 0)
            {
                await ReverseCompletedWorkOrderAsync(companyId, order, cancelDate, username, token);
                order.Cancel();
            }
            else if (order.Status == WorkOrderStatus.Completed)
            {
                await ReverseCompletedWorkOrderAsync(companyId, order, cancelDate, username, token);
                order.MarkCancelledAfterCompletion();
            }
            else
            {
                // ‏(ح-١٤٧) ‏**وأمرٌ صادرٌ صُرف له يُعكس صرفُه** — تعود الموادُّ ويُفرَّغ WIP؛ وبغيره يبقى رصيدٌ بلا أمر.
                if (order.UsesActualIssues)
                    await ReverseIssuesAsync(companyId, order, cancelDate, username, token);
                order.Cancel();
            }

            await db.SaveChangesAsync(token);
            await new AuditService(db).LogAsync(companyId, username, "إلغاء أمر إنتاج",
                "WorkOrder", order.OrderNumber ?? order.Id.ToString(),
                order.Status == WorkOrderStatus.Cancelled ? "تم الإلغاء مع عكس المخزون/القيود إن لزم" : "تم الإلغاء");
        }, ct);
    }

    private async Task ReverseCompletedWorkOrderAsync(
        long companyId, WorkOrder order, DateOnly cancelDate, string username, CancellationToken token)
    {
        await new JournalPostingService(db).EnsurePeriodOpenAsync(companyId, cancelDate);

        var sourcePrefix = $"WorkOrder:{order.Id}";
        var entryKeys = new[]
        {
            $"WorkOrderComplete:{order.Id}",
            $"WorkOrderReceipt:{order.Id}",
        };

        // ‏(ح-١٤٧) ‏وقيودُ الصرف الفعليّ مع قيدي الإكمال.
        entryKeys = [.. entryKeys, .. Enumerable.Range(1, order.IssueCount).Select(n => IssueSource(order.Id, n))];
        // ‏(ح-١٤٨) ‏وقيدا كلِّ دفعة إكمالٍ بعد الأولى.
        entryKeys = [.. entryKeys, .. Enumerable.Range(2, Math.Max(0, order.CompletionCount - 1))
            .SelectMany(n => new[] { CompleteSource(order.Id, n), ReceiptSource(order.Id, n) })];
        foreach (var key in entryKeys)
        {
            var entry = await db.JournalEntries.FirstOrDefaultAsync(e =>
                e.CompanyId == companyId
                && e.SourceDocument == key
                && e.Status == JournalEntryStatus.Posted, token);
            if (entry is null) continue;

            await new JournalReversalService(db).ReverseAsync(
                companyId, entry.Id, cancelDate, username, fromSourceDocument: true, token);
        }

        var movements = await db.StockMovements
            .Where(m => m.CompanyId == companyId
                        && m.SourceDocument == sourcePrefix
                        && m.Status == StockMovementStatus.Posted)
            .ToListAsync(token);

        if (movements.Count == 0)
            return;

        // (جاهزيّةُ الإطلاق · ح-٣٠) ‏**يُعكَس الأصلُ — لا تُرحَّل حركةٌ مضادّة.**
        //
        // ‏كان الإلغاءُ يُرحّل حركتين مقابلتين: وارِداً مقابلَ صرف المواد، وصادراً مقابلَ
        // ‏استلام التامّ. **والرصيدُ يعود صحيحاً — والتكلفةُ لا تعود.**
        //
        // ‏**فالوارِدُ المقابل يُنشئ طبقةً جديدةً بتكلفةٍ مخلوطة** بدل أن يُرمّم الطبقاتِ
        // ‏التي استهلكها الصرفُ بأعيانها (وقد تكون طبقتين بسعرين). **والصادرُ المقابل
        // ‏يستهلك بالوارد-أوّلاً** فيأكل **طبقةً أقدمَ من طبقة التامّ التي أنشأها هذا
        // ‏الأمر** — فتُحذف تكلفةٌ ليست تكلفتَه ويبقى التامُّ في الطبقات.
        //
        // ‏**فينشقّ مجموعُ الطبقات عن الرصيد**، ويخرج كلُّ بيعٍ لاحقٍ بتكلفةٍ خاطئة.
        //
        // ‏**والمسارُ الصحيحُ قائمٌ وموثَّق**: `ReverseDocumentMovementAsync` — يُرمّم
        // ‏المستهلَكَ للصادر، ويُزيل الطبقةَ التي أنشأها المستندُ بعينها للوارد
        // ‏(‏CEEF-229 · م0-٣ و م0-٣ب). **وخمسُ خدماتِ إلغاءٍ تستعمله، وهذه لم تكن منها** —
        // ‏وتعليقُ `GoodsReceiptCancellationService` يقول نصّاً ما كان يقع هنا:
        // ‏«الطبقات 250 والرصيد 200».
        var inv = new InventoryService(db);
        foreach (var movement in movements.OrderByDescending(m => m.Id))
        {
            await inv.ReverseDocumentMovementAsync(movement);
            movement.MarkCancelled();
        }
    }

    /// <summary>
    /// (ح-٩٧) <b>مستودعُ الأمر إن اختير — في المنشأة وغيرُ موقوف — وإلا الافتراضيّ.</b> والرفضُ يُسمّي الدورَ والمستودع:
    /// فالمالكُ يعرف أيَّ مستودعٍ أوقفه، ولا يقرأ «طبقات FIFO غير كافية» عن مستودعٍ لم يقصده.
    /// </summary>
    /// <summary>(ح-١٤٧) يعكس قيودَ الصرف الفعليّ وحركاتِه لأمرٍ صادرٍ يُلغى قبل إكماله.</summary>
    private async Task ReverseIssuesAsync(long companyId, WorkOrder order, DateOnly cancelDate, string username,
        CancellationToken token)
    {
        await new JournalPostingService(db).EnsurePeriodOpenAsync(companyId, cancelDate);
        for (var n = 1; n <= order.IssueCount; n++)
        {
            var key = IssueSource(order.Id, n);
            var entry = await db.JournalEntries.FirstOrDefaultAsync(e =>
                e.CompanyId == companyId && e.SourceDocument == key && e.Status == JournalEntryStatus.Posted, token);
            if (entry is not null)
                await new JournalReversalService(db).ReverseAsync(
                    companyId, entry.Id, cancelDate, username, fromSourceDocument: true, token);
        }

        var source = $"WorkOrder:{order.Id}";
        var movements = await db.StockMovements
            .Where(m => m.CompanyId == companyId && m.SourceDocument == source && m.Status == StockMovementStatus.Posted)
            .ToListAsync(token);
        var inv = new InventoryService(db);
        foreach (var movement in movements.OrderByDescending(m => m.Id))
        {
            await inv.ReverseDocumentMovementAsync(movement);
            movement.MarkCancelled();
        }
    }

    private async Task<long> WarehouseForAsync(InventoryService inv, long companyId, long? chosenId, string role,
        CancellationToken ct)
    {
        if (chosenId is not long id)
            return (await inv.GetDefaultWarehouseAsync(companyId)).Id;

        var warehouse = await db.Warehouses.SingleOrDefaultAsync(w => w.CompanyId == companyId && w.Id == id, ct)
            ?? throw new AccountingException($"{role} المختارُ لهذا الأمر غير موجود في المنشأة.");
        if (!warehouse.IsActive)
            throw new AccountingException(
                $"{role} «{warehouse.NameAr}» موقوف — فعِّله من شاشة المستودعات، أو ألغِ الأمرَ وأنشئه بمستودعٍ آخر.");

        return warehouse.Id;
    }

    private async Task<Account> AccountAsync(long companyId, string code, CancellationToken ct) =>
        await db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == code, ct);
}
