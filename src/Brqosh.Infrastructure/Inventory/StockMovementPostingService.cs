using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Platform;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Companies;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Platform;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Inventory;

/// <summary>ترحيل حركات المخزون اليدوية مع القيود المحاسبية حيث يلزم.</summary>
public class StockMovementPostingService(BrqoshDbContext db) : Brqosh.Application.Accounting.IStockMovementPostingService
{
    public async Task<StockMovement> PostAsync(StockMovement movement)
    {
        SecurityContext.EnsureCanPost();
        return await UnitOfWork.ExecuteInTransactionAsync(db, async ct =>
        {
            if (movement.BranchId is null)
                movement.AssignBranch(await new BranchAssignmentService(db)
                .ResolveBranchIdAsync(movement.CompanyId, BranchSessionContext.PreferredBranchId, ct));

            await FinancialPostingSupport.ValidateAsync(
                db, movement.CompanyId, movement.MovementDate, PostingDocumentKind.StockMovement, ct: ct);

            var inventory = await AccountAsync(movement.CompanyId, SaudiChartOfAccounts.SystemCodes.Inventory);
            var inv = new InventoryService(db);

            if (movement.IsOutbound || movement.Type == StockMovementType.Transfer)
            {
                foreach (var line in movement.Lines)
                {
                    var onHand = await inv.GetOnHandAsync(movement.CompanyId, line.ProductId, movement.WarehouseId);
                    if (line.Quantity > onHand)
                    {
                        var p = await db.Products.SingleAsync(x => x.Id == line.ProductId, ct);
                        throw new AccountingException($"رصيد '{p.NameAr}' غير كافٍ — المتوفر {onHand:N2}.");
                    }
                }
            }

            var entry = await BuildJournalEntryAsync(movement, inventory, inv);

            if (movement.IsInbound)
            {
                // (CEEF-229 · م0-٥ / I-NEW-2) ‏**المتوسطُ يُحسب لكلّ صنفٍ مرّةً، والطبقةُ لكلّ سطر.**
                //
                // ‏كان النداءُ لكلّ سطرٍ على حدة، وكلٌّ منها يقرأ الرصيدَ ليبني المتوسط —
                // ‏**والرصيدُ لا يتقدّم**، لأنّ `ApplyMovementBalancesAsync` أدناه تُطبّق
                // ‏سطورَ المستند كلَّها **بعد** هذه الحلقة. فكلُّ سطرٍ يحسب كأنّه وحده.
                //
                // ‏**مقيسٌ:** افتتاحيٌّ 10@10 ثمّ وارِدٌ بثلاثة سطور لصنفٍ واحد
                // ‏(2@12 · 3@15 · 5@20) ⟵ المتوسط يتدحرج `10.3333 → 11.4103 → 14.2735`
                // ‏والصواب `13.45`. وعند الترحيل مباشرةً — **قبل أيّ إلغاء** — يصير
                // ‏الدفترُ المساعد **285.47** والأستاذُ **269.00**: هوية هـ-٣ مكسورةً
                // ‏بـ16.47 ريالاً. **انفصالُ دفترٍ مساعدٍ عن الأستاذ، لا رقمٌ متّسق.**
                //
                // ‏**ولا يقع في مسار المستندات** (`PostDocumentMovementAsync`): هناك يُطبَّق
                // ‏رصيدُ كلّ سطرٍ داخل الحلقة نفسِها، فيتقدّم. فالعطلُ في حركات المخزون
                // ‏اليدوية وحدها — **الرصيدِ الافتتاحيّ وتسويةِ الوارد**، وهما أوّلُ ما
                // ‏تُدخله منشأةٌ جديدة.
                //
                // ‏والتكلفةُ الموحَّدة **مرجّحةٌ بالكمية**: قيمةُ ما دخل مقسومةً على كمّيته.
                var costing = new InventoryCostingService(db);
                foreach (var group in movement.Lines.GroupBy(l => l.ProductId))
                {
                    var stockable = false;
                    foreach (var line in group)
                    {
                        stockable = await costing.AddCostLayerAsync(
                            movement.CompanyId, line.ProductId, movement.WarehouseId,
                            movement.MovementDate, line.Quantity, line.UnitCost,
                            movement.SourceDocument ?? movement.Description ?? "StockMovement", ct);
                    }

                    if (!stockable) continue;

                    var quantity = group.Sum(l => l.Quantity);
                    if (quantity <= 0) continue;

                    await inv.ApplyReceiptCostAsync(movement.CompanyId, group.Key, quantity,
                        group.Sum(l => l.Quantity * l.UnitCost) / quantity);
                }
            }

            var number = await DocumentNumberGenerator.NextAsync(db, 
                db.StockMovements.Where(m => m.CompanyId == movement.CompanyId).Select(m => m.MovementNumber),
                "SM", movement.MovementDate, movement.CompanyId,
                db.StockMovements.Local.Where(m => m.CompanyId == movement.CompanyId).Select(m => m.MovementNumber));

            // (CEEF-231 · ط4-٤) ‏**والتحويلُ ينقل طبقاتِ التكلفة معه.**
            //
            // ‏**ما كان:** التحويلُ يُحرّك الكمّياتِ **ولا يمسّ الطبقاتِ البتّة**. فمستودعٌ
            // ‏يُفرَّغ عن آخره تبقى طبقاتُه فيه كاملةً، **والمستقبِلُ يمتلئ بلا طبقةٍ واحدة**.
            // ‏فجردُ الأوّل يُقوَّم بمالٍ لبضاعةٍ ليست فيه، والثاني بصفرٍ وفيه بضاعة،
            // ‏**وبيعٌ من الأوّل بعدها يُرفض بـ«طبقات FIFO غير كافية»** وفيه رصيدٌ ماديّ.
            //
            // ‏**والموضعُ هنا لا في `InventoryService.TransferAsync`** — أو بالأحرى في
            // ‏كليهما: تلك بلا مستدعٍ في المستودع كلِّه، **وهذه ما تستدعيه الشاشة**.
            // ‏وإصلاحُ أحد مسارَي حسابٍ واحدٍ هو عطلُ `ط4-٣` بعينه عائداً.
            //
            // ‏**والرقمُ يُخصَّص قبل الاستهلاك** — فهو مفتاحُ سجلّ الترميم عند الإلغاء.
            if (movement.Type == StockMovementType.Transfer && movement.ToWarehouseId is long destination)
            {
                // ‏**والمفتاحُ يُشتقّ من رقم الحركة ولا يُخزَّن في `SourceDocument`.**
                //
                // ‏فذلك الحقلُ معناه في هذا المستودع **«حركةٌ يملكها مستندٌ آخر»** —
                // ‏ويرفض `StockMovementCancellationService` إلغاءَ ما يحمله. **فتخزينُه
                // ‏هنا يمنع إلغاءَ كلّ تحويل**، وهو عطلٌ أُدخل باسم إصلاح. والرقمُ محفوظٌ
                // ‏ثابتٌ يُشتقّ منه المفتاحُ نفسُه عند العكس.
                var costKey = $"StockMovement:{number}";
                var costing = new InventoryCostingService(db);

                foreach (var line in movement.Lines)
                {
                    var (_, unitCost) = await costing.ConsumeForIssueAsync(
                        movement.CompanyId, line.ProductId, movement.WarehouseId,
                        line.Quantity, costKey, ct);

                    await costing.AddCostLayerAsync(movement.CompanyId, line.ProductId, destination,
                        movement.MovementDate, line.Quantity, unitCost, costKey, ct);
                }
            }

            await inv.ApplyMovementBalancesAsync(movement);

            movement.MarkPosted(number);

            if (entry is not null)
            {
                var entryNumber = await new JournalEntryNumberGenerator(db)
                    .NextAsync(movement.CompanyId, movement.MovementDate);
                entry.Post(entryNumber);
                db.JournalEntries.Add(entry);
                await db.SaveChangesAsync(ct);
                movement.LinkJournalEntry(entry.Id);
            }

            // (CEEF-230 · ط2-٠) ‏يُضاف إن لم يكن محفوظاً — واصطلاحُ `JournalPostingService` نفسُه.
            // ‏وكان الإضافةُ بلا شرطٍ في تسع خدمات، **وأسقطت شاشةَ الرواتب بـ`23505`** حين
            // ‏حفظت مسودّتَها ثمّ رحّلتها. والشرطُ يجعل المسارين مقبولين معاً، **فيُغلق
            // ‏صنفُ العطل لا حالتُه**: شاشةٌ تُكتب غداً بالاصطلاح الآخر لا تسقط.
            if (movement.Id == 0)
                db.StockMovements.Add(movement);
            await db.SaveChangesAsync(ct);

            await DomainEventHub.PublishAsync(new DocumentPostedEvent(
                movement.CompanyId, "StockMovement", movement.Id, movement.MovementNumber!), ct);
            if (entry is not null)
            {
                await DomainEventHub.PublishAsync(new JournalEntryPostedEvent(
                    movement.CompanyId, entry.Id, entry.EntryNumber!, movement.MovementDate), ct);
            }

            await FinancialPostingSupport.AuditPostAsync(
                db, movement.CompanyId, "StockMovement", movement.MovementNumber!,
                reason: "ترحيل حركة مخزون", ct: ct);

            return movement;
        });
    }

