using Brqosh.Domain.Accounting;

using Brqosh.Domain.Security;

using Brqosh.Domain.Workflow;

using Brqosh.Infrastructure.Persistence;

using Brqosh.Infrastructure.Security;

using Microsoft.EntityFrameworkCore;



namespace Brqosh.Infrastructure.Workflow;



public sealed class WorkflowEngine(BrqoshDbContext db) : IWorkflowEngine

{

    private static readonly IReadOnlyDictionary<WorkflowDocumentKind, WorkflowApproverSlot[]> Chains =

        new Dictionary<WorkflowDocumentKind, WorkflowApproverSlot[]>

        {

            [WorkflowDocumentKind.PurchaseRequest] =

                [WorkflowApproverSlot.Manager, WorkflowApproverSlot.Finance],

            [WorkflowDocumentKind.PurchaseOrder] =

                [WorkflowApproverSlot.Manager, WorkflowApproverSlot.Finance],

            [WorkflowDocumentKind.PaymentVoucher] =

                [WorkflowApproverSlot.Finance, WorkflowApproverSlot.Administrator],

            [WorkflowDocumentKind.ManualJournalEntry] =

                [WorkflowApproverSlot.Accountant, WorkflowApproverSlot.Administrator],

        };



    public IReadOnlyList<WorkflowApproverSlot> GetChain(WorkflowDocumentKind kind) =>

        Chains.TryGetValue(kind, out var chain) ? chain : [];



    public async Task<WorkflowInstance> SubmitAsync(

        long companyId,

        WorkflowDocumentKind kind,

        long documentId,

        string submittedBy,

        CancellationToken ct = default)

    {

        if (GetChain(kind).Count == 0)

            throw new AccountingException("لا توجد سلسلة موافقات لهذا النوع.");



        var existing = await db.WorkflowInstances.FirstOrDefaultAsync(w =>

            w.CompanyId == companyId &&

            w.DocumentKind == kind &&

            w.DocumentId == documentId, ct);

        if (existing is not null)

            throw new AccountingException("يوجد سير عمل مسبق لهذا المستند.");



        var instance = WorkflowInstance.Start(companyId, kind, documentId, submittedBy);

        db.WorkflowInstances.Add(instance);

        await db.SaveChangesAsync(ct);

        return instance;

    }



    public async Task<WorkflowInstance> ApproveCurrentStepAsync(

        long workflowInstanceId,

        string approver,

        string? comment = null,

        CancellationToken ct = default)

    {

        var instance = await db.WorkflowInstances

            .Include(w => w.Approvals)

            .SingleAsync(w => w.Id == workflowInstanceId, ct);



        var chain = GetChain(instance.DocumentKind);

        var slot = instance.CurrentRequiredSlot(chain)

            ?? throw new AccountingException("لا توجد خطوة موافقة معلّقة.");



        EnsureCanApprove(instance.DocumentKind, slot);

        instance.ApproveStep(slot, approver, comment, chain);

        await db.SaveChangesAsync(ct);

        return instance;

    }



    public async Task<WorkflowInstance> RejectAsync(

        long workflowInstanceId,

        string rejectedBy,

        string reason,

        CancellationToken ct = default)

    {

        // الرفض يتطلّب ما تتطلّبه الموافقة — وكان سائباً تماماً.
        //
        // الموافقة محروسة بـ EnsureCanApprove والرفض بلا فحصٍ إطلاقاً، **في الملف نفسه**.
        // والاثنان يحسمان مصير المستند بالقدر ذاته: أيّ دور — بما فيه «مُطّلع» — كان يستطيع
        // رفض أمر شراءٍ أو سند صرفٍ أو قيد يوميّة نهائياً. وهو حرمانُ خدمةٍ ماليّ: تعطيل دورة
        // الشراء والصرف كلّها بيد مستخدمٍ لا يملك إلا القراءة. والأثر كان يتدفّق إلى أربع
        // خدماتٍ فوقها، ولا حارس في أيٍّ منها.
        var instance = await db.WorkflowInstances
            .Include(w => w.Approvals)
            .SingleAsync(w => w.Id == workflowInstanceId, ct);
        var chain = GetChain(instance.DocumentKind);
        var slot = instance.CurrentRequiredSlot(chain)
            ?? throw new AccountingException("لا توجد خطوة موافقة معلّقة.");
        EnsureCanApprove(instance.DocumentKind, slot);

        instance.Reject(rejectedBy, reason);

        await db.SaveChangesAsync(ct);

        return instance;

    }



    public async Task<bool> IsApprovedAsync(long workflowInstanceId, CancellationToken ct = default)

    {

        var instance = await db.WorkflowInstances.SingleAsync(w => w.Id == workflowInstanceId, ct);

        return instance.Status == WorkflowInstanceStatus.Approved;

    }



    internal static void EnsureCanApprove(WorkflowDocumentKind kind, WorkflowApproverSlot slot)

    {

        var role = SecurityContext.CurrentRole;

        if (role is null)

            throw new AccountingException("لا يوجد مستخدم مسجّل — الموافقة مرفوضة.");



        var allowed = (kind, slot) switch

        {

            (WorkflowDocumentKind.PurchaseRequest or WorkflowDocumentKind.PurchaseOrder, WorkflowApproverSlot.Manager) =>

                role is UserRole.Accountant or UserRole.Administrator,

            (WorkflowDocumentKind.PurchaseRequest or WorkflowDocumentKind.PurchaseOrder, WorkflowApproverSlot.Finance) =>

                role is UserRole.Administrator,



            (WorkflowDocumentKind.PaymentVoucher, WorkflowApproverSlot.Finance) =>

                role is UserRole.Accountant or UserRole.Administrator,

            (WorkflowDocumentKind.PaymentVoucher, WorkflowApproverSlot.Administrator) =>

                role is UserRole.Administrator,



            (WorkflowDocumentKind.ManualJournalEntry, WorkflowApproverSlot.Accountant) =>

                role is UserRole.Accountant or UserRole.Administrator,

            (WorkflowDocumentKind.ManualJournalEntry, WorkflowApproverSlot.Administrator) =>

                role is UserRole.Administrator,



            _ => false,

        };



        if (!allowed)

            throw new AccountingException($"دورك الحالي لا يملك موافقة {slot}.");

    }

}


