using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Platform;

/// <summary>صفٌّ محذوفٌ حذفاً ناعماً — ما يكفي ليعرفه المالكُ ويقرّر.</summary>
public record SoftDeletedRow(long Id, string Code, string Name, DateTime DeletedAtUtc);

/// <summary>
/// <b>الصفُّ المحذوف حذفاً ناعماً: يُرى ويُسترجَع.</b>
///
/// <para><b>ما كان:</b> الصفُّ المحذوف يبقى في الجدول <b>ولا يظهر في أيّ شاشة</b> — يحجز
/// كودَه، ولا سبيلَ إلى إعادته. والمخرجُ الوحيد اختيارُ كودٍ آخر. وسُجّل بنداً مفتوحاً منذ
/// <c>م2-٢٧</c>.</para>
///
/// <para><b>وأخطرُ ممّا يبدو:</b> تعبئةٌ في المُرقِّع كانت تُنفَّذ في <b>كلّ إقلاع</b> —
/// <c>UPDATE … SET DeletedAtUtc = NOW() WHERE IsActive = false</c> — فحوّلت كلَّ صفٍّ
/// <b>موقوفٍ</b> إلى <b>محذوف</b>. أُوقفت في <c>م0-١٣</c>، <b>ولم يُسترجَع ما حوّلته</b>:
/// فحسابٌ أوقفه المحاسبُ صار غائباً عن شجرة الحسابات بلا رجعة.</para>
///
/// <para><b>ولا يُصلَح ذلك آلياً</b>: الصفُّ الذي حوّلته التعبئةُ والصفُّ المحذوفُ قصداً
/// <b>لا يفترقان في البيانات</b> — كلاهما <c>IsActive = false</c> و<c>DeletedAtUtc</c>
/// مضبوط. <b>واسترجاعُ ما لم يُقصَد استرجاعُه أسوأ من تركه</b>. فالعلاجُ إظهارٌ وقرارٌ
/// بيد المالك — وهو مذهبُ <c>م2-١١</c> نفسُه: يُكشف ما لا يُحسَم.</para>
/// </summary>
public class SoftDeletedRowService(BrqoshDbContext db)
{
    /// <summary>يسرد المحذوفَ ليراه المالك — وهو أوّلُ ما ينقص: لا شاشةَ تُظهره اليوم.</summary>
    public async Task<IReadOnlyList<SoftDeletedRow>> ListAsync(
        CodeAvailabilityScope scope, long companyId, CancellationToken ct = default) =>
        scope switch
        {
            CodeAvailabilityScope.Product => await db.Products.IgnoreQueryFilters()
                .Where(p => p.CompanyId == companyId && p.DeletedAtUtc != null)
                .Select(p => new SoftDeletedRow(p.Id, p.Code, p.NameAr, p.DeletedAtUtc!.Value))
                .ToListAsync(ct),

            CodeAvailabilityScope.Account => await db.Accounts.IgnoreQueryFilters()
                .Where(a => a.CompanyId == companyId && a.DeletedAtUtc != null)
                .Select(a => new SoftDeletedRow(a.Id, a.Code, a.NameAr, a.DeletedAtUtc!.Value))
                .ToListAsync(ct),

            CodeAvailabilityScope.Warehouse => await db.Warehouses.IgnoreQueryFilters()
                .Where(w => w.CompanyId == companyId && w.DeletedAtUtc != null)
                .Select(w => new SoftDeletedRow(w.Id, w.Code, w.NameAr, w.DeletedAtUtc!.Value))
                .ToListAsync(ct),

            CodeAvailabilityScope.CostCenter => await db.CostCenters.IgnoreQueryFilters()
                .Where(c => c.CompanyId == companyId && c.DeletedAtUtc != null)
                .Select(c => new SoftDeletedRow(c.Id, c.Code, c.NameAr, c.DeletedAtUtc!.Value))
                .ToListAsync(ct),

            _ => [],
        };

