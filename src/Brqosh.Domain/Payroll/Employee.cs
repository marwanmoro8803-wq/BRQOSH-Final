using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;

namespace Brqosh.Domain.Payroll;

/// <summary>
/// جنسيةُ الموظّف — <b>وهي متغيّرٌ محاسبيٌّ لا حقلٌ تعريفيّ</b>: نسبةُ التأمينات تختلف بها
/// اختلافاً جوهرياً، فالسعوديُّ يُشترك في المعاش والساند والوافدُ في الأخطار المهنية وحدها.
/// </summary>
public enum EmployeeNationality
{
    Saudi = 0,
    NonSaudi = 1,
}

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١٣٨) <b>فئةُ المشترك في التأمينات الاجتماعيّة.</b>
///
/// <para>‏<see cref="NewLaw2024"/>: السعوديُّ المسجَّلُ أوّلَ مرّةٍ بعد ٣ يوليو ٢٠٢٤ بلا مددِ اشتراكٍ سابقة — نسبةُ معاشه ترتفع
/// بجدول <see cref="GosiPolicy"/>. و<see cref="Legacy"/>: كلُّ من سبق تسجيلُه، على النسب القديمة. <b>وتُختار صراحةً</b>: تاريخُ
/// الالتحاق لا يكفي، فأكثرُ من يلتحق بعد يوليو ٢٠٢٤ له مددٌ سابقة.</para>
/// </summary>
public enum GosiRegime
{
    Legacy = 0,
    NewLaw2024 = 1,
}

