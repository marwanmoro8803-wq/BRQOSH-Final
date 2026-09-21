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
            var number = await DocumentNumberGenerator.NextAsync(db, 
                db.WorkOrders.Where(w => w.CompanyId == companyId).Select(w => w.OrderNumber),
                "WO", order.OrderDate.Year, companyId);
            order.Release(number);
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
    public async Task<WorkOrder> CompleteAsync(long companyId, long workOrderId, decimal producedQuantity,
        string username, DateOnly? completionDate = null, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPost();
        username = SecurityContext.ResolveUsername(username);
        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var order = await db.WorkOrders.SingleAsync(w => w.CompanyId == companyId && w.Id == workOrderId, token);
            var completedOn = completionDate ?? DateOnly.FromDateTime(DateTime.Today);
            var bom = await db.BillsOfMaterials.SingleAsync(b => b.Id == order.BomId, token);
            var inv = new InventoryService(db);
            var warehouse = await inv.GetDefaultWarehouseAsync(companyId);

            var scale = producedQuantity / bom.OutputQuantity;
            var issueLines = new List<(long ProductId, decimal Qty, decimal UnitCost)>();
            decimal materialCost = 0;
            var costing = new InventoryCostingService(db);

            foreach (var line in bom.Lines)
            {
                var qty = Math.Round(line.Quantity * scale, 4, MidpointRounding.AwayFromZero);
                // (جاهزيّةُ الإطلاق · ح-٣٠) ‏**مفتاحُ الاستهلاك هو مفتاحُ الحركة بعينه.**
                //
                // ‏كان هذا يُسجّل الاستهلاكَ تحت `WorkOrder:{OrderNumber}` **والحركةُ
                // ‏المخزنيّة تُرحَّل تحت `WorkOrder:{Id}`** (انظر أسفله) — **مفتاحان
                // ‏لأمرٍ واحد**. فدفترُ الاستهلاك والحركةُ لا يلتقيان أبداً،
                // ‏و`RestoreConsumedLayersAsync` — وهي تبحث بمفتاح الحركة — **لا تجد
                // ‏شيئاً فلا تُرمّم شيئاً**.
                //
                // ‏**وقيس: طبقةُ المادّة تبقى ستّاً بعد إلغاءٍ يجب أن يُعيدها عشراً.**
                //
                // ‏**والمعرّفُ أثبتُ من الرقم**: رقمُ الأمر نصٌّ قد يكون فارغاً وقتَ
                // ‏الاستهلاك، فيصير المفتاحُ `WorkOrder:` — وهو عينُ صنفِ العطل المسجَّل
                // ‏في التدقيق للإشعار المدين (`DebitNote:` فارغاً).
                var (totalCost, componentUnitCost) = await costing.ConsumeForIssueAsync(
                    companyId, line.ComponentProductId, warehouse.Id, qty,
                    $"WorkOrder:{order.Id}", token);
                materialCost += totalCost;
                issueLines.Add((line.ComponentProductId, qty, componentUnitCost));
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
                bom.LabourCostPerUnit * producedQuantity, 2, MidpointRounding.AwayFromZero);
            var overheadCost = Math.Round(
                (materialCost + labourCost) * bom.OverheadRatePercent / 100m,
                2, MidpointRounding.AwayFromZero);

            var conversionCost = labourCost + overheadCost;
            var totalProductionCost = materialCost + conversionCost;

            var unitCost = producedQuantity > 0
                ? Math.Round(totalProductionCost / producedQuantity, 4, MidpointRounding.AwayFromZero)
                : 0m;

            await inv.PostDocumentMovementAsync(companyId, warehouse.Id, StockMovementType.Issue,
                completedOn, $"صرف مواد — أمر {order.OrderNumber}", $"WorkOrder:{order.Id}", issueLines);

            await inv.PostDocumentMovementAsync(companyId, warehouse.Id, StockMovementType.Receipt,
                completedOn, $"إنتاج منتج — أمر {order.OrderNumber}", $"WorkOrder:{order.Id}",
                [(bom.FinishedProductId, producedQuantity, unitCost)]);

            var posting = new JournalPostingService(db);
            await posting.EnsurePeriodOpenAsync(companyId, completedOn);

            var wip = await AccountAsync(companyId, "1113", token);
            var inventory = await AccountAsync(companyId, SaudiChartOfAccounts.SystemCodes.Inventory, token);
            var entry = JournalEntry.CreateDraft(companyId, completedOn,
                $"إكمال أمر إنتاج {order.OrderNumber}", $"WorkOrderComplete:{order.Id}");
            // ‏(ح-١٣) ‏**وWIP يُحمَّل بالثلاثة**: الموادُّ من المخزون، والأجرُ والتكاليفُ
            // ‏الصناعيّةُ من حسابَيهما — **فتُنقَل تكلفةٌ وقعت، لا تُخترع**. وحسابا 5106
            // ‏و5107 موجودان في الدليل منذ نشأته **ولا يكتبهما أحد**؛ وبعد هذا يحملان
            // ‏المُحمَّلَ دائناً، **ورصيدُهما الباقي هو فرقُ التحميل** يُقرأ في قائمة الدخل.
            entry.AddLine(wip, totalProductionCost, 0m, "تحميل WIP");
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
                $"استلام منتج تام — {order.OrderNumber}", $"WorkOrderReceipt:{order.Id}");
            entry2.AddLine(inventory, totalProductionCost, 0m, "منتج تام");
            entry2.AddLine(wip, 0m, totalProductionCost, "إغلاق WIP");

            var num1 = await new JournalEntryNumberGenerator(db).NextAsync(companyId, completedOn);
            entry.Post(num1);
            db.JournalEntries.Add(entry);

            var num2 = await new JournalEntryNumberGenerator(db).NextAsync(companyId, completedOn);
            entry2.Post(num2);
            db.JournalEntries.Add(entry2);
            await db.SaveChangesAsync(token);

            order.Complete(producedQuantity);
            order.LinkCompletionJournal(entry2.Id);
            await db.SaveChangesAsync(token);

            await new AuditService(db).LogAsync(companyId, username, "إكمال أمر إنتاج",
                "WorkOrder", order.OrderNumber ?? order.Id.ToString(), $"كمية: {producedQuantity:N2}");

            return order;
        }, ct);
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

            if (order.Status == WorkOrderStatus.Completed)
            {
                await ReverseCompletedWorkOrderAsync(companyId, order, cancelDate, username, token);
                order.MarkCancelledAfterCompletion();
            }
            else
            {
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

    private async Task<Account> AccountAsync(long companyId, string code, CancellationToken ct) =>
        await db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == code, ct);
}
