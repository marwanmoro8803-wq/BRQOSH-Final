using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Healthcare;
using Brqosh.Domain.Inventory;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Healthcare;

/// <summary>حصيلةُ صرفِ صيدلية — والمصروفُ مُعلَنٌ لا مطويّ.</summary>
public record PharmacyDispenseResult(
    long StockMovementId,
    decimal TotalCost,
    long? JournalEntryId,
    IReadOnlyList<(long ProductId, decimal Quantity)> Dispensed);

/// <summary>
/// (CEEF-231 · ط3-٢ج) <b>صرفُ دواءِ الزيارة من المستودع — الرابطُ الغائب في كلّ مستوصف.</b>
///
/// <para><b>ما كان:</b> <c>VisitLine.ProductId</c> <b>حقلٌ ميّت</b> — يُكتب ويُخزَّن
/// <b>ولا يقرؤه أحدٌ في المستودع كلِّه</b>. فالمستوصفُ يصرف للمريض علبةَ دواءٍ بمئتين،
/// <b>والدواءُ في المخزن لا ينقص</b>. فتكلفةُ المبيعات صفر والربحُ مئةٌ بالمئة، ثمّ يأتي
/// جردُ آخر السنة بفارقٍ هائل <b>يُقيَّد «عجزَ جرد»</b> — وهو صرفٌ صحيحٌ بلا مصروف.
/// <b>وقوائمُ سنةٍ كاملةٍ تُقدَّم للزكاة بربحٍ لا وجودَ له</b>، والصيدليةُ لا تُعرف
/// كميّتُها ولا صلاحيةُ ما فيها.</para>
///
/// <para><b>ولماذا الصرفُ من الزيارة لا من الفاتورة:</b> الزيارةُ تُخرج <b>فاتورتين</b> —
/// حصّةَ المريض ومطالبةَ الشركة — <b>والمبلغُ يُقسَم بينهما بنسبة التغطية، والكمّيةُ لا
/// تُقسَم</b>. فصرفٌ من كلٍّ منهما <b>يُخرج الدواءَ مرّتين</b>، وصرفٌ من إحداهما اختيارٌ
/// بلا سند — <b>وتنهار الكمّيةُ إذا كانت الشركةُ تُغطّي كلَّ شيءٍ فلا تُصدَر فاتورةُ
/// مريضٍ أصلاً</b>. <b>والزيارةُ هي المستندُ الذي استُهلكت فيه البضاعة</b>، فمنها الصرف.</para>
///
/// <para><b>وهويّةُ هـ-١٨:</b> بعد فوترةِ زيارةٍ تحمل <c>ن</c> من دواء، ينقص رصيدُ ذلك
/// الدواء <c>ن</c> بالضبط، <b>وتُقيَّد تكلفتُه دائنةً على المخزون مدينةً على تكلفة
/// المبيعات</b> — لا أقلّ ولا أكثر.</para>
/// </summary>
public class PharmacyDispenseService(BrqoshDbContext db)
{
    /// <summary>
    /// يصرف أصنافَ الزيارة المخزنية ويُقيّد تكلفتَها.
    /// </summary>
    /// <remarks>
    /// <b>ويُستدعى داخل معاملةِ الفوترة</b>: صرفٌ بعد الـcommit يترك زيارةً مفوترةً
    /// <b>ودواءً لم يخرج</b> إن تعثّر، وصرفٌ قبلها يُخرج دواءً لزيارةٍ لم تُفوتَر.
    /// </remarks>
    public async Task<PharmacyDispenseResult?> DispenseForVisitAsync(
        long companyId, Visit visit, string sourceDocument, CancellationToken ct = default)
    {
        var requested = visit.Lines
            .Where(l => l.ProductId is not null)
            .GroupBy(l => (ProductId: l.ProductId!.Value, l.BatchNumber))
            .Select(g => (g.Key.ProductId, g.Key.BatchNumber,
                Quantity: Math.Round(g.Sum(l => l.Quantity), 4, MidpointRounding.AwayFromZero)))
            .ToList();

        if (requested.Count == 0) return null;

        var productIds = requested.Select(r => r.ProductId).Distinct().ToList();
        var products = await db.Products.IgnoreQueryFilters()
            .Where(p => p.CompanyId == companyId && productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        // ‏**ومعرّفٌ لا يقابله صنفٌ يُرفض باسمه.** صمتُ التخطّي يُفوتِر دواءً ولا يصرفه —
        // ‏**وهو العطلُ الأوّلُ بعينه عائداً في ثوبِ حراسة**.
        var unknown = productIds.Where(id => !products.ContainsKey(id)).ToList();
        if (unknown.Count > 0)
            throw new AccountingException(
                $"أصنافٌ في الزيارة {visit.VisitNumber} غير موجودةٍ في هذه المنشأة: {string.Join("، ", unknown)}.");

        // ‏**والصنفُ الخدميُّ لا يُصرف.** ربطُ سطرِ استشارةٍ بصنفٍ خدميٍّ لا يُنقص مخزوناً
        // ‏— **ولا يُقيَّد له مصروف**. والرفضُ باسمه أصدقُ من قبولٍ صامتٍ لا أثرَ له.
        var services = requested
            .Where(r => !products[r.ProductId].IsStockable())
            .Select(r => products[r.ProductId].NameAr)
            .ToList();

        if (services.Count > 0)
            throw new AccountingException(
                $"أصنافٌ خدميةٌ لا تُصرف من المخزون: {string.Join("، ", services)} — "
                + "اربِطْ سطرَ الصيدلية بصنفٍ مخزنيّ، ودَعِ الخدمةَ بلا صنف.");

        InventoryService.EnsureTradable(products, "صرف صيدلية");

        var inv = new InventoryService(db);
        var warehouse = await inv.GetDefaultWarehouseAsync(companyId);
        var costing = new InventoryCostingService(db);

        var issueLines = new List<(long, decimal, decimal, string?, DateOnly?)>();
        var totalCost = 0m;
        var costByProduct = new List<(long ProductId, decimal Amount)>();

        foreach (var (productId, batchNumber, quantity) in requested)
        {
            var onHand = await inv.GetOnHandAsync(companyId, productId, warehouse.Id);
            if (quantity > onHand)
                throw new AccountingException(
                    $"رصيد '{products[productId].NameAr}' غير كافٍ للصرف — المتوفر {onHand:N2}.");

            var (lineCost, unitCost) = await costing.ConsumeForIssueAsync(
                companyId, productId, warehouse.Id, quantity, sourceDocument, ct);

            totalCost += lineCost;
            costByProduct.Add((productId, lineCost));
            issueLines.Add((productId, quantity, unitCost, batchNumber, null));
        }

        // ‏**والمَخنَقُ يفرض الدفعة**: صنفٌ متتبَّعٌ بلا رقمٍ يُرفض هنا باسمه — فلا يخرج
        // ‏دواءٌ بلا هويّةٍ ولا صلاحية، **ولا تُباع دفعةٌ منتهية**.
        var movement = await inv.PostDocumentMovementAsync(companyId, warehouse.Id,
            StockMovementType.Issue, visit.VisitDate,
            $"صرف صيدلية — زيارة {visit.VisitNumber}", sourceDocument, issueLines);

        long? entryId = null;
        if (totalCost > 0m)
            entryId = await PostCostAsync(companyId, visit.VisitDate, totalCost, costByProduct, sourceDocument, ct);

        await db.SaveChangesAsync(ct);

        return new PharmacyDispenseResult(movement.Id, totalCost, entryId,
            [.. requested.Select(r => (r.ProductId, r.Quantity))]);
    }

    private async Task<long> PostCostAsync(long companyId, DateOnly date, decimal totalCost,
        List<(long ProductId, decimal Amount)> costByProduct,
        string sourceDocument, CancellationToken ct)
    {
        var cogs = await SystemAccountProvisioning.EnsureAsync(
            db, companyId, SaudiChartOfAccounts.SystemCodes.CostOfGoodsSold, ct);
        var inventory = await SystemAccountProvisioning.EnsureAsync(
            db, companyId, SaudiChartOfAccounts.SystemCodes.Inventory, ct);

        await FinancialPostingSupport.ValidateAsync(
            db, companyId, date, PostingDocumentKind.JournalEntry,
            accountIds: [cogs.Id, inventory.Id], ct: ct);

        var entry = JournalEntry.CreateDraft(companyId, date, "تكلفة أدوية مصروفة",
            sourceDocument: sourceDocument);
        entry.AddLine(cogs, totalCost, 0m, "تكلفة المبيعات — صيدلية");
        entry.AddLine(inventory, 0m, totalCost, "صرف مخزون الصيدلية");

        // ‏حساباتُ تكلفة الصنف ومخزونه (الصنف ← الفئة ← العامّ).
        await ItemAccountResolver.RerouteAsync(db, companyId, entry, cogs, costByProduct, a => a.Cogs, debitSide: true, ct);
        await ItemAccountResolver.RerouteAsync(db, companyId, entry, inventory, costByProduct, a => a.Inventory, debitSide: false, ct);
        var number = await new JournalEntryNumberGenerator(db).NextAsync(companyId, date);
        entry.Post(number);
        db.JournalEntries.Add(entry);
        await db.SaveChangesAsync(ct);

        return entry.Id;
    }
}
