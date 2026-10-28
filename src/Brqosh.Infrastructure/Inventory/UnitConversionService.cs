using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Inventory;

/// <summary>
/// <b>تحويلُ كمّيةٍ من وحدة السطر إلى وحدة الصنف.</b>
///
/// <para><b>وموضعُ التحويل قرارٌ لا صدفة:</b> يقع <b>عند حدود المستند</b> — حيث يصير سطرُ
/// فاتورةٍ حركةً مخزنية — <b>ولا يقع أعمق من ذلك</b>. فكلُّ ما دون هذا الحدّ (الأرصدة
/// وطبقاتُ التكلفة والدفعات) <b>بوحدة الصنف وحدَها</b>، ولا يحمل رقمٌ فيه وحدةً مختلفة.</para>
///
/// <para><b>ولماذا لا يُحوَّل أعمق:</b> لو حُوِّل داخل خدمة المخزون لصار كلُّ رقمٍ هناك
/// يحتاج وحدتَه معه، وضياعُ واحدةٍ منها يُنتج رصيداً خاطئاً <b>لا تكشفه هويّةٌ ولا فهرس</b>
/// — لأنّ الرقمَ صحيحُ الشكل خاطئُ المعنى. فحدٌّ واحدٌ ضيّق أأمنُ من انتشارٍ واسع.</para>
///
/// <para><b>وسطرٌ بلا وحدةٍ هو الأصل</b> — أي بوحدة الصنف، وهو سلوكُ كلّ ما مضى. فلا
/// يتغيّر رقمٌ واحدٌ في قاعدةٍ قائمة بإضافة هذه الميزة.</para>
/// </summary>
public class UnitConversionService(BrqoshDbContext db)
{
    /// <summary>
    /// يُحوّل كمّيةَ سطرٍ إلى وحدة الصنف.
    ///
    /// <para><b>ويرفض ثلاثاً:</b> وحدةَ سطرٍ لا وجودَ لها في القالب · وصنفاً وحدتُه نصٌّ
    /// لا يُطابق وحدةً معروفة (فلا يُعرف إلى أيّ شيءٍ يُحوَّل) · وتحويلاً بين أسرتين.
    /// <b>والرفضُ في الثلاث أصدقُ من رقمٍ يبدو سليماً</b>.</para>
    /// </summary>
    public async Task<decimal> ToProductUnitAsync(
        long companyId, Product product, decimal quantity, string? lineUnitCode,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(lineUnitCode))
            return quantity;

        var units = await db.UnitsOfMeasure.AsNoTracking()
            .Where(u => u.CompanyId == companyId)
            .ToListAsync(ct);

        var from = units.FirstOrDefault(u => u.Code == lineUnitCode.Trim())
            ?? throw new AccountingException(
                $"وحدةُ القياس «{lineUnitCode}» غير معرَّفة في هذه المنشأة.");

        var productUnit = Resolve(units, product.Unit)
            ?? throw new AccountingException(
                $"وحدةُ الصنف '{product.NameAr}' مكتوبةٌ نصّاً («{product.Unit}») ولا تُطابق "
                + "وحدةً معرَّفة — فلا يُعرف إلى أيّ وحدةٍ يُحوَّل. اربطِ الصنفَ بوحدةٍ من القائمة.");

        if (from.BaseCode != productUnit.BaseCode)
            throw new AccountingException(
                $"لا تحويلَ بين «{from.NameAr}» و«{productUnit.NameAr}» — "
                + "وحدتان من أسرتين مختلفتين.");

        // ‏من وحدةِ السطر إلى الأصل، ثمّ من الأصل إلى وحدة الصنف.
        return Math.Round(quantity * from.Factor / productUnit.Factor, 4, MidpointRounding.AwayFromZero);
    }

    /// <summary>رمزُ UN/ECE لسطرٍ — من وحدته إن ذُكرت، وإلا من وحدة الصنف النصّية.</summary>
    public async Task<string> UnCodeForAsync(
        long companyId, string? lineUnitCode, string? productUnitText, CancellationToken ct = default)
    {
        var units = await db.UnitsOfMeasure.AsNoTracking()
            .Where(u => u.CompanyId == companyId)
            .ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(lineUnitCode)
            && units.FirstOrDefault(u => u.Code == lineUnitCode.Trim()) is { } lineUnit)
            return lineUnit.UnCode;

        return Resolve(units, productUnitText)?.UnCode ?? UnitCatalog.FallbackUnCode;
    }

    /// <summary>
    /// ‏يُطابق نصّاً حرّاً بوحدةٍ معرَّفة — بالرمز أوّلاً، ثمّ بالاسم، ثمّ بمرادفات القالب.
    ///
    /// <para><b>والمرادفاتُ هي الجسرُ إلى ما مضى</b>: كلُّ صنفٍ قائمٍ يحمل وحدتَه نصّاً
    /// عربياً حرّاً، فبغير جسرٍ تفقد الأصنافُ القائمةُ وحداتِها يومَ الترقية.</para>
    /// </summary>
    private static UnitOfMeasure? Resolve(List<UnitOfMeasure> units, string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var trimmed = text.Trim();
        var direct = units.FirstOrDefault(u =>
            string.Equals(u.Code, trimmed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(u.NameAr, trimmed, StringComparison.Ordinal));
        if (direct is not null) return direct;

        return UnitCatalog.Match(trimmed) is { } seed
            ? units.FirstOrDefault(u => u.Code == seed.Code)
            : null;
    }
}
