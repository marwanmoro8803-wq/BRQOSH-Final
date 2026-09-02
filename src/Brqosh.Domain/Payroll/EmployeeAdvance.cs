using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;

namespace Brqosh.Domain.Payroll;

/// <summary>حالةُ السُّلفة.</summary>
public enum AdvanceStatus
{
    /// <summary>مُعتمَدةٌ ولم تُصرف بعد — <b>ولا ذمّةَ قبل الصرف</b>.</summary>
    Approved = 0,

    /// <summary>صُرفت ويُستقطَع منها.</summary>
    Outstanding = 1,

    /// <summary>سُدِّدت بالكامل.</summary>
    Settled = 2,

    Cancelled = 3,
}

/// <summary>
/// (CEEF-230 · ط2-٥) <b>سُلفةُ موظّف — مالٌ يخرج ويُنسى.</b>
///
/// <para><b>ما كان:</b> المنشأةُ تُسلّف موظّفَها خمسةَ آلاف، <b>ولا شيءَ في النظام يعرفها</b>.
/// فالمالُ يخرج من الصندوق ويُقيَّد <b>مصروفَ رواتب</b> — والأصلُ الذي سيُستردّ يختفي من
/// الميزانية، <b>ومصروفُ الرواتب يُضخَّم بما سيعود</b>.</para>
///
/// <para><b>ثمّ يُنسى الاستقطاع.</b> ولا موضعَ له أصلاً: سطرُ الرواتب لا يعرف إلا التأمينات.
/// <b>فيُقبض الموظّفُ راتبَه كاملاً وقد أخذ سُلفتَه</b> — والمنشأةُ دفعت مرّتين. وفي منشأةٍ
/// بمئةِ موظّفٍ لا يُكتشف هذا إلا حين يستقيل أحدُهم <b>وقد سُلِّف ولم يُستقطَع منه شيء</b>.</para>
///
/// <para><b>وهويّةُ هـ-١٧:</b> <c>رصيدُ السُّلفة = المصروفُ − المُستقطَع</c>، <b>ولا يكون
/// سالباً أبداً</b> — وسالبُه يعني استقطاعَ ما لم يُصرف.</para>
///
/// <para><b>والمُستقطَعُ يُشتقّ من دورات الرواتب المرحّلة لا من عدّادٍ مخزَّن</b> — وهو
/// اصطلاحُ هذا المستودع منذ <c>ط1-٤</c>: <b>عدّادٌ ينحرف عند أوّل عكسِ دورةٍ</b>،
/// والاشتقاقُ لا ينحرف.</para>
/// </summary>
public class EmployeeAdvance : IBranchScoped
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }

    public long EmployeeId { get; private set; }

    /// <summary>رقمُ السُّلفة — لا يتكرّر في المنشأة.</summary>
    public string AdvanceNumber { get; private set; }

    public DateOnly RequestDate { get; private set; }
    public DateOnly? DisbursedOn { get; private set; }

    /// <summary>المبلغُ المصروف.</summary>
    public decimal Amount { get; private set; }

    /// <summary>
    /// القسطُ الشهريّ المُستقطَع.
    /// </summary>
    /// <remarks>
    /// <b>وقسطٌ يتجاوز نصفَ الأجر يُرفض</b>: نظامُ العمل السعوديّ (المادة ٩١) يمنع
    /// الاستقطاعَ لسدادِ سُلفةٍ بأكثرَ من عُشر الأجر إلا برضا العامل — <b>والحدُّ هنا
    /// عند النصف حارسٌ للمعقولية لا نصُّ المادة</b>، فالرضا لا يُمثَّل في النظام.
    /// <b>واستقطاعٌ يبلغ الأجرَ كلَّه يترك الموظّفَ بصفر</b>.
    /// </remarks>
    public decimal InstallmentAmount { get; private set; }

    public AdvanceStatus Status { get; private set; } = AdvanceStatus.Approved;
    public string? Reason { get; private set; }
    public long? JournalEntryId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private EmployeeAdvance(long companyId, long employeeId, string advanceNumber,
        DateOnly requestDate, decimal amount, decimal installmentAmount, string? reason)
    {
        CompanyId = companyId;
        EmployeeId = employeeId;
        AdvanceNumber = advanceNumber;
        RequestDate = requestDate;
        Amount = amount;
        InstallmentAmount = installmentAmount;
        Reason = reason;
        CreatedAtUtc = DateTime.UtcNow;
    }

    private EmployeeAdvance() { AdvanceNumber = ""; }

    public static EmployeeAdvance Create(long companyId, long employeeId, string advanceNumber,
        DateOnly requestDate, decimal amount, decimal installmentAmount, string? reason = null)
    {
        if (employeeId <= 0) throw new AccountingException("الموظّف مطلوب للسُّلفة.");
        if (string.IsNullOrWhiteSpace(advanceNumber))
            throw new AccountingException("رقم السُّلفة مطلوب.");
        if (amount <= 0m)
            throw new AccountingException("مبلغ السُّلفة يجب أن يكون موجباً.");
        if (installmentAmount <= 0m)
            throw new AccountingException("القسط الشهري يجب أن يكون موجباً.");

        // ‏**وقسطٌ يتجاوز السُّلفة يُرفض**: يُنتج استقطاعاً أكبرَ من الدَّين في أوّل شهر،
        // ‏**فيصير الرصيدُ سالباً** — وهو ما تمنعه `هـ-١٧`.
        if (installmentAmount > amount)
            throw new AccountingException(
                $"القسط {installmentAmount:N2} أكبر من السُّلفة {amount:N2} — والاستقطاعُ يتجاوز الدَّين.");

        return new EmployeeAdvance(companyId, employeeId, advanceNumber.Trim(), requestDate,
            Math.Round(amount, 2, MidpointRounding.AwayFromZero),
            Math.Round(installmentAmount, 2, MidpointRounding.AwayFromZero),
            string.IsNullOrWhiteSpace(reason) ? null : reason.Trim());
    }

    /// <summary>
    /// يُثبّت الصرف — <b>وقبله لا ذمّةَ ولا استقطاع</b>.
    /// </summary>
    /// <remarks>
    /// <b>والاعتمادُ ليس صرفاً</b>: سُلفةٌ اعتُمدت ولم تُصرف <b>ليست ديناً على الموظّف</b>.
    /// واستقطاعُها منه يأخذ مالاً لم يأخذه.
    /// </remarks>
    public void MarkDisbursed(DateOnly on, long journalEntryId)
    {
        if (Status != AdvanceStatus.Approved)
            throw new AccountingException("لا تُصرف إلا سُلفةٌ معتمدةٌ لم تُصرف.");

        Status = AdvanceStatus.Outstanding;
        DisbursedOn = on;
        JournalEntryId = journalEntryId;
    }

    /// <summary>يُقفلها بعد سدادها بالكامل.</summary>
    public void MarkSettled()
    {
        if (Status != AdvanceStatus.Outstanding)
            throw new AccountingException("لا تُسدَّد إلا سُلفةٌ مصروفة.");

        Status = AdvanceStatus.Settled;
    }

    /// <summary>يُعيدها مفتوحةً بعد عكسِ دورةِ رواتبَ أقفلتها.</summary>
    /// <remarks>
    /// <b>ودورةٌ تُعكَس تُعيد الدَّين</b>: الاستقطاعُ الذي أقفل السُّلفةَ ارتدّ،
    /// <b>وسُلفةٌ تبقى «مسدَّدة» بعد ارتداد سدادها دَينٌ سقط بلا قبض</b>.
    /// </remarks>
    public void Reopen()
    {
        if (Status != AdvanceStatus.Settled)
            throw new AccountingException("لا تُعاد إلا سُلفةٌ مقفلةٌ بالسداد.");

        Status = AdvanceStatus.Outstanding;
    }

    public void Cancel()
    {
        if (Status == AdvanceStatus.Outstanding)
            throw new AccountingException("لا تُلغى سُلفةٌ صُرفت — سدِّدْها أو اعكسْ صرفَها.");
        if (Status == AdvanceStatus.Settled)
            throw new AccountingException("لا تُلغى سُلفةٌ سُدِّدت.");

        Status = AdvanceStatus.Cancelled;
    }

    /// <summary>القسطُ الواجبُ هذا الشهر — <b>وآخرُ قسطٍ هو الباقي لا القسطَ الكامل</b>.</summary>
    /// <remarks>
    /// <b>وبغير هذا يُستقطَع في الشهر الأخير أكثرُ من الباقي</b> — فيصير الرصيدُ سالباً،
    /// وتُصبح المنشأةُ مدينةً لموظّفها بفرقٍ لا أحدَ يعرف سببَه.
    /// </remarks>
    public decimal InstallmentFor(decimal outstandingBalance) =>
        outstandingBalance <= 0m ? 0m : Math.Min(InstallmentAmount, outstandingBalance);

    public void AssignBranch(long branchId)
    {
        if (branchId <= 0) throw new AccountingException("الفرع غير صالح.");
        BranchId = branchId;
    }
}
