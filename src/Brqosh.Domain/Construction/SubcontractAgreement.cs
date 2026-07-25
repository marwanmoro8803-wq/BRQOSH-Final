using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;

namespace Brqosh.Domain.Construction;

public enum SubcontractAgreementStatus
{
    Draft = 0,
    Active = 1,
    Completed = 2,
    Cancelled = 3,
}

/// <summary>عقد مقاول باطن مرتبط بمشروع ومورد.</summary>
public class SubcontractAgreement : IBranchScoped
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }
    public long ProjectId { get; private set; }
    public long SupplierId { get; private set; }
    public string AgreementNumber { get; private set; }
    public DateOnly AgreementDate { get; private set; }
    public decimal ContractValue { get; private set; }
    public decimal RetentionPercent { get; private set; }
    public string ScopeDescription { get; private set; }
    public SubcontractAgreementStatus Status { get; private set; } = SubcontractAgreementStatus.Draft;

    private SubcontractAgreement()
    {
        AgreementNumber = null!;
        ScopeDescription = null!;
    }

    private SubcontractAgreement(long companyId, long projectId, long supplierId, string agreementNumber,
        DateOnly agreementDate, decimal contractValue, decimal retentionPercent, string scopeDescription)
    {
        CompanyId = companyId;
        ProjectId = projectId;
        SupplierId = supplierId;
        AgreementNumber = agreementNumber;
        AgreementDate = agreementDate;
        ContractValue = contractValue;
        RetentionPercent = retentionPercent;
        ScopeDescription = scopeDescription;
    }

    public static SubcontractAgreement Create(long companyId, long projectId, long supplierId,
        string agreementNumber, DateOnly agreementDate, decimal contractValue,
        decimal retentionPercent = 10m, string scopeDescription = "")
    {
        if (projectId <= 0)
            throw new AccountingException("المشروع مطلوب.");
        if (supplierId <= 0)
            throw new AccountingException("المورد (مقاول الباطن) مطلوب.");
        if (string.IsNullOrWhiteSpace(agreementNumber))
            throw new AccountingException("رقم عقد الباطن مطلوب.");
        if (contractValue <= 0)
            throw new AccountingException("قيمة العقد يجب أن تكون أكبر من صفر.");

        return new SubcontractAgreement(companyId, projectId, supplierId, agreementNumber.Trim(),
            agreementDate, Math.Round(contractValue, 2, MidpointRounding.AwayFromZero),
            Math.Round(retentionPercent, 2, MidpointRounding.AwayFromZero), (scopeDescription ?? "").Trim());
    }

    public void Activate()
    {
        if (Status != SubcontractAgreementStatus.Draft)
            throw new AccountingException("يمكن تفعيل العقد من حالة المسودة فقط.");
        Status = SubcontractAgreementStatus.Active;
    }

    public void Complete()
    {
        if (Status != SubcontractAgreementStatus.Active)
            throw new AccountingException("يُستكمل عقد الباطن النشط فقط.");
        Status = SubcontractAgreementStatus.Completed;
    }

    public void Cancel()
    {
        if (Status is SubcontractAgreementStatus.Completed or SubcontractAgreementStatus.Cancelled)
            throw new AccountingException("لا يمكن إلغاء العقد في حالته الحالية.");
        Status = SubcontractAgreementStatus.Cancelled;
    }

    public static string StatusLabel(SubcontractAgreementStatus status) => status switch
    {
        SubcontractAgreementStatus.Draft => "مسودة",
        SubcontractAgreementStatus.Active => "نشط",
        SubcontractAgreementStatus.Completed => "مكتمل",
        SubcontractAgreementStatus.Cancelled => "ملغاة",
        _ => status.ToString(),
    };
    public void AssignBranch(long branchId)
    {
        if (branchId <= 0) throw new AccountingException("الفرع غير صالح.");
        BranchId = branchId;
    }

}
