using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Inventory;

/// <summary>حالةُ كودِ صنفٍ قبل الحفظ.</summary>
public enum ProductCodeAvailability
{
    /// <summary>متاح.</summary>
    Available,

    /// <summary>مستعمَلٌ لصنفٍ قائم — نشطٍ أو موقوف.</summary>
    TakenByExistingProduct,

    /// <summary>محجوزٌ لصنفٍ <b>محذوف</b> — لا يظهر في أيّ شاشة، والفهرس الفريد يمنع إعادة استعماله.</summary>
    TakenByDeletedProduct,
}

/// <summary>
/// (CEEF-229 · م2-٢٧) <b>لماذا يُرفض كودٌ لا يراه المستخدم في أيّ مكان.</b>
///
/// <para><b>العطل:</b> الفهرس الفريد على <c>(CompanyId, Code)</c> <b>لا يعرف الحذف الناعم</b> —
/// فالصفّ المحذوف يبقى في الجدول ويحجز كوده إلى الأبد. وشاشةُ الصنف لا تفحص التكرار مسبقاً
/// إطلاقاً، فيصطدم الحفظُ بانتهاك تفرّدٍ من قاعدة البيانات، ويُعرض نصُّ
/// <c>DbUpdateException</c> <b>الإنجليزيّ</b> كما هو داخل حوارٍ عربيّ:
/// «An error occurred while saving the entity changes…».</para>
///
/// <para>فالمالك لا يعرف السبب، ولا يرى الصنف القديم في أيّ قائمة، ولا يستطيع إعادة تفعيله —
/// ويبقى الكود محروقاً بلا تفسير. وهذا الفحص يقول له <b>ما الذي يحجز الكود</b> بدل أن يترك
/// قاعدة البيانات تتحدّث بلغتها.</para>
///
/// <para>ولم يُغيَّر الفهرس نفسه عمداً: جعلُه جزئياً (يتجاهل المحذوف) يسمح بكودَين متطابقَين
/// أحدهما محذوف، فيصير سجلّ الحركة القديم ملتبساً بين صنفَين — والالتباس في الدفاتر أسوأ من
/// رفضٍ مفهوم. والرسالة الواضحة تكفي، ويبقى استرجاعُ المحذوف بنداً مفتوحاً.</para>
/// </summary>
public class ProductCodeAvailabilityQuery(BrqoshDbContext db)
{
    public async Task<ProductCodeAvailability> CheckAsync(
        long companyId, string code, long? excludingProductId = null, CancellationToken ct = default)
    {
        var normalized = (code ?? "").Trim();
        if (normalized.Length == 0)
            return ProductCodeAvailability.Available;

        var match = await db.Products
            .IgnoreQueryFilters()
            .Where(p => p.CompanyId == companyId && p.Code == normalized)
            .Where(p => excludingProductId == null || p.Id != excludingProductId)
            .Select(p => new { p.DeletedAtUtc })
            .FirstOrDefaultAsync(ct);

        if (match is null)
            return ProductCodeAvailability.Available;

        return match.DeletedAtUtc is null
            ? ProductCodeAvailability.TakenByExistingProduct
            : ProductCodeAvailability.TakenByDeletedProduct;
    }

    /// <summary>رسالةٌ عربية تقول ما الذي يحجز الكود — أو فارغةٌ إن كان متاحاً.</summary>
    public static string? Explain(ProductCodeAvailability availability, string code) => availability switch
    {
        ProductCodeAvailability.TakenByExistingProduct =>
            $"الكود «{code}» مستعمَلٌ لصنفٍ آخر. إن كان موقوفاً فأظهِر الموقوف من قائمة الأصناف.",
        ProductCodeAvailability.TakenByDeletedProduct =>
            $"الكود «{code}» محجوزٌ لصنفٍ محذوف لا يظهر في القوائم — اختر كوداً آخر.",
        _ => null,
    };
}
