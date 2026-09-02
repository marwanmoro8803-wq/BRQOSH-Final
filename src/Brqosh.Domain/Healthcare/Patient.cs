using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;

namespace Brqosh.Domain.Healthcare;

/// <summary>الجنس — يُطلب في الملفّ الطبيّ وفي مطالبات التأمين.</summary>
public enum PatientGender
{
    Male = 0,
    Female = 1,
}

/// <summary>
/// (CEEF-230 · ط3-٢) <b>مريضٌ — وملفُّه رقمٌ لا يتكرّر.</b>
///
/// <para><b>ورقمُ الملفّ الطبيّ فريدٌ في المنشأة</b>: ملفّان لشخصٍ واحد يُفرّقان تاريخَه
/// المرضيّ نصفين، <b>فيُصرف له دواءٌ يتعارض مع آخرَ في الملفّ الثاني</b>. وهو في العيادة
/// خطرٌ على البدن قبل أن يكون خطأً في الدفاتر.</para>
///
/// <para><b>والمريضُ عميلٌ في دفتر الذمم</b>: حصّتُه من الفاتورة ذمّةٌ عليه تُحصَّل وتُتابَع
/// كأيّ ذمّة. <b>وفصلُه عن دفتر العملاء يُنشئ دفتراً مساعداً ثانياً</b> لا يراه ميزانُ
/// المراجعة ولا تقريرُ الأعمار.</para>
/// </summary>
public class Patient : IBranchScoped
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }

    /// <summary>رقمُ الملفّ الطبيّ — فريدٌ في المنشأة.</summary>
    public string MedicalRecordNumber { get; private set; }

    public string NameAr { get; private set; }
    public string NationalId { get; private set; }
    public DateOnly DateOfBirth { get; private set; }
    public PatientGender Gender { get; private set; }
    public string Phone { get; private set; }

    /// <summary>ذمّةُ المريض — الرابطُ إلى دفتر العملاء.</summary>
    public long CustomerId { get; private set; }

    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAtUtc { get; private set; }

    // EF Core
    private Patient()
    {
        MedicalRecordNumber = null!;
        NameAr = null!;
        NationalId = null!;
        Phone = null!;
    }

    private Patient(long companyId, string mrn, string nameAr, string nationalId,
        DateOnly dateOfBirth, PatientGender gender, string phone, long customerId)
    {
        CompanyId = companyId;
        MedicalRecordNumber = mrn;
        NameAr = nameAr;
        NationalId = nationalId;
        DateOfBirth = dateOfBirth;
        Gender = gender;
        Phone = phone;
        CustomerId = customerId;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static Patient Create(long companyId, string medicalRecordNumber, string nameAr,
        string nationalId, DateOnly dateOfBirth, PatientGender gender, long customerId,
        string phone = "")
    {
        if (string.IsNullOrWhiteSpace(medicalRecordNumber))
            throw new AccountingException("رقم الملفّ الطبيّ مطلوب.");
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new AccountingException("اسم المريض مطلوب.");
        if (customerId <= 0)
            throw new AccountingException("لا مريضَ بلا ذمّةٍ في دفتر العملاء.");

        return new Patient(companyId, medicalRecordNumber.Trim(), nameAr.Trim(),
            (nationalId ?? "").Trim(), dateOfBirth, gender, (phone ?? "").Trim(), customerId);
    }

    /// <summary>العمرُ في تاريخٍ — ويُستعمل في قواعد الجرعة وفي شروط الوثائق.</summary>
    public int AgeOn(DateOnly date)
    {
        var age = date.Year - DateOfBirth.Year;
        if (date < DateOfBirth.AddYears(age)) age--;
        return age;
    }

    public void Deactivate() => IsActive = false;

    public void AssignBranch(long branchId)
    {
        if (BranchId is null) BranchId = branchId;
    }
}
