using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;

namespace Brqosh.Domain.Healthcare;

/// <summary>حالةُ المطالبة.</summary>
public enum ClaimStatus
{
    Draft = 0,
    Submitted = 1,
    Adjudicated = 2,
    Closed = 3,
}

/// <summary>
/// مصيرُ المرفوض — <b>وهو قرارٌ بشريٌّ لا يُشتقّ من الرفض نفسِه</b>.
/// </summary>
public enum RejectionDisposition
{
    /// <summary>لم يُقرَّر بعد — <b>والمطالبةُ لا تُقفل عليه</b>.</summary>
    Undecided = 0,

    /// <summary>يُعاد تحميلُه على المريض: الشركةُ لم تكن مسؤولةً عنه أصلاً.</summary>
    RebillPatient = 1,

    /// <summary>يُشطب: خطأُ المزوّد — ترميزٌ أو توثيقٌ ناقص، ولا يُحمَّل على المريض.</summary>
    WriteOff = 2,
}

/// <summary>
/// (CEEF-230 · ط3-٢ب) <b>دورةُ المطالبات — والمالُ الذي يضيع في العيادات يضيع هنا.</b>
///
/// <para><b>ما كان:</b> الزيارةُ تُفوتَر فاتورتين — واحدةٌ للمريض وواحدةٌ للشركة (<c>ط3-٢</c>).
/// <b>وفاتورةُ الشركة ذمّةٌ تبقى بكامل قيمتها إلى الأبد</b>: لا في النظام ما يقول إنّ
/// الشركةَ اعتمدت بعضَها ورفضت بعضَها.</para>
///
/// <para><b>والرفضُ ليس نادراً — هو القاعدة.</b> عيادةٌ تُطالب بمئة ألفٍ شهرياً تُعتمد لها
/// اثنان وثمانون، <b>وثمانيةَ عشرَ تُرفض</b>: وثيقةٌ منتهية، خدمةٌ غيرُ مغطّاة، ترميزٌ خطأ،
/// موافقةٌ مسبقةٌ لم تُؤخذ. <b>وثمانيةَ عشرَ ألفاً شهرياً تبقى في الذمم</b> — فبعد سنةٍ
/// <b>مئتان وستّةَ عشرَ ألفاً وهميّة</b> في الميزانية، تُقرأ سيولةً وليست شيئاً.</para>
///
/// <para><b>ثمّ يأتي الشطبُ جملةً واحدة</b> آخرَ السنة بلا أثرٍ لأيّ زيارةٍ رُفضت ولا لماذا
/// — <b>فلا يُتعلَّم من الرفض شيء</b>، ويُعاد الخطأُ نفسُه السنةَ القادمة.</para>
///
/// <para><b>وهويّةُ هـ-١٥:</b> في كلّ سطرٍ مفصولٍ فيه: <c>المطالَبُ = المعتمَدُ + المرفوض</c>
/// — <b>لا أقلّ ولا أكثر</b>. وبقيّةٌ صامتةٌ بينهما مالٌ لا يعرف أحدٌ أين ذهب.</para>
///
/// <para><b>والمرفوضُ يخرج من الذمّة بإشعارٍ دائن لا بقيدٍ يدويّ</b>: فاتورةٌ ضريبيةٌ صدرت
/// للشركة، <b>وإنقاصُها بلا إشعارٍ يترك الضريبةَ المستحقّةَ مُعلَنةً بأكثر ممّا وجب</b>،
/// ويجعل دفترَنا يخالف دفترَها. وهو أيضاً ما تشترطه الهيئة.</para>
/// </summary>
public class InsuranceClaim : IBranchScoped
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }

    /// <summary>شركةُ التأمين المُطالَبة.</summary>
    public long PayerId { get; private set; }

    /// <summary>رقمُ المطالبة — لا يتكرّر لدى الشركة الواحدة.</summary>
    public string ClaimNumber { get; private set; }

    /// <summary>مدّةُ الدفعة — والمطالباتُ تُقدَّم دفعاتٍ شهريةً لا زيارةً زيارة.</summary>
    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }

    public ClaimStatus Status { get; private set; } = ClaimStatus.Draft;
    public DateOnly? SubmittedOn { get; private set; }
    public DateOnly? AdjudicatedOn { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    private readonly List<InsuranceClaimLine> _lines = [];
    public IReadOnlyList<InsuranceClaimLine> Lines => _lines;

    public decimal ClaimedTotal => Round(_lines.Sum(l => l.ClaimedAmount));
    public decimal ApprovedTotal => Round(_lines.Sum(l => l.ApprovedAmount));
    public decimal RejectedTotal => Round(_lines.Sum(l => l.RejectedAmount));

    /// <summary>المرفوضُ الذي لم يُقرَّر مصيرُه — <b>وهو ما يمنع الإقفال</b>.</summary>
    public decimal UndecidedRejected => Round(_lines
        .Where(l => l.RejectedAmount > 0m && l.Disposition == RejectionDisposition.Undecided)
        .Sum(l => l.RejectedAmount));

    private InsuranceClaim(long companyId, long payerId, string claimNumber,
        DateOnly periodStart, DateOnly periodEnd)
    {
        CompanyId = companyId;
        PayerId = payerId;
        ClaimNumber = claimNumber;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        CreatedAtUtc = DateTime.UtcNow;
    }

    private InsuranceClaim() { ClaimNumber = ""; }

    public static InsuranceClaim Create(long companyId, long payerId, string claimNumber,
        DateOnly periodStart, DateOnly periodEnd)
    {
        if (string.IsNullOrWhiteSpace(claimNumber))
            throw new AccountingException("رقم المطالبة مطلوب.");
        if (periodEnd < periodStart)
            throw new AccountingException("نهاية المدة قبل بدايتها.");

        return new InsuranceClaim(companyId, payerId, claimNumber.Trim(), periodStart, periodEnd);
    }

    /// <summary>
    /// يضمّ فاتورةَ مطالبةٍ إلى الدفعة.
    /// </summary>
    /// <remarks>
    /// <b>والفاتورةُ الواحدة لا تُطالَب مرّتين</b>: الشركةُ ترفض المكرّرَ وتُسجّله على
    /// المزوّد، <b>وعندنا يُعفى المدينُ مرّتين عن دينٍ واحد</b> فيصير الرصيدُ سالباً
    /// لا يقابله شيء. والحارسُ هنا للدفعة الواحدة، <b>وقيدُ التفرّد في القاعدة عبر
    /// الدفعات</b>.
    /// </remarks>
    public void AddVisit(long visitId, long payerInvoiceId, decimal claimedAmount)
    {
        EnsureDraft();

        if (claimedAmount <= 0m)
            throw new AccountingException("المبلغ المطالَب به يجب أن يكون موجباً.");
        if (_lines.Any(l => l.PayerInvoiceId == payerInvoiceId))
            throw new AccountingException(
                "فاتورةُ المطالبة مضمومةٌ سلفاً إلى هذه الدفعة — والمكرّرُ يُعفي الدينَ مرّتين.");

        _lines.Add(new InsuranceClaimLine(visitId, payerInvoiceId, Round(claimedAmount)));
    }

    public void RemoveVisit(long payerInvoiceId)
    {
        EnsureDraft();
        _lines.RemoveAll(l => l.PayerInvoiceId == payerInvoiceId);
    }

    /// <summary>
    /// يُقدّم الدفعةَ للشركة.
    /// </summary>
    /// <remarks>
    /// <b>ودفعةٌ فارغةٌ تُرفض</b>: تقديمُها يُنشئ سجلَّ مطالبةٍ لا يقابله مال، ويُفسد
    /// عدّادَ «مطالباتٌ معلّقة» الذي يُقاس به أداءُ التحصيل.
    /// </remarks>
    public void Submit(DateOnly on)
    {
        EnsureDraft();
        if (_lines.Count == 0)
            throw new AccountingException("لا تُقدَّم مطالبةٌ بلا زيارات.");

        Status = ClaimStatus.Submitted;
        SubmittedOn = on;
    }

    /// <summary>
    /// يُسجّل قرارَ الشركة على سطرٍ واحد.
    /// </summary>
    /// <remarks>
    /// <para><b>والمعتمدُ والمرفوضُ يُطبقان المطالَبَ تماماً (هـ-١٥)</b>: بقيّةٌ بينهما
    /// مالٌ لا حالةَ له — <b>لا في الذمم ولا في المشطوب</b> — ويبقى في الميزانية بلا
    /// من يسأل عنه.</para>
    ///
    /// <para><b>ورفضٌ بلا سببٍ يُرفض</b>: السببُ هو ما يُبنى عليه القرارُ بين إعادة
    /// التحميل والشطب، <b>وهو ما يُتعلَّم منه</b>. ورفضٌ بلا سببٍ يُشطب حتماً لأنّ أحداً
    /// لا يستطيع الطعنَ فيه.</para>
    /// </remarks>
    public void Adjudicate(long payerInvoiceId, decimal approvedAmount, decimal rejectedAmount,
        string? rejectionReason)
    {
        if (Status != ClaimStatus.Submitted && Status != ClaimStatus.Adjudicated)
            throw new AccountingException("لا يُفصَل إلا في مطالبةٍ مُقدَّمة.");

        var line = _lines.SingleOrDefault(l => l.PayerInvoiceId == payerInvoiceId)
            ?? throw new AccountingException("فاتورةُ المطالبة ليست في هذه الدفعة.");

        line.Adjudicate(approvedAmount, rejectedAmount, rejectionReason);
    }

    /// <summary>يُثبّت أنّ الفصلَ تمّ في كلّ السطور.</summary>
    public void CloseAdjudication(DateOnly on)
    {
        if (Status != ClaimStatus.Submitted && Status != ClaimStatus.Adjudicated)
            throw new AccountingException("لا يُفصَل إلا في مطالبةٍ مُقدَّمة.");

        var pending = _lines.Count(l => !l.IsAdjudicated);
        if (pending > 0)
            throw new AccountingException(
                $"{pending} سطراً بلا قرارٍ من الشركة — والمطالبةُ لا تُفصَل نصفَها.");

        Status = ClaimStatus.Adjudicated;
        AdjudicatedOn = on;
    }

    /// <summary>يُقرّر مصيرَ المرفوض في سطر.</summary>
    public void DisposeRejection(long payerInvoiceId, RejectionDisposition disposition)
    {
        var line = _lines.SingleOrDefault(l => l.PayerInvoiceId == payerInvoiceId)
            ?? throw new AccountingException("فاتورةُ المطالبة ليست في هذه الدفعة.");

        line.Dispose(disposition);
    }

    /// <summary>
    /// يُقفل المطالبةَ بعد معالجة المرفوض.
    /// </summary>
    /// <remarks>
    /// <b>ومرفوضٌ بلا قرارٍ يمنع الإقفال</b>: إقفالُها عليه يترك المبلغَ في ذمّة الشركة
    /// <b>وقد رفضته</b> — وهو عينُ الذمّة الوهمية التي بُني هذا الباب لإغلاقها.
    /// </remarks>
    public void Close()
    {
        if (Status != ClaimStatus.Adjudicated)
            throw new AccountingException("لا تُقفل إلا مطالبةٌ فُصل فيها.");

        if (UndecidedRejected > 0m)
            throw new AccountingException(
                $"مرفوضٌ بلا قرارٍ: {UndecidedRejected:N2} — وإقفالُها عليه يُبقيه ذمّةً وهميةً "
                + "على شركةٍ رفضته.");

        Status = ClaimStatus.Closed;
    }

    /// <summary>
    /// يربط السطرَ بالإشعار الدائن الذي أخرج مرفوضَه من الذمّة.
    /// </summary>
    /// <remarks>
    /// <b>والربطُ عبر الجذر لا بالسطر مباشرةً</b>: الجذرُ هو ما يضمن أنّ السطرَ من هذه
    /// الدفعة. <b>وسطرٌ يُعدَّل من خارجه يفتح بابَ ربطِ إشعارِ دفعةٍ بسطرِ أخرى</b> —
    /// فيبدو الدَّينُ معالَجاً وهو قائم.
    /// </remarks>
    public void LinkCreditNote(long payerInvoiceId, long creditNoteId)
    {
        var line = _lines.SingleOrDefault(l => l.PayerInvoiceId == payerInvoiceId)
            ?? throw new AccountingException("فاتورةُ المطالبة ليست في هذه الدفعة.");
        line.LinkCreditNote(creditNoteId);
    }

    /// <summary>يربط السطرَ بفاتورة إعادة التحميل على المريض.</summary>
    public void LinkRebillInvoice(long payerInvoiceId, long invoiceId)
    {
        var line = _lines.SingleOrDefault(l => l.PayerInvoiceId == payerInvoiceId)
            ?? throw new AccountingException("فاتورةُ المطالبة ليست في هذه الدفعة.");
        line.LinkRebillInvoice(invoiceId);
    }

    public void AssignBranch(long branchId)
    {
        if (branchId <= 0) throw new AccountingException("الفرع غير صالح.");
        BranchId = branchId;
    }

    private void EnsureDraft()
    {
        if (Status != ClaimStatus.Draft)
            throw new AccountingException("لا تُعدَّل مطالبةٌ قُدِّمت — أنشئ دفعةً جديدة.");
    }

    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

/// <summary>سطرُ مطالبة — زيارةٌ واحدةٌ بفاتورتها على الشركة.</summary>
public class InsuranceClaimLine
{
    public long Id { get; private set; }
    public long VisitId { get; private set; }

    /// <summary>فاتورةُ المطالبة الأصلية — <b>وهي التي يُنقصها الإشعارُ الدائن</b>.</summary>
    public long PayerInvoiceId { get; private set; }

    public decimal ClaimedAmount { get; private set; }
    public decimal ApprovedAmount { get; private set; }
    public decimal RejectedAmount { get; private set; }
    public string? RejectionReason { get; private set; }

    public RejectionDisposition Disposition { get; private set; } = RejectionDisposition.Undecided;
    public bool IsAdjudicated { get; private set; }

    /// <summary>الإشعارُ الدائن الذي أخرج المرفوضَ من الذمّة — و<c>null</c> قبل المعالجة.</summary>
    public long? CreditNoteId { get; private set; }

    /// <summary>فاتورةُ المريض بإعادة التحميل — و<c>null</c> إن شُطب أو لم يُرفض شيء.</summary>
    public long? RebillInvoiceId { get; private set; }

    internal InsuranceClaimLine(long visitId, long payerInvoiceId, decimal claimedAmount)
    {
        VisitId = visitId;
        PayerInvoiceId = payerInvoiceId;
        ClaimedAmount = claimedAmount;
    }

    private InsuranceClaimLine() { }

    internal void Adjudicate(decimal approved, decimal rejected, string? reason)
    {
        approved = Math.Round(approved, 2, MidpointRounding.AwayFromZero);
        rejected = Math.Round(rejected, 2, MidpointRounding.AwayFromZero);

        if (approved < 0m || rejected < 0m)
            throw new AccountingException("المعتمدُ والمرفوض لا يكونان سالبين.");

        // ‏هـ-١٥: **الطرفان يُطبقان المطالَب تماماً** — ولا بقيّةَ صامتة.
        if (approved + rejected != ClaimedAmount)
            throw new AccountingException(
                $"المعتمد {approved:N2} + المرفوض {rejected:N2} = {approved + rejected:N2} "
                + $"ولا يساوي المطالَب {ClaimedAmount:N2} — والفرقُ مالٌ بلا حالة.");

        if (rejected > 0m && string.IsNullOrWhiteSpace(reason))
            throw new AccountingException(
                "الرفضُ بلا سببٍ لا يُقبل — وبه يُقرَّر بين إعادة التحميل والشطب، وعليه يُطعَن.");

        ApprovedAmount = approved;
        RejectedAmount = rejected;
        RejectionReason = rejected > 0m ? reason!.Trim() : null;
        IsAdjudicated = true;

        // ‏**والمرفوضُ صفراً لا مصيرَ له** — وإبقاءُ قرارٍ قديمٍ بعد تعديلِ الفصل يمنع
        // ‏الإقفالَ أو يُجيزه بغير وجه.
        if (rejected == 0m) Disposition = RejectionDisposition.Undecided;
    }

    internal void Dispose(RejectionDisposition disposition)
    {
        if (!IsAdjudicated)
            throw new AccountingException("لا يُقرَّر مصيرُ رفضٍ لم يقع.");
        if (RejectedAmount <= 0m)
            throw new AccountingException("لا مرفوضَ في هذا السطر.");
        if (disposition == RejectionDisposition.Undecided)
            throw new AccountingException("المصير مطلوب: إعادةُ تحميلٍ أو شطب.");

        Disposition = disposition;
    }

    internal void LinkCreditNote(long creditNoteId) => CreditNoteId = creditNoteId;
    internal void LinkRebillInvoice(long invoiceId) => RebillInvoiceId = invoiceId;
}
