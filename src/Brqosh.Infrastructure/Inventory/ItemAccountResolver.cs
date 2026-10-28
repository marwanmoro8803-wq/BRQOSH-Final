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
}
