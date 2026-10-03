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

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٧٧ · <c>manufacturing#5</c>) <b>بندٌ من وصفة الأمر كما كان يومَ صدوره</b> — مكوّنٌ وكمّيّتُه
/// لإنتاج <c>OutputQuantity</c> من قائمة الموادّ.
/// </summary>
public class WorkOrderMaterial
{
    public long ComponentProductId { get; private set; }
    public decimal Quantity { get; private set; }

    private WorkOrderMaterial() { }

    internal WorkOrderMaterial(long componentProductId, decimal quantity)
    {
        ComponentProductId = componentProductId;
        Quantity = quantity;
    }
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

    private readonly List<WorkOrderMaterial> _materials = [];

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٧٧ · <c>manufacturing#5</c>) <b>وصفةُ الأمر — تُجمَّد عند إصداره، ولا تتبع القائمةَ بعده.</b>
    ///
    /// <para><b>ما كان:</b> الإكمالُ يقرأ بنودَ قائمة الموادّ وتكاليفَ تحويلها <b>يومَ الإكمال</b>. فقائمةٌ تُعدَّل بين
    /// الإصدار والإكمال <b>تُغيّر ما يستهلكه أمرٌ صادرٌ وبكم يدخل تامُّه — بلا أثر</b>: أمرٌ صدر لثلاث وحداتٍ من الخامة
    /// للوحدة يُكمَل بأربع لأنّ أحداً عدّل القائمةَ لأوامرَ قادمة.</para>
    ///
    /// <para><b>والإصدارُ هو الالتزام</b> — عنده يُحجَز الرقمُ ويبدأ التنفيذ؛ فالمسودّةُ تتبع القائمةَ حتى تصدر.</para>
    /// </summary>
    public IReadOnlyList<WorkOrderMaterial> Materials => _materials.AsReadOnly();

    /// <summary>(ح-٧٧) أجرُ الوحدة كما كان في القائمة يومَ الإصدار.</summary>
    public decimal RecipeLabourCostPerUnit { get; private set; }

    /// <summary>(ح-٧٧) نسبةُ تحميل التكاليف الصناعيّة كما كانت يومَ الإصدار.</summary>
    public decimal RecipeOverheadRatePercent { get; private set; }

    /// <summary>
    /// (ح-٧٧) <b>جُمِّدت الوصفة</b> — بالإصدار، أو بالمُرقِّع لأمرٍ صدر قبل الترقية. <b>وأمرٌ صادرٌ بلا وصفةٍ مجمَّدة
    /// لا يُكمَل</b>: لا يُستكمَل من قائمة اليوم.
    /// </summary>
    public bool RecipeFrozen { get; private set; }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٩٧ · <c>manufacturing#7</c>) <b>مستودعُ الموادّ — منه تُصرف الخامات.</b> وفارغُه هو المستودعُ الافتراضيّ
    /// كما كان كلُّ أمرٍ قبله؛ <b>والاستلامُ والجردُ والتحويلُ تختار أيَّ مستودع</b>، فالتصنيعُ وحدَه كان حبيسَ الافتراضيّ.
    /// </summary>
    public long? MaterialsWarehouseId { get; private set; }

    /// <summary>(ح-٩٧) <b>مستودعُ الإنتاج — إليه يُسلَّم التامّ.</b> وفارغُه الافتراضيّ.</summary>
    public long? OutputWarehouseId { get; private set; }

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

    /// <summary>
    /// إصدارُ الأمر — <b>وتجميدُ وصفته من قائمة موادّه</b> (ح-٧٧): البنودُ وتكاليفُ التحويل كما هي الآن، فتعديلُ
    /// القائمة بعده لا يمسّه. <b>ولا إصدارَ بلا القائمة</b> — فلا طريقَ إلى أمرٍ صادرٍ بلا وصفة.
    /// </summary>
    public void Release(string orderNumber, BillOfMaterials bom)
    {
        EnsureStatus(WorkOrderStatus.Draft, "إصدار الأمر");
        if (string.IsNullOrWhiteSpace(orderNumber))
            throw new AccountingException("رقم أمر الإنتاج مطلوب.");
        ArgumentNullException.ThrowIfNull(bom);
        if (bom.Id != BomId)
            throw new AccountingException("قائمة المواد لا تخصّ هذا الأمر.");

        _materials.Clear();
        foreach (var line in bom.Lines)
            _materials.Add(new WorkOrderMaterial(line.ComponentProductId, line.Quantity));
        RecipeLabourCostPerUnit = bom.LabourCostPerUnit;
        RecipeOverheadRatePercent = bom.OverheadRatePercent;
        RecipeFrozen = true;

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

    /// <summary>
    /// (ح-٩٧) يضبط مستودعَي الأمر — <b>والفارغُ هو الافتراضيّ</b>. <b>ولا يُغيَّر لأمرٍ مكتملٍ أو ملغى</b>: حركاتُه مرحَّلةٌ في
    /// مستودعاتها، والإلغاءُ يعكس كلَّ حركةٍ في مستودعها هي. <b>والمستودعُ نفسُه يُسأل عند الإكمال</b> (في المنشأة وغيرُ موقوف).
    /// </summary>
    public void SetWarehouses(long? materialsWarehouseId, long? outputWarehouseId)
    {
        if (Status is WorkOrderStatus.Completed or WorkOrderStatus.Cancelled)
            throw new AccountingException("لا يُغيَّر مستودعُ أمرٍ مكتملٍ أو ملغى.");
        if (materialsWarehouseId is <= 0 || outputWarehouseId is <= 0)
            throw new AccountingException("المستودع غير صالح.");

        MaterialsWarehouseId = materialsWarehouseId;
        OutputWarehouseId = outputWarehouseId;
    }

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
