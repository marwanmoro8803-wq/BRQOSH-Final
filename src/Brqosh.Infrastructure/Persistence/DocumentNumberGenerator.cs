using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Persistence;

/// <summary>
/// ترقيم موحد للمستندات: {بادئة}-{سنة}-{تسلسل من 6 خانات}، تسلسل مستقل لكل سنة.
/// </summary>
public static class DocumentNumberGenerator
{
    public static Task<string> NextAsync(IQueryable<string?> existingNumbers, string prefix, int year) =>
        NextAsync(existingNumbers, prefix, year, []);

    public static async Task<string> NextAsync(
        IQueryable<string?> existingNumbers,
        string prefix,
        int year,
        IEnumerable<string?> extraNumbers)
    {
        var fullPrefix = $"{prefix}-{year}-";
        var last = (await existingNumbers
                .Where(n => n != null && n.StartsWith(fullPrefix))
                .ToListAsync())
            .Concat(extraNumbers.Where(n => n != null && n.StartsWith(fullPrefix)))
            .OrderByDescending(n => n)
            .FirstOrDefault();

        var next = 1;
        if (last is not null && int.TryParse(last[fullPrefix.Length..], out var seq))
            next = seq + 1;
        return $"{fullPrefix}{next:D6}";
    }
}
