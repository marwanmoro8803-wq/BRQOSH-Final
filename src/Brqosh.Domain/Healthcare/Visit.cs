using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Tax;

namespace Brqosh.Domain.Healthcare;

/// <summary>حالةُ الزيارة.</summary>
public enum VisitStatus
{
    Open = 0,
    Billed = 1,
    Cancelled = 2,
}

/// <summary>
/// <b>زيارةٌ — والخدماتُ المصروفةُ فيها.</b>
///
/// <para><b>ولا تُفوتَر مرّتين</b>: الزيارةُ تُقفل بالفوترة، ومرورٌ ثانٍ يُنشئ فاتورتين
/// للمريض ومطالبتين للشركة. <b>والشركةُ ترفض المطالبةَ المكرّرة وتُسجّلها على المزوّد</b> —
/// وتكرارُها يُخرجه من الشبكة.</para>
/// </summary>
public class Visit : IBranchScoped
{
    private readonly List<VisitLine> _lines = [];

    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }

    public long PatientId { get; private set; }
    public string VisitNumber { get; private set; }
    public DateOnly VisitDate { get; private set; }
    public string DoctorName { get; private set; }
    public string? Diagnosis { get; private set; }

    /// <summary>الوثيقةُ المطبَّقة — و<c>null</c> زيارةٌ على الحساب الخاصّ.</summary>
    public long? PolicyId { get; private set; }

    public VisitStatus Status { get; private set; } = VisitStatus.Open;

    /// <summary>فاتورةُ حصّة المريض — و<c>null</c> إن كانت حصّتُه صفراً.</summary>
    public long? PatientInvoiceId { get; private set; }

    /// <summary>فاتورةُ مطالبة الشركة — و<c>null</c> إن لم يكن لها نصيب.</summary>
    public long? PayerInvoiceId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyList<VisitLine> Lines => _lines;

    /// <summary>إجماليُّ الزيارة قبل القسمة.</summary>
    public decimal Total => Math.Round(_lines.Sum(l => l.LineTotal), 2, MidpointRounding.AwayFromZero);

    public decimal CoveredTotal => Math.Round(
        _lines.Where(l => l.IsCovered).Sum(l => l.LineTotal), 2, MidpointRounding.AwayFromZero);

    public decimal UncoveredTotal => Math.Round(
        _lines.Where(l => !l.IsCovered).Sum(l => l.LineTotal), 2, MidpointRounding.AwayFromZero);

    // EF Core
    private Visit()
    {
        VisitNumber = null!;
        DoctorName = null!;
    }

    private Visit(long companyId, long patientId, string visitNumber, DateOnly visitDate,
        string doctorName, long? policyId)
    {
        CompanyId = companyId;
        PatientId = patientId;
        VisitNumber = visitNumber;
        VisitDate = visitDate;
        DoctorName = doctorName;
        PolicyId = policyId;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static Visit Create(long companyId, long patientId, string visitNumber,
        DateOnly visitDate, string doctorName, long? policyId = null)
    {
        if (patientId <= 0) throw new AccountingException("المريض مطلوب للزيارة.");
        if (string.IsNullOrWhiteSpace(visitNumber))
            throw new AccountingException("رقم الزيارة مطلوب.");
        if (string.IsNullOrWhiteSpace(doctorName))
            throw new AccountingException("اسم الطبيب مطلوب للزيارة.");

        return new Visit(companyId, patientId, visitNumber.Trim(), visitDate,
            doctorName.Trim(), policyId);
    }

    public void SetDiagnosis(string? diagnosis) =>
        Diagnosis = string.IsNullOrWhiteSpace(diagnosis) ? null : diagnosis.Trim();

    /// <summary>
    /// يُضيف خدمةً مصروفة.
    /// </summary>
    /// <param name="isCovered">
    /// <b>شمولُ الوثيقة لهذه الخدمة بعينها</b> — والتجميلُ وما استُثني نصّاً غيرُ مشمول
    /// مهما بلغت نسبةُ التغطية. <b>وشمولُ كلّ شيءٍ افتراضاً يُطالِب الشركةَ بما ترفضه.</b>
    /// </param>
    /// <param name="batchNumber">
    /// <b>رقمُ الدفعة المصروفة</b> — ويلزم لكلّ دواءٍ متتبَّع.
    /// <b>وصنفٌ متتبَّعٌ بلا رقمٍ يرفضه المَخنَقُ باسمه عند الصرف</b>، فلا يخرج دواءٌ
    /// بلا هويّةٍ ولا صلاحية.
    /// </param>
    public void AddLine(string description, decimal quantity, decimal unitPrice,
        bool isCovered = true, long? productId = null, VatCategory vatCategory = VatCategory.Standard,
        string? taxExemptionReasonCode = null, string? taxExemptionReason = null,
        string? batchNumber = null)
    {
        if (Status != VisitStatus.Open)
            throw new AccountingException("لا تُعدَّل زيارةٌ فُوتِرت أو أُلغيت.");
        if (string.IsNullOrWhiteSpace(description))
            throw new AccountingException("وصف الخدمة مطلوب.");
        if (quantity <= 0m)
            throw new AccountingException("الكمية يجب أن تكون أكبر من صفر.");
        if (unitPrice < 0m)
            throw new AccountingException("سعر الخدمة لا يكون سالباً.");

        // ‏**ورقمُ دفعةٍ بلا صنفٍ يُرفض**: الدفعةُ هويّةُ صنفٍ في مستودع، **وسطرُ خدمةٍ
        // ‏يحمل رقمَ دفعةٍ يُخزَّن بياناً لا يقرؤه أحد** — ثمّ يُقرأ يوماً فيُصرَف به.
        if (!string.IsNullOrWhiteSpace(batchNumber) && productId is null)
            throw new AccountingException(
                $"سطرُ «{description.Trim()}» يحمل رقمَ دفعةٍ بلا صنف — والدفعةُ هويّةُ صنفٍ لا وصفِ خدمة.");

        _lines.Add(VisitLine.Create(description.Trim(), quantity, unitPrice, isCovered,
            productId, vatCategory, taxExemptionReasonCode, taxExemptionReason,
            string.IsNullOrWhiteSpace(batchNumber) ? null : batchNumber.Trim()));
    }

    /// <summary>يُقفل الزيارةَ بالفوترة — <b>ورقمُ فاتورةٍ واحدةٍ على الأقلّ يلزم</b>.</summary>
    public void MarkBilled(long? patientInvoiceId, long? payerInvoiceId)
    {
        if (Status != VisitStatus.Open)
            throw new AccountingException("لا تُفوتَر إلا زيارةٌ مفتوحة.");
        if (patientInvoiceId is null && payerInvoiceId is null)
            throw new AccountingException("لا تُقفل زيارةٌ بلا فاتورةٍ واحدةٍ على الأقلّ.");

        PatientInvoiceId = patientInvoiceId;
        PayerInvoiceId = payerInvoiceId;
        Status = VisitStatus.Billed;
    }

    public void Cancel()
    {
        if (Status == VisitStatus.Billed)
            throw new AccountingException("زيارةٌ فُوتِرت لا تُلغى — أصدِرْ إشعارَ دائن.");
        Status = VisitStatus.Cancelled;
    }

    public void AssignBranch(long branchId)
    {
        if (BranchId is null) BranchId = branchId;
    }
}

