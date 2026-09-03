namespace Brqosh.Domain.Sales;

/// <summary>عميل — طرف الذمم المدينة؛ الرقم الضريبي مطلوب للفاتورة الضريبية (B2B).</summary>
public class Customer : ISoftDeletable
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public string NameAr { get; private set; }
    public string VatRegistrationNumber { get; private set; }
    public string Phone { get; private set; }
    public string Address { get; private set; }

    /// <summary>
    /// (CEEF-229 · م1-١ب) الاسم المسجَّل للمشتري — <c>cbc:RegistrationName</c> في الفاتورة القياسية.
    ///
    /// <para>طرفُ البائع أخذ هذه الحقول في Z-10، وطرفُ المشتري بقي اسماً ورقماً ضريبياً وحدهما.
    /// والفاتورةُ القياسية تذهب إلى <b>المقاصّة قبل التسليم</b> وطرفُ مشتريها معرَّف.</para>
    /// </summary>
    public string? RegistrationName { get; private set; }

    /// <summary>العنوان الوطنيّ للمشتري — كائنُ قيمةٍ مسطَّح في الجدول نفسه، كما في المنشأة.</summary>
    public Companies.NationalAddress NationalAddress { get; private set; } = Companies.NationalAddress.Empty;

    public bool IsActive { get; private set; } = true;
    public DateTime? DeletedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public string? ModifiedBy { get; private set; }

    // EF Core
    private Customer()
    {
        NameAr = null!;
        VatRegistrationNumber = null!;
        Phone = null!;
        Address = null!;
    }

    private Customer(long companyId, string nameAr, string vatNumber, string phone, string address)
    {
        CompanyId = companyId;
        NameAr = nameAr;
        VatRegistrationNumber = vatNumber;
        Phone = phone;
        Address = address;
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// (CEEF-230 · ط4-٣ج) <b>مهلةُ السداد بالأيام — وبها وحدَها يصير التنبّؤ تنبّؤاً.</b>
    ///
    /// <para><b>وبغيرها لا يعرف النظامُ متى يُتوقَّع المال</b>، فيُعرض كلُّ مفتوحٍ
    /// «مستحقّاً الآن» — <b>وذاك ليس تنبّؤاً بل صورةً للحاضر</b>.</para>
    ///
    /// <para><b>وصفرُها يعني نقداً</b>: الاستحقاقُ يومَ الفاتورة.</para>
    /// </summary>
    public int PaymentTermDays { get; private set; }

    /// <summary>يضبط مهلةَ السداد.</summary>
    public void SetPaymentTerms(int days)
    {
        if (days < 0)
            throw new Domain.Accounting.AccountingException("مهلة السداد لا تكون سالبة.");
        if (days > 365)
            throw new Domain.Accounting.AccountingException(
                "مهلة السداد تتجاوز سنة — وذاك ليس بيعاً آجلاً بل قرضاً.");

        PaymentTermDays = days;
    }

    public static Customer Create(long companyId, string nameAr, string vatNumber = "", string phone = "", string address = "")
    {
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new ArgumentException("اسم العميل مطلوب.", nameof(nameAr));

        var vat = (vatNumber ?? string.Empty).Trim();
        if (vat.Length > 0 && (vat.Length != 15 || !vat.All(char.IsDigit)))
            throw new ArgumentException("الرقم الضريبي للعميل يجب أن يتكون من 15 رقماً.", nameof(vatNumber));

        return new Customer(companyId, nameAr.Trim(), vat, (phone ?? "").Trim(), (address ?? "").Trim());
    }

    /// <summary>فاتورة ضريبية (B2B) تتطلب رقماً ضريبياً؛ بدونه تكون الفاتورة مبسطة (B2C).</summary>
    public bool SupportsStandardTaxInvoice => VatRegistrationNumber.Length == 15;

    /// <summary>
    /// (CEEF-229 · م1-١ب) ما ينقص هذا العميل لتصحّ له فاتورةٌ <b>قياسية</b> — رسائل عربية تُعرض كما هي.
    ///
    /// <para>وتُقاس على من يحمل رقماً ضريبياً وحده: من لا رقمَ له تُصدَر له فاتورةٌ مبسّطة لا
    /// تحتاج عنواناً — فالكاشيرُ لا يسأل الزبونَ عن حيّه.</para>
    /// </summary>
    public IReadOnlyList<string> StandardInvoiceGaps()
    {
        if (!SupportsStandardTaxInvoice)
            return [];

        var gaps = new List<string>();
        if (string.IsNullOrWhiteSpace(RegistrationName))
            gaps.Add("الاسم المسجَّل للعميل مطلوب للفاتورة القياسية.");
        gaps.AddRange(NationalAddress.Problems);
        return gaps;
    }

    /// <summary>تحديث هوية المشتري النظامية — الاسم المسجَّل والعنوان الوطنيّ.</summary>
    public void UpdateRegulatoryIdentity(string? registrationName, Companies.NationalAddress address)
    {
        RegistrationName = string.IsNullOrWhiteSpace(registrationName) ? null : registrationName.Trim();
        NationalAddress = address;
    }

    public void Update(string nameAr, string vatNumber, string phone, string address, string? modifiedBy = null)
    {
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new ArgumentException("اسم العميل مطلوب.", nameof(nameAr));
        var vat = (vatNumber ?? string.Empty).Trim();
        if (vat.Length > 0 && (vat.Length != 15 || !vat.All(char.IsDigit)))
            throw new ArgumentException("الرقم الضريبي للعميل يجب أن يتكون من 15 رقماً.", nameof(vatNumber));

        NameAr = nameAr.Trim();
        VatRegistrationNumber = vat;
        Phone = (phone ?? "").Trim();
        Address = (address ?? "").Trim();
        ModifiedAtUtc = DateTime.UtcNow;
        ModifiedBy = string.IsNullOrWhiteSpace(modifiedBy) ? null : modifiedBy.Trim();
    }

    /// <summary>
    /// إيقاف العميل — <b>حالةٌ تشغيلية لا واقعةُ حذف</b>.
    ///
    /// <para>(CEEF-229 · م2-١٧) كان يضبط <c>DeletedAtUtc</c> أيضاً، ومرشّح الاستعلام العامّ
    /// يُخفي كلَّ ما له تاريخ حذف — <b>فيختفي من قوائمه تماماً</b> لا يظهر موقوفاً. وزرُّ
    /// الشاشة مكتوبٌ عليه «إيقاف» ورسالةُ التأكيد تسأل «إيقاف …؟»، فكان الوعد إيقافاً
    /// والفعلُ حذفاً. ولا رجعة: لا <c>Reactivate</c> في الكيان أصلاً.</para>
    ///
    /// <para>أُصلح هذا في <see cref="Brqosh.Domain.Accounting.Account"/> ضمن م0-١٣، وبقيت
    /// سبعةُ كياناتٍ أخرى بالجسم القديم حرفاً بحرف — فالإصلاح طُبِّق على كيانٍ واحد ولم يُعمَّم.</para>
    /// </summary>
    public void Deactivate() => IsActive = false;

    /// <summary>إعادة تفعيل العميل الموقوف.</summary>
    public void Reactivate() => IsActive = true;
}
