using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;

namespace Brqosh.Domain.Construction;

/// <summary>قيد تكلفة مشروع — يُستخدم لنسبة إنجاز IFRS 15 (cost-to-cost).</summary>
public class ProjectCostEntry : IBranchScoped
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }
    public long ProjectId { get; private set; }
    public DateOnly CostDate { get; private set; }
    public string Description { get; private set; }
    public decimal Amount { get; private set; }
    public string SourceDocument { get; private set; }
    public long? JournalEntryId { get; private set; }
    public bool IsReversed { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private ProjectCostEntry()
    {
        Description = null!;
        SourceDocument = null!;
    }

    private ProjectCostEntry(long companyId, long projectId, DateOnly costDate,
        string description, decimal amount, string sourceDocument)
    {
        CompanyId = companyId;
        ProjectId = projectId;
        CostDate = costDate;
        Description = description;
        Amount = amount;
        SourceDocument = sourceDocument;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static ProjectCostEntry Create(long companyId, long projectId, DateOnly costDate,
        string description, decimal amount, string sourceDocument = "")
    {
        if (projectId <= 0)
            throw new AccountingException("المشروع مطلوب لتسجيل التكلفة.");
        if (amount <= 0)
            throw new AccountingException("مبلغ تكلفة المشروع يجب أن يكون أكبر من صفر.");
        if (string.IsNullOrWhiteSpace(description))
            throw new AccountingException("بيان التكلفة مطلوب.");

        return new ProjectCostEntry(companyId, projectId, costDate,
            description.Trim(), Math.Round(amount, 2, MidpointRounding.AwayFromZero),
            (sourceDocument ?? "").Trim());
    }

    public void LinkJournalEntry(long journalEntryId) => JournalEntryId = journalEntryId;

    public void MarkReversed()
    {
        if (IsReversed)
            throw new AccountingException("تكلفة المشروع معكوسة مسبقاً.");
        IsReversed = true;
    }
    public void AssignBranch(long branchId)
    {
        if (branchId <= 0) throw new AccountingException("الفرع غير صالح.");
        BranchId = branchId;
    }

}
