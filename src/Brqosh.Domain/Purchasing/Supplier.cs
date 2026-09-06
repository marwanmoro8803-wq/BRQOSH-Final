using Brqosh.Domain.Shared;
﻿namespace Brqosh.Domain.Purchasing;

/// <summary>مورد — طرف الذمم الدائنة؛ الرقم الضريبي لازم لخصم ضريبة المدخلات.</summary>
public class Supplier : ISoftDeletable
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
    private Supplier()
    {
        NameAr = null!;
        VatRegistrationNumber = null!;
        Phone = null!;
        Address = null!;
    }

    private Supplier(long companyId, string nameAr, string vatNumber, string phone, string address)
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

    public static Supplier Create(long companyId, string nameAr, string vatNumber = "", string phone = "", string address = "")
    {
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new ArgumentException("اسم المورد مطلوب.", nameof(nameAr));

        var vat = (vatNumber ?? string.Empty).Trim();
        if (vat.Length > 0 && (vat.Length != 15 || !vat.All(AsciiDigits.Is)))
            throw new ArgumentException("الرقم الضريبي للمورد يجب أن يتكون من 15 رقماً.", nameof(vatNumber));

        return new Supplier(companyId, nameAr.Trim(), vat, (phone ?? "").Trim(), (address ?? "").Trim());
    }

    /// <summary>خصم ضريبة المدخلات يتطلب فاتورة ضريبية من مورد مسجل.</summary>
    public bool IsVatRegistered => VatRegistrationNumber.Length == 15;

    public void Update(string nameAr, string vatNumber, string phone, string address, string? modifiedBy = null)
    {
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new ArgumentException("اسم المورد مطلوب.", nameof(nameAr));
        var vat = (vatNumber ?? string.Empty).Trim();
        if (vat.Length > 0 && (vat.Length != 15 || !vat.All(AsciiDigits.Is)))
            throw new ArgumentException("الرقم الضريبي للمورد يجب أن يتكون من 15 رقماً.", nameof(vatNumber));

        NameAr = nameAr.Trim();
        VatRegistrationNumber = vat;
        Phone = (phone ?? "").Trim();
        Address = (address ?? "").Trim();
        ModifiedAtUtc = DateTime.UtcNow;
        ModifiedBy = string.IsNullOrWhiteSpace(modifiedBy) ? null : modifiedBy.Trim();
    }

    /// <summary>
    /// إيقاف المورّد — <b>حالةٌ تشغيلية لا واقعةُ حذف</b>.
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

    /// <summary>إعادة تفعيل المورّد الموقوف.</summary>
    public void Reactivate() => IsActive = true;
}
