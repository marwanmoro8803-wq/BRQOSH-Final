using Brqosh.Domain.Accounting;
using Brqosh.Domain.Shared;

namespace Brqosh.Domain.Companies;

/// <summary>
/// (CEEF-229 · Z-10) العنوان الوطنيّ السعوديّ — بحقوله المفكَّكة كما تفرضها الفاتورة الإلكترونية.
///
/// <para><b>لماذا كائن قيمة لا حقول متناثرة:</b> العنوان في <c>Company</c> كان حقلاً نصّياً حرّاً
/// واحداً (<c>AddressAr</c>) يُطبع على الترويسة. ومعيار الهيئة يفرض <c>cac:PostalAddress</c>
/// بحقولٍ منفصلة — رقم مبنى ورقم إضافيّ وشارع وحيّ ومدينة ورمز بريديّ ومنطقة ودولة — فلا سبيل
/// إلى توليدها من نصٍّ حرّ مهما بلغت البراعة. أي أن العطل <b>بنيويّ في نموذج البيانات</b> لا في
/// المولّد، وهو أطول ما في عنقود ZATCA إصلاحاً.</para>
///
/// <para>وجمعُها في كائنٍ واحد يجعل «مكتمل أو غير مكتمل» سؤالاً <b>يُجاب عنه في مكان واحد</b>
/// (<see cref="IsComplete"/>)، بدل سبعة فحوصٍ متفرّقة في الواجهة والمولّد والمطبوعة.</para>
///
/// <para><b>حدُّ تحقّق مُصرَّح به:</b> رقم المبنى أربع خانات والرمز البريديّ خمس — وهذا هو
/// الشائع في العنوان الوطنيّ السعوديّ. <b>لم أتحقّق من النصّ النظاميّ</b>، فالتحقق تنبيهيّ لا
/// مانع: قيمةٌ بطول مختلف تُقبل ويُبلَّغ عنها في <see cref="Problems"/>، ولا تُمنع من الحفظ.
/// يُراجَع نصّاً قبل الإرسال الحقيقيّ (م4-٤).</para>
/// </summary>
public record NationalAddress(
    string BuildingNumber,
    string StreetName,
    string DistrictName,
    string CityName,
    string PostalZone,
    string CountrySubentity,
    string AdditionalNumber = "",
    string CountryCode = "SA")
{
    /// <summary>
    /// عنوانٌ فارغ — <b>نسخةٌ جديدة في كل استدعاء، وهذا مقصود</b>.
    ///
    /// <para>كانت هذه القيمة نسخةً ساكنة واحدة (<c>{ get; } = new(...)</c>) تُسنَد افتراضياً إلى
    /// كل <c>Company</c>. وبما أن العنوان كائنُ قيمةٍ <b>مملوك</b> في EF، فإن كائناً واحداً
    /// مشتركاً بين مالكَين يجعل المتتبِّع يظنّ صفَّ المنشأة الثانية هو صفَّ الأولى، فيحاول تغيير
    /// <c>CompanyId</c> وهو جزء من المفتاح — فيسقط إنشاء أيّ منشأة ثانية بأكمله.</para>
    ///
    /// <para>العطل لا يظهر مع منشأةٍ واحدة، ولذلك أمسكته اختبارات المنشآت المتعدّدة وحدها
    /// (التوحيد والتهيئة). والتساوي هنا بنيويّ لا مرجعيّ — فـ<c>x == NationalAddress.Empty</c>
    /// يظلّ صحيحاً رغم اختلاف المرجع.</para>
    /// </summary>
    public static NationalAddress Empty => new("", "", "", "", "", "");

    /// <summary>الحقول التي لا تقوم الفاتورة الإلكترونية بدونها.</summary>
    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(BuildingNumber)
        && !string.IsNullOrWhiteSpace(StreetName)
        && !string.IsNullOrWhiteSpace(DistrictName)
        && !string.IsNullOrWhiteSpace(CityName)
        && !string.IsNullOrWhiteSpace(PostalZone)
        && !string.IsNullOrWhiteSpace(CountrySubentity)
        && !string.IsNullOrWhiteSpace(CountryCode);

    /// <summary>
    /// ما ينقص العنوان أو يبدو غير معتاد — رسائل عربية تُعرض للمستخدم كما هي.
    /// فارغةٌ تعني عنواناً صالحاً للفوترة الإلكترونية.
    /// </summary>
    public IReadOnlyList<string> Problems
    {
        get
        {
            var problems = new List<string>();

            void Require(string value, string label)
            {
                if (string.IsNullOrWhiteSpace(value))
                    problems.Add($"{label} مطلوب للفاتورة الإلكترونية.");
            }

            Require(BuildingNumber, "رقم المبنى");
            Require(StreetName, "اسم الشارع");
            Require(DistrictName, "الحي");
            Require(CityName, "المدينة");
            Require(PostalZone, "الرمز البريدي");
            Require(CountrySubentity, "المنطقة");
            Require(CountryCode, "رمز الدولة");

            // تنبيهات لا موانع — انظر حدّ التحقق في وصف النوع.
            if (BuildingNumber.Length > 0 && (BuildingNumber.Length != 4 || !BuildingNumber.All(AsciiDigits.Is)))
                problems.Add("رقم المبنى يُتوقَّع أن يكون أربع خانات رقمية.");
            if (PostalZone.Length > 0 && (PostalZone.Length != 5 || !PostalZone.All(AsciiDigits.Is)))
                problems.Add("الرمز البريدي يُتوقَّع أن يكون خمس خانات رقمية.");

            return problems;
        }
    }

    /// <summary>سطرٌ واحد للطباعة على الترويسة — يتخطّى الحقول الفارغة.</summary>
    public string ToPrintLine()
    {
        var parts = new[]
        {
            string.IsNullOrWhiteSpace(BuildingNumber) ? "" : $"مبنى {BuildingNumber}",
            StreetName,
            DistrictName,
            CityName,
            string.IsNullOrWhiteSpace(PostalZone) ? "" : $"ر.ب {PostalZone}",
            CountrySubentity,
        };

        return string.Join(" · ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    public static NationalAddress Create(
        string? buildingNumber, string? streetName, string? districtName, string? cityName,
        string? postalZone, string? countrySubentity, string? additionalNumber = null,
        string? countryCode = "SA")
    {
        var code = (countryCode ?? "SA").Trim().ToUpperInvariant();
        if (code.Length is not (0 or 2))
            throw new AccountingException("رمز الدولة يجب أن يكون حرفين (ISO 3166-1).");

        return new NationalAddress(
            (buildingNumber ?? "").Trim(),
            (streetName ?? "").Trim(),
            (districtName ?? "").Trim(),
            (cityName ?? "").Trim(),
            (postalZone ?? "").Trim(),
            (countrySubentity ?? "").Trim(),
            (additionalNumber ?? "").Trim(),
            code.Length == 0 ? "SA" : code);
    }
}
