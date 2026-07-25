using Brqosh.Domain.Accounting;

using Brqosh.Domain.Treasury;

using Brqosh.Domain.Workflow;

using Brqosh.Infrastructure.Persistence;

using Brqosh.Infrastructure.Security;

using Microsoft.EntityFrameworkCore;



namespace Brqosh.Infrastructure.Workflow;



public sealed class PaymentVoucherWorkflowService(BrqoshDbContext db, IWorkflowEngine workflow)

{

    public async Task<PaymentVoucher> SubmitAsync(PaymentVoucher voucher, CancellationToken ct = default)

    {

        if (voucher.Status != PaymentVoucherStatus.Draft)

            throw new AccountingException("يُرسَل السند من مسودة فقط.");



        var submitter = SecurityContext.ResolveUsername();

        var instance = await workflow.SubmitAsync(

            voucher.CompanyId, WorkflowDocumentKind.PaymentVoucher, voucher.Id, submitter, ct);



        voucher.MarkSubmitted(instance.Id);

        await db.SaveChangesAsync(ct);

        return voucher;

    }



    public async Task<PaymentVoucher> ApproveAsync(long paymentVoucherId, string? comment = null, CancellationToken ct = default)

    {

        var voucher = await db.PaymentVouchers.SingleAsync(v => v.Id == paymentVoucherId, ct);

        if (voucher.WorkflowInstanceId is not long wfId)

            throw new AccountingException("سند بلا سير عمل.");



        await workflow.ApproveCurrentStepAsync(wfId, SecurityContext.ResolveUsername(), comment, ct);

        await db.SaveChangesAsync(ct);

        return voucher;

    }



    public async Task<PaymentVoucher> RejectAsync(long paymentVoucherId, string reason, CancellationToken ct = default)

    {

        var voucher = await db.PaymentVouchers.SingleAsync(v => v.Id == paymentVoucherId, ct);

        if (voucher.WorkflowInstanceId is not long wfId)

            throw new AccountingException("سند بلا سير عمل.");



        await workflow.RejectAsync(wfId, SecurityContext.ResolveUsername(), reason, ct);

        voucher.MarkRejected();

        await db.SaveChangesAsync(ct);

        return voucher;

    }



    public async Task EnsureWorkflowApprovedAsync(PaymentVoucher voucher, CancellationToken ct = default)

    {

        if (voucher.WorkflowInstanceId is not long wfId)

            return;



        if (!await workflow.IsApprovedAsync(wfId, ct))

            throw new AccountingException("سند الصرف بانتظار اكتمال سلسلة الموافقات.");

    }

}


