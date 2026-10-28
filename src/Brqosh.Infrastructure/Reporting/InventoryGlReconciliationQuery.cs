using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Reporting;

/// <summary>مطابقةُ تقييم المخزون (طبقات التكلفة) مع رصيد حساب المخزون في الأستاذ.</summary>
/// <param name="LayerValuation">مجموعُ الكمية المتبقية × تكلفة الوحدة في طبقات التكلفة.</param>
/// <param name="GlBalance">رصيدُ حساب المخزون (مدين − دائن) من القيود المرحّلة والمعكوسة.</param>
/// <param name="Difference">الأستاذ ناقصَ الطبقات — صفرٌ عند التطابق.</param>
/// <param name="Warning">تنبيهٌ عند تعذّر المقارنة الدقيقة (مثل طريقة المتوسط المرجّح).</param>
public record InventoryGlReconciliation(
    string InventoryAccountCode,
    decimal LayerValuation,
    decimal GlBalance,
    decimal Difference,
    bool IsReconciled,
    string? Warning);

public class InventoryGlReconciliationQuery(BrqoshDbContext db)
{
    public const string DefaultInventoryAccountCode = "1107";

    public async Task<InventoryGlReconciliation> RunAsync(
        long companyId, string inventoryAccountCode = DefaultInventoryAccountCode, CancellationToken ct = default)
    {
        // ‏SQLite لا يجمع `decimal` — فالجمعُ في الذاكرة.
        var layers = await db.InventoryCostLayers.AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.RemainingQuantity != 0)
            .Select(l => new { l.RemainingQuantity, l.UnitCost })
            .ToListAsync(ct);
        var valuation = Math.Round(layers.Sum(l => l.RemainingQuantity * l.UnitCost), 2, MidpointRounding.AwayFromZero);

        var accountIds = await db.Accounts.AsNoTracking()
            .Where(a => a.CompanyId == companyId && a.Code == inventoryAccountCode)
            .Select(a => a.Id)
            .ToListAsync(ct);
        // ‏والمخزونُ موزَّعٌ على حسابات الأصناف/الفئات أيضاً — فالأستاذُ مجموعُها كلِّها.
        if (inventoryAccountCode == DefaultInventoryAccountCode && accountIds.Count > 0)
            accountIds = accountIds.Concat(await ItemAccountResolver.InventoryAccountIdsAsync(db, companyId, ct)).Distinct().ToList();

        var lines = await db.JournalEntries.AsNoTracking()
            .Where(e => e.CompanyId == companyId
                && (e.Status == JournalEntryStatus.Posted || e.Status == JournalEntryStatus.Reversed))
            .SelectMany(e => e.Lines, (e, l) => new { l.AccountId, l.Debit, l.Credit })
            .Where(x => accountIds.Contains(x.AccountId))
            .Select(x => new { x.Debit, x.Credit })
            .ToListAsync(ct);
        var gl = lines.Sum(x => x.Debit - x.Credit);

        string? warning = null;
        if (accountIds.Count == 0)
            warning = $"حساب المخزون «{inventoryAccountCode}» غير موجود في دليل الحسابات.";
        else if (await new InventoryCostingService(db).GetMethodAsync(companyId, ct) != Brqosh.Domain.Inventory.InventoryCostingMethod.Fifo)
            warning = "طريقة التكلفة ليست FIFO — تقييم الطبقات تقريبيّ؛ راجع الفرق يدوياً.";

        var diff = gl - valuation;
        return new InventoryGlReconciliation(inventoryAccountCode, valuation, gl, diff, diff == 0m && warning is null, warning);
    }
}
