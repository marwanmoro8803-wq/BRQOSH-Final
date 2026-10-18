namespace Brqosh.Domain.Tax;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٦٥ · <c>tax-zatca#12</c>) <b>خاناتُ نموذج إقرار ضريبة القيمة المضافة — كما في بوّابة الهيئة.</b>
///
/// <para><b>ما كان:</b> الإقرارُ يُفكَّك بالفئة (S · Z · E · O) ولا يُسنَد إلى خانات النموذج — «نصُّه ليس في المستودع،
/// والإسنادُ بالتخمين أسوأ من عدمه». والمصادرُ الثانويّةُ مختلفةٌ فعلاً في الخانة 2 (مبيعاتٌ خليجيّةٌ عند بعضها).</para>
///
/// <para><b>والمصدرُ الآن نصُّ الهيئة نفسُه:</b> دليلُ خدمة «تقديم إقرار ضريبة القيمة المضافة»
/// (<c>zatca.gov.sa/en/eServices/Documents/eServices_Manual_009.pdf</c>) — لقطاتُ النموذج في البوّابة بأرقامها
/// وعناوينها، قُرئت في 2026-10-18. ويؤكّد دليلُ الاستيراد والتصدير (الإصدار الثاني، مايو 2026) الخانتين 8 و9:
/// «VAT shall be included in Field 9 of the VAT return instead of Field 8»، والاحتسابُ العكسيُّ في الخانة 9.</para>
///
/// <para><b>والإسنادُ من رمز سبب الإعفاء</b> (BT-121) — قائمةٌ نظاميّةٌ مغلقةٌ على كلّ سطرٍ غيرِ أساسيّ:
/// تصديرُ السلع والخدمات (<c>VATEX-SA-32/33</c>) خانةُ الصادرات، والتعليمُ والرعايةُ الصحّيّةُ الخاصّةُ للمواطن
/// (<c>VATEX-SA-EDU/HEA</c>) خانةُ «المبيعات التي تتحمّل الدولةُ ضريبتها»، وما بقي من الصفريّ محلّيٌّ.
/// وخارجُ النطاق (<c>O</c>) لا خانةَ له — لا يُفصَح عنه في الإقرار.</para>
///
/// <para><b>وحدُّه المعلَن:</b> الخانتان 8 (استيرادٌ دُفعت ضريبتُه في الجمارك) و9 (الاحتسابُ العكسيّ) لا يبلغهما
/// مستندٌ في المنتج بعد — فاتورةُ الشراء لا تعرف الاستيرادَ ولا المورّدَ غيرَ المقيم؛ فهما صفرٌ معلَنٌ لا مُخمَّن.</para>
/// </summary>
public static class VatReturnBox
{
    public const int StandardRatedSales = 1;
    public const int GovernmentBearsVatSales = 2;
    public const int ZeroRatedDomesticSales = 3;
    public const int Exports = 4;
    public const int ExemptSales = 5;
    public const int TotalSales = 6;
    public const int StandardRatedDomesticPurchases = 7;
    public const int ImportsPaidAtCustoms = 8;
    public const int ImportsUnderReverseCharge = 9;
    public const int ZeroRatedPurchases = 10;
    public const int ExemptPurchases = 11;
    public const int TotalPurchases = 12;

    /// <summary>خاناتُ المبيعات التي تُملأ — بترتيب النموذج، بلا خانة الإجماليّ.</summary>
    public static readonly IReadOnlyList<int> SalesBoxes =
        [StandardRatedSales, GovernmentBearsVatSales, ZeroRatedDomesticSales, Exports, ExemptSales];

    /// <summary>خاناتُ المشتريات التي تُملأ — بترتيب النموذج، بلا خانة الإجماليّ.</summary>
    public static readonly IReadOnlyList<int> PurchaseBoxes =
        [StandardRatedDomesticPurchases, ImportsPaidAtCustoms, ImportsUnderReverseCharge, ZeroRatedPurchases, ExemptPurchases];

    /// <summary>
    /// خانةُ سطر مبيعاتٍ — أو <c>null</c> لخارج النطاق (لا يُفصَح عنه).
    /// </summary>
    public static int? ForSale(VatCategory category, string? exemptionReasonCode) => category switch
    {
        VatCategory.Standard => StandardRatedSales,
        VatCategory.Exempt => ExemptSales,
        VatCategory.ZeroRated => exemptionReasonCode?.Trim() switch
        {
            "VATEX-SA-32" or "VATEX-SA-33" => Exports,
            "VATEX-SA-EDU" or "VATEX-SA-HEA" => GovernmentBearsVatSales,
            _ => ZeroRatedDomesticSales,
        },
        _ => null,
    };

    /// <summary>
    /// خانةُ سطر مشترياتٍ محلّيّة — أو <c>null</c> لخارج النطاق. (الاستيرادُ والاحتسابُ العكسيُّ لا يبلغان هنا بعد.)
    /// </summary>
    public static int? ForPurchase(VatCategory category) => category switch
    {
        VatCategory.Standard => StandardRatedDomesticPurchases,
        VatCategory.ZeroRated => ZeroRatedPurchases,
        VatCategory.Exempt => ExemptPurchases,
        _ => null,
    };

    /// <summary>عنوانُ الخانة بالإنجليزيّة حرفاً كما في نموذج البوّابة.</summary>
    public static string EnglishTitle(int box) => box switch
    {
        StandardRatedSales => "Standard Rated Sales",
        GovernmentBearsVatSales => "Sales on which the government bears the VAT",
        ZeroRatedDomesticSales => "Zero Rated Domestic Sales",
        Exports => "Exports",
        ExemptSales => "Exempt Sales",
        TotalSales => "Total Sales",
        StandardRatedDomesticPurchases => "Standard Rated Domestic Purchases",
        ImportsPaidAtCustoms => "Imports subject to VAT paid on import",
        ImportsUnderReverseCharge => "Imports Subject to VAT accounted for through the reverse charge mechanism",
        ZeroRatedPurchases => "Zero Rated Purchases",
        ExemptPurchases => "Exempt Purchases",
        TotalPurchases => "Total Purchases",
        _ => throw new ArgumentOutOfRangeException(nameof(box), box, "لا خانةَ بهذا الرقم في النموذج."),
    };
}