    private async Task<JournalEntry?> BuildJournalEntryAsync(StockMovement movement, Account inventory, InventoryService inv)
    {
        // (CEEF-229 · م2-٢١) حركةٌ قيمتُها صفر **لا قيدَ لها** — لا قيدٌ بأصفار.
        //
        // كان القيد يُبنى دائماً، فيمرّ على `AddLine(inventory, 0m, 0m)` ويرمي «سطر القيد يجب
        // أن يحمل مبلغاً مديناً أو دائناً». وذلك يقع في **اليوم الأول** من حياة كل منشأة:
        // تُدخل دليل أصنافها ثم تجري جرداً لإثبات الأرصدة الافتتاحية، وكلُّ صنفٍ لم يُشترَ بعدُ
        // متوسطُ تكلفته صفر — فيرتدّ الجرد **بأكمله** برسالةٍ لا علاقة لها بما فعل المستخدم،
        // وعمودُ تكلفة الوحدة في الشاشة للقراءة فقط فلا يستطيع تصحيحه.
        //
        // والامتناع عن القيد ليس التفافاً بل الصواب المحاسبيّ: حركةٌ بلا قيمة لا أثر لها في
        // الأستاذ. والكمية تُسجَّل، وهوية المخزون (رصيد الحساب == Σ الكمية × المتوسط) تبقى
        // صادقة لأن الطرفين صفر. ويظلّ الجردُ المختلط يُرحَّل قيدَ ما له تكلفة وحده.
        // الحارس داخل كل بانٍ لا هنا: `AdjustmentOut` يستهلك طبقات التكلفة قبل أن يعرف
        // مجموعه، فالخروج المبكر هنا كان سيتخطّى الاستهلاك ويترك طبقاتٍ معلّقة.
        return movement.Type switch
        {
            StockMovementType.Opening => await OpeningEntryAsync(movement, inventory),
            StockMovementType.AdjustmentIn => await AdjustmentInEntryAsync(movement, inventory),
            StockMovementType.AdjustmentOut => await AdjustmentOutEntryAsync(movement, inventory, inv),
            StockMovementType.Transfer => null,
            _ => throw new AccountingException("نوع الحركة غير مدعوم."),
        };
    }

