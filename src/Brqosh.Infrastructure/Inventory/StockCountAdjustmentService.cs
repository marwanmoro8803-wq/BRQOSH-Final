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
    public Task<int> PostVariancesAsync(
        long companyId, long warehouseId, DateOnly countDate,
        IReadOnlyList<(long ProductId, decimal SystemQty, decimal CountedQty, decimal? OpeningUnitCost)> lines,
        string username, CancellationToken ct = default) =>
        PostVariancesAsync(companyId, warehouseId, countDate,
            lines.Select(l => (l.ProductId, (string?)null, l.SystemQty, l.CountedQty, l.OpeningUnitCost)).ToList(),
            username, ct);

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-١٠٩ · <c>order-to-cash-procure-to-pay-inventory#7</c>) ‏**الجردُ بالدفعة.** الصنفُ المتتبَّع يُجرد بدفعاته:
    /// السطرُ يحمل <c>BatchNumber</c> ولقطتُه رصيدُ تلك الدفعة، وبلا دفعةٍ يُرفض باسم الصنف. وغيرُ المتتبَّع كما كان.
    /// </summary>
    public async Task<int> PostVariancesAsync(
        long companyId, long warehouseId, DateOnly countDate,
        IReadOnlyList<(long ProductId, string? BatchNumber, decimal SystemQty, decimal CountedQty, decimal? OpeningUnitCost)> lines,
        string username, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPost();
        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            // (CEEF-229 · م1-١٢ / A-5) ‏**اللقطةُ تُعاد قراءتُها هنا — داخل المعاملة.**
            //
            // ‏`SystemQty` تأتي من المستدعي: من لقطةِ الشاشة لحظةَ فُتحت ورقةُ الجرد. والفرقُ
            // ‏يُرحَّل حركةً **مطلقة** (`SystemQty − CountedQty`)، فإن تحرّك الرصيدُ بين العدّ
            // ‏والترحيل صار الرقمُ المكتوب في الدفتر خاطئاً.
            //
            // ‏**وأشيعُ صورتين:** أمينان يفتحان الجردَ على المستودع نفسه فيُحسم العجزُ مرّتين
            // ‏(مقيسٌ: ١٠٠ ⟵ ٩٥ ⟵ **٩٠**، ونقص خمسٌ لا عشر) · وبيعٌ يقع بين العدّ صباحاً
            // ‏والترحيل ظهراً، فيُعيد الترحيلُ البضاعةَ المباعة إلى الدفتر.
            //
            // ‏**وليس سباقاً**، فلا يُصلحه قفلٌ ولا رمزُ تزامن: الترحيلان متباعدان في الزمن
            // ‏وكلُّ معاملةٍ سليمةٌ بذاتها. العطلُ أنّ **الرقمَ الذي بُني عليه الفرق قديم** —
            // ‏ولهذا يقع بصمتٍ تامّ: لا تعارضَ ولا استثناء، رقمٌ خاطئٌ في دفترٍ صحيح. ولا
            // ‏يكشفه حارسُ الهوية هـ-٣ لأنّ الأستاذ ينزل مع الدفتر المساعد، فيبقى متّسقاً.
            //
            // ‏**والفحصُ يشمل كلَّ سطرٍ مُرِّر لا سطورَ الفروقات وحدها.** ورقةُ الجرد بيانٌ
            // ‏عن المستودع **في لحظةٍ واحدة**؛ فصنفٌ تحرّك رصيدُه وفرقُه صفرٌ ليس سليماً بل
            // ‏هو أدلُّ على البطلان: العدُّ الفعليّ قال ١٠٠ والرصيدُ اليوم ٩٧، فالصوابُ فرقُ
            // ‏زيادةٍ ثلاثةً — والتصفيةُ إلى «لا فرق» تُخفيه. فالورقةُ تُرفض كاملةً وتُعاد.
            //
            // ‏**والرفضُ لا الإصلاحُ الصامت:** ضبطُ الرصيد على المعدود مباشرةً كان يمحو حركةً
            // ‏حقيقية وقعت بين العدّ والترحيل. من عدّ لا يعلم بها، فالقرارُ له لا للنظام.
            //
            // ‏(جاهزيّةُ الإطلاق · ح-١٠٩ · `order-to-cash-procure-to-pay-inventory#7`) ‏**والمتتبَّع يُقارَن بدفعته لا بمجموعه** — وبلا دفعةٍ يُرفض:
            // ‏فالحركةُ التي لا تحمل هويّةً يرفضها المَخنَقُ، والأجدى أن يُرفض هنا باسم الصنف وقبل أيّ ترحيل.
            var inventory = new InventoryService(db);
            foreach (var line in lines)
            {
                var tracked = await db.Products
                    .Where(p => p.Id == line.ProductId)
                    .Select(p => new { p.NameAr, p.IsTracked })
                    .SingleOrDefaultAsync(token);
                var name = tracked?.NameAr ?? line.ProductId.ToString();
                var batchNo = string.IsNullOrWhiteSpace(line.BatchNumber) ? null : line.BatchNumber.Trim();

                if (tracked?.IsTracked == true && batchNo is null)
                    throw new AccountingException(
                        $"الصنف '{name}' يُتتبَّع بالدفعة — يُجرد بدفعاته: اذكر رقمَ الدفعة في كلّ سطر.");

                var actual = tracked?.IsTracked == true
                    ? await db.StockBatches
                        .Where(b => b.CompanyId == companyId && b.WarehouseId == warehouseId
                            && b.ProductId == line.ProductId && b.BatchNumber == batchNo)
                        .Select(b => b.Quantity).SingleOrDefaultAsync(token)
                    : await inventory.GetOnHandAsync(companyId, line.ProductId, warehouseId);
                if (actual == line.SystemQty)
                    continue;

                var label = batchNo is null ? name : $"{name}» دفعة «{batchNo}";
                throw new AccountingException(
                    $"تغيّر رصيدُ «{label}» بعد فتح ورقة الجرد: كان {line.SystemQty:0.####} "
                    + $"وصار {actual:0.####}. أعِد فتحَ الورقة وأعِد العدّ — "
                    + "فترحيلُها الآن يكتب فرقاً محسوباً على رصيدٍ قديم.");
            }

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

                    movement.AddLine(v.ProductId, qty, unitCost, Batch(v.BatchNumber), await ExpiryAsync(v, warehouseId));
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
                    movement.AddLine(v.ProductId, qty, product.WeightedAverageCost, Batch(v.BatchNumber), await ExpiryAsync(v, warehouseId));
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

    // ‏(ح-١٠٩) ‏رقمُ الدفعة المُمرَّر، وانتهاؤها القائمُ إن وُجدت — `AdjustmentOut` لا يمنع إخراجَ المنتهية، فيُزال بالعدّ.
    private static string? Batch(string? number) => string.IsNullOrWhiteSpace(number) ? null : number.Trim();

    private async Task<DateOnly?> ExpiryAsync(
        (long ProductId, string? BatchNumber, decimal SystemQty, decimal CountedQty, decimal? OpeningUnitCost) v, long warehouseId) =>
        Batch(v.BatchNumber) is { } no
            ? await db.StockBatches.Where(b => b.WarehouseId == warehouseId
                && b.ProductId == v.ProductId && b.BatchNumber == no)
                .Select(b => b.ExpiryDate).FirstOrDefaultAsync()
            : null;
}
