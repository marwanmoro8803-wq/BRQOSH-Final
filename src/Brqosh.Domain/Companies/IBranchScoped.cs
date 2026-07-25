namespace Brqosh.Domain.Companies;

/// <summary>كيان تشغيلي يُربط بفرع المنشأة — اختياري حتى الترحيل/الاعتماد.</summary>
public interface IBranchScoped
{
    long CompanyId { get; }
    long? BranchId { get; }
    void AssignBranch(long branchId);
}
