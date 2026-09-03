using Brqosh.Domain.Manufacturing;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Manufacturing;

/// <summary>
/// (CEEF-230 · ط6-٨) <b>الإنتاجُ تحت التنفيذ — وكانت الشاشةُ تسقط بقائمةِ موادٍ محذوفة.</b>
///
/// <para><b>وكان <c>bomMap[o.BomId]</c> بلا حارس</b>: مُرشِّحُ الحذف الناعم يُخفي قائمةَ
/// المواد، <b>وأمرُ الإنتاج المُطلَق يبقى</b> — فيرمي القاموسُ <c>KeyNotFoundException</c>
/// <b>وتسقط الشاشةُ كلُّها</b>، لا صفٌّ واحد.</para>
///
/// <para><b>وهي حالةٌ واقعةٌ على قواعدَ قائمة</b>: التعبئةُ التي أُوقفت في <c>م0-١٣</c>
/// <b>كانت قد جرت على كلّ قاعدةٍ قبل إيقافها</b> — فكلُّ قائمةِ موادٍ أُوقفت قبلها تحمل
/// تاريخَ حذفٍ إلى الأبد.</para>
///
/// <para><b>والأمرُ يبقى مرئياً باسمٍ بديل</b> ولا يُخفى: <b>إخفاؤه يُخفي بضاعةً في
/// المصنع</b> — وهي قائمةٌ تُقرأ لمعرفة ما تحت التشغيل، فنقصُ سطرٍ منها أخطرُ من سطرٍ
/// ناقص الاسم.</para>
/// </summary>
public class WipQuery(BrqoshDbContext db)
{
    public record WipRow(long WorkOrderId, string? OrderNumber, string BomName, string ProductName,
        decimal PlannedQuantity, decimal ProducedQuantity, string StatusAr, WorkOrderStatus Status);

    public async Task<IReadOnlyList<WipRow>> ListOpenAsync(long companyId, CancellationToken ct = default)
    {
        var orders = await db.WorkOrders
            .Where(w => w.CompanyId == companyId && w.Status == WorkOrderStatus.Released)
            .OrderByDescending(w => w.OrderDate)
            .ToListAsync(ct);

        if (orders.Count == 0) return [];

        var bomIds = orders.Select(o => o.BomId).Distinct().ToList();
        var boms = await db.BillsOfMaterials.Where(b => bomIds.Contains(b.Id)).ToListAsync(ct);
        var productIds = boms.Select(b => b.FinishedProductId).Distinct().ToList();
        var products = await db.Products.Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.NameAr, ct);
        var bomMap = boms.ToDictionary(b => b.Id);

        return orders.Select(o =>
        {
            // ‏**والقائمةُ قد تكون محذوفةً حذفاً ناعماً والأمرُ باقياً** — فلا يُفهرس
            // ‏القاموسُ مباشرةً، **وسطرٌ باسمٍ بديلٍ خيرٌ من شاشةٍ لا تفتح**.
            var bom = bomMap.GetValueOrDefault(o.BomId);
            return new WipRow(o.Id, o.OrderNumber,
                bom?.NameAr ?? "قائمة موادٍ محذوفة",
                bom is null ? "—" : products.GetValueOrDefault(bom.FinishedProductId, "—"),
                o.PlannedQuantity, o.ProducedQuantity,
                WorkOrder.StatusLabel(o.Status), o.Status);
        }).ToList();
    }
}
