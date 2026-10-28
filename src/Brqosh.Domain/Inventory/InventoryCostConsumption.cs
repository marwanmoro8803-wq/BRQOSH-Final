namespace Brqosh.Domain.Inventory;

/// <summary>
/// <b>سجلُّ ما استُهلك من أيّ طبقة — وبلا هذا لا يمكن ترميمُها.</b>
///
/// <para><b>لماذا وُجد:</b> كان الإلغاء يُنشئ طبقةً جديدة بتاريخه بدل إعادة ما استُهلك،
/// فتبقى الأصليةُ صفراً وتقع الجديدةُ <b>في آخر طابور FIFO</b>. وقيس الأثر:
/// بيعُ 100 من طبقةٍ بـ10 تكلفتُه 1,000؛ فإن أُلغي وأُعيد البيعُ نفسُه صارت التكلفة
/// <b>2,000</b> — لأنّه يأكل الطبقةَ الغالية. <b>ضعفُ الصواب، ومجملُ الربح ينهار إلى النصف.</b></para>
///
/// <para><b>ولا تكشفه الكمية:</b> المجموعُ يعود صحيحاً تماماً بعد الإلغاء. العطلُ في
/// <b>التكلفة</b> لا في العدد، ولا يظهر إلا في البيعة التالية.</para>
///
/// <para><b>والترميمُ يقتضي ذاكرة:</b> استهلاكُ FIFO يوزّع الكميةَ على طبقاتٍ عدّة بنسبٍ
/// مختلفة، ولا سبيل إلى عكسه بالتخمين. فيُسجَّل ما أُخذ من كلّ طبقة، مربوطاً بالمستند —
/// وهو ما يجعل الإلغاء <b>عكساً حقيقياً</b> لا حركةً مضادّة.</para>
///
/// <para>ونفعُه يتجاوز الإلغاء: يُجيب «أيُّ دفعةٍ خرجت في هذه الفاتورة؟» — وهو سؤالُ
/// التتبّع الذي تطلبه الرقابةُ على الأصناف ذات الصلاحية.</para>
/// </summary>
public class InventoryCostConsumption
{
    private InventoryCostConsumption() { }

    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long CostLayerId { get; private set; }
    public long ProductId { get; private set; }
    public long WarehouseId { get; private set; }

    /// <summary>المستندُ الذي استهلك — ومفتاحُ الترميم عند إلغائه.</summary>
    public string SourceDocument { get; private set; } = "";

    public decimal Quantity { get; private set; }
    public decimal UnitCost { get; private set; }
    public DateTime ConsumedAtUtc { get; private set; }

    public static InventoryCostConsumption Record(
        long companyId, long costLayerId, long productId, long warehouseId,
        string sourceDocument, decimal quantity, decimal unitCost, DateTime consumedAtUtc) =>
        new()
        {
            CompanyId = companyId,
            CostLayerId = costLayerId,
            ProductId = productId,
            WarehouseId = warehouseId,
            SourceDocument = sourceDocument,
            Quantity = quantity,
            UnitCost = unitCost,
            ConsumedAtUtc = consumedAtUtc,
        };
}
