using Brqosh.Application.MasterData;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.MasterData;

public sealed class MasterDataGovernanceService(BrqoshDbContext db) : IMasterDataGovernanceService
{
    public async Task<bool> CheckDuplicateVatAsync(
        long companyId,
        string vat,
        long? excludeId,
        MasterDataEntityKind entityKind,
        CancellationToken ct = default)
    {
        var normalized = (vat ?? string.Empty).Trim();
        if (normalized.Length == 0)
            return false;

        return entityKind switch
        {
            MasterDataEntityKind.Customer => await db.Customers.AnyAsync(c =>
                c.CompanyId == companyId
                && c.VatRegistrationNumber == normalized
                && (excludeId == null || c.Id != excludeId.Value), ct),
            MasterDataEntityKind.Supplier => await db.Suppliers.AnyAsync(s =>
                s.CompanyId == companyId
                && s.VatRegistrationNumber == normalized
                && (excludeId == null || s.Id != excludeId.Value), ct),
            _ => false,
        };
    }

    public async Task<IReadOnlyList<SimilarNameMatch>> FindSimilarNamesAsync(
        long companyId,
        string nameAr,
        double threshold,
        CancellationToken ct = default)
    {
        var needle = NormalizeName(nameAr);
        if (needle.Length == 0)
            return [];

        var matches = new List<SimilarNameMatch>();

        var customers = await db.Customers
            .Where(c => c.CompanyId == companyId)
            .Select(c => new { c.Id, c.NameAr })
            .ToListAsync(ct);

        foreach (var c in customers)
        {
            var score = NameSimilarity(needle, NormalizeName(c.NameAr));
            if (score >= threshold)
                matches.Add(new SimilarNameMatch(MasterDataEntityKind.Customer, c.Id, c.NameAr, score));
        }

        var suppliers = await db.Suppliers
            .Where(s => s.CompanyId == companyId)
            .Select(s => new { s.Id, s.NameAr })
            .ToListAsync(ct);

        foreach (var s in suppliers)
        {
            var score = NameSimilarity(needle, NormalizeName(s.NameAr));
            if (score >= threshold)
                matches.Add(new SimilarNameMatch(MasterDataEntityKind.Supplier, s.Id, s.NameAr, score));
        }

        return matches
            .OrderByDescending(m => m.SimilarityScore)
            .ThenBy(m => m.NameAr)
            .ToList();
    }

    private static string NormalizeName(string value) =>
        string.Concat((value ?? string.Empty).Trim().ToLowerInvariant().Where(c => !char.IsWhiteSpace(c)));

    private static double NameSimilarity(string left, string right)
    {
        if (left.Length == 0 || right.Length == 0)
            return 0d;
        if (left == right)
            return 1d;
        if (left.Contains(right, StringComparison.Ordinal) || right.Contains(left, StringComparison.Ordinal))
            return 0.9d;

        var distance = LevenshteinDistance(left, right);
        var maxLen = Math.Max(left.Length, right.Length);
        return 1d - distance / (double)maxLen;
    }

    private static int LevenshteinDistance(string left, string right)
    {
        var costs = new int[right.Length + 1];
        for (var j = 0; j <= right.Length; j++)
            costs[j] = j;

        for (var i = 1; i <= left.Length; i++)
        {
            var previous = costs[0];
            costs[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                var current = costs[j];
                var substitution = left[i - 1] == right[j - 1] ? 0 : 1;
                costs[j] = Math.Min(
                    Math.Min(costs[j] + 1, costs[j - 1] + 1),
                    previous + substitution);
                previous = current;
            }
        }

        return costs[right.Length];
    }
}
