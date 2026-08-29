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

        // (CEEF-229 · م2-٤أ) **الأكبرُ تأخذه القاعدة** — انظر الشرح المفصّل في
        // <see cref="JournalEntryNumberGenerator"/>. كان هنا `ToListAsync()` ثمّ فرزٌ في
        // الذاكرة، فتُقرأ كلُّ أرقام السنة لإعطاء رقمٍ واحد: كلفةُ المستند تنمو مع الدفتر
        // والإجماليّ تربيعيّ. وهذا المولّد يخدم الفواتير والسندات والإشعارات وأوامر الشراء
        // والحركات المخزنية — أي كلَّ ما يُرقَّم في النظام.
        var fromDb = await existingNumbers
            .Where(n => n != null && n.StartsWith(fullPrefix))
            .OrderByDescending(n => n)
            .FirstOrDefaultAsync();

        var pending = extraNumbers
            .Where(n => n != null && n.StartsWith(fullPrefix))
            .OrderByDescending(n => n)
            .FirstOrDefault();

        var last = new[] { fromDb, pending }
            .Where(n => n is not null)
            .OrderByDescending(n => n)
            .FirstOrDefault();

        var next = 1;
        if (last is not null && int.TryParse(last[fullPrefix.Length..], out var seq))
            next = seq + 1;

        // ‏حدُّ الستّ خانات: بعده يلتفّ الترتيبُ النصّيّ ويتكرّر الرقم — والتوقّفُ أوضح.
        if (next > 999_999)
            throw new Brqosh.Domain.Accounting.AccountingException(
                $"تجاوز تسلسل «{prefix}» لسنة {year} ستّ خانات — لا يمكن توليد رقمٍ جديد بأمان.");

        return $"{fullPrefix}{next:D6}";
    }
}
