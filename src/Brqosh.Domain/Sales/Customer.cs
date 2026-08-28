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
