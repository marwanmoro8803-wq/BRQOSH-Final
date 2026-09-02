using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Inventory;

/// <summary>ما يُسحب من دفعةٍ بعينها — <b>رقمُها وصلاحيتُها وكمّيتُها</b>.</summary>
public record BatchDraw(string BatchNumber, DateOnly? ExpiryDate, decimal Quantity);

/// <summary>
/// (CEEF-231 · ط1-٣ج) <b>الصرفُ بالأقدم انتهاءً — FEFO.</b>
///
/// <para><b>ما كان:</b> بندٌ مُعلَنٌ مفتوحاً منذ <c>ط1-٣</c>، <b>ويرفض بسببه المطبخُ كلَّ
/// مكوّنٍ متتبَّع</b>: «والصرفُ بالأقدم انتهاءً لم يُبنَ، واختيارٌ عشوائيٌّ للدفعة يُفقد
/// الأثر». فمطعمٌ يتتبّع صلاحيةَ لحمه <b>لا يستطيع تشغيلَ وصفةٍ واحدة</b>.</para>
///
/// <para><b>ولماذا الانتهاءُ لا الاستلام:</b> <c>FIFO</c> يُخرج الأقدمَ <b>وروداً</b>، وهو
/// صوابٌ للتكلفة. <b>والصلاحيةُ لا تتبع الورود</b>: شحنةٌ وصلت اليوم قد تنتهي قبل شحنةٍ
/// وصلت الشهرَ الماضي. <b>فالصرفُ بالورود يُبقي الأقربَ انتهاءً على الرفّ حتّى يتلف</b> —
/// وهو خسارةٌ مباشرة، ومخالفةٌ في الدواء والغذاء.</para>
///
/// <para><b>والمنتهيةُ لا تُخصَّص أصلاً</b>: المَخنَقُ يرفض بيعَها، <b>واقتراحُها يُنتج
/// رفضاً بعد عمل</b>. وتخطّيها هنا صمتٌ مقصود — إخراجُها يقع بتسوية إتلاف، لا بمنعِ
/// الصرف من غيرها.</para>
///
/// <para><b>وهويّةُ هـ-٢١:</b> مجموعُ ما خُصّص = المطلوبُ بالضبط، <b>وكلُّ دفعةٍ لا تُعطي
/// أكثرَ ممّا فيها</b>، والترتيبُ بالانتهاء ثمّ بالرقم.</para>
/// </summary>
public class BatchAllocationService(BrqoshDbContext db)
{
    /// <summary>
    /// يُخصّص الكمّيةَ المطلوبة على دفعاتٍ سارية — <b>الأقربُ انتهاءً أوّلاً</b>.
    /// </summary>
    /// <param name="asOf">اليومُ الذي تُقاس عليه الصلاحية.</param>
    /// <remarks>
    /// <b>ويرمي إن لم يكفِ الساري</b> — ولا يُخصّص جزءاً بصمت: تخصيصٌ ناقصٌ يُصرَف فيُنقص
    /// المخزونَ بأقلَّ ممّا خرج، <b>ويظهر الفرقُ عجزَ جردٍ آخرَ السنة</b>.
    /// </remarks>
    public async Task<IReadOnlyList<BatchDraw>> AllocateAsync(long companyId, long productId,
        long warehouseId, decimal quantity, DateOnly asOf, CancellationToken ct = default)
    {
        if (quantity <= 0m)
            throw new AccountingException("الكمية المطلوبة يجب أن تكون موجبة.");

        var batches = await db.StockBatches.AsNoTracking()
            .Where(b => b.CompanyId == companyId
                        && b.ProductId == productId
                        && b.WarehouseId == warehouseId
                        && b.Quantity > 0m)
            .ToListAsync(ct);

        // ‏**والمنتهيةُ تُستبعَد قبل الترتيب** — فلا تُقترَح ثمّ تُرفض عند الصرف.
        var usable = batches
            .Where(b => !b.IsExpiredOn(asOf))
            // ‏**وما لا صلاحيةَ له آخِرُ الطابور**: دفعةٌ بلا تاريخٍ لا يُعرف متى تنتهي،
            // ‏**وتقديمُها يُبقي المؤرَّخَ على الرفّ حتّى يتلف** — وهو عينُ ما بُني هذا لمنعه.
            .OrderBy(b => b.ExpiryDate ?? DateOnly.MaxValue)
            .ThenBy(b => b.BatchNumber, StringComparer.Ordinal)
            .ToList();

        var available = usable.Sum(b => b.Quantity);
        if (available + 0.0001m < quantity)
        {
            var product = await db.Products.IgnoreQueryFilters()
                .SingleAsync(p => p.Id == productId, ct);

            var expired = batches.Count - usable.Count;
            throw new AccountingException(
                $"دفعاتٌ ساريةٌ غير كافية من '{product.NameAr}' — المتاح {available:N4} والمطلوب {quantity:N4}"
                + (expired > 0 ? $"، و{expired} دفعةً منتهيةُ الصلاحية لا تُصرف." : "."));
        }

        var draws = new List<BatchDraw>();
        var remaining = quantity;

        foreach (var batch in usable)
        {
            if (remaining <= 0m) break;

            var take = Math.Min(remaining, batch.Quantity);
            draws.Add(new BatchDraw(batch.BatchNumber, batch.ExpiryDate,
                Math.Round(take, 4, MidpointRounding.AwayFromZero)));

            remaining = Math.Round(remaining - take, 4, MidpointRounding.AwayFromZero);
        }

        return draws;
    }

    /// <summary>
    /// الدفعاتُ التي تقارب الانتهاء — <b>ولا تُعرَض المنتهيةُ هنا</b>.
    /// </summary>
    /// <remarks>
    /// <b>وهي ما يُتّخذ به قرارُ تخفيضٍ أو تحويلٍ أو إتلاف</b> قبل أن تصير خسارةً.
    /// </remarks>
    public async Task<IReadOnlyList<StockBatch>> ExpiringWithinAsync(long companyId, int days,
        DateOnly asOf, CancellationToken ct = default)
    {
        var horizon = asOf.AddDays(days);

        return await db.StockBatches.AsNoTracking()
            .Where(b => b.CompanyId == companyId && b.Quantity > 0m
                        && b.ExpiryDate != null
                        && b.ExpiryDate >= asOf && b.ExpiryDate <= horizon)
            .OrderBy(b => b.ExpiryDate)
            .ToListAsync(ct);
    }
}
