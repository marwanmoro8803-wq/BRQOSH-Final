namespace Brqosh.Domain.Tax;

/// <summary>
/// (CEEF-229 · م1-١ب الشقّ الثاني) <b>سببُ الإعفاء — قائمةٌ نظاميةٌ مغلقة، لا نصٌّ حرّ.</b>
///
/// <para><b>القاعدة:</b> <c>BR-KSA-CL-06</c> من معيار الفوترة الإلكترونية: «إذا كان رمزُ فئة
/// الضريبة <c>Z</c> أو <c>E</c> أو <c>O</c>، فرمزُ سبب الإعفاء (BT-121) <b>يجب</b> أن يوجد
/// بإحدى القيم الواردة في الفقرة 11.2.4». ويؤكّدها <c>BR-KSA-23/24/25/26</c> على المسار
/// <c>cac:TaxTotal/cac:TaxSubtotal/cac:TaxCategory/cbc:TaxExemptionReasonCode</c>.</para>
///
/// <para><b>ولماذا يهمّ:</b> شاشةُ المبيعات تعرض الفئات الأربع منذ اليوم الأول، ولم يكن في
/// المستودع كلِّه ذكرٌ لـ<c>TaxExemption</c>. فمن باع سلعةً مصدَّرةً (فئة Z) أو خدمةً معفاة
/// (فئة E) أنتج مستنداً <b>ترفضه الهيئة</b> — وقد استهلك عدّاداً تسلسلياً وبنى بصمةَ ما بعده
/// عليه. الرفضُ عند الإصدار أرخص.</para>
///
/// <para>والقائمةُ منقولةٌ حرفياً عن جدول «Tax exemption (or exception) reason code and text —
/// specific to Saudi Arabia» في معيار ZATCA (الإصدار 1.2، ص 32). والنصُّ الإنجليزيّ هو ما
/// يُكتب في <c>cbc:TaxExemptionReason</c>؛ والعربيُّ للعرض في الشاشة وحدها.</para>
/// </summary>
public sealed record VatExemptionReason(
    string Code,
    VatCategory Category,
    string EnglishText,
    string ArabicText)
{
    /// <summary>الفئةُ <c>O</c> وحدَها سببُها نصٌّ حرّ يكتبه المكلَّف لكلّ حالة.</summary>
    public const string OutOfScopeCode = "VATEX-SA-OOS";

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-١٠) رمزُ إعفاء الإيجار السكنيّ — المادة 30 من اللائحة.
    ///
    /// <para><b>ويُسمّى ولا يُنسخ حرفيّاً حيث يُستعمل</b>: رمزٌ مكتوبٌ نصّاً في خدمةٍ
    /// يفترق يومَ يتغيّر، <b>والافتراقُ هنا لا يُرى</b> — تخرج الفاتورةُ برمزٍ لا تعرفه
    /// الهيئةُ فتُردّ، أو بلا رمزٍ فتُردّ.</para>
    /// </summary>
    public const string ResidentialRentCode = "VATEX-SA-30";

    private static readonly VatExemptionReason[] All =
    [
        // ‏E — معفى من الضريبة
        new("VATEX-SA-29", VatCategory.Exempt,
            "Financial services mentioned in Article 29 of the VAT Regulations",
            "الخدمات المالية المذكورة في المادة 29 من اللائحة التنفيذية"),
        new("VATEX-SA-29-7", VatCategory.Exempt,
            "Life insurance services mentioned in Article 29 of the VAT Regulations",
            "خدمات التأمين على الحياة المذكورة في المادة 29 من اللائحة التنفيذية"),
        new("VATEX-SA-30", VatCategory.Exempt,
            "Real estate transactions mentioned in Article 30 of the VAT Regulations",
            "التعاملات العقارية المذكورة في المادة 30 من اللائحة التنفيذية"),

        // ‏Z — خاضعٌ بنسبة صفر
        new("VATEX-SA-32", VatCategory.ZeroRated, "Export of goods", "تصدير السلع"),
        new("VATEX-SA-33", VatCategory.ZeroRated, "Export of services", "تصدير الخدمات"),
        new("VATEX-SA-34-1", VatCategory.ZeroRated,
            "The international transport of Goods", "النقل الدولي للسلع"),
        new("VATEX-SA-34-2", VatCategory.ZeroRated,
            "international transport of passengers", "النقل الدولي للركاب"),
        new("VATEX-SA-34-3", VatCategory.ZeroRated,
            "services directly connected and incidental to a Supply of international passenger transport",
            "الخدمات المرتبطة مباشرةً بتوريد النقل الدولي للركاب والتابعة له"),
        new("VATEX-SA-34-4", VatCategory.ZeroRated,
            "Supply of a qualifying means of transport", "توريد وسيلة نقل مؤهَّلة"),
        new("VATEX-SA-34-5", VatCategory.ZeroRated,
            "Any services relating to Goods or passenger transportation, as defined in article twenty five of these Regulations",
            "أي خدمات متعلقة بنقل السلع أو الركاب وفق المادة الخامسة والعشرين من اللائحة"),
        new("VATEX-SA-35", VatCategory.ZeroRated,
            "Medicines and medical equipment", "الأدوية والمعدات الطبية"),
        new("VATEX-SA-36", VatCategory.ZeroRated, "Qualifying metals", "المعادن المؤهَّلة"),
        new("VATEX-SA-EDU", VatCategory.ZeroRated,
            "Private education to citizen", "التعليم الخاص للمواطن"),
        new("VATEX-SA-HEA", VatCategory.ZeroRated,
            "Private healthcare to citizen", "الرعاية الصحية الخاصة للمواطن"),
        new("VATEX-SA-MLTRY", VatCategory.ZeroRated,
            "supply of qualified military goods", "توريد السلع العسكرية المؤهَّلة"),

        // ‏O — خارج نطاق الضريبة
        new(OutOfScopeCode, VatCategory.OutOfScope,
            "Reason is free text, to be provided by the taxpayer on case to case basis.",
            "سببٌ نصّيّ يكتبه المكلَّف لكلّ حالة على حدة"),
    ];

    /// <summary>هل تطلب هذه الفئةُ سببَ إعفاءٍ أصلاً؟ (<c>S</c> وحدها لا تطلب).</summary>
    public static bool IsRequiredFor(VatCategory category) => category != VatCategory.Standard;

    /// <summary>الأسبابُ المسموحة لفئةٍ بعينها — وهي ما تعرضه الشاشة.</summary>
    public static IReadOnlyList<VatExemptionReason> ForCategory(VatCategory category) =>
        All.Where(r => r.Category == category).ToList();

    /// <summary>الرمزُ إن كان معروفاً — وإلا <c>null</c>. ولا يُقبل رمزُ فئةٍ في فئةٍ أخرى.</summary>
    public static VatExemptionReason? Find(string? code) =>
        string.IsNullOrWhiteSpace(code)
            ? null
            : All.FirstOrDefault(r => string.Equals(r.Code, code.Trim(), StringComparison.Ordinal));

    /// <summary>القائمةُ كلُّها — للحرّاس التي تتحقّق من اكتمال ما يُعرض.</summary>
    public static IReadOnlyList<VatExemptionReason> Catalogue => All;
}
