namespace Brqosh.Domain.Tax;

/// <summary>
/// فئة ضريبة القيمة المضافة لسطر المستند — رموز ZATCA/UBL المعتمدة.
///
/// كانت النسبة 15% مسمّرة في كل المسارات، فيستحيل إصدار فاتورة فيها سطر
/// صادرات صفريّ أو دواء معفى: كل سطر يُضرَّب 15% إجبارياً. كما كان XML يصنّف السطر
/// الصفريّ "O" بينما يعلن نسبة 15.00 — تناقض يرفضه مدقّق الهيئة.
/// </summary>
public enum VatCategory
{
    /// <summary>S — النسبة الأساسية (15% في السعودية).</summary>
    Standard = 0,

    /// <summary>Z — صفريّ النسبة: خاضع للضريبة بنسبة صفر (صادرات، نقل دولي).</summary>
    ZeroRated = 1,

    /// <summary>E — معفى: خارج نطاق الخصم (خدمات مالية، إيجار سكني).</summary>
    Exempt = 2,

    /// <summary>O — خارج نطاق الضريبة.</summary>
    OutOfScope = 3,
}

public static class VatCategoryExtensions
{
    /// <summary>رمز الفئة في UBL/ZATCA (cac:TaxCategory/cbc:ID).</summary>
    public static string UblCode(this VatCategory category) => category switch
    {
        VatCategory.Standard => "S",
        VatCategory.ZeroRated => "Z",
        VatCategory.Exempt => "E",
        VatCategory.OutOfScope => "O",
        _ => "S",
    };

    /// <summary>
    /// نسبة الضريبة الفعلية للفئة. الفئات غير الأساسية نسبتها صفر دائماً —
    /// وهذا ما يجعل النسبة المعلنة في XML متسقة مع مبلغ الضريبة المحسوب.
    /// </summary>
    public static decimal RateFor(this VatCategory category, decimal standardRate) =>
        category == VatCategory.Standard ? standardRate : 0m;

    public static string NameAr(this VatCategory category) => category switch
    {
        VatCategory.Standard => "أساسية",
        VatCategory.ZeroRated => "صفرية",
        VatCategory.Exempt => "معفاة",
        VatCategory.OutOfScope => "خارج النطاق",
        _ => "أساسية",
    };
}