    private async Task<JournalEntry?> OpeningEntryAsync(StockMovement movement, Account inventory)
    {
        if (movement.TotalValue == 0m) return null;

        // ‏(جاهزيّةُ الإطلاق · ح-١٣٥) **بعد الميزان الافتتاحيّ تُدائَن 3107 لا الأرباحُ المبقاة**: الميزانُ أقام حقوقَ الملكية
        // ‏كاملةً، ورصيدُ المخزون فيه وقف في 3107 حتى تُفصّله الحركاتُ صنفاً صنفاً — فحركةٌ افتتاحيّةٌ بعده تفصيلٌ لرقمه لا ملكيّةٌ
        // ‏جديدة، ومُدائنةُ 3103 تُضاعف الملكيّةَ بقيمة المخزون وتُبقي 3107 مديناً أبداً. وقبل الميزان (أو بلا ميزان) كما كانت.
        var afterTheOpeningTrialBalance = await db.JournalEntries
            .Where(e => e.CompanyId == movement.CompanyId
                        && e.SourceDocument == Setup.CsvDataMigrationService.OpeningTrialBalanceSource)
            .WhereStanding(db.JournalEntries)
            .AnyAsync();
        var counter = await AccountAsync(movement.CompanyId, afterTheOpeningTrialBalance
            ? Setup.CsvDataMigrationService.OpeningClearingCode
            : SaudiChartOfAccounts.SystemCodes.RetainedEarnings);

        var entry = JournalEntry.CreateDraft(movement.CompanyId, movement.MovementDate,
            movement.Description, "StockOpening");
        entry.AddLine(inventory, movement.TotalValue, 0m, "رصيد افتتاحي للمخزون");
        await RerouteInboundAsync(movement, entry, inventory);
        entry.AddLine(counter, 0m, movement.TotalValue, afterTheOpeningTrialBalance
            ? "رصيد افتتاحي — تفصيلُ مخزون الميزان"
            : "رصيد افتتاحي — حقوق ملكية");
        return entry;
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٢٣) <b>فائضُ الجرد ربحُ الفترة — لا إضافةٌ إلى حقوق الملكية.</b>
    ///
    /// <para><b>وكان يُدائن الأرباحَ المبقاة مباشرةً</b>، <b>فلا يمرّ بقائمة الدخل أبداً</b>.
    /// والنقصُ يُقيَّد مصروفاً على <c>5215</c> — <b>فالمعالجتان غيرُ متماثلتين</b>:</para>
    ///
    /// <para><b>جردٌ يجد نقصاً بألفٍ وفائضاً بألف</b> ⟵ حقوقُ الملكية لا تتغيّر،
    /// <b>والربحُ المُعلَن ينقص ألفاً كاملة</b>. <b>ويتكرّر مع كلّ جرد</b>، فتُعلَن أرباحٌ
    /// أدنى من الحقيقة دائماً — <b>وتُبنى عليها الزكاةُ والتوزيعات</b>.</para>
    ///
    /// <para><b>وتنكسر معه هويّةٌ أساسيّة</b>: صافي الدخل + الأرباحُ المبقاة أوّلَ المدّة
    /// ≠ الأرباحُ المبقاة آخرَها. <b>فقائمةُ الدخل لا تُفسّر تغيّرَ حقوق الملكية</b>،
    /// ولا يظهر الفرقُ في ميزان المراجعة لأنّ القيد متوازن.</para>
    ///
    /// <para><b>والمقابلُ الصحيحُ <c>4106</c> «إيرادات أخرى»</b> — نظيرُ <c>5215</c>
    /// تماماً، وهو الحسابُ الذي يحمل ربحَ استبعاد الأصل الثابت. <b>فيتماثل الطرفان.</b></para>
    ///
    /// <para><b>ولا يُمَسّ الرصيدُ الافتتاحيّ</b> (<see cref="OpeningEntryAsync"/>): ذاك
    /// <b>حقوقُ ملكيّةٍ بحقّ</b> — بضاعةٌ دخلت يومَ التأسيس لا ربحٌ كُسب في فترة.</para>
    /// </summary>
    private async Task<JournalEntry?> AdjustmentInEntryAsync(StockMovement movement, Account inventory)
    {
        if (movement.TotalValue == 0m) return null;

        var otherIncome = await AccountAsync(movement.CompanyId, SaudiChartOfAccounts.SystemCodes.OtherIncome);

        var entry = JournalEntry.CreateDraft(movement.CompanyId, movement.MovementDate,
            movement.Description, "StockAdjustmentIn");
        entry.AddLine(inventory, movement.TotalValue, 0m, "زيادة مخزون — جرد");
        entry.AddLine(otherIncome, 0m, movement.TotalValue, "فائض جرد — إيراد");
        await RerouteInboundAsync(movement, entry, inventory);
        return entry;
    }