/// <summary>
/// (CEEF-230 · ط2-١) <b>سجلُّ الموظّف.</b>
///
/// <para><b>ما كان:</b> لا كيانَ موظّفٍ في النظام كلِّه — لا جدولَ ولا شاشة. والاسمُ
/// <b>نصٌّ حرٌّ يُعاد كتابتُه في كلّ شهر</b> داخل شبكة إدخال دورة الرواتب، بلا هويّةٍ ولا
/// إقامةٍ ولا آيبان ولا تاريخِ التحاق.</para>
///
/// <para><b>وأثرُ ذلك ليس ترفاً:</b> بلا سجلٍّ لا تُحسب تأميناتٌ (فالنسبةُ تتبع الجنسية)،
/// ولا يُصدَّر ملفُّ حماية الأجور (فهو يقوم على الهويّة والآيبان)، ولا تُحتسب مكافأةُ
/// نهاية الخدمة (فهي دالّةٌ في تاريخ الالتحاق). <b>وكلُّ ذلك كان صفراً بسبب حقلٍ نصّيّ.</b></para>
/// </summary>
public class Employee : IBranchScoped
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }

    public string Code { get; private set; }
    public string NameAr { get; private set; }

    /// <summary>
    /// رقمُ الهويّة أو الإقامة — <b>عشرةُ أرقام</b>، وهو مفتاحُ الموظّف لدى الجهات.
    ///
    /// <para><b>ولا يتكرّر في المنشأة</b>: موظّفان بالرقم نفسِه يعنيان راتباً مزدوجاً
    /// لشخصٍ واحد، أو ملفَّ أجورٍ ترفضه الجهةُ المستقبِلة.</para>
    /// </summary>
    public string NationalId { get; private set; }

    public EmployeeNationality Nationality { get; private set; }

    /// <summary>‏(ح-١٣٨) فئةُ الاشتراك — «قديم» افتراضاً، فلا يتغيّر راتبٌ قائمٌ صامتاً.</summary>
    public GosiRegime GosiRegime { get; private set; } = GosiRegime.Legacy;
    public string JobTitle { get; private set; }
    public DateOnly HireDate { get; private set; }
    public DateOnly? TerminationDate { get; private set; }

    /// <summary>الآيبان — يلزم ملفَّ حماية الأجور، ويجوز أن يغيب حتى يُفتح الحساب.</summary>
    public string? Iban { get; private set; }

    public decimal BasicSalary { get; private set; }
    public decimal HousingAllowance { get; private set; }
    public decimal TransportAllowance { get; private set; }
    public decimal OtherAllowance { get; private set; }

    public long? CostCenterId { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAtUtc { get; private set; }

    // EF Core
    private Employee()
    {
        Code = null!;
        NameAr = null!;
        NationalId = null!;
        JobTitle = null!;
    }

    private Employee(long companyId, string code, string nameAr, string nationalId,
        EmployeeNationality nationality, string jobTitle, DateOnly hireDate)
    {
        CompanyId = companyId;
        Code = code;
        NameAr = nameAr;
        NationalId = nationalId;
        Nationality = nationality;
        JobTitle = jobTitle;
        HireDate = hireDate;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static Employee Create(long companyId, string code, string nameAr, string nationalId,
        EmployeeNationality nationality, string jobTitle, DateOnly hireDate)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new AccountingException("كود الموظف مطلوب.");
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new AccountingException("اسم الموظف مطلوب.");
        if (string.IsNullOrWhiteSpace(jobTitle))
            throw new AccountingException("المسمّى الوظيفي مطلوب.");

        var id = (nationalId ?? "").Trim();
        if (id.Length != 10 || !id.All(char.IsAsciiDigit))
            throw new AccountingException(
                $"رقم الهوية أو الإقامة عشرةُ أرقام — ووردَ «{nationalId}».");

        return new Employee(companyId, code.Trim(), nameAr.Trim(), id, nationality,
            jobTitle.Trim(), hireDate);
    }

    /// <summary>
    /// يضبط عناصرَ الأجر.
    ///
    /// <para><b>ويُفصَل بدلُ السكن عن الأساسيّ عمداً</b>: الأجرُ الخاضع للتأمينات هو
    /// الأساسيُّ + السكن دون بقيّة البدلات — <b>فطيُّهما في رقمٍ واحدٍ يُخرج اشتراكاً
    /// خاطئاً</b> لا يمكن تصحيحُه بعد الترحيل.</para>
    /// </summary>
    /// <summary>‏(ح-١٣٨) فئةُ الاشتراك للسعوديّ وحدَه — وافدٌ «على النظام الجديد» لا معنى له.</summary>
    public void SetGosiRegime(GosiRegime regime)
    {
        if (regime != GosiRegime.Legacy && Nationality != EmployeeNationality.Saudi)
            throw new AccountingException("فئةُ نظام التأمينات الجديد للسعوديّ وحدَه.");
        GosiRegime = regime;
    }

    public void SetPay(decimal basic, decimal housing, decimal transport, decimal other = 0m)
    {
        if (basic < 0 || housing < 0 || transport < 0 || other < 0)
            throw new AccountingException("عناصر الأجر لا تكون سالبة.");

        BasicSalary = Math.Round(basic, 2, MidpointRounding.AwayFromZero);
        HousingAllowance = Math.Round(housing, 2, MidpointRounding.AwayFromZero);
        TransportAllowance = Math.Round(transport, 2, MidpointRounding.AwayFromZero);
        OtherAllowance = Math.Round(other, 2, MidpointRounding.AwayFromZero);
    }

    public void SetIban(string? iban)
    {
        var value = (iban ?? "").Trim().Replace(" ", "").ToUpperInvariant();
        if (value.Length == 0)
        {
            Iban = null;
            return;
        }

        // ‏آيبانٌ سعوديّ: SA ثمّ اثنان وعشرون رقماً.
        if (!value.StartsWith("SA", StringComparison.Ordinal) || value.Length != 24
            || !value[2..].All(char.IsAsciiDigit))
            throw new AccountingException($"الآيبان السعودي «SA» و22 رقماً — ووردَ «{iban}».");

        Iban = value;
    }

    /// <summary>
    /// يُصحّح الاسمَ والمسمّى الوظيفيّ.
    ///
    /// <para><b>ولا يمسّ الهويّةَ ولا تاريخَ التعيين</b>: الأوّلُ مفتاحُ الموظّف لدى
    /// التأمينات والبنك، والثاني أساسُ حساب مكافأة نهاية الخدمة. <b>وتعديلُهما بعد
    /// أوّل ترحيلٍ يُعيد كتابة تاريخٍ مضى</b> — ويُغيّر التزاماً حُسب وقُيّد.</para>
    ///
    /// <para><b>والترقيةُ تغيّر المسمّى لا الأجرَ</b> — والأجرُ في <see cref="SetPay"/>،
    /// فالفصلُ يجعل كلَّ تغييرٍ مقصوداً.</para>
    /// </summary>
    public void Rename(string nameAr, string jobTitle)
    {
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new AccountingException("اسم الموظف مطلوب.");
        if (string.IsNullOrWhiteSpace(jobTitle))
            throw new AccountingException("المسمّى الوظيفي مطلوب.");

        NameAr = nameAr.Trim();
        JobTitle = jobTitle.Trim();
    }

    public void AssignCostCenter(long? costCenterId) => CostCenterId = costCenterId;

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-١٤٦ · <c>sector-modules#9</c>) <b>المشروعُ الذي يعمل عليه الموظّف</b> — فتُحمَّل عليه
    /// تكلفتُه في دورة الرواتب (الإجماليُّ بعد الغياب وحصّةُ صاحب العمل في التأمينات) بدل مصروف الرواتب.
    /// </summary>
    public long? ProjectId { get; private set; }

    public void AssignProject(long? projectId)
    {
        if (projectId is <= 0)
            throw new AccountingException("المشروع غير صالح.");
        ProjectId = projectId;
    }

    /// <summary>
    /// ينهي خدمةَ الموظّف.
    ///
    /// <para><b>ولا يُحذف</b>: راتبُه المرحَّل جزءٌ من دفاترِ سنواتٍ مضت، ومكافأةُ نهاية
    /// خدمته تُحسب من تاريخَي التحاقه وانتهائه. <b>فالحذفُ يمحو أثراً نظامياً.</b></para>
    /// </summary>
    public void Terminate(DateOnly date)
    {
        if (date < HireDate)
            throw new AccountingException("تاريخ انتهاء الخدمة قبل تاريخ الالتحاق.");

        TerminationDate = date;
        IsActive = false;
    }

    public void Reinstate()
    {
        TerminationDate = null;
        IsActive = true;
    }

    /// <summary>على رأس العمل في تاريخٍ بعينه — وهو ما يُبنى عليه سِجلُّ الرواتب.</summary>
    public bool IsEmployedOn(DateOnly date) =>
        HireDate <= date && (TerminationDate is null || TerminationDate >= date);

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٨٦) <b>على رأس العمل يوماً واحداً على الأقلّ بين التاريخين</b> — فيُدرَج في دورة شهره ويُصرف له
    /// ما عمل؛ لا «يومَ الصرف» وحدَه، فمغادرٌ في الخامس عشر يعمل نصفَ الشهر ولا يكون على رأس العمل يومَ الصرف.
    /// </summary>
    public bool IsEmployedDuring(DateOnly from, DateOnly to) =>
        HireDate <= to && (TerminationDate is null || TerminationDate >= from);

    /// <summary>
    /// (ح-١١٧) <b>ما خدمه من الفترة</b> — من أبعدِ بدايتها وتعيينه إلى أقربِ نهايتها وانتهاءِ خدمته.
    /// وهو متمّمُ <see cref="DaysNotEmployedIn"/>: ما يقع خارجه يخصمه ذاك، فلا يُخصم مرّةً ثانيةً إجازةً.
    /// </summary>
    public (DateOnly From, DateOnly To) ServedWithin(DateOnly from, DateOnly to) =>
        (HireDate > from ? HireDate : from,
         TerminationDate is DateOnly end && end < to ? end : to);

    /// <summary>(ح-٨٦) أيّامُ الفترة التي لم يكن فيها على رأس العمل — قبل التعيين وبعد انتهاء الخدمة.</summary>
    public int DaysNotEmployedIn(DateOnly from, DateOnly to)
    {
        var before = HireDate > from ? Math.Min(HireDate.DayNumber, to.DayNumber + 1) - from.DayNumber : 0;
        var after = TerminationDate is DateOnly end && end < to
            ? to.DayNumber - Math.Max(end.DayNumber, from.DayNumber - 1)
            : 0;
        return Math.Max(0, before) + Math.Max(0, after);
    }

    /// <summary>إجماليُّ الأجر الشهريّ.</summary>
    public decimal GrossPay => BasicSalary + HousingAllowance + TransportAllowance + OtherAllowance;

    /// <summary>
    /// سنواتُ الخدمة حتى تاريخٍ — <b>كسريّةً لا صحيحة</b>، فمكافأةُ نهاية الخدمة تُحسب
    /// بالشهر واليوم لا بالسنة الكاملة.
    /// </summary>
    public decimal ServiceYearsAt(DateOnly date) =>
        Math.Round((date.DayNumber - HireDate.DayNumber) / 365.25m, 4, MidpointRounding.AwayFromZero);

    public void AssignBranch(long branchId)
    {
        if (BranchId is null) BranchId = branchId;
    }
}
