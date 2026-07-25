using Brqosh.Domain.Accounting;

namespace Brqosh.Domain.Workflow;

public class WorkflowApprovalRecord
{
    public WorkflowApproverSlot Slot { get; private set; }
    public string ApprovedBy { get; private set; }
    public DateTime ApprovedAtUtc { get; private set; }
    public string? Comment { get; private set; }

    private WorkflowApprovalRecord()
    {
        ApprovedBy = null!;
    }

    internal WorkflowApprovalRecord(WorkflowApproverSlot slot, string approvedBy, string? comment)
    {
        Slot = slot;
        ApprovedBy = approvedBy.Trim();
        ApprovedAtUtc = DateTime.UtcNow;
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
    }
}

public class WorkflowInstance
{
    private readonly List<WorkflowApprovalRecord> _approvals = [];

    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public WorkflowDocumentKind DocumentKind { get; private set; }
    public long DocumentId { get; private set; }
    public string SubmittedBy { get; private set; }
    public DateTime SubmittedAtUtc { get; private set; }
    public int CurrentStepIndex { get; private set; }
    public WorkflowInstanceStatus Status { get; private set; } = WorkflowInstanceStatus.Pending;
    public string? RejectedBy { get; private set; }
    public string? RejectionReason { get; private set; }

    public IReadOnlyList<WorkflowApprovalRecord> Approvals => _approvals.AsReadOnly();

    private WorkflowInstance()
    {
        SubmittedBy = null!;
    }

    private WorkflowInstance(long companyId, WorkflowDocumentKind kind, long documentId, string submittedBy)
    {
        CompanyId = companyId;
        DocumentKind = kind;
        DocumentId = documentId;
        SubmittedBy = submittedBy.Trim();
        SubmittedAtUtc = DateTime.UtcNow;
    }

    public static WorkflowInstance Start(long companyId, WorkflowDocumentKind kind, long documentId, string submittedBy) =>
        new(companyId, kind, documentId, submittedBy);

    public WorkflowApproverSlot? CurrentRequiredSlot(IReadOnlyList<WorkflowApproverSlot> chain) =>
        Status != WorkflowInstanceStatus.Pending || CurrentStepIndex >= chain.Count
            ? null
            : chain[CurrentStepIndex];

    public void ApproveStep(WorkflowApproverSlot slot, string approver, string? comment, IReadOnlyList<WorkflowApproverSlot> chain)
    {
        EnsurePending();
        var required = CurrentRequiredSlot(chain)
            ?? throw new AccountingException("لا توجد خطوة موافقة معلّقة.");
        if (required != slot)
            throw new AccountingException($"الموافقة المطلوبة من: {required} — وليس {slot}.");
        if (string.Equals(SubmittedBy, approver, StringComparison.OrdinalIgnoreCase))
            throw new AccountingException("لا يمكن اعتماد مستند قدّمته بنفسك (فصل المهام).");

        _approvals.Add(new WorkflowApprovalRecord(slot, approver, comment));
        CurrentStepIndex++;
        if (CurrentStepIndex >= chain.Count)
            Status = WorkflowInstanceStatus.Approved;
    }

    public void Reject(string rejectedBy, string reason)
    {
        EnsurePending();
        if (string.IsNullOrWhiteSpace(reason))
            throw new AccountingException("سبب الرفض مطلوب.");
        Status = WorkflowInstanceStatus.Rejected;
        RejectedBy = rejectedBy.Trim();
        RejectionReason = reason.Trim();
    }

    private void EnsurePending()
    {
        if (Status != WorkflowInstanceStatus.Pending)
            throw new AccountingException("سير العمل ليس في حالة انتظار موافقة.");
    }
}
