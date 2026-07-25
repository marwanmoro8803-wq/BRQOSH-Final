using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;

namespace Brqosh.Domain.Manufacturing;

public enum WorkOrderStatus
{
    Draft = 0,
    Released = 1,
    Completed = 2,
    Cancelled = 3,
}

/// <summary>أمر إنتاج — يُنفَّذ من BOM ويُرحَّل عند الاكتمال.</summary>
public class WorkOrder : IBranchScoped
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }
    public long BomId { get; private set; }
    public string? OrderNumber { get; private set; }
    public DateOnly OrderDate { get; private set; }
    public decimal PlannedQuantity { get; private set; }
    public decimal ProducedQuantity { get; private set; }
    public WorkOrderStatus Status { get; private set; } = WorkOrderStatus.Draft;
    public long? CompletionJournalEntryId { get; private set; }
    public string Notes { get; private set; }

    private WorkOrder()
    {
        Notes = null!;
    }

    private WorkOrder(long companyId, long bomId, DateOnly orderDate, decimal plannedQuantity, string notes)
    {
        CompanyId = companyId;
        BomId = bomId;
        OrderDate = orderDate;
        PlannedQuantity = plannedQuantity;
        Notes = notes;
    }

    public static WorkOrder CreateDraft(long companyId, long bomId, DateOnly orderDate,
        decimal plannedQuantity, string notes = "")
    {
        if (bomId <= 0)
            throw new AccountingException("قائمة المواد مطلوبة.");
        if (plannedQuantity <= 0)
            throw new AccountingException("الكمية المخططة يجب أن تكون موجبة.");

        return new WorkOrder(companyId, bomId, orderDate,
            Math.Round(plannedQuantity, 4, MidpointRounding.AwayFromZero), (notes ?? "").Trim());
    }

    public void Release(string orderNumber)
    {
        EnsureStatus(WorkOrderStatus.Draft, "إصدار الأمر");
        if (string.IsNullOrWhiteSpace(orderNumber))
            throw new AccountingException("رقم أمر الإنتاج مطلوب.");
        OrderNumber = orderNumber.Trim();
        Status = WorkOrderStatus.Released;
    }

    public void Complete(decimal producedQuantity)
    {
        EnsureStatus(WorkOrderStatus.Released, "إكمال الأمر");
        if (producedQuantity <= 0)
            throw new AccountingException("الكمية المنتجة يجب أن تكون موجبة.");
        ProducedQuantity = Math.Round(producedQuantity, 4, MidpointRounding.AwayFromZero);
        Status = WorkOrderStatus.Completed;
    }

    public void Cancel()
    {
        if (Status is WorkOrderStatus.Completed or WorkOrderStatus.Cancelled)
            throw new AccountingException("لا يمكن إلغاء الأمر في حالته الحالية.");
        Status = WorkOrderStatus.Cancelled;
    }

    /// <summary>إلغاء أمر مكتمل بعد عكس القيود والمخزون (ACC-007).</summary>
    public void MarkCancelledAfterCompletion()
    {
        if (Status != WorkOrderStatus.Completed)
            throw new AccountingException("هذه العملية للأوامر المكتملة فقط.");
        Status = WorkOrderStatus.Cancelled;
    }

    public void LinkCompletionJournal(long journalEntryId) => CompletionJournalEntryId = journalEntryId;

    private void EnsureStatus(WorkOrderStatus expected, string operation)
    {
        if (Status != expected)
            throw new AccountingException($"{operation} غير مسموح — الحالة: {StatusLabel(Status)}.");
    }

    public static string StatusLabel(WorkOrderStatus status) => status switch
    {
        WorkOrderStatus.Draft => "مسودة",
        WorkOrderStatus.Released => "صادر",
        WorkOrderStatus.Completed => "مكتمل",
        WorkOrderStatus.Cancelled => "ملغاة",
        _ => status.ToString(),
    };
    public void AssignBranch(long branchId)
    {
        if (branchId <= 0) throw new AccountingException("الفرع غير صالح.");
        EnsureStatus(WorkOrderStatus.Draft, "تعيين الفرع");
        BranchId = branchId;
    }

}
