using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;

namespace Brqosh.Domain.Payroll;

/// <summary>حالةُ طلب الإجازة.</summary>
public enum LeaveStatus
{
    /// <summary>مُقدَّمٌ ولم يُبتّ فيه — <b>ولا يُخصم من رصيدٍ ولا يمسّ راتباً</b>.</summary>
    Pending = 0,

    /// <summary>مُعتمَد — <b>وهو وحدَه ما يُحتسب</b>.</summary>
    Approved = 1,

    Rejected = 2,
    Cancelled = 3,
}

/// <summary>
/// <b>طلبُ إجازةٍ أو غياب — والمستندُ الذي يُبنى منه كلُّ شيء.</b>
///
/// <para><b>واصطلاحُ هذا المستودع أن يُشتقّ لا يُخزَّن</b> (‏<c>ط1-٤</c>‏): الرصيدُ
/// والمُستهلَكُ والخصمُ كلُّها تُحسب من هذه المستندات المعتمدة، <b>لا من عدّادٍ على
/// الموظّف</b>. <b>وعدّادٌ ينحرف عند أوّل إلغاءٍ أو تعديل</b>، والاشتقاقُ لا ينحرف.</para>
///
/// <para><b>والمعتمدُ وحدَه يُحتسب</b>: طلبٌ معلَّقٌ لا يُنقص رصيداً ولا يخصم أجراً —
/// <b>وحسابُه يمنع الموظّفَ من إجازةٍ لم تُقرّ بعد</b>، ويخصم من راتبه ما قد يُرفض.</para>
/// </summary>
public class LeaveRequest : IBranchScoped
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }

    public long EmployeeId { get; private set; }

    /// <summary>رقمُ الطلب — لا يتكرّر في المنشأة.</summary>
    public string RequestNumber { get; private set; }

    public LeaveType Type { get; private set; }

    public DateOnly FromDate { get; private set; }
    public DateOnly ToDate { get; private set; }

    public LeaveStatus Status { get; private set; } = LeaveStatus.Pending;

    public string? Reason { get; private set; }
    public DateOnly? DecidedOn { get; private set; }
    public string? DecidedBy { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>
    /// <b>عددُ الأيّام — والطرفان محسوبان.</b>
    /// </summary>
    /// <remarks>
    /// <b>فإجازةُ يومٍ واحدٍ من الأحد إلى الأحد يومٌ لا صفر</b>. والفرقُ المجرّد بين
    /// التاريخين يُسقط يوماً من كلّ إجازة — <b>وهو يومُ أجرٍ في كلّ طلبٍ في المنشأة</b>.
    /// </remarks>
    public int Days => ToDate.DayNumber - FromDate.DayNumber + 1;

    /// <summary>هل يُحتسب هذا الطلبُ فعلاً؟ — <b>المعتمدُ وحدَه</b>.</summary>
    public bool Counts => Status == LeaveStatus.Approved;

    // EF Core
    private LeaveRequest() => RequestNumber = null!;

    private LeaveRequest(long companyId, long employeeId, string requestNumber,
        LeaveType type, DateOnly fromDate, DateOnly toDate, string? reason)
    {
        CompanyId = companyId;
        EmployeeId = employeeId;
        RequestNumber = requestNumber;
        Type = type;
        FromDate = fromDate;
        ToDate = toDate;
        Reason = reason;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static LeaveRequest Create(long companyId, long employeeId, string requestNumber,
        LeaveType type, DateOnly fromDate, DateOnly toDate, string? reason = null)
    {
        if (employeeId <= 0)
            throw new AccountingException("الموظف مطلوب للإجازة.");
        if (string.IsNullOrWhiteSpace(requestNumber))
            throw new AccountingException("رقم طلب الإجازة مطلوب.");

        // ‏**والمغادرةُ لا تسبق الوصول** — ونطاقٌ مقلوبٌ يُنتج أيّاماً سالبةً تُضاف إلى
        // ‏الرصيد بدل أن تُنقصه، **فيكسب الموظّفُ إجازةً بطلبها**.
        if (toDate < fromDate)
            throw new AccountingException("تاريخ نهاية الإجازة لا يسبق بدايتها.");

        return new LeaveRequest(companyId, employeeId, requestNumber.Trim(),
            type, fromDate, toDate,
            string.IsNullOrWhiteSpace(reason) ? null : reason.Trim());
    }

    /// <summary>هل تتداخل مع مدّةٍ أخرى؟ — <b>والطرفان محسوبان في الجهتين</b>.</summary>
    /// <remarks>
    /// <b>وموظّفٌ في إجازتين متداخلتين يُخصم مرّتين عن يومٍ واحد</b>، أو يُستهلك رصيدُه
    /// ضِعفَ ما غاب. <b>والتداخلُ يُفحص بالتواريخ لا بالحالة</b>.
    /// </remarks>
    public bool OverlapsWith(DateOnly otherFrom, DateOnly otherTo) =>
        FromDate <= otherTo && otherFrom <= ToDate;

    public void Approve(DateOnly on, string by)
    {
        EnsurePending("اعتماد");
        Status = LeaveStatus.Approved;
        DecidedOn = on;
        DecidedBy = by;
    }

    public void Reject(DateOnly on, string by)
    {
        EnsurePending("رفض");
        Status = LeaveStatus.Rejected;
        DecidedOn = on;
        DecidedBy = by;
    }

    /// <summary>
    /// يُلغى الطلب — <b>ويُقبل بعد الاعتماد كذلك</b>.
    /// </summary>
    /// <remarks>
    /// <b>وإجازةٌ اعتُمدت ثمّ لم تُؤخذ يجب أن تُلغى</b> وإلّا استهلكت رصيداً لم يُستعمل،
    /// <b>ونقص بدلُ نهاية الخدمة بأيّامٍ قضاها الموظّفُ في عمله</b>.
    /// </remarks>
    public void Cancel()
    {
        if (Status is LeaveStatus.Rejected or LeaveStatus.Cancelled)
            throw new AccountingException("الطلب ملغى أو مرفوض مسبقاً.");

        Status = LeaveStatus.Cancelled;
    }

    private void EnsurePending(string operation)
    {
        if (Status != LeaveStatus.Pending)
            throw new AccountingException($"لا يُقبل {operation} إلا لطلبٍ معلَّق.");
    }

    public void AssignBranch(long branchId)
    {
        if (BranchId is null) BranchId = branchId;
    }
}