    /// <summary>
    /// يُعيد صفّاً محذوفاً إلى الحياة.
    ///
    /// <para><b>ولا يتصادم أبداً</b> — والسببُ مقيسٌ لا مفترَض: الفهرسُ الفريد الذي يحجز
    /// الكودَ يمنع أن يوجد صفٌّ حيٌّ يحمله. انظر التعليقَ داخل الدالّة.</para>
    ///
    /// <para><b>وأثرُ تدقيقٍ إلزاميّ</b>: إعادةُ صفٍّ إلى الدفاتر تغييرٌ لا يقلّ عن حذفه —
    /// وقد كان الحذفُ نفسُه بلا أثرٍ في بعض المسارات.</para>
    /// </summary>
    public async Task RestoreAsync(
        CodeAvailabilityScope scope, long companyId, long id, string username,
        CancellationToken ct = default)
    {
        SecurityContext.EnsureAdministrator();

        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var target = (await ListAsync(scope, companyId, token))
                .FirstOrDefault(r => r.Id == id)
                ?? throw new AccountingException("لا يوجد صفٌّ محذوفٌ بهذا المعرّف.");

            // ‏**ولا حارسَ تصادمٍ هنا — وهو قرارٌ قِيس لا سهو.**
            //
            // ‏بنيتُ فحصاً يرفض الاسترجاعَ إن صار الكودُ مستعمَلاً، فسقط الاختبارُ عند
            // ‏**تهيئته**: لا يمكن إنشاءُ صفٍّ حيٍّ بكود المحذوف أصلاً — الفهرسُ الفريد
            // ‏يمنعه، وهو نفسُه سببُ هذا البند كلِّه.
            //
            // ‏فالفهرسُ الذي يحجز الكودَ **يضمن أنّ الاسترجاعَ لا يتصادم أبداً**. وحارسٌ
            // ‏لحالةٍ لا تقع شيفرةٌ ميتة — وقد أُزيل من هذا المستودع أكثرُ من واحدٍ منها.

            await ClearDeletionAsync(scope, companyId, id, token);
            await db.SaveChangesAsync(token);

            await new AuditService(db).LogAsync(
                companyId, username, "استرجاع صفٍّ محذوف",
                scope.ToString(), id.ToString(),
                $"«{target.Code}» — {target.Name} (حُذف في {target.DeletedAtUtc:yyyy-MM-dd})",
                "استرجاعٌ بطلب المالك — الصفُّ كان محجوباً عن كلّ الشاشات.", token);
        }, ct);
    }

    /// <summary>
    /// ‏يمحو لحظةَ الحذف عبر EF.
    ///
    /// <para><b>ولا دالّةَ نطاقٍ لذلك</b>: ستّةٌ من الكيانات الثمانية <b>لا مسارَ حذفٍ ناعمٍ
    /// لها أصلاً</b> — أصابها الحذفُ من تعبئةٍ في المُرقِّع لا من النطاق. فالاسترجاعُ في
    /// الطبقة نفسِها التي وقع فيها الحذف.</para>
    /// </summary>
    private async Task ClearDeletionAsync(
        CodeAvailabilityScope scope, long companyId, long id, CancellationToken ct)
    {
        object entity = scope switch
        {
            CodeAvailabilityScope.Product => await db.Products.IgnoreQueryFilters()
                .SingleAsync(p => p.Id == id && p.CompanyId == companyId, ct),
            CodeAvailabilityScope.Account => await db.Accounts.IgnoreQueryFilters()
                .SingleAsync(a => a.Id == id && a.CompanyId == companyId, ct),
            CodeAvailabilityScope.Warehouse => await db.Warehouses.IgnoreQueryFilters()
                .SingleAsync(w => w.Id == id && w.CompanyId == companyId, ct),
            CodeAvailabilityScope.CostCenter => await db.CostCenters.IgnoreQueryFilters()
                .SingleAsync(c => c.Id == id && c.CompanyId == companyId, ct),
            _ => throw new AccountingException("هذا النوع لا يدعم الاسترجاع بعد."),
        };

        db.Entry(entity).Property("DeletedAtUtc").CurrentValue = null;
    }
}
