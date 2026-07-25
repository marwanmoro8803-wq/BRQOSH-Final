using Brqosh.Domain.Accounting;

using Brqosh.Domain.Purchasing;

using Brqosh.Domain.Workflow;

using Brqosh.Infrastructure.Persistence;

using Brqosh.Infrastructure.Security;

using Microsoft.EntityFrameworkCore;



namespace Brqosh.Infrastructure.Workflow;



public sealed class PurchaseOrderWorkflowService(BrqoshDbContext db, IWorkflowEngine workflow)

{

    public async Task<PurchaseOrder> SubmitAsync(PurchaseOrder order, CancellationToken ct = default)

    {

        if (order.Status != PurchaseOrderStatus.Draft)

            throw new AccountingException("يُرسَل الأمر من مسودة فقط.");



        var submitter = SecurityContext.ResolveUsername();

        var instance = await workflow.SubmitAsync(

            order.CompanyId, WorkflowDocumentKind.PurchaseOrder, order.Id, submitter, ct);



        order.MarkSubmitted(instance.Id);

        await db.SaveChangesAsync(ct);

        return order;

    }



    public async Task<PurchaseOrder> ApproveAsync(long purchaseOrderId, string orderNumber, string? comment = null, CancellationToken ct = default)

    {

        var order = await db.PurchaseOrders.SingleAsync(o => o.Id == purchaseOrderId, ct);

        if (order.WorkflowInstanceId is not long wfId)

            throw new AccountingException("أمر بلا سير عمل.");



        var instance = await workflow.ApproveCurrentStepAsync(wfId, SecurityContext.ResolveUsername(), comment, ct);

        if (instance.Status == WorkflowInstanceStatus.Approved)

            order.MarkWorkflowApproved(orderNumber);



        await db.SaveChangesAsync(ct);

        return order;

    }



    public async Task<PurchaseOrder> RejectAsync(long purchaseOrderId, string reason, CancellationToken ct = default)

    {

        var order = await db.PurchaseOrders.SingleAsync(o => o.Id == purchaseOrderId, ct);

        if (order.WorkflowInstanceId is not long wfId)

            throw new AccountingException("أمر بلا سير عمل.");



        await workflow.RejectAsync(wfId, SecurityContext.ResolveUsername(), reason, ct);

        order.MarkRejected();

        await db.SaveChangesAsync(ct);

        return order;

    }

}


