using System.Globalization;
using Brqosh.Domain.Manufacturing;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Manufacturing;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٧٧ · <c>manufacturing#5</c>) <b>تعديلُ قائمة الموادّ يُسجَّل — وكان يُكتب من النافذة بلا أثر.</b>
///
/// <para><b>ما كان:</b> <c>BomDialog</c> يستبدل البنودَ وتكاليفَ التحويل ويحفظ مباشرةً — <b>بلا سطرٍ في سجلّ
/// التدقيق</b>. وقائمةُ الموادّ تقرّر كم يُصرَف من المخزون وبكم يدخل التامُّ — فتعديلُها يُغيّر تكلفةَ كلّ ما
/// يُنتَج بعده، <b>ولا يُعرف مَن غيّرها ولا متى ولا من ماذا إلى ماذا</b>.</para>
///
/// <para><b>والسجلُّ يحمل الفرقَ لا النسختين:</b> كلُّ مكوّنٍ تغيّرت كمّيّتُه أو أُضيف أو حُذف، والأجرُ ونسبةُ
/// التحميل والاسمُ إن تغيّرت — <b>وعددُ الأوامر الصادرة التي تبقى على وصفتها</b>، فالقارئُ يعرف أنّ التعديلَ
/// لم يمسّها. <b>وحفظٌ بلا تغييرٍ لا يُسجَّل</b> — سجلٌّ يمتلئ بما لم يقع يُخفي ما وقع.</para>
///
/// <para><b>وبلا حارسِ صلاحيّةٍ جديد</b>: مَن يحقّ له تعديلُ القائمة قرارُ صلاحيّاتٍ خارجَ هذا البند، والصفحةُ
/// محكومةٌ بالوحدة والدور كما كانت.</para>
/// </summary>
public class BillOfMaterialsService(BrqoshDbContext db)
{
    /// <summary>حدُّ عمود التفاصيل في سجلّ التدقيق — وPostgreSQL يرفض ما يتجاوزه.</summary>
    private const int DetailsLimit = 2048;

    public async Task<BillOfMaterials> UpdateAsync(long companyId, long bomId, string nameAr,
        IReadOnlyList<(long ComponentProductId, decimal Quantity)> lines,
        decimal labourCostPerUnit, decimal overheadRatePercent, string username, CancellationToken ct = default)
    {
        username = SecurityContext.ResolveUsername(username);
        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var bom = await db.BillsOfMaterials.SingleAsync(b => b.CompanyId == companyId && b.Id == bomId, token);

            var nameBefore = bom.NameAr;
            var linesBefore = Quantities(bom);
            var labourBefore = bom.LabourCostPerUnit;
            var overheadBefore = bom.OverheadRatePercent;

            bom.Rename(nameAr);
            bom.ReplaceLines(lines);
            bom.SetConversionCosts(labourCostPerUnit, overheadRatePercent);
            await db.SaveChangesAsync(token);

            var changes = new List<string>();
            if (nameBefore != bom.NameAr)
                changes.Add($"الاسم: {nameBefore} ← {bom.NameAr}");

            var linesAfter = Quantities(bom);
            var touched = linesBefore.Keys.Union(linesAfter.Keys)
                .Where(id => linesBefore.GetValueOrDefault(id) != linesAfter.GetValueOrDefault(id))
                .ToList();
            if (touched.Count > 0)
            {
                var codes = await db.Products.IgnoreQueryFilters()
                    .Where(p => p.CompanyId == companyId && touched.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, p => p.Code, token);
                foreach (var id in touched.OrderBy(i => codes.GetValueOrDefault(i, ""), StringComparer.Ordinal))
                    changes.Add($"{codes.GetValueOrDefault(id, id.ToString(CultureInfo.InvariantCulture))}: "
                                + $"{Amount(linesBefore, id)} ← {Amount(linesAfter, id)}");
            }

            if (labourBefore != bom.LabourCostPerUnit)
                changes.Add($"أجر الوحدة: {labourBefore.ToString("0.##", CultureInfo.InvariantCulture)} ← "
                            + bom.LabourCostPerUnit.ToString("0.##", CultureInfo.InvariantCulture));
            if (overheadBefore != bom.OverheadRatePercent)
                changes.Add($"نسبة التحميل: {overheadBefore.ToString("0.####", CultureInfo.InvariantCulture)}% ← "
                            + $"{bom.OverheadRatePercent.ToString("0.####", CultureInfo.InvariantCulture)}%");

            if (changes.Count == 0)
                return bom;

            // ‏(ح-٧٧) ‏**والأوامرُ الصادرةُ تبقى على وصفتها** — جُمِّدت عند إصدارها (`WorkOrder.Release`)؛
            // ‏فيُقال عددُها في السجلّ ليعرف القارئُ أنّ التعديلَ لم يمسّها.
            var open = await db.WorkOrders.CountAsync(w =>
                w.CompanyId == companyId && w.BomId == bomId && w.Status == WorkOrderStatus.Released, token);
            changes.Add($"أوامر صادرة تبقى على وصفتها: {open}");

            var details = string.Join(" · ", changes);
            if (details.Length > DetailsLimit)
                details = details[..(DetailsLimit - 1)] + "…";

            await new AuditService(db).LogAsync(companyId, username, "تعديل قائمة مواد",
                "BillOfMaterials", bom.Code, details, ct: token);
            return bom;
        }, ct);
    }

    private static Dictionary<long, decimal> Quantities(BillOfMaterials bom) =>
        bom.Lines.GroupBy(l => l.ComponentProductId).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

    private static string Amount(Dictionary<long, decimal> lines, long id) =>
        lines.TryGetValue(id, out var qty) ? qty.ToString("0.####", CultureInfo.InvariantCulture) : "—";
}
