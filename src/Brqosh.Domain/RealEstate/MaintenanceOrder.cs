using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;

namespace Brqosh.Domain.RealEstate;

/// <summary>حالةُ أمر الصيانة.</summary>
public enum MaintenanceStatus
{
    Open = 0,
    InProgress = 1,

    /// <summary>انتهى العملُ وثبتت تكلفتُه — <b>وعندها فقط يُحمَّل</b>.</summary>
    Completed = 2,

    Cancelled = 3,
}

/// <summary>
/// سببُ الصيانة — <b>وهو ما يُقرّر مَن يدفع، لا مبلغُها ولا مَن طلبها</b>.
/// </summary>
public enum MaintenanceKind
{
    /// <summary>
    /// دوريّةٌ وقائية: دهانٌ، مكيّفٌ، فحصٌ سنويّ. <b>والتزامُها على المالك</b>.
    /// </summary>
    Preventive = 0,

    /// <summary>
    /// إصلاحُ عطلٍ بالاستعمال العاديّ — تسريبٌ، عطلُ سخّان. <b>وهي على المالك كذلك</b>:
    /// الاستهلاكُ العاديُّ ثمنُ التأجير لا ذنبُ المستأجر.
    /// </summary>
    Corrective = 1,

    /// <summary>
    /// تلفٌ بفعل المستأجر — <b>وهو وحدَه ما يُحمَّل عليه</b>.
    /// </summary>
    TenantDamage = 2,
}

