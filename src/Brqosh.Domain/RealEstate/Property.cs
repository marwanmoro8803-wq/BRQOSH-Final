using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;

namespace Brqosh.Domain.RealEstate;

/// <summary>نوعُ العقار — ويحكم ما يُتوقَّع من وحداته.</summary>
public enum PropertyKind
{
    Building = 0,
    Compound = 1,
    Villa = 2,
    Land = 3,
    Commercial = 4,
}

/// <summary>حالةُ الوحدة — <b>وهي ما يمنع تأجيرَ المؤجَّر</b>.</summary>
public enum UnitStatus
{
    Vacant = 0,
    Leased = 1,
    Maintenance = 2,
}

/// <summary>
/// (CEEF-230 · ط3-١) <b>عقارٌ — والوحداتُ تحته.</b>
///
/// <para><b>ما كان:</b> ملفُّ «العقارات» أحدُ ثمانية ملفّاتِ عملٍ تُعرض في شاشة الإنشاء،
/// <b>وليس في النظام كيانٌ واحدٌ يخصّه</b>: لا عقارَ ولا وحدةَ ولا عقدَ إيجار. فالمنشأةُ
/// العقارية تحصل على دفترِ أستاذٍ عامّ لا أكثر — <b>وتُدير عقاراتها في جدولٍ خارجيّ</b>.</para>
///
/// <para>ومسبارُ القطاعات كان يُظهرها خضراءَ لأنّها <b>تبيع وتُحصّل وتُقرّ</b>، وقد قيل
/// حينها صراحةً إنّ <b>خضرتَه إشارةٌ ضعيفة</b>: يقيس الدورةَ العامّة ولا يستطيع قياسَ
/// ميزةٍ لا وجودَ لها. <b>وهذا البندُ هو ما يُحوّل تلك الخضرةَ إلى معنى.</b></para>
/// </summary>
public class Property : IBranchScoped
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }

    public string Code { get; private set; }
    public string NameAr { get; private set; }
    public PropertyKind Kind { get; private set; }
    public string City { get; private set; }
    public string Address { get; private set; }

    /// <summary>رقمُ الصكّ — يُميّز العقارَ لدى الجهات، ويجوز أن يغيب للمستأجَر من الغير.</summary>
    public string? DeedNumber { get; private set; }

    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAtUtc { get; private set; }

    // EF Core
    private Property()
    {
        Code = null!;
        NameAr = null!;
        City = null!;
        Address = null!;
    }

    private Property(long companyId, string code, string nameAr, PropertyKind kind,
        string city, string address)
    {
        CompanyId = companyId;
        Code = code;
        NameAr = nameAr;
        Kind = kind;
        City = city;
        Address = address;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static Property Create(long companyId, string code, string nameAr, PropertyKind kind,
        string city, string address = "")
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new AccountingException("كود العقار مطلوب.");
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new AccountingException("اسم العقار مطلوب.");
        if (string.IsNullOrWhiteSpace(city))
            throw new AccountingException("المدينة مطلوبة.");

        return new Property(companyId, code.Trim(), nameAr.Trim(), kind, city.Trim(),
            (address ?? "").Trim());
    }

    public void SetDeed(string? deedNumber) =>
        DeedNumber = string.IsNullOrWhiteSpace(deedNumber) ? null : deedNumber.Trim();

    public void Deactivate() => IsActive = false;
    public void Reactivate() => IsActive = true;

    public void AssignBranch(long branchId)
    {
        if (BranchId is null) BranchId = branchId;
    }
}

/// <summary>
/// (CEEF-230 · ط3-١) <b>وحدةٌ مؤجَّرة — شقّةٌ أو محلٌّ أو مكتب.</b>
///
/// <para><b>والحالةُ ليست وصفاً بل قيداً</b>: وحدةٌ مؤجَّرةٌ لا تُؤجَّر ثانيةً في المدّة
/// نفسِها. <b>وتأجيرُ المؤجَّر أخطرُ عطلٍ في هذا القطاع</b> — يُنتج عقدين على شيءٍ واحد،
/// ونزاعاً لا يحسمه النظام.</para>
/// </summary>
public class PropertyUnit
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long PropertyId { get; private set; }

    public string UnitNumber { get; private set; }
    public decimal AreaSquareMeters { get; private set; }
    public int Bedrooms { get; private set; }
    public UnitStatus Status { get; private set; } = UnitStatus.Vacant;

    /// <summary>الإيجارُ السنويُّ المُعلَن — وهو مرجعٌ للتسعير لا قيمةَ عقدٍ قائم.</summary>
    public decimal ListedAnnualRent { get; private set; }

    // EF Core
    private PropertyUnit() => UnitNumber = null!;

    private PropertyUnit(long companyId, long propertyId, string unitNumber,
        decimal area, int bedrooms, decimal listedAnnualRent)
    {
        CompanyId = companyId;
        PropertyId = propertyId;
        UnitNumber = unitNumber;
        AreaSquareMeters = area;
        Bedrooms = bedrooms;
        ListedAnnualRent = listedAnnualRent;
    }

    public static PropertyUnit Create(long companyId, long propertyId, string unitNumber,
        decimal areaSquareMeters = 0m, int bedrooms = 0, decimal listedAnnualRent = 0m)
    {
        if (propertyId <= 0)
            throw new AccountingException("العقار مطلوب للوحدة.");
        if (string.IsNullOrWhiteSpace(unitNumber))
            throw new AccountingException("رقم الوحدة مطلوب.");
        if (areaSquareMeters < 0m || listedAnnualRent < 0m || bedrooms < 0)
            throw new AccountingException("بيانات الوحدة لا تكون سالبة.");

        return new PropertyUnit(companyId, propertyId, unitNumber.Trim(),
            Math.Round(areaSquareMeters, 2, MidpointRounding.AwayFromZero), bedrooms,
            Math.Round(listedAnnualRent, 2, MidpointRounding.AwayFromZero));
    }

    // ‏(CEEF-230 · ط3-١) **وحالةُ الوحدة لا تُضبط إلا من عقد.**
    //
    // ‏كُتبت `internal` أوّلاً فلم تعبر حدودَ التجميعة، **فكادت تصير `public`** — أي حالةً
    // ‏يضبطها أيُّ مستدعٍ. والصوابُ أن يقودها العقدُ نفسُه: `LeaseContract.Activate` يأخذ
    // ‏الوحدةَ ويشغلها، فلا يُشغَّل شيءٌ بلا عقدٍ يُبرّره. **والحاجزُ حينها في النطاق
    // ‏لا في مُعدِّل الوصول.**
    internal void OccupyByLease() => Status = UnitStatus.Leased;
    internal void ReleaseFromLease() => Status = UnitStatus.Vacant;

    /// <summary>
    /// يُدخل الوحدةَ الصيانة.
    ///
    /// <para><b>ولا يُقبل على وحدةٍ مؤجَّرة</b>: المستأجرُ فيها، وإدخالُها الصيانةَ يُخفيها
    /// عن قوائم المؤجَّر <b>فيبدو العقارُ شاغراً وهو مسكون</b>.</para>
    /// </summary>
    public void EnterMaintenance()
    {
        if (Status == UnitStatus.Leased)
            throw new AccountingException("الوحدة مؤجَّرة — أنهِ العقدَ قبل إدخالها الصيانة.");
        Status = UnitStatus.Maintenance;
    }

    public void LeaveMaintenance()
    {
        if (Status == UnitStatus.Maintenance) Status = UnitStatus.Vacant;
    }
}
