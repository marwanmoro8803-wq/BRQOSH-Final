using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Hospitality;
using Brqosh.Domain.Inventory;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Hospitality;

/// <summary>حصيلةُ صرفِ مطبخ — والمكوّناتُ المصروفةُ مُعلَنةٌ لا مطويّة.</summary>
public record KitchenIssueResult(
    long StockMovementId,
    decimal TotalCost,
    long? JournalEntryId,
    IReadOnlyList<(long ProductId, decimal Quantity)> Consumed);

/// <summary>
/// (CEEF-230 · ط3-٣) <b>بيعُ طبقٍ يصرف مكوّناتِه — وهذا هو الرابطُ الغائب في كلّ مطعم.</b>
///
/// <para><b>ما كان:</b> المطعمُ يبيع «برجر» صنفاً في نقطة البيع، <b>والخبزُ واللحمُ في
/// المستودع لا ينقصان</b>. فتكلفةُ المبيعات <b>صفر</b> والربحُ <b>مئةٌ بالمئة</b>، ثمّ
/// يأتي جردُ آخر السنة بفارقٍ هائل <b>يُقيَّد «عجزَ جرد»</b> — وهو في الحقيقة بيعٌ صحيحٌ
/// لم يُسجَّل له مصروف. <b>وقوائمُ سنةٍ كاملةٍ تُقدَّم للزكاة بربحٍ لا وجودَ له.</b></para>
///
/// <para><b>وهويّةُ هـ-١١:</b> بعد صرف <c>ن</c> حصّةً من طبقٍ، ينقص كلُّ مكوّنٍ بمقدار
/// <c>ن × كمّيتِه في الوصفة</c> — لا أقلّ ولا أكثر.</para>
/// </summary>
public class KitchenIssueService(BrqoshDbContext db)
{
    /// <summary>
    /// يصرف مكوّناتِ ما بيع، ويُقيّد تكلفتَه.
    /// </summary>
    /// <param name="sold">ما بيع: صنفُ القائمة وعددُ الحصص.</param>
    public async Task<KitchenIssueResult> IssueForSalesAsync(long companyId, DateOnly date,
        IEnumerable<(long MenuItemId, decimal Servings)> sold, string sourceDocument,
        long? warehouseId = null, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPost();

        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var order = sold.ToList();
            if (order.Count == 0)
                throw new AccountingException("لا شيءَ بيع — ولا صرفَ بلا بيع.");

            var itemIds = order.Select(o => o.MenuItemId).Distinct().ToList();
            var items = await db.MenuItems
                .Where(m => m.CompanyId == companyId && itemIds.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id, token);

            var required = ExplodeRecipes(order, items);

            var inv = new InventoryService(db);
            var warehouse = warehouseId is long id
                ? await db.Warehouses.SingleAsync(w => w.Id == id && w.CompanyId == companyId, token)
                : await inv.GetDefaultWarehouseAsync(companyId);

            // (CEEF-231 · ط1-٣ج) ‏**والمتتبَّعُ يُصرف بالأقدم انتهاءً — ولم يكن يُصرف.**
            //
            // ‏كان يُرفض باسمه: «والصرفُ بالأقدم انتهاءً لم يُبنَ». **فمطعمٌ يتتبّع صلاحيةَ
            // ‏لحمه لا يستطيع تشغيلَ وصفةٍ واحدة** — والتتبّعُ هو ما يفعله كلُّ مطعمٍ
            // ‏جادّ. وقد بُني `BatchAllocationService`، فصار الرفضُ تخصيصاً.
            var tracked = await TrackedProductsAsync(companyId, required, token);
            var allocator = new BatchAllocationService(db);

            var costing = new InventoryCostingService(db);
            var issueLines = new List<(long, decimal, decimal, string?, DateOnly?)>();
            var totalCost = 0m;

            foreach (var (productId, quantity) in required)
            {
                var (lineCost, unitCost) = await costing.ConsumeForIssueAsync(
                    companyId, productId, warehouse.Id, quantity, sourceDocument, token);
                totalCost += lineCost;

                if (!tracked.Contains(productId))
                {
                    issueLines.Add((productId, quantity, unitCost, null, null));
                    continue;
                }

                // ‏**وسطرٌ لكلّ دفعةٍ تُسحب منها** — فالمَخنَقُ يُنقص كلَّ دفعةٍ بعينها،
                // ‏**وسطرٌ واحدٌ بمجموعٍ يُفقد الأثر** الذي بُني التتبّعُ لأجله.
                foreach (var draw in await allocator.AllocateAsync(
                             companyId, productId, warehouse.Id, quantity, date, token))
                {
                    issueLines.Add((productId, draw.Quantity, unitCost,
                        draw.BatchNumber, draw.ExpiryDate));
                }
            }

            var movement = await inv.PostDocumentMovementAsync(companyId, warehouse.Id,
                StockMovementType.Issue, date, "صرف مطبخ", sourceDocument, issueLines);

            long? entryId = null;
            if (totalCost > 0m)
                entryId = await PostCostAsync(companyId, date, totalCost, sourceDocument, token);

            await db.SaveChangesAsync(token);

            return new KitchenIssueResult(movement.Id, totalCost, entryId,
                [.. required.Select(r => (r.Key, r.Value))]);
        }, ct);
    }

    /// <summary>
    /// (CEEF-230 · ط3-٣ج) <b>يتحقّق أنّ صنفَ البيع صالحٌ لأن يُربط بوصفة.</b>
    ///
    /// <para><b>والاشتراطُ أن يكون خدمياً</b>: الطبقُ لا يُخزَّن — مكوّناتُه تُخزَّن.
    /// <b>وربطُ وصفةٍ بصنفٍ مخزنيٍّ يخصم الطبقَ من المخزون ثمّ يخصم مكوّناتِه</b>،
    /// فتُقيَّد التكلفةُ مرّتين ويُنقص المخزونُ لشيءٍ لم يكن فيه أصلاً.</para>
    ///
    /// <para><b>والتحقّقُ هنا لا في النطاق</b>: النطاقُ لا يقرأ الأصناف، والقرارُ يحتاج
    /// نوعَ الصنف — <b>وهو في القاعدة</b>.</para>
    /// </summary>
    public async Task EnsureSaleProductLinkableAsync(long companyId, long? saleProductId,
        CancellationToken ct = default)
    {
        if (saleProductId is not long productId) return;

        var product = await db.Products.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == productId && p.CompanyId == companyId, ct)
            ?? throw new AccountingException($"صنف البيع {productId} غير موجود في هذه المنشأة.");

        if (product.Type != ProductType.Service)
            throw new AccountingException(
                $"«{product.NameAr}» صنفٌ مخزنيّ — وربطُ وصفةٍ به يخصم الطبقَ ومكوّناتِه معاً "
                + "فتُقيَّد التكلفةُ مرّتين. اربِطِ الوصفةَ بصنفٍ خدميّ.");
    }

    /// <summary>
    /// (CEEF-230 · ط3-٣ج) <b>يصرف مكوّناتِ ما بيع من أصنافٍ مرتبطةٍ بوصفات — ويصمت عن غيرها.</b>
    ///
    /// <para><b>وهذا ما يُغلق صنفَ العطل لا حالتَه</b>: الصرفُ اليدويّ يُغلق الفجوةَ لمَن
    /// يضغط الزرَّ، <b>والربطُ بالبيع يُغلقها لمَن نسي</b>. فكلُّ بيعٍ لطبقٍ مرتبطٍ يصرف
    /// وصفتَه أيّاً كان الطريق — نقطةَ بيعٍ كانت أو فاتورة.</para>
    ///
    /// <para><b>ويصمت عمّا لا وصفةَ له</b>: الفاتورةُ الواحدة تحمل أطباقاً وسلعاً عادية،
    /// <b>ورفضُ ما ليس طبقاً يمنع البيعَ كلَّه</b>. فالصمتُ هنا صوابٌ — بخلاف الصرف
    /// اليدويّ حيث اختيارُ صنفٍ بلا وصفةٍ خطأٌ صريح.</para>
    /// </summary>
    public async Task<KitchenIssueResult?> IssueForSoldProductsAsync(
        long companyId, DateOnly date,
        IReadOnlyList<(long ProductId, decimal Quantity)> soldProducts, string sourceDocument,
        long? warehouseId = null, CancellationToken ct = default)
    {
        if (soldProducts.Count == 0) return null;

        var productIds = soldProducts.Select(s => s.ProductId).Distinct().ToList();

        var menuByProduct = await db.MenuItems
            .Where(m => m.CompanyId == companyId && m.IsActive
                        && m.SaleProductId != null && productIds.Contains(m.SaleProductId!.Value))
            .ToDictionaryAsync(m => m.SaleProductId!.Value, m => m.Id, ct);

        if (menuByProduct.Count == 0) return null;

        var order = soldProducts
            .Where(s => menuByProduct.ContainsKey(s.ProductId))
            .Select(s => (MenuItemId: menuByProduct[s.ProductId], Servings: s.Quantity))
            .ToList();

        return await IssueForSalesAsync(companyId, date, order, sourceDocument, warehouseId, ct);
    }

    /// <summary>
    /// يُفجّر الوصفات إلى كمّياتٍ مخزنية.
    ///
    /// <para><b>والمكوّنُ المشترك يُجمع لا يُستبدل</b>: البرجرُ والساندويتش كلاهما يستهلك
    /// خبزاً. <b>وإسنادٌ في القاموس بدل جمعٍ يُسقط أحدَهما صامتاً</b> — فينقص المصروفُ
    /// عن الحقيقة، ويظهر الفرقُ عجزَ جردٍ آخرَ السنة لا خطأً في الشيفرة.</para>
    ///
    /// <para><b>وطبقٌ بلا وصفةٍ يُرفض باسمه</b>: تفجيرُه يُنتج صفراً، <b>فيمرّ البيعُ
    /// بلا صرفٍ ولا تكلفة</b> — وهو عينُ العطل الذي بُني هذا الباب لإغلاقه. <b>وصمتُه
    /// أخطرُ من رفضه</b>: لا شيءَ في النظام يقول إنّ الوصفةَ ناقصة.</para>
    /// </summary>
    private static Dictionary<long, decimal> ExplodeRecipes(
        List<(long MenuItemId, decimal Servings)> order, Dictionary<long, MenuItem> items)
    {
        var required = new Dictionary<long, decimal>();

        foreach (var (menuItemId, servings) in order)
        {
            if (!items.TryGetValue(menuItemId, out var item))
                throw new AccountingException($"صنف القائمة {menuItemId} غير موجود.");
            if (servings <= 0m)
                throw new AccountingException($"عدد الحصص من «{item.NameAr}» يجب أن يكون موجباً.");
            if (item.Components.Count == 0)
                throw new AccountingException(
                    $"«{item.NameAr}» بلا وصفة — وبيعُه بلا صرفٍ يُظهر ربحاً مئةً بالمئة.");

            foreach (var component in item.Components)
            {
                var quantity = Math.Round(component.QuantityPerServing * servings, 4,
                    MidpointRounding.AwayFromZero);

                required[component.ProductId] =
                    required.GetValueOrDefault(component.ProductId) + quantity;
            }
        }

        return required;
    }

    /// <summary>
    /// (CEEF-231 · ط1-٣ج) <b>أيُّ المكوّنات متتبَّعٌ — فيُخصَّص بالأقدم انتهاءً.</b>
    ///
    /// <para><b>وكان هذا الفحصُ رفضاً</b>: مكوّنٌ متتبَّعٌ يمنع الوصفةَ كلَّها، لأنّ الصرفَ
    /// بالأقدم انتهاءً لم يكن مبنياً <b>واختيارٌ عشوائيٌّ للدفعة يُفقد الأثر</b>. وقد بُني،
    /// فصار الفحصُ فرزاً لا سدّاً.</para>
    /// </summary>
    private async Task<HashSet<long>> TrackedProductsAsync(long companyId,
        Dictionary<long, decimal> required, CancellationToken ct) =>
        [.. await db.Products.IgnoreQueryFilters()
            .Where(p => p.CompanyId == companyId
                        && required.Keys.Contains(p.Id)
                        && p.Tracking != InventoryTracking.None)
            .Select(p => p.Id)
            .ToListAsync(ct)];

    private async Task<long> PostCostAsync(long companyId, DateOnly date, decimal totalCost,
        string sourceDocument, CancellationToken ct)
    {
        var cogs = await SystemAccountProvisioning.EnsureAsync(
            db, companyId, SaudiChartOfAccounts.SystemCodes.CostOfGoodsSold, ct);
        var inventory = await SystemAccountProvisioning.EnsureAsync(
            db, companyId, SaudiChartOfAccounts.SystemCodes.Inventory, ct);

        await FinancialPostingSupport.ValidateAsync(
            db, companyId, date, PostingDocumentKind.JournalEntry,
            accountIds: [cogs.Id, inventory.Id], ct: ct);

        var entry = JournalEntry.CreateDraft(companyId, date, "تكلفة مكوّنات المطبخ",
            sourceDocument: sourceDocument);
        entry.AddLine(cogs, totalCost, 0m, "تكلفة المبيعات — مكوّنات");
        entry.AddLine(inventory, 0m, totalCost, "صرف مخزون المطبخ");

        var number = await new JournalEntryNumberGenerator(db).NextAsync(companyId, date);
        entry.Post(number);
        db.JournalEntries.Add(entry);
        await db.SaveChangesAsync(ct);

        return entry.Id;
    }
}
