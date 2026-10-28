using Brqosh.Domain.Accounting;

namespace Brqosh.Domain.Inventory;

/// <summary>
/// <b>وحدةُ قياس — وأسرةٌ تنتمي إليها.</b>
///
/// <para><b>ما كان:</b> <c>Product.Unit</c> نصٌّ حرٌّ يُكتب باليد («قطعة»، «كجم»، «كرتون»)،
/// <b>لا يُتحقَّق منه ولا يُحوَّل به شيء</b>. فمَن اشترى كرتوناً وباع حبّةً سجّل <c>1+</c>
/// ثمّ <c>24−</c> على الرصيد نفسِه. والمخرجُ الموصى به في الشيفرة نفسِها كان: «أدخل
/// الكرتونَ سطراً واحداً بسعره الكامل» — <b>التفافٌ لا حلّ</b>.</para>
///
/// <para><b>وثانيةٌ نظامية:</b> مُولِّد الفوترة الإلكترونية كان يُثبّت <c>unitCode="PCE"</c>
/// لكلّ سطر — <b>فبيعُ خمسة كيلوجرامات يُبلَّغ للهيئة خمسَ قطع</b>. وهو تصريحٌ مخالفٌ
/// للواقع يمرّ على كلّ تصديقٍ بنيويّ.</para>
///
/// <para><b>والأسرة هي الفكرة:</b> الوحدةُ إمّا أصلٌ في أسرتها (<see cref="Factor"/> = 1)
/// أو مشتقّةٌ منها بمعامل. <b>ولا يُحوَّل بين أسرتين</b>: الكيلوجرامُ لا يصير متراً،
/// ومحاولةُ ذلك خطأُ إدخالٍ يجب أن يُقال لا أن يُحسَب.</para>
/// </summary>
public class UnitOfMeasure
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }

    /// <summary>الرمزُ الداخليّ — وهو ما يُشير إليه الصنفُ والسطر.</summary>
    public string Code { get; private set; }

    public string NameAr { get; private set; }

    /// <summary>
    /// رمزُ UN/ECE Rec 20 — <b>وهو ما تفهمه الهيئة</b>. ووحداتٌ عربيةٌ كثيرةٌ تشترك فيه
    /// (العلبةُ والصندوقُ والكرتون كلُّها <c>XBX</c>)، فليس مفتاحاً بل ترجمة.
    /// </summary>
    public string UnCode { get; private set; }

    /// <summary>رمزُ أصلِ الأسرة — ويساوي <see cref="Code"/> نفسَه للوحدة الأصل.</summary>
    public string BaseCode { get; private set; }

    /// <summary>كم وحدةً أصلاً في واحدةٍ من هذه — و1 للأصل نفسِه.</summary>
    public decimal Factor { get; private set; }

    public bool IsActive { get; private set; } = true;

    // EF Core
    private UnitOfMeasure()
    {
        Code = null!;
        NameAr = null!;
        UnCode = null!;
        BaseCode = null!;
    }

    private UnitOfMeasure(long companyId, string code, string nameAr, string unCode,
        string baseCode, decimal factor)
    {
        CompanyId = companyId;
        Code = code;
        NameAr = nameAr;
        UnCode = unCode;
        BaseCode = baseCode;
        Factor = factor;
    }

    public static UnitOfMeasure Create(long companyId, string code, string nameAr, string unCode,
        string? baseCode = null, decimal factor = 1m)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new AccountingException("رمز الوحدة مطلوب.");
        if (string.IsNullOrWhiteSpace(nameAr)) throw new AccountingException("اسم الوحدة مطلوب.");
        if (string.IsNullOrWhiteSpace(unCode)) throw new AccountingException("رمز UN/ECE مطلوب.");
        if (factor <= 0m) throw new AccountingException("معامل التحويل يجب أن يكون موجباً.");

        var resolvedBase = string.IsNullOrWhiteSpace(baseCode) ? code.Trim() : baseCode.Trim();

        // ‏**الأصلُ معاملُه واحدٌ بالضرورة**: وحدةٌ تُعلن نفسَها أصلاً بمعاملٍ غيرِ واحد
        // ‏تُفسد كلَّ تحويلٍ في أسرتها بصمت — فتُمنع في النطاق لا في الشاشة.
        if (resolvedBase == code.Trim() && factor != 1m)
            throw new AccountingException(
                $"الوحدة «{code}» أصلُ أسرتها، فمعاملُها واحدٌ لا {factor:N4}.");

        return new UnitOfMeasure(companyId, code.Trim(), nameAr.Trim(), unCode.Trim().ToUpperInvariant(),
            resolvedBase, factor);
    }

    public bool IsBase => Code == BaseCode;

    public void Deactivate() => IsActive = false;
    public void Reactivate() => IsActive = true;
}
