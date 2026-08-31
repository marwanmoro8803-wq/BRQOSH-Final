using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Platform;

/// <summary>الكياناتُ التي تجمع الحذفَ الناعم مع فهرسٍ فريد على الكود.</summary>
public enum CodeAvailabilityScope
{
    Product,
    Account,
    Warehouse,
    BankAccount,
    CostCenter,
    ConstructionProject,
    BillOfMaterials,
    AppUser,
}

/// <summary>حالةُ كودٍ قبل الحفظ.</summary>
public enum CodeAvailability
{
    /// <summary>متاح.</summary>
    Available,

    /// <summary>مستعمَلٌ لصفٍّ قائم — نشطٍ أو موقوف.</summary>
    TakenByExistingRow,

    /// <summary>محجوزٌ لصفٍّ <b>محذوف</b> — لا يظهر في أيّ شاشة، والفهرسُ الفريد يمنع إعادةَ استعماله.</summary>
    TakenByDeletedRow,
}

/// <summary>
/// (CEEF-229 · م2-١٠ / D-6) <b>لماذا يُرفض كودٌ لا يراه المستخدم في أيّ مكان.</b>
///
/// <para><b>العطل:</b> الفهرسُ الفريد على <c>(CompanyId, Code)</c> <b>لا يعرف الحذف الناعم</b> —
/// فالصفُّ المحذوف يبقى في الجدول ويحجز كودَه إلى الأبد. والشاشةُ لا تفحص التكرار مسبقاً،
/// فيصطدم الحفظُ بانتهاك تفرّدٍ ويُعرض نصُّ <c>DbUpdateException</c> <b>الإنجليزيّ</b> كما
/// هو داخل واجهةٍ عربية: «An error occurred while saving the entity changes…».</para>
///
/// <para><b>والفهرسُ لم يُجزَّأ عمداً</b> — قرارٌ مكتوبٌ منذ <c>م2-٢٧</c>: جعلُه جزئياً
/// (يتجاهل المحذوف) يسمح بكودَين متطابقَين أحدهما محذوف، <b>فيصير سجلُّ الحركة القديم
/// ملتبساً بين صفَّين</b> — والالتباسُ في الدفاتر أسوأ من رفضٍ مفهوم.</para>
///
/// <para><b>وما وُسِّع هنا:</b> كان الشرحُ مبنياً <b>للأصناف وحدها</b>، وسبعةُ كياناتٍ أخرى
/// تحمل العطلَ نفسَه تُخرج نصَّ المحرّك. فصار الفحصُ واحداً لثمانيتها — <b>فالعطلُ واحدٌ
/// فلا يُعالَج بثمانية علاجات</b>.</para>
///
/// <para><b>ويبقى استرجاعُ المحذوف بنداً مفتوحاً</b>: الرسالةُ تقول ما يحجز الكود، ولا تُتيح
/// إعادةَ الصفّ المحذوف — فالمخرجُ الوحيد اليوم اختيارُ كودٍ آخر.</para>
/// </summary>
public class CodeAvailabilityQuery(BrqoshDbContext db)
{
    public async Task<CodeAvailability> CheckAsync(
        CodeAvailabilityScope scope, long companyId, string code,
        long? excludingId = null, CancellationToken ct = default)
    {
        var normalized = (code ?? "").Trim();
        if (normalized.Length == 0)
            return CodeAvailability.Available;

        var (found, deletedAt) = await FindAsync(scope, companyId, normalized, excludingId, ct);

        if (!found) return CodeAvailability.Available;
        return deletedAt is null
            ? CodeAvailability.TakenByExistingRow
            : CodeAvailability.TakenByDeletedRow;
    }

