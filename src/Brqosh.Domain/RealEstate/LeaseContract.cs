using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Sales;

namespace Brqosh.Domain.RealEstate;

/// <summary>حالةُ العقد.</summary>
public enum LeaseStatus
{
    Draft = 0,
    Active = 1,
    Terminated = 2,
    Expired = 3,
}

/// <summary>
/// (CEEF-230 · ط3-١) <b>عقدُ إيجار.</b>
///
/// <para><b>وتفعيلُه يُنشئ خطةَ فوترةٍ دورية</b> — من <c>ط1-٥</c>. <b>وهذا هو الربطُ الذي
/// يجعل القطاعَ عاملاً لا مجرَّدَ جداول</b>: العقدُ يُوقَّع مرّةً، والفواتيرُ تُولَّد
/// شهراً بشهر بقيدِ تفرّدٍ يمنع فوترةَ الشهر مرّتين.</para>
///
/// <para><b>والقيدُ الحاكم: لا عقدان على وحدةٍ في مدّةٍ متداخلة.</b> فتأجيرُ المؤجَّر
/// أخطرُ عطلٍ في هذا القطاع — <b>عقدان على شيءٍ واحد، ونزاعٌ لا يحسمه النظام</b>. ويُفحص
/// <b>بالتداخل الزمنيّ</b> لا بحالة الوحدة وحدَها: عقدٌ ينتهي في ديسمبر لا يمنع عقداً
/// يبدأ في يناير، <b>وحالةُ الوحدة لا تعرف التواريخ</b>.</para>
/// </summary>
public class LeaseContract : IBranchScoped
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }

    public long PropertyUnitId { get; private set; }

    /// <summary>المستأجرُ — وهو عميلٌ في دفتر الذمم، فالإيجارُ إيرادٌ يُفوتَر.</summary>
    public long TenantCustomerId { get; private set; }

    public string ContractNumber { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }

    /// <summary>الإيجارُ لكلّ دورةٍ — لا السنويّ: العقدُ قد يكون ربعَ سنويٍّ أو شهرياً.</summary>
    public decimal RentPerPeriod { get; private set; }

    public RecurrenceFrequency Frequency { get; private set; }

    /// <summary>
    /// التأمينُ المدفوع — <b>وهو أمانةٌ لا إيراد</b>.
    ///
    /// <para>فيُردّ عند الإخلاء ما لم يُخصم منه. <b>وقيدُه إيراداً يُضخّم الربحَ بمالٍ
    /// ليس للمنشأة</b> — وهو من أشيع أخطاء المحاسبة العقارية.</para>
    /// </summary>
    public decimal SecurityDeposit { get; private set; }

    public LeaseStatus Status { get; private set; } = LeaseStatus.Draft;

    /// <summary>خطةُ الفوترة المولَّدة عند التفعيل — والرابطُ إلى <c>ط1-٥</c>.</summary>
    public long? RecurringPlanId { get; private set; }

    /// <summary>يومُ قبض التأمين — وغيابُه يعني أنّه لم يُقبض، لا أنّه صفر.</summary>
    public DateOnly? DepositReceivedOn { get; private set; }

    /// <summary>يومُ ردّه.</summary>
    public DateOnly? DepositReturnedOn { get; private set; }

    /// <summary>ما خُصم منه عند الردّ — <b>وهذا وحده يصير إيراداً</b>.</summary>
    public decimal DepositForfeited { get; private set; }

    public DateOnly? TerminatedOn { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    // EF Core
    private LeaseContract() => ContractNumber = null!;

    private LeaseContract(long companyId, long unitId, long tenantId, string contractNumber,
        DateOnly startDate, DateOnly endDate, decimal rentPerPeriod, RecurrenceFrequency frequency,
        decimal securityDeposit)
    {
        CompanyId = companyId;
        PropertyUnitId = unitId;
        TenantCustomerId = tenantId;
        ContractNumber = contractNumber;
        StartDate = startDate;
        EndDate = endDate;
        RentPerPeriod = rentPerPeriod;
        Frequency = frequency;
        SecurityDeposit = securityDeposit;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static LeaseContract Create(long companyId, long propertyUnitId, long tenantCustomerId,
        string contractNumber, DateOnly startDate, DateOnly endDate, decimal rentPerPeriod,
        RecurrenceFrequency frequency = RecurrenceFrequency.Monthly, decimal securityDeposit = 0m)
    {
        if (propertyUnitId <= 0) throw new AccountingException("الوحدة مطلوبة للعقد.");
        if (tenantCustomerId <= 0) throw new AccountingException("المستأجر مطلوب للعقد.");
        if (string.IsNullOrWhiteSpace(contractNumber))
            throw new AccountingException("رقم العقد مطلوب.");
        if (endDate <= startDate)
            throw new AccountingException("تاريخ نهاية العقد يجب أن يكون بعد بدايته.");
        if (rentPerPeriod <= 0m)
            throw new AccountingException("قيمة الإيجار يجب أن تكون موجبة.");
        if (securityDeposit < 0m)
            throw new AccountingException("التأمين لا يكون سالباً.");

        return new LeaseContract(companyId, propertyUnitId, tenantCustomerId, contractNumber.Trim(),
            startDate, endDate, Math.Round(rentPerPeriod, 2, MidpointRounding.AwayFromZero),
            frequency, Math.Round(securityDeposit, 2, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// مدّتان متداخلتان.
    ///
    /// <para><b>والطرفان محسوبان</b>: <c>EndDate</c> يومٌ <b>داخلٌ</b> في العقد — المستأجرُ
    /// يسكن فيه. فعقدٌ يبدأ يومَ انتهاء سابقه <b>تداخلٌ ليلةً واحدة</b>: مستأجران في شقّةٍ
    /// واحدةٍ في يومٍ واحد. <b>وحصرُ المقارنة بـ«أصغرَ من» يُمرّره</b> — والفارقُ يومٌ
    /// يُنشئ نزاعاً كاملاً.</para>
    /// </summary>
    public bool OverlapsWith(DateOnly otherStart, DateOnly otherEnd) =>
        StartDate <= otherEnd && otherStart <= EndDate;

    /// <summary>
    /// يُفعّل العقدَ ويربطه بخطة الفوترة.
    ///
    /// <para><b>ولا يُفعَّل إلا مسودّة</b>: تفعيلُ عقدٍ مفعَّلٍ يُنشئ خطةَ فوترةٍ ثانية —
    /// <b>وفاتورتين لشهرٍ واحد</b>.</para>
    /// </summary>
    public void Activate(long recurringPlanId, PropertyUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        if (Status != LeaseStatus.Draft)
            throw new AccountingException("لا يُفعَّل إلا عقدٌ مسودّة.");
        if (recurringPlanId <= 0)
            throw new AccountingException("خطة الفوترة مطلوبة لتفعيل العقد.");
        if (unit.Id != PropertyUnitId)
            throw new AccountingException("الوحدة المُمرَّرة ليست وحدةَ هذا العقد.");

        RecurringPlanId = recurringPlanId;
        Status = LeaseStatus.Active;

        // ‏**والعقدُ هو ما يشغل الوحدة** — فلا حالةَ إشغالٍ بلا عقدٍ يُبرّرها.
        unit.OccupyByLease();
    }

    /// <summary>
    /// يُنهي العقدَ قبل مدّته.
    ///
    /// <para><b>ولا يُنهى قبل بدايته</b> — ذلك إلغاءٌ لا إنهاء، ويترك خطةَ فوترةٍ ولّدت
    /// فواتيرَ لعقدٍ «لم يبدأ».</para>
    /// </summary>
    public void Terminate(DateOnly on, PropertyUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        if (Status != LeaseStatus.Active)
            throw new AccountingException("لا يُنهى إلا عقدٌ مفعَّل.");
        if (on < StartDate)
            throw new AccountingException("تاريخ الإنهاء قبل بداية العقد.");
        if (unit.Id != PropertyUnitId)
            throw new AccountingException("الوحدة المُمرَّرة ليست وحدةَ هذا العقد.");

        TerminatedOn = on;
        Status = LeaseStatus.Terminated;
        unit.ReleaseFromLease();
    }

    /// <summary>
    /// يُثبت قبضَ التأمين.
    ///
    /// <para><b>ولا يُقبض مرّتين</b>: قبضٌ ثانٍ يُضاعف الأمانةَ في الدفاتر، <b>فتُردّ
    /// للمستأجر مرّةً وتبقى الأخرى التزاماً أبديّاً</b> لا يُغلقه شيء.</para>
    /// </summary>
    public void RecordDepositReceived(DateOnly on)
    {
        if (SecurityDeposit <= 0m)
            throw new AccountingException("لا تأمينَ على هذا العقد ليُقبض.");
        if (DepositReceivedOn is not null)
            throw new AccountingException("التأمين مقبوضٌ سلفاً — ولا يُقبض مرّتين.");

        DepositReceivedOn = on;
    }

    /// <summary>
    /// يُثبت ردَّ التأمين، وما خُصم منه.
    /// </summary>
    /// <param name="forfeited">
    /// المخصومُ لقاء تلفٍ أو متأخّرات — <b>وهو وحده الذي يصير إيراداً</b>، لأنّ الالتزامَ
    /// بردّه سقط. <b>والباقي يخرج نقداً</b>.
    /// </param>
    public void RecordDepositReturned(DateOnly on, decimal forfeited)
    {
        if (DepositReceivedOn is null)
            throw new AccountingException("لا يُردّ تأمينٌ لم يُقبض.");
        if (DepositReturnedOn is not null)
            throw new AccountingException("التأمين مردودٌ سلفاً.");
        if (on < DepositReceivedOn)
            throw new AccountingException("تاريخ الردّ قبل تاريخ القبض.");
        if (forfeited < 0m)
            throw new AccountingException("المخصوم من التأمين لا يكون سالباً.");
        if (forfeited > SecurityDeposit)
            throw new AccountingException(
                "المخصوم أكبر من التأمين — والزائدُ مطالبةٌ على المستأجر لا خصمٌ من أمانة.");

        DepositReturnedOn = on;
        DepositForfeited = Math.Round(forfeited, 2, MidpointRounding.AwayFromZero);
    }

    public void MarkExpired()
    {
        if (Status == LeaseStatus.Active) Status = LeaseStatus.Expired;
    }

    /// <summary>ساري المفعول في تاريخ — ويُستعمل لمعرفة إشغال الوحدة.</summary>
    public bool IsActiveOn(DateOnly date) =>
        Status == LeaseStatus.Active && StartDate <= date && date <= EndDate
        && (TerminatedOn is null || date <= TerminatedOn);

    public void AssignBranch(long branchId)
    {
        if (BranchId is null) BranchId = branchId;
    }
}
