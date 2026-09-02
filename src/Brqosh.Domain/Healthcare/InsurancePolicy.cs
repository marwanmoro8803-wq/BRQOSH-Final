using Brqosh.Domain.Accounting;

namespace Brqosh.Domain.Healthcare;

/// <summary>
/// (CEEF-230 · ط3-٢) <b>شركةُ تأمينٍ — وهي عميلٌ في دفتر الذمم قبل أن تكون جهةً.</b>
///
/// <para><b>وهذا هو البند كلُّه</b>: ثمانون بالمئة من فاتورة العيادة ذمّةٌ على شركة التأمين
/// لا على المريض. <b>وقيدُها على المريض يُخرج أعمارَ الديون كلَّها خطأً</b> — ويُطالَب
/// مريضٌ بما لا يلزمه، وتبقى الشركةُ خارج المتابعة حتّى يسقط حقُّ المطالبة بالتقادم.</para>
/// </summary>
public class InsurancePayer
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }

    public string Code { get; private set; }
    public string NameAr { get; private set; }

    /// <summary>ذمّةُ الشركة — والرابطُ إلى دفتر العملاء نفسِه الذي يرى المريض.</summary>
    public long CustomerId { get; private set; }

    /// <summary>مهلةُ السداد بالأيام — وهي في الواقع ستّون يوماً أو أكثر.</summary>
    public int PaymentTermDays { get; private set; } = 60;

    public bool IsActive { get; private set; } = true;

    // EF Core
    private InsurancePayer()
    {
        Code = null!;
        NameAr = null!;
    }

    private InsurancePayer(long companyId, string code, string nameAr, long customerId, int termDays)
    {
        CompanyId = companyId;
        Code = code;
        NameAr = nameAr;
        CustomerId = customerId;
        PaymentTermDays = termDays;
    }

    public static InsurancePayer Create(long companyId, string code, string nameAr,
        long customerId, int paymentTermDays = 60)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new AccountingException("كود شركة التأمين مطلوب.");
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new AccountingException("اسم شركة التأمين مطلوب.");
        if (customerId <= 0)
            throw new AccountingException("لا شركةَ تأمينٍ بلا ذمّةٍ في دفتر العملاء.");
        if (paymentTermDays < 0)
            throw new AccountingException("مهلة السداد لا تكون سالبة.");

        return new InsurancePayer(companyId, code.Trim(), nameAr.Trim(), customerId, paymentTermDays);
    }

    public void Deactivate() => IsActive = false;
}

/// <summary>
/// (CEEF-230 · ط3-٢) <b>وثيقةُ تأمينٍ تربط مريضاً بشركة.</b>
///
/// <para><b>وسقفُها سنةُ وثيقةٍ لا سنةً ميلادية</b>: وثيقةٌ تبدأ في يوليو يُعاد سقفُها في
/// يوليو. <b>وحسابُه بالسنة الميلادية يُصفّره في يناير</b> — فتُطالَب الشركةُ ستّةَ أشهرٍ
/// بما جاوز سقفَها، وتُرفض المطالباتُ جملةً بعد أن صُرفت الخدمات.</para>
/// </summary>
public class InsurancePolicy
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long PatientId { get; private set; }
    public long PayerId { get; private set; }

    public string PolicyNumber { get; private set; }

    /// <summary>رقمُ العضوية في البطاقة — وهو ما تطلبه الشركةُ في المطالبة.</summary>
    public string MemberNumber { get; private set; }

    /// <summary>نسبةُ تحمّل الشركة بعد التحمّل الثابت.</summary>
    public decimal CoveragePercent { get; private set; }

    /// <summary>تحمّلُ المريض الثابت للزيارة — يُخصم قبل النسبة.</summary>
    public decimal CopayFixed { get; private set; }

    /// <summary>سقفُ سنة الوثيقة — و<c>null</c> بلا سقف.</summary>
    public decimal? AnnualLimit { get; private set; }

    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public bool IsActive { get; private set; } = true;

    // EF Core
    private InsurancePolicy()
    {
        PolicyNumber = null!;
        MemberNumber = null!;
    }

    private InsurancePolicy(long companyId, long patientId, long payerId, string policyNumber,
        string memberNumber, decimal coveragePercent, decimal copayFixed, decimal? annualLimit,
        DateOnly startDate, DateOnly endDate)
    {
        CompanyId = companyId;
        PatientId = patientId;
        PayerId = payerId;
        PolicyNumber = policyNumber;
        MemberNumber = memberNumber;
        CoveragePercent = coveragePercent;
        CopayFixed = copayFixed;
        AnnualLimit = annualLimit;
        StartDate = startDate;
        EndDate = endDate;
    }

    public static InsurancePolicy Create(long companyId, long patientId, long payerId,
        string policyNumber, string memberNumber, decimal coveragePercent, DateOnly startDate,
        DateOnly endDate, decimal copayFixed = 0m, decimal? annualLimit = null)
    {
        if (patientId <= 0) throw new AccountingException("المريض مطلوب للوثيقة.");
        if (payerId <= 0) throw new AccountingException("شركة التأمين مطلوبة للوثيقة.");
        if (string.IsNullOrWhiteSpace(policyNumber))
            throw new AccountingException("رقم الوثيقة مطلوب.");
        if (coveragePercent < 0m || coveragePercent > 100m)
            throw new AccountingException("نسبة التغطية بين صفر ومئة.");
        if (copayFixed < 0m)
            throw new AccountingException("التحمّل لا يكون سالباً.");
        if (annualLimit is decimal limit && limit <= 0m)
            throw new AccountingException("سقف الوثيقة إمّا موجبٌ أو بلا سقف.");
        if (endDate <= startDate)
            throw new AccountingException("تاريخ نهاية الوثيقة يجب أن يكون بعد بدايتها.");

        return new InsurancePolicy(companyId, patientId, payerId, policyNumber.Trim(),
            (memberNumber ?? "").Trim(),
            Math.Round(coveragePercent, 2, MidpointRounding.AwayFromZero),
            Math.Round(copayFixed, 2, MidpointRounding.AwayFromZero),
            annualLimit is decimal l ? Math.Round(l, 2, MidpointRounding.AwayFromZero) : null,
            startDate, endDate);
    }

    /// <summary>
    /// سارية في تاريخ.
    ///
    /// <para><b>والطرفان داخلان</b>: وثيقةٌ تنتهي في ٣١ ديسمبر تُغطّي زيارةَ ذلك اليوم.</para>
    /// </summary>
    public bool IsValidOn(DateOnly date) =>
        IsActive && StartDate <= date && date <= EndDate;

    /// <summary>
    /// بدايةُ سنة الوثيقة التي يقع فيها التاريخ.
    ///
    /// <para><b>وهذه هي الدالّة التي يُخطئها الجميع</b>: السقفُ سقفُ <b>سنة وثيقة</b>،
    /// فوثيقةٌ تبدأ في ١ يوليو 2025 تُعيد سقفَها في ١ يوليو 2026 لا في ١ يناير. <b>وحسابُه
    /// ميلادياً يمنح المريضَ سقفاً كاملاً في منتصف سنة الوثيقة</b> — وتُصرف خدماتٌ ثمّ
    /// تُرفض مطالباتُها جملةً.</para>
    /// </summary>
    public DateOnly PolicyYearStart(DateOnly on)
    {
        if (on < StartDate) return StartDate;

        var years = on.Year - StartDate.Year;
        var anniversary = StartDate.AddYears(years);
        if (anniversary > on) anniversary = StartDate.AddYears(years - 1);
        return anniversary;
    }

    public void Deactivate() => IsActive = false;
}
