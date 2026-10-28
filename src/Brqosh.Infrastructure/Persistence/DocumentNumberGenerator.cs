using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Persistence;

/// <summary>
/// ترقيم موحد للمستندات: {بادئة}-{سنة}-{تسلسل من 6 خانات}، تسلسل مستقل لكل سنة.
/// </summary>
public static class DocumentNumberGenerator
{
    public static Task<string> NextAsync(
        BrqoshDbContext db, IQueryable<string?> existingNumbers, string prefix, int year, long companyId) =>
        NextAsync(db, existingNumbers, prefix, year, companyId, []);

    /// <summary>
    /// (CEEF-229 · م1-١٤) <b>السياقُ معاملٌ إلزاميّ — لأنّ القفلَ لا يجوز أن يُنسى.</b>
    ///
    /// <para>كان التوقيعُ يأخذ <c>IQueryable</c> وحدَه، فلا يملك المولّدُ سبيلاً إلى قفلٍ
    /// أصلاً — وهذا هو السببُ البنيويّ لغياب الحماية عن سبعةَ عشرَ موضعَ ترقيم. وبإلزام
    /// السياق يصير القفلُ <b>مفروضاً بالمترجِم</b>: لا يُمكن استدعاءُ المولّد بلا تمريره،
    /// فلا يوجد «موضعٌ نُسي».</para>
    /// </summary>
    /// <summary>
    /// ‏بتاريخ المستند: السنةُ في الرقم والقفل هي <b>سنةُ البداية المالية</b> (FY) — فيبدأ التسلسلُ من 1 كلَّ سنةٍ مالية.
    /// ‏ولمنشأةٍ تبدأ سنتُها في يناير هي السنةُ الميلادية نفسُها، فلا يتغيّر رقمٌ قائم.
    /// </summary>
    public static Task<string> NextAsync(
        BrqoshDbContext db, IQueryable<string?> existingNumbers, string prefix, DateOnly documentDate, long companyId) =>
        NextAsync(db, existingNumbers, prefix, documentDate, companyId, []);

    public static async Task<string> NextAsync(
        BrqoshDbContext db, IQueryable<string?> existingNumbers, string prefix, DateOnly documentDate, long companyId,
        IEnumerable<string?> extraNumbers) =>
        await NextAsync(db, existingNumbers, prefix,
            await Accounting.FiscalYearResolver.StartYearForAsync(db, companyId, documentDate),
            companyId, extraNumbers);

    public static async Task<string> NextAsync(
        BrqoshDbContext db,
        IQueryable<string?> existingNumbers,
        string prefix,
        int year,
        long companyId,
        IEnumerable<string?> extraNumbers)
    {
        // ‏قبل القراءة لا بعدها — وإلا قرأ المتزاحمان القيمةَ نفسَها ثمّ تسلسلا بلا فائدة.
        await DocumentSequenceLock.AcquireAsync(db.Database, companyId, prefix, year);

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
