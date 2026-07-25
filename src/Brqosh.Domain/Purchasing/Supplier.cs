namespace Brqosh.Domain.Purchasing;

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

    public static Supplier Create(long companyId, string nameAr, string vatNumber = "", string phone = "", string address = "")
    {
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new ArgumentException("اسم المورد مطلوب.", nameof(nameAr));

        var vat = (vatNumber ?? string.Empty).Trim();
        if (vat.Length > 0 && (vat.Length != 15 || !vat.All(char.IsDigit)))
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
        if (vat.Length > 0 && (vat.Length != 15 || !vat.All(char.IsDigit)))
            throw new ArgumentException("الرقم الضريبي للمورد يجب أن يتكون من 15 رقماً.", nameof(vatNumber));

        NameAr = nameAr.Trim();
        VatRegistrationNumber = vat;
        Phone = (phone ?? "").Trim();
        Address = (address ?? "").Trim();
        ModifiedAtUtc = DateTime.UtcNow;
        ModifiedBy = string.IsNullOrWhiteSpace(modifiedBy) ? null : modifiedBy.Trim();
    }

    public void Deactivate()
    {
        if (DeletedAtUtc is not null) return;
        DeletedAtUtc = DateTime.UtcNow;
        IsActive = false;
    }
}