/// <summary>
/// (CEEF-230 · ط3-١ب) <b>أمرُ صيانة — والتكلفةُ التي لا يعرف أحدٌ أيَّ وحدةٍ أكلتها.</b>
///
/// <para><b>ما كان:</b> المؤجِّرُ يُصلح ويدفع، <b>ويُقيَّد المبلغُ مصروفَ صيانةٍ عامّاً</b>
/// بلا رابطٍ بوحدةٍ ولا عقار. فمحفظةٌ بأربعين وحدةً تُظهر ربحاً إجمالياً <b>ولا تقول أيَّ
/// وحدةٍ تخسر</b> — وثلاثُ وحداتٍ قد تأكل ربحَ السبعةِ والثلاثين، <b>ولا شيءَ في النظام
/// يُظهر ذلك</b>. فيُجدَّد عقدُ الوحدة الخاسرة بالسعر نفسِه سنةً بعد سنة.</para>
///
/// <para><b>والأخطرُ أنّ المصادرةَ كانت بلا سند:</b> <c>SecurityDepositService.ReturnAsync</c>
/// تأخذ مبلغَ المصادرة <b>رقماً مجرّداً يكتبه محاسب</b> — بلا أمرِ صيانةٍ يُفصّل ما أُصلح
/// وبكم. <b>ومصادرةٌ بلا بيانٍ دعوى يخسرها المؤجِّر</b>: المستأجرُ يطلب التفصيل فلا يجده.
/// وهذا الأمرُ هو سندُها.</para>
///
/// <para><b>وهويّةُ هـ-١٨:</b> <c>تكلفةُ الأمر = ما على المالك + ما على المستأجر</c> — ولا
/// بقيّةَ بينهما. <b>وحصّةُ المالك مُشتقّةٌ بالطرح لا مُخزَّنة</b>: قيمتان تصفان شيئاً
/// واحداً تفترقان يوماً.</para>
///
/// <para><b>والصيانةُ الدوريّةُ لا تُحمَّل على المستأجر</b> — وهو أهمُّ حارسٍ هنا: الاستهلاكُ
/// العاديُّ ثمنُ التأجير، <b>وتحميلُه على المستأجر يُخرج مطالبةً لا يقبلها ولا تصمد</b>.</para>
/// </summary>
public class MaintenanceOrder : IBranchScoped
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }

    public long PropertyId { get; private set; }

    /// <summary>
    /// الوحدةُ المعنيّة — و<c>null</c> يعني <b>عملاً في المرافق المشتركة</b>.
    /// </summary>
    /// <remarks>
    /// <b>ومشتركٌ لا يُحمَّل على مستأجرٍ بعينه</b>: مصعدُ العمارة لا يخصّ ساكنَ الشقّة
    /// الثالثة، <b>وتحميلُه عليه يُطالبه بما ينتفع به غيرُه</b>.
    /// </remarks>
    public long? UnitId { get; private set; }

    /// <summary>رقمُ الأمر — لا يتكرّر في المنشأة.</summary>
    public string OrderNumber { get; private set; }

    public MaintenanceKind Kind { get; private set; }
    public DateOnly RequestDate { get; private set; }
    public DateOnly? CompletedOn { get; private set; }

    public string Description { get; private set; }
    public string? VendorName { get; private set; }

    public MaintenanceStatus Status { get; private set; } = MaintenanceStatus.Open;

    /// <summary>
    /// العقدُ الذي يُحمَّل عليه المستأجر — <b>ولا تحميلَ بغيره</b>.
    /// </summary>
    /// <remarks>
    /// <b>فلا يُحمَّل على مستأجرٍ لا وجودَ له</b>: وحدةٌ شاغرةٌ تُصلَح على حساب المالك
    /// حتماً، <b>وتحميلُ تلفِها على «المستأجر» بلا عقدٍ يُنشئ ذمّةً على لا أحد</b>.
    /// </remarks>
    public long? LeaseContractId { get; private set; }

    /// <summary>ما يُحمَّل على المستأجر من التكلفة.</summary>
    public decimal TenantChargeAmount { get; private set; }

    public long? JournalEntryId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private readonly List<MaintenanceCostLine> _lines = [];
    public IReadOnlyList<MaintenanceCostLine> Lines => _lines;

    /// <summary>تكلفةُ الأمر — <b>مجموعُ سطوره لا رقمٌ يُكتب</b>.</summary>
    public decimal TotalCost => Round(_lines.Sum(l => l.Amount));

    /// <summary>
    /// ما يتحمّله المالك — <b>مُشتقٌّ بالطرح، وبه تصمد هـ-١٨ بناءً لا بحارس</b>.
    /// </summary>
    public decimal OwnerCost => Round(TotalCost - TenantChargeAmount);

    private MaintenanceOrder(long companyId, long propertyId, long? unitId, string orderNumber,
        MaintenanceKind kind, DateOnly requestDate, string description, string? vendorName)
    {
        CompanyId = companyId;
        PropertyId = propertyId;
        UnitId = unitId;
        OrderNumber = orderNumber;
        Kind = kind;
        RequestDate = requestDate;
        Description = description;
        VendorName = vendorName;
        CreatedAtUtc = DateTime.UtcNow;
    }

    private MaintenanceOrder()
    {
        OrderNumber = "";
        Description = "";
    }

    public static MaintenanceOrder Create(long companyId, long propertyId, string orderNumber,
        MaintenanceKind kind, DateOnly requestDate, string description,
        long? unitId = null, string? vendorName = null)
    {
        if (propertyId <= 0) throw new AccountingException("العقار مطلوب لأمر الصيانة.");
        if (string.IsNullOrWhiteSpace(orderNumber))
            throw new AccountingException("رقم أمر الصيانة مطلوب.");
        if (string.IsNullOrWhiteSpace(description))
            throw new AccountingException("وصف العمل مطلوب — وأمرٌ بلا وصفٍ لا يُراجَع ولا يُطعَن فيه.");
        if (unitId is long id && id <= 0)
            throw new AccountingException("الوحدة غير صالحة.");

        return new MaintenanceOrder(companyId, propertyId, unitId, orderNumber.Trim(), kind,
            requestDate, description.Trim(),
            string.IsNullOrWhiteSpace(vendorName) ? null : vendorName.Trim());
    }

    /// <summary>يضيف سطرَ تكلفة.</summary>
    /// <remarks>
    /// <b>والتكلفةُ سطورٌ لا رقمٌ واحد</b>: «إصلاحٌ — ٣٢٠٠» لا يُراجَع ولا يُطعَن فيه.
    /// <b>وهو ما يجعل المصادرةَ سنداً</b>: المستأجرُ يرى قطعَ الغيار والأجرةَ مفصّلة.
    /// </remarks>
    public void AddCost(string description, decimal amount)
    {
        EnsureEditable();

        if (string.IsNullOrWhiteSpace(description))
            throw new AccountingException("وصف سطر التكلفة مطلوب.");
        if (amount <= 0m)
            throw new AccountingException("مبلغ سطر التكلفة يجب أن يكون موجباً.");

        _lines.Add(new MaintenanceCostLine(description.Trim(),
            Round(amount)));
    }

    public void RemoveCost(int index)
    {
        EnsureEditable();
        if (index < 0 || index >= _lines.Count)
            throw new AccountingException("سطر التكلفة غير موجود.");

        _lines.RemoveAt(index);

        // ‏**وحذفُ سطرٍ قد يُنقص التكلفةَ دون التحميل** — فيصير المحمَّلُ أكبرَ من الكلّ،
        // ‏**وحصّةُ المالك سالبة**. والتصفيرُ هنا يُجبر إعادةَ القرار صراحةً.
        if (TenantChargeAmount > TotalCost) TenantChargeAmount = 0m;
    }

    public void Start()
    {
        if (Status != MaintenanceStatus.Open)
            throw new AccountingException("لا يبدأ إلا أمرٌ مفتوح.");
        Status = MaintenanceStatus.InProgress;
    }

    /// <summary>
    /// يُحمِّل جزءاً من التكلفة على المستأجر.
    /// </summary>
    /// <remarks>
    /// <para><b>والدوريّةُ والإصلاحيّةُ لا تُحمَّلان عليه</b>: الاستهلاكُ العاديُّ ثمنُ
    /// التأجير <b>لا ذنبُ الساكن</b>. وتحميلُهما عليه يُخرج مطالبةً لا تصمد، <b>وإن
    /// خُصمت من التأمين صارت مصادرةً بلا وجه</b>.</para>
    ///
    /// <para><b>ولا تحميلَ بلا عقد</b>، <b>ولا على عملٍ في المرافق المشتركة</b> — فمصعدُ
    /// العمارة ينتفع به الجميع.</para>
    /// </remarks>
    public void ChargeTenant(decimal amount, long leaseContractId)
    {
        EnsureEditable();

        if (amount < 0m)
            throw new AccountingException("المحمَّل على المستأجر لا يكون سالباً.");
        if (leaseContractId <= 0)
            throw new AccountingException("العقد مطلوب للتحميل على المستأجر.");

        if (Kind != MaintenanceKind.TenantDamage)
            throw new AccountingException(
                "لا يُحمَّل على المستأجر إلا تلفٌ بفعله — والصيانةُ الدوريّةُ والإصلاحيّةُ "
                + "على المالك، فالاستهلاكُ العاديُّ ثمنُ التأجير.");

        if (UnitId is null)
            throw new AccountingException(
                "عملٌ في المرافق المشتركة لا يُحمَّل على مستأجرٍ بعينه — فينتفع به غيرُه.");

        // ‏هـ-١٨: **المحمَّلُ لا يتجاوز التكلفة** — وتجاوزُه يجعل حصّةَ المالك سالبة،
        // ‏**فتظهر المنشأةُ رابحةً من صيانةٍ دفعتها**.
        if (amount > TotalCost)
            throw new AccountingException(
                $"المحمَّل {amount:N2} أكبر من تكلفة الأمر {TotalCost:N2} — "
                + "ولا يُحمَّل المستأجرُ بأكثرَ ممّا كلّف الإصلاح.");

        TenantChargeAmount = Round(amount);
        LeaseContractId = leaseContractId;
    }

    /// <summary>يُلغي التحميلَ على المستأجر ويُعيده كلَّه على المالك.</summary>
    public void ClearTenantCharge()
    {
        EnsureEditable();
        TenantChargeAmount = 0m;
        LeaseContractId = null;
    }

    /// <summary>
    /// يُنهي الأمرَ ويُثبّت تكلفتَه.
    /// </summary>
    /// <remarks>
    /// <b>وأمرٌ بلا تكلفةٍ لا يُنهى</b>: إنهاؤه بصفرٍ يُنتج سنداً <b>يبدو مُثبَتاً وهو
    /// فارغ</b> — ثمّ يُصادَر عليه تأمين.
    /// </remarks>
    public void Complete(DateOnly on)
    {
        if (Status != MaintenanceStatus.Open && Status != MaintenanceStatus.InProgress)
            throw new AccountingException("لا يُنهى إلا أمرٌ قائم.");
        if (_lines.Count == 0)
            throw new AccountingException(
                "أمرُ صيانةٍ بلا تكلفة — ولا يُنهى، فسندٌ فارغٌ يُصادَر عليه تأمين.");

        Status = MaintenanceStatus.Completed;
        CompletedOn = on;
    }

    public void LinkJournalEntry(long journalEntryId) => JournalEntryId = journalEntryId;

    public void Cancel()
    {
        if (Status == MaintenanceStatus.Completed)
            throw new AccountingException("لا يُلغى أمرٌ أُنهي — وتكلفتُه قُيّدت.");

        Status = MaintenanceStatus.Cancelled;
    }

    public void AssignBranch(long branchId)
    {
        if (branchId <= 0) throw new AccountingException("الفرع غير صالح.");
        BranchId = branchId;
    }

    private void EnsureEditable()
    {
        if (Status is MaintenanceStatus.Completed or MaintenanceStatus.Cancelled)
            throw new AccountingException("لا يُعدَّل أمرٌ أُنهي أو أُلغي.");
    }

    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

/// <summary>سطرُ تكلفةٍ في أمر صيانة — <b>وبه يصير الأمرُ سنداً يُراجَع</b>.</summary>
public class MaintenanceCostLine
{
    public long Id { get; private set; }
    public string Description { get; private set; }
    public decimal Amount { get; private set; }

    internal MaintenanceCostLine(string description, decimal amount)
    {
        Description = description;
        Amount = amount;
    }

    private MaintenanceCostLine() { Description = ""; }
}
