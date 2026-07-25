using Brqosh.Domain.Workflow;

namespace Brqosh.Infrastructure.Workflow;

public interface IWorkflowEngine
{
    IReadOnlyList<WorkflowApproverSlot> GetChain(WorkflowDocumentKind kind);

    Task<WorkflowInstance> SubmitAsync(
        long companyId,
        WorkflowDocumentKind kind,
        long documentId,
        string submittedBy,
        CancellationToken ct = default);

    Task<WorkflowInstance> ApproveCurrentStepAsync(
        long workflowInstanceId,
        string approver,
        string? comment = null,
        CancellationToken ct = default);

    Task<WorkflowInstance> RejectAsync(
        long workflowInstanceId,
        string rejectedBy,
        string reason,
        CancellationToken ct = default);

    Task<bool> IsApprovedAsync(long workflowInstanceId, CancellationToken ct = default);
}
