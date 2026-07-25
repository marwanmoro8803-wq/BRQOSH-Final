using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;

namespace Brqosh.Domain.Construction;

public enum ConstructionContractStatus
{
    Draft = 0,
    Active = 1,
    Completed = 2,
    Cancelled = 3,
}

/// <summary>عقد مقاولات رئيسي مرتبط بمشروع.</summary>
public class ConstructionContract : IBranchScoped
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }
    public long ProjectId { get; private set; }
    public string ContractNumber { get; private set; }
    public DateOnly ContractDate { get; private set; }
    public decimal ContractValue { get; private set; }
    public string Description { get; private set; }
    public ConstructionContractStatus Status { get; private set; } = ConstructionContractStatus.Draft;

    private ConstructionContract()
    {
        ContractNumber = null!;
        Description = null!;
    }

    private ConstructionContract(long companyId, long projectId, string contractNumber,
        DateOnly contractDate, decimal contractValue, string description)
    {
        CompanyId = companyId;
        ProjectId = projectId;
        ContractNumber = contractNumber;
        ContractDate = contractDate;
        ContractValue = contractValue;
        Description = description;
    }

    public static ConstructionContract Create(long companyId, long projectId, string contractNumber,
        DateOnly contractDate, decimal contractValue, string description = "")
    {
        if (projectId <= 0)
            throw new AccountingException("المشروع مطلوب للعقد.");
        if (string.IsNullOrWhiteSpace(contractNumber))
            throw new AccountingException("رقم العقد مطلوب.");
        if (contractValue <= 0)
            throw new AccountingException("قيمة العقد يجب أن تكون أكبر من صفر.");

        return new ConstructionContract(companyId, projectId, contractNumber.Trim(), contractDate,
            Math.Round(contractValue, 2, MidpointRounding.AwayFromZero), (description ?? "").Trim());
    }

    public void Activate()
    {
        if (Status != ConstructionContractStatus.Draft)
            throw new AccountingException("يمكن تفعيل العقد من حالة المسودة فقط.");
        Status = ConstructionContractStatus.Active;
    }

    public void Complete()
    {
        if (Status != ConstructionContractStatus.Active)
            throw new AccountingException("يُستكمل العقد النشط فقط.");
        Status = ConstructionContractStatus.Completed;
    }

    public void Cancel()
    {
        if (Status is ConstructionContractStatus.Completed or ConstructionContractStatus.Cancelled)
            throw new AccountingException("لا يمكن إلغاء العقد في حالته الحالية.");
        Status = ConstructionContractStatus.Cancelled;
    }

    public static string StatusLabel(ConstructionContractStatus status) => status switch
    {
        ConstructionContractStatus.Draft => "مسودة",
        ConstructionContractStatus.Active => "نشط",
        ConstructionContractStatus.Completed => "مكتمل",
        ConstructionContractStatus.Cancelled => "ملغاة",
        _ => status.ToString(),
    };
    public void AssignBranch(long branchId)
    {
        if (branchId <= 0) throw new AccountingException("الفرع غير صالح.");
        BranchId = branchId;
    }

}