/// <summary>خدمةٌ في زيارة.</summary>
public class VisitLine
{
    public string Description { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public bool IsCovered { get; private set; }

    /// <summary>
    /// صنفُ الصيدلية المصروف — <b>وسطرُ الخدمة بلا صنف</b>.
    /// </summary>
    /// <remarks>
    /// <b>وكان هذا الحقلُ ميّتاً منذ بنائه</b>: يُكتب ولا يقرؤه أحد. فدواءٌ يُصرف للمريض
    /// <b>لا ينقص من المستودع، ولا تُقيَّد له تكلفةُ مبيعات، ولا تُستهلَك دفعتُه</b> —
    /// فيبيع المستوصفُ الدواءَ بربحٍ مئةٍ بالمئة في دفاتره، <b>ثمّ يُقيَّد الفرقُ «عجزَ
    /// جرد» آخرَ السنة</b> وهو صرفٌ صحيحٌ بلا مصروف.
    /// </remarks>
    public long? ProductId { get; private set; }

    /// <summary>
    /// رقمُ الدفعة المصروفة — <b>والدواءُ المتتبَّع لا يخرج بلا رقم</b>.
    /// </summary>
    public string? BatchNumber { get; private set; }

    public VatCategory VatCategory { get; private set; }

    /// <summary>سببُ الإعفاء النظاميّ — يلزم للصفريّ والمعفى، ويُحمَل إلى الفاتورتين معاً.</summary>
    public string? TaxExemptionReasonCode { get; private set; }
    public string? TaxExemptionReason { get; private set; }

    public decimal LineTotal => Math.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);

    // EF Core
    private VisitLine() => Description = null!;

    internal static VisitLine Create(string description, decimal quantity, decimal unitPrice,
        bool isCovered, long? productId, VatCategory vatCategory,
        string? taxExemptionReasonCode, string? taxExemptionReason,
        string? batchNumber = null) =>
        new()
        {
            Description = description,
            Quantity = quantity,
            UnitPrice = unitPrice,
            IsCovered = isCovered,
            ProductId = productId,
            BatchNumber = batchNumber,
            VatCategory = vatCategory,
            TaxExemptionReasonCode = taxExemptionReasonCode,
            TaxExemptionReason = taxExemptionReason,
        };
}
