using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Inventory;

/// <summary>
/// حساباتُ الصنف بالأسبقيّة: <b>حسابُ الصنف الخاصّ ← حسابُ فئته ← <c>null</c></b>،
/// و<c>null</c> يعني أن يستعمل المُرحِّلُ الحسابَ النظاميّ العامّ كما كان.
/// </summary>
public static class ItemAccountResolver
{
    public readonly record struct ItemAccounts(long? Revenue, long? Inventory, long? Cogs);

    public static async Task<IReadOnlyDictionary<long, ItemAccounts>> ResolveAsync(
        BrqoshDbContext db, long companyId, IEnumerable<Product> products, CancellationToken ct = default)
    {
        var list = products.ToList();
        var catIds = list.Where(p => p.CategoryId != null).Select(p => p.CategoryId!.Value).Distinct().ToList();
        var cats = catIds.Count == 0
            ? new Dictionary<long, ProductCategory>()
            : await db.ProductCategories.AsNoTracking()
                .Where(c => c.CompanyId == companyId && catIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, ct);
        var result = new Dictionary<long, ItemAccounts>();
        foreach (var p in list)
        {
            ProductCategory? c = p.CategoryId is { } id && cats.TryGetValue(id, out var cc) ? cc : null;
            result[p.Id] = new ItemAccounts(
                p.RevenueAccountId ?? c?.RevenueAccountId,
                p.InventoryAccountId ?? c?.InventoryAccountId,
                p.CogsAccountId ?? c?.CogsAccountId);
        }
        return result;
    }

    /// <summary>
    /// ‏يوجّه نصيبَ كلّ صنفٍ له حسابٌ خاصّ (أو حسابُ فئة) من سطر الحساب العامّ <paramref name="global"/>
    /// إلى حسابه — والباقي يبقى على العامّ. ‏يُستعمل في كلّ مسارٍ يُدين المخزونَ أو يُدائنه كي لا ينشقّ الرصيد.
    /// </summary>
    public static async Task RerouteAsync(BrqoshDbContext db, long companyId, JournalEntry entry, Account global,
        IEnumerable<(long ProductId, decimal Amount)> values, Func<ItemAccounts, long?> pick, bool debitSide,
        CancellationToken ct = default)
    {
        var byProduct = values.Where(v => v.Amount > 0m).GroupBy(v => v.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(v => v.Amount));
        if (byProduct.Count == 0) return;
        var ids = byProduct.Keys.ToList();
        var products = await db.Products.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.CompanyId == companyId && ids.Contains(p.Id)).ToListAsync(ct);
        if (!products.Any(p => p.CategoryId != null || p.RevenueAccountId != null
                               || p.InventoryAccountId != null || p.CogsAccountId != null))
            return;
        var accounts = await ResolveAsync(db, companyId, products, ct);
        foreach (var g in byProduct.GroupBy(kv => accounts.TryGetValue(kv.Key, out var a) ? pick(a) : null))
        {
            if (g.Key is not { } id || id == global.Id) continue;
            var target = await db.Accounts.SingleOrDefaultAsync(a => a.Id == id && a.CompanyId == companyId, ct)
                ?? throw new AccountingException($"حساب الصنف/الفئة رقم {id} غير موجود في دليل حسابات المنشأة.");
            entry.RerouteAmount(global.Id, target, g.Sum(kv => kv.Value), debitSide);
        }
    }

    /// <summary>كلُّ حسابات المخزون الخاصّة (أصنافاً وفئات) — للمطابقة مع الأستاذ.</summary>
    public static async Task<List<long>> InventoryAccountIdsAsync(BrqoshDbContext db, long companyId, CancellationToken ct = default)
    {
        var fromProducts = await db.Products.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.CompanyId == companyId && p.InventoryAccountId != null)
            .Select(p => p.InventoryAccountId!.Value).ToListAsync(ct);
        var fromCategories = await db.ProductCategories.AsNoTracking()
            .Where(c => c.CompanyId == companyId && c.InventoryAccountId != null)
            .Select(c => c.InventoryAccountId!.Value).ToListAsync(ct);
        return fromProducts.Concat(fromCategories).Distinct().ToList();
    }
}
