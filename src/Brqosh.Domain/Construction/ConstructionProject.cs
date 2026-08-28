using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;

namespace Brqosh.Domain.Construction;

public enum ConstructionProjectStatus
{
    Planning = 0,
    Active = 1,
    OnHold = 2,
    Completed = 3,
    Cancelled = 4,
}

/// <summary>مشروع مقاولات — يحمل BOQ ويرتبط بالعقود والمستخلصات.</summary>
public class ConstructionProject : IBranchScoped, ISoftDeletable
{
    private readonly List<BoqItem> _boqItems = [];

    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }
    public string Code { get; private set; }
    public string NameAr { get; private set; }
    public long? CustomerId { get; private set; }
    public DateOnly? StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public decimal ContractValue { get; private set; }

    /// <summary>إجمالي التكلفة المقدّرة للعقد — أساس نسبة الإنجاز cost-to-cost (IFRS 15).</summary>
    public decimal EstimatedTotalCost { get; private set; }

    public decimal RetentionPercent { get; private set; }
    public string Location { get; private set; }
    public ConstructionProjectStatus Status { get; private set; } = ConstructionProjectStatus.Planning;
    public bool IsActive { get; private set; } = true;
    public DateTime? DeletedAtUtc { get; private set; }

    public IReadOnlyList<BoqItem> BoqItems => _boqItems.AsReadOnly();
    public decimal BoqTotal => _boqItems.Sum(b => b.Amount);

    private ConstructionProject()
    {
        Code = null!;
        NameAr = null!;
        Location = null!;
    }

    private ConstructionProject(long companyId, string code, string nameAr, long? customerId,
        DateOnly? startDate, DateOnly? endDate, decimal contractValue, decimal estimatedTotalCost,
        decimal retentionPercent, string location)
    {
        CompanyId = companyId;
        Code = code;
        NameAr = nameAr;
        CustomerId = customerId;
        StartDate = startDate;
        EndDate = endDate;
        ContractValue = contractValue;
        EstimatedTotalCost = estimatedTotalCost;
        RetentionPercent = retentionPercent;
        Location = location;
    }

    public static ConstructionProject Create(long companyId, string code, string nameAr,
        long? customerId = null, DateOnly? startDate = null, DateOnly? endDate = null,
        decimal contractValue = 0m, decimal retentionPercent = 10m, string location = "",
        decimal estimatedTotalCost = 0m)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new AccountingException("كود المشروع مطلوب.");
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new AccountingException("اسم المشروع مطلوب.");
        if (contractValue < 0)
            throw new AccountingException("قيمة العقد لا يمكن أن تكون سالبة.");
        if (estimatedTotalCost < 0)
            throw new AccountingException("التكلفة المقدّرة لا يمكن أن تكون سالبة.");
        if (retentionPercent is < 0 or > 100)
            throw new AccountingException("نسبة المحتجز يجب أن تكون بين 0 و 100.");

        return new ConstructionProject(companyId, code.Trim(), nameAr.Trim(), customerId,
            startDate, endDate, Math.Round(contractValue, 2, MidpointRounding.AwayFromZero),
            Math.Round(estimatedTotalCost, 2, MidpointRounding.AwayFromZero),
            Math.Round(retentionPercent, 2, MidpointRounding.AwayFromZero), (location ?? "").Trim());
    }

    public void Update(string nameAr, long? customerId, DateOnly? startDate, DateOnly? endDate,
        decimal contractValue, decimal retentionPercent, string location, ConstructionProjectStatus status,
        decimal? estimatedTotalCost = null)
    {
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new AccountingException("اسم المشروع مطلوب.");
        if (contractValue < 0)
            throw new AccountingException("قيمة العقد لا يمكن أن تكون سالبة.");
        if (retentionPercent is < 0 or > 100)
            throw new AccountingException("نسبة المحتجز يجب أن تكون بين 0 و 100.");

        NameAr = nameAr.Trim();
        CustomerId = customerId;
        StartDate = startDate;
        EndDate = endDate;
        ContractValue = Math.Round(contractValue, 2, MidpointRounding.AwayFromZero);
        if (estimatedTotalCost is decimal est)
        {
            if (est < 0)
                throw new AccountingException("التكلفة المقدّرة لا يمكن أن تكون سالبة.");
            EstimatedTotalCost = Math.Round(est, 2, MidpointRounding.AwayFromZero);
        }
        RetentionPercent = Math.Round(retentionPercent, 2, MidpointRounding.AwayFromZero);
        Location = (location ?? "").Trim();
        Status = status;
    }

    public void SetEstimatedTotalCost(decimal estimatedTotalCost)
    {
        if (estimatedTotalCost < 0)
            throw new AccountingException("التكلفة المقدّرة لا يمكن أن تكون سالبة.");
        EstimatedTotalCost = Math.Round(estimatedTotalCost, 2, MidpointRounding.AwayFromZero);
    }

    public void AddBoqItem(string itemNumber, string description, string unit, decimal quantity, decimal unitPrice)
    {
        if (string.IsNullOrWhiteSpace(itemNumber))
            throw new AccountingException("رقم البند مطلوب.");
        if (string.IsNullOrWhiteSpace(description))
            throw new AccountingException("وصف البند مطلوب.");
        if (quantity <= 0 || unitPrice < 0)
            throw new AccountingException("الكمية يجب أن تكون موجبة والسعر لا يكون سالباً.");

        _boqItems.Add(new BoqItem(itemNumber.Trim(), description.Trim(),
            string.IsNullOrWhiteSpace(unit) ? "وحدة" : unit.Trim(), quantity, unitPrice));
    }

    public void ClearBoq() => _boqItems.Clear();

    public void Activate() => Status = ConstructionProjectStatus.Active;

    /// <summary>
    /// إيقاف المشروع — <b>حالةٌ تشغيلية لا واقعةُ حذف</b>.
    ///
    /// <para>(CEEF-229 · م2-١٧) كان يضبط <c>DeletedAtUtc</c> أيضاً، ومرشّح الاستعلام العامّ
    /// يُخفي كلَّ ما له تاريخ حذف — <b>فيختفي من قوائمه تماماً</b> لا يظهر موقوفاً. وزرُّ
    /// الشاشة مكتوبٌ عليه «إيقاف» ورسالةُ التأكيد تسأل «إيقاف …؟»، فكان الوعد إيقافاً
    /// والفعلُ حذفاً. ولا رجعة: لا <c>Reactivate</c> في الكيان أصلاً.</para>
    ///
    /// <para>أُصلح هذا في <see cref="Brqosh.Domain.Accounting.Account"/> ضمن م0-١٣، وبقيت
    /// سبعةُ كياناتٍ أخرى بالجسم القديم حرفاً بحرف — فالإصلاح طُبِّق على كيانٍ واحد ولم يُعمَّم.</para>
    /// </summary>
    public void Deactivate() => IsActive = false;

    /// <summary>إعادة تفعيل المشروع الموقوف.</summary>
    public void Reactivate() => IsActive = true;

    public static string StatusLabel(ConstructionProjectStatus status) => status switch
    {
        ConstructionProjectStatus.Planning => "تخطيط",
        ConstructionProjectStatus.Active => "نشط",
        ConstructionProjectStatus.OnHold => "متوقف",
        ConstructionProjectStatus.Completed => "مكتمل",
        ConstructionProjectStatus.Cancelled => "ملغاة",
        _ => status.ToString(),
    };
    public void AssignBranch(long branchId)
    {
        if (branchId <= 0) throw new AccountingException("الفرع غير صالح.");
        BranchId = branchId;
    }

}
