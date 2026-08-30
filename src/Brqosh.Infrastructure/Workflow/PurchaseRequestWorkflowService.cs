using Brqosh.Domain.Accounting;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Workflow;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Workflow;

public sealed class PurchaseRequestWorkflowService(BrqoshDbContext db, IWorkflowEngine workflow)
{
    /// <summary>
    /// (CEEF-229 · م1-١٤) الرقمُ يُخصَّص داخل معاملة الكتابة لا في الشاشة — فقفلُ
    /// التسلسل مربوطٌ بالمعاملة، وخارجَها يُحرَّر فوراً فلا يحمي شيئاً.
    /// </summary>
    public async Task<PurchaseRequest> SubmitAsync(PurchaseRequest request, CancellationToken ct = default)
    {
        if (request.Status != PurchaseRequestStatus.Draft)
            throw new AccountingException("يُرسَل الطلب من مسودة فقط.");

        return await Persistence.UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
        var submitter = SecurityContext.ResolveUsername();
        var instance = await workflow.SubmitAsync(
            request.CompanyId, WorkflowDocumentKind.PurchaseRequest, request.Id, submitter, ct);

        var requestNumber = await Persistence.DocumentNumberGenerator.NextAsync(
            db, db.PurchaseRequests.Where(r => r.CompanyId == request.CompanyId).Select(r => r.RequestNumber),
            "PR", request.RequestDate.Year, request.CompanyId);
        request.MarkSubmitted(requestNumber, instance.Id);
        await db.SaveChangesAsync(token);
        return request;
        }, ct);
    }

    public async Task<PurchaseRequest> ApproveAsync(long purchaseRequestId, string? comment = null, CancellationToken ct = default)
    {
        var request = await db.PurchaseRequests.SingleAsync(r => r.Id == purchaseRequestId, ct);
        if (request.WorkflowInstanceId is not long wfId)
            throw new AccountingException("طلب بلا سير عمل.");

        var instance = await workflow.ApproveCurrentStepAsync(wfId, SecurityContext.ResolveUsername(), comment, ct);
        if (instance.Status == WorkflowInstanceStatus.Approved)
            request.MarkApproved();

        await db.SaveChangesAsync(ct);
        return request;
    }

    public async Task<PurchaseRequest> RejectAsync(long purchaseRequestId, string reason, CancellationToken ct = default)
    {
        var request = await db.PurchaseRequests.SingleAsync(r => r.Id == purchaseRequestId, ct);
        if (request.WorkflowInstanceId is not long wfId)
            throw new AccountingException("طلب بلا سير عمل.");

        await workflow.RejectAsync(wfId, SecurityContext.ResolveUsername(), reason, ct);
        request.MarkRejected();
        await db.SaveChangesAsync(ct);
        return request;
    }

    public async Task<PurchaseOrder> ConvertToPurchaseOrderAsync(
        long purchaseRequestId,
        CancellationToken ct = default)
    {
        return await Persistence.UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
        var request = await db.PurchaseRequests
            .Include(r => r.Lines)
            .SingleAsync(r => r.Id == purchaseRequestId, token);

        if (request.Status != PurchaseRequestStatus.Approved)
            throw new AccountingException("يُحوَّل الطلب المعتمد فقط.");

        var po = PurchaseOrder.CreateDraft(request.CompanyId, request.SupplierId, request.RequestDate, request.Justification);
        if (request.BranchId is long branchId)
            po.AssignBranch(branchId);

        foreach (var line in request.Lines)
            po.AddLine(line.Description, line.Quantity, line.UnitPrice, line.ProductId);

        db.PurchaseOrders.Add(po);
        await db.SaveChangesAsync(ct);

        var orderNumber = await Persistence.DocumentNumberGenerator.NextAsync(
            db, db.PurchaseOrders.Where(o => o.CompanyId == po.CompanyId).Select(o => o.OrderNumber),
            "PO", po.OrderDate.Year, po.CompanyId);
        po.Approve(orderNumber);
        request.MarkConvertedToPo(po.Id);
        await db.SaveChangesAsync(ct);
        return po;
        }, ct);
    }
}