    /// <summary>
    /// ‏يُعيد <c>null</c> إن لم يوجد صفّ، وإلا لحظةَ حذفه (وهي فارغةٌ للصفّ القائم).
    /// <b>و<c>IgnoreQueryFilters</c> شرطٌ لا تحسين</b>: المحذوفُ هو ما نبحث عنه.
    /// </summary>
    private async Task<(bool Found, DateTime? DeletedAt)> FindAsync(
        CodeAvailabilityScope scope, long companyId, string code, long? excludingId, CancellationToken ct)
    {
        static (bool, DateTime?) Wrap(DateTime? value, bool found) => (found, value);

        switch (scope)
        {
            case CodeAvailabilityScope.Product:
            {
                var m = await db.Products.IgnoreQueryFilters()
                    .Where(p => p.CompanyId == companyId && p.Code == code)
                    .Where(p => excludingId == null || p.Id != excludingId)
                    .Select(p => new { p.DeletedAtUtc }).FirstOrDefaultAsync(ct);
                return Wrap(m?.DeletedAtUtc, m is not null);
            }
            case CodeAvailabilityScope.Account:
            {
                var m = await db.Accounts.IgnoreQueryFilters()
                    .Where(a => a.CompanyId == companyId && a.Code == code)
                    .Where(a => excludingId == null || a.Id != excludingId)
                    .Select(a => new { a.DeletedAtUtc }).FirstOrDefaultAsync(ct);
                return Wrap(m?.DeletedAtUtc, m is not null);
            }
            case CodeAvailabilityScope.Warehouse:
            {
                var m = await db.Warehouses.IgnoreQueryFilters()
                    .Where(w => w.CompanyId == companyId && w.Code == code)
                    .Where(w => excludingId == null || w.Id != excludingId)
                    .Select(w => new { w.DeletedAtUtc }).FirstOrDefaultAsync(ct);
                return Wrap(m?.DeletedAtUtc, m is not null);
            }
            case CodeAvailabilityScope.BankAccount:
            {
                var m = await db.BankAccounts.IgnoreQueryFilters()
                    .Where(b => b.CompanyId == companyId && b.Code == code)
                    .Where(b => excludingId == null || b.Id != excludingId)
                    .Select(b => new { b.DeletedAtUtc }).FirstOrDefaultAsync(ct);
                return Wrap(m?.DeletedAtUtc, m is not null);
            }
            case CodeAvailabilityScope.CostCenter:
            {
                var m = await db.CostCenters.IgnoreQueryFilters()
                    .Where(c => c.CompanyId == companyId && c.Code == code)
                    .Where(c => excludingId == null || c.Id != excludingId)
                    .Select(c => new { c.DeletedAtUtc }).FirstOrDefaultAsync(ct);
                return Wrap(m?.DeletedAtUtc, m is not null);
            }
            case CodeAvailabilityScope.ConstructionProject:
            {
                var m = await db.ConstructionProjects.IgnoreQueryFilters()
                    .Where(p => p.CompanyId == companyId && p.Code == code)
                    .Where(p => excludingId == null || p.Id != excludingId)
                    .Select(p => new { p.DeletedAtUtc }).FirstOrDefaultAsync(ct);
                return Wrap(m?.DeletedAtUtc, m is not null);
            }
            case CodeAvailabilityScope.BillOfMaterials:
            {
                var m = await db.BillsOfMaterials.IgnoreQueryFilters()
                    .Where(b => b.CompanyId == companyId && b.Code == code)
                    .Where(b => excludingId == null || b.Id != excludingId)
                    .Select(b => new { b.DeletedAtUtc }).FirstOrDefaultAsync(ct);
                return Wrap(m?.DeletedAtUtc, m is not null);
            }
            case CodeAvailabilityScope.AppUser:
            {
                var m = await db.AppUsers.IgnoreQueryFilters()
                    .Where(u => u.CompanyId == companyId && u.Username == code)
                    .Where(u => excludingId == null || u.Id != excludingId)
                    .Select(u => new { u.DeletedAtUtc }).FirstOrDefaultAsync(ct);
                return Wrap(m?.DeletedAtUtc, m is not null);
            }
            default:
                return (false, null);
        }
    }

    /// <summary>رسالةٌ عربية تقول ما الذي يحجز الكود — أو فارغةٌ إن كان متاحاً.</summary>
    public static string? Explain(CodeAvailability availability, CodeAvailabilityScope scope, string code)
    {
        var noun = Noun(scope);
        return availability switch
        {
            CodeAvailability.TakenByExistingRow =>
                $"الكود «{code}» مستعمَلٌ لـ{noun} آخر. إن كان موقوفاً فأظهِر الموقوف من القائمة.",
            CodeAvailability.TakenByDeletedRow =>
                $"الكود «{code}» محجوزٌ لـ{noun} محذوف لا يظهر في القوائم — اختر كوداً آخر.",
            _ => null,
        };
    }

    private static string Noun(CodeAvailabilityScope scope) => scope switch
    {
        CodeAvailabilityScope.Product => "صنفٍ",
        CodeAvailabilityScope.Account => "حسابٍ",
        CodeAvailabilityScope.Warehouse => "مستودعٍ",
        CodeAvailabilityScope.BankAccount => "حسابٍ بنكيّ",
        CodeAvailabilityScope.CostCenter => "مركز تكلفةٍ",
        CodeAvailabilityScope.ConstructionProject => "مشروعٍ",
        CodeAvailabilityScope.BillOfMaterials => "قائمة موادَّ",
        CodeAvailabilityScope.AppUser => "مستخدمٍ",
        _ => "صفٍّ",
    };
}
