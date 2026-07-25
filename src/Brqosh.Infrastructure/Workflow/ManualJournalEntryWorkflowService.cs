using Brqosh.Domain.Accounting;

using Brqosh.Domain.Workflow;

using Brqosh.Infrastructure.Persistence;

using Brqosh.Infrastructure.Security;

using Microsoft.EntityFrameworkCore;



namespace Brqosh.Infrastructure.Workflow;



public sealed class ManualJournalEntryWorkflowService(BrqoshDbContext db, IWorkflowEngine workflow)

{

    public async Task<JournalEntry> SubmitAsync(JournalEntry entry, CancellationToken ct = default)

    {

        if (!entry.IsManual)

            throw new AccountingException("سير العمل للقيود اليدوية فقط.");



        var submitter = SecurityContext.ResolveUsername();

        var instance = await workflow.SubmitAsync(

            entry.CompanyId, WorkflowDocumentKind.ManualJournalEntry, entry.Id, submitter, ct);



        entry.MarkSubmittedForApproval(instance.Id);

        await db.SaveChangesAsync(ct);

        return entry;

    }



    public async Task<JournalEntry> ApproveAsync(long journalEntryId, string? comment = null, CancellationToken ct = default)

    {

        var entry = await db.JournalEntries.SingleAsync(e => e.Id == journalEntryId, ct);

        if (entry.WorkflowInstanceId is not long wfId)

            throw new AccountingException("قيد بلا سير عمل.");



        await workflow.ApproveCurrentStepAsync(wfId, SecurityContext.ResolveUsername(), comment, ct);

        await db.SaveChangesAsync(ct);

        return entry;

    }



    public async Task<JournalEntry> RejectAsync(long journalEntryId, string reason, CancellationToken ct = default)

    {

        var entry = await db.JournalEntries.SingleAsync(e => e.Id == journalEntryId, ct);

        if (entry.WorkflowInstanceId is not long wfId)

            throw new AccountingException("قيد بلا سير عمل.");



        await workflow.RejectAsync(wfId, SecurityContext.ResolveUsername(), reason, ct);

        entry.MarkRejected();

        await db.SaveChangesAsync(ct);

        return entry;

    }



    public async Task EnsureWorkflowApprovedAsync(JournalEntry entry, CancellationToken ct = default)

    {

        if (entry.WorkflowInstanceId is not long wfId)

            return;



        if (!await workflow.IsApprovedAsync(wfId, ct))

            throw new AccountingException("القيد اليدوي بانتظار اكتمال سلسلة الموافقات.");

    }

}


