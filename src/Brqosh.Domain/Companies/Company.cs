using Brqosh.Domain.Modules;

namespace Brqosh.Domain.Companies;

/// <summary>المنشأة — الكيان الأعلى في النظام؛ كل البيانات معزولة بـ CompanyId (multi-company).</summary>
public class Company
{
    public long Id { get; private set; }
    public string NameAr { get; private set; }

    /// <summary>الرقم الضريبي (15 خانة) — مطلوب لتفعيل ZATCA.</summary>
    public string VatRegistrationNumber { get; private set; }

    public BusinessProfile Profile { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    // EF Core
    private Company()
    {
        NameAr = null!;
        VatRegistrationNumber = null!;
    }

    private Company(string nameAr, string vatRegistrationNumber, BusinessProfile profile)
    {
        NameAr = nameAr;
        VatRegistrationNumber = vatRegistrationNumber;
        Profile = profile;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static Company Create(string nameAr, string vatRegistrationNumber, BusinessProfile profile)
    {
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new ArgumentException("اسم المنشأة مطلوب.", nameof(nameAr));

        var vat = (vatRegistrationNumber ?? string.Empty).Trim();
        if (vat.Length > 0 && (vat.Length != 15 || !vat.All(char.IsDigit)))
            throw new ArgumentException("الرقم الضريبي يجب أن يتكون من 15 رقماً.", nameof(vatRegistrationNumber));

        return new Company(nameAr.Trim(), vat, profile);
    }
}
