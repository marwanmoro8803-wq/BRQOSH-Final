using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Inventory;

/// <summary>ترحيل فروقات الجرد — تسوية زيادة/نقص مخزون.</summary>
public class StockCountAdjustmentService(BrqoshDbContext db)
{
    /// <summary>
    /// ترحيل فروقات الجرد.
    ///
    /// <para>(CEEF-229 · م2-٢٤) <c>OpeningUnitCost</c> تكلفةٌ يُدخلها المالك لصنفٍ <b>لا تكلفة
    /// له بعد</b> — وهو حال كل صنفٍ في أوّل جردٍ تُجريه المنشأة. وبدونها يدخل المخزون بكميّةٍ
    /// بلا قيمة، فيُقيَّد أوّلُ بيعٍ له تكلفةً صفراً <b>ويُضخَّم مجمل الربح</b>.</para>
    ///
    /// <para><b>وقاعدةٌ لا تُخترق:</b> تُستعمل <b>فقط</b> حين يكون متوسط تكلفة الصنف صفراً.
    /// فالجردُ لا يجوز أن يكون باباً خلفياً لإعادة كتابة تقييم المخزون: من له تكلفةٌ محسوبة
    /// من مشترياته لا تُمسّ بإدخالٍ يدويّ.</para>
    /// </summary>
    public async Task<int> PostVariancesAsync(
        long companyId, long warehouseId, DateOnly countDate,
        IReadOnlyList<(long ProductId, decimal SystemQty, decimal CountedQty, decimal? OpeningUnitCost)> lines,
        string username, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPost();
        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var variances = lines
                .Where(l => l.CountedQty != l.SystemQty)
                .ToList();

            if (variances.Count == 0)
                return 0;

            var posting = new StockMovementPostingService(db);
            var movementsPosted = 0;

            var surplus = variances.Where(v => v.CountedQty > v.SystemQty).ToList();
            if (surplus.Count > 0)
            {
                var movement = StockMovement.CreateDraft(
                    companyId, countDate, StockMovementType.AdjustmentIn, warehouseId,
                    $"جرد — فائض {countDate:yyyy-MM-dd}");
                foreach (var v in surplus)
                {
                    var product = await db.Products.SingleAsync(p => p.Id == v.ProductId, token);
                    var qty = v.CountedQty - v.SystemQty;

                    // التكلفة المُدخَلة تُقبل **فقط** حين لا تكلفة للصنف — انظر الشرح على الدالّة.
                    var unitCost = product.WeightedAverageCost;
                    if (unitCost == 0m && v.OpeningUnitCost is decimal supplied && supplied > 0m)
                        unitCost = supplied;

                    movement.AddLine(v.ProductId, qty, unitCost);
                }
                await posting.PostAsync(movement);
                movementsPosted++;
            }

            var shortage = variances.Where(v => v.CountedQty < v.SystemQty).ToList();
            if (shortage.Count > 0)
            {
                var movement = StockMovement.CreateDraft(
                    companyId, countDate, StockMovementType.AdjustmentOut, warehouseId,
                    $"جرد — عجز {countDate:yyyy-MM-dd}");
                foreach (var v in shortage)
                {
                    var product = await db.Products.SingleAsync(p => p.Id == v.ProductId, token);
                    var qty = v.SystemQty - v.CountedQty;
                    movement.AddLine(v.ProductId, qty, product.WeightedAverageCost);
                }
                await posting.PostAsync(movement);
                movementsPosted++;
            }

            await new AuditService(db).LogAsync(companyId, username, "ترحيل جرد",
                "StockCount", warehouseId.ToString(),
                $"{variances.Count} صنف — {movementsPosted} حركة");

            return variances.Count;
        }, ct);
    }
}