    private async Task<JournalEntry?> AdjustmentOutEntryAsync(StockMovement movement, Account inventory, InventoryService inv)
    {
        var costing = new InventoryCostingService(db);
        decimal total = 0;
        var byProduct = new List<(long ProductId, decimal Amount)>();
        foreach (var line in movement.Lines)
        {
            var (lineCost, _) = await costing.ConsumeForIssueAsync(
                movement.CompanyId, line.ProductId, movement.WarehouseId, line.Quantity,
                movement.SourceDocument ?? movement.Description ?? "StockMovement");
            total += lineCost;
            byProduct.Add((line.ProductId, lineCost));
        }
        if (total == 0m) return null;

        var misc = await AccountAsync(movement.CompanyId, await Brqosh.Infrastructure.Accounting.PostingRoleService.ResolveAsync(db, movement.CompanyId, SaudiChartOfAccounts.PostingRoleCodes.MiscellaneousExpenses));
        var entry = JournalEntry.CreateDraft(movement.CompanyId, movement.MovementDate,
            movement.Description, "StockAdjustmentOut");
        entry.AddLine(misc, total, 0m, "نقص مخزون — جرد");
        entry.AddLine(inventory, 0m, total, "تخفيض المخزون");
        await ItemAccountResolver.RerouteAsync(db, movement.CompanyId, entry, inventory, byProduct, a => a.Inventory, debitSide: false);
        return entry;
    }

    // ‏حسابُ مخزون الصنف/الفئة للحركات الداخلة — بقيمة كلّ سطر.
    private Task RerouteInboundAsync(StockMovement movement, JournalEntry entry, Account inventory) =>
        ItemAccountResolver.RerouteAsync(db, movement.CompanyId, entry, inventory,
            movement.Lines.Select(l => (l.ProductId, Math.Round(l.Quantity * l.UnitCost, 2, MidpointRounding.AwayFromZero))),
            a => a.Inventory, debitSide: true);

    private async Task<Account> AccountAsync(long companyId, string code) =>
        await db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == code);
}
