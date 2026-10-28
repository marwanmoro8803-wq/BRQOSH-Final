using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Brqosh.Infrastructure.Persistence;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٧٨ · <c>found#1</c>) <b>الدفترُ نفسُه يرفض قيداً يُرحَّل في فترةٍ مقفلة — بتاريخ القيد لا بالتاريخ
/// الذي فحصته الخدمة.</b>
///
/// <para><b>ما كان:</b> سبعٌ وثلاثون خدمةً تكتب في <c>JournalEntries</c> رأساً، وكلُّها تمرّ من
/// <see cref="JournalPostingService.EnsurePeriodOpenAsync"/> — <b>للنداء لا للتاريخ</b>: خدمةٌ تفحص بتاريخٍ ثمّ تبني القيدَ
/// بآخر تمرّ من الحارس، <b>ولا يُفحص ما دخل الدفترَ أصلاً</b>. وقِيس في الشيكات (ح-٣٢): قيدٌ دخل فترةً <c>Closed</c> والعمليّةُ
/// <c>ACCEPTED</c> — وأُغلق هناك وحدَه، والستُّ والثلاثون الباقيةُ لم تُقَس.</para>
///
/// <para><b>فالحارسُ عند الحفظ</b> يسأل عن كلّ قيدٍ <b>يصير مُرحَّلاً</b> في هذا الحفظ (يُضاف مُرحَّلاً، أو يتحوّل من مسودّةٍ
/// إلى مُرحَّل) <b>بتاريخه هو</b> — بالقاعدة نفسِها التي في <c>EnsurePeriodOpenAsync</c>: فترتُه <c>Closed</c>، أو لا صفَّ لها
/// وسنتُها مقفلةٌ بقيد الإقفال السنويّ. <b>فالسبعُ والثلاثون ومَن يأتي بعدها محروسون بالبناء</b> لا بانضباط كلٍّ منها.</para>
///
/// <para><b>وما لا يرفضه عمداً:</b> المسوّدات (لم تدخل الدفتر)، <b>وتعليمُ الأصل «معكوساً»</b> حين يُعكس قيدُ فترةٍ مقفلةٍ بقيدٍ
/// في فترةٍ مفتوحة (التصحيحُ المشروع). <b>وقيدُ الإقفال السنويّ</b> يُحفظ قبل إقفال فترات سنته، فلا يصطدم به.</para>
///
/// <para><b>وهو حارسٌ ثانٍ لا بديل</b>: <c>EnsurePeriodOpenAsync</c> باقٍ بأقفاله (السنةُ ثمّ الفترة) يُرتّب المرحّلين والإقفال،
/// وهذا يسأل عمّا يُكتب فعلاً. <b>ويُسجَّل حيث يُسجَّل حارسُ التدقيق</b> — المصنعُ ومسارُ DI معاً.</para>
/// </summary>
public sealed class ClosedPeriodPostingInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is BrqoshDbContext db)
        {
            foreach (var (companyId, date) in BecomingPosted(db))
            {
                if (IsClosed(db, companyId, date))
                    throw Refusal(date);
            }
        }

        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is BrqoshDbContext db)
        {
            foreach (var (companyId, date) in BecomingPosted(db))
            {
                if (await IsClosedAsync(db, companyId, date, cancellationToken))
                    throw Refusal(date);
            }
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// القيودُ التي <b>تصير مُرحَّلةً</b> في هذا الحفظ — تُضاف بحالة الترحيل، أو كانت مسودّةً فرُحِّلت. ولكلّ منشأةٍ وتاريخٍ
    /// سؤالٌ واحد.
    /// </summary>
    internal static IReadOnlyList<(long CompanyId, DateOnly Date)> BecomingPosted(DbContext db) =>
        db.ChangeTracker.Entries<JournalEntry>()
            .Where(e => e.Entity.Status == JournalEntryStatus.Posted && (e.State == EntityState.Added || WasJustPosted(e)))
            .Select(e => (e.Entity.CompanyId, e.Entity.EntryDate))
            .Distinct()
            .ToList();

    private static bool WasJustPosted(EntityEntry<JournalEntry> e) =>
        e.State == EntityState.Modified
        && e.Property(x => x.Status).IsModified
        && e.OriginalValues.GetValue<JournalEntryStatus>(nameof(JournalEntry.Status)) != JournalEntryStatus.Posted;

    private static bool IsClosed(BrqoshDbContext db, long companyId, DateOnly date)
    {
        var code = FiscalPeriod.CodeFor(date);
        var status = db.FiscalPeriods.IgnoreQueryFilters()
            .Where(p => p.CompanyId == companyId && p.Code == code)
            .Select(p => (FiscalPeriodStatus?)p.Status)
            .FirstOrDefault();
        if (status is not null)
            return status == FiscalPeriodStatus.Closed;

        var mark = YearEndCloseService.SourceDocumentFor(
            FiscalYear.StartYearFor(date, FiscalYearResolver.StartMonth(db, companyId)));
        return db.JournalEntries.IgnoreQueryFilters().Any(e =>
            e.CompanyId == companyId && e.SourceDocument == mark && e.Status != JournalEntryStatus.Draft);
    }

    private static async Task<bool> IsClosedAsync(BrqoshDbContext db, long companyId, DateOnly date, CancellationToken ct)
    {
        var code = FiscalPeriod.CodeFor(date);
        var status = await db.FiscalPeriods.IgnoreQueryFilters()
            .Where(p => p.CompanyId == companyId && p.Code == code)
            .Select(p => (FiscalPeriodStatus?)p.Status)
            .FirstOrDefaultAsync(ct);
        if (status is not null)
            return status == FiscalPeriodStatus.Closed;

        var mark = YearEndCloseService.SourceDocumentFor(
            await FiscalYearResolver.StartYearForAsync(db, companyId, date, ct));
        return await db.JournalEntries.IgnoreQueryFilters().AnyAsync(e =>
            e.CompanyId == companyId && e.SourceDocument == mark && e.Status != JournalEntryStatus.Draft, ct);
    }

    private static AccountingException Refusal(DateOnly date) =>
        new($"الفترة المالية {FiscalPeriod.CodeFor(date)} مقفلة — القيدُ المؤرَّخ {date:yyyy-MM-dd} لا يُرحَّل فيها. "
            + "أعد فتح الفترة أولاً إن كنت مخوّلاً، أو صحِّح بقيدٍ في فترةٍ مفتوحة.");
}
