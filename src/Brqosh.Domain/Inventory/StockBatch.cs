using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;

namespace Brqosh.Domain.Inventory;

/// <summary>
/// (CEEF-230 · ط1-٣) <b>رصيدُ دفعةٍ واحدةٍ من صنفٍ في مستودعٍ واحد.</b>
///
/// <para>وهو <b>تفصيلٌ لرصيد المخزون لا بديلٌ عنه</b>: <see cref="InventoryBalance"/> يبقى
/// مصدرَ الكمّية الإجمالية، وهذا يفصّلها على الدفعات. <b>والهويّةُ هـ-٩ تربطهما</b>: مجموعُ
/// دفعات صنفٍ في مستودعٍ يساوي رصيدَه فيه — <b>وانفصالُهما هو العطلُ الذي تحرسه</b>، من
/// صنف انفصال الدفتر المساعد عن الأستاذ في <c>هـ-٣</c>.</para>
///
/// <para><b>ولماذا كيانٌ مستقلٌّ عن طبقات التكلفة</b> (<see cref="InventoryCostLayer"/>):
/// الطبقةُ وحدةُ <b>تكلفة</b> تُنشأ بكلّ استلامٍ وتُستهلك بـFIFO، والدفعةُ وحدةُ <b>هويّةٍ
/// مادّية</b> يجتمع فيها استلامان بالرقم نفسِه. فدمجُهما يُخرج دفعةً تنقسم بلا سببٍ مادّيّ،
/// أو طبقةً تُستهلك بغير ترتيب تكلفتها.</para>
/// </summary>
public class StockBatch : IBranchScoped
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }
    public long WarehouseId { get; private set; }
    public long ProductId { get; private set; }

    /// <summary>رقمُ الدفعة — أو الرقمُ التسلسليّ حين يكون التتبّعُ سريالاً.</summary>
    public string BatchNumber { get; private set; }

    /// <summary>تاريخُ انتهاء الصلاحية — اختياريّ: ليس لكلّ دفعةٍ صلاحية.</summary>
    public DateOnly? ExpiryDate { get; private set; }

    public decimal Quantity { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    // EF Core
    private StockBatch() => BatchNumber = null!;

    private StockBatch(long companyId, long warehouseId, long productId,
        string batchNumber, DateOnly? expiryDate, decimal quantity)
    {
        CompanyId = companyId;
        WarehouseId = warehouseId;
        ProductId = productId;
        BatchNumber = batchNumber;
        ExpiryDate = expiryDate;
        Quantity = quantity;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static StockBatch Create(long companyId, long warehouseId, long productId,
        string batchNumber, DateOnly? expiryDate, decimal quantity = 0m)
    {
        if (string.IsNullOrWhiteSpace(batchNumber))
            throw new AccountingException("رقم الدفعة مطلوب.");
        if (quantity < 0)
            throw new AccountingException("كمية الدفعة لا تكون سالبة.");

        return new StockBatch(companyId, warehouseId, productId,
            batchNumber.Trim(), expiryDate, Math.Round(quantity, 4, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// يُحرّك كمّيةَ الدفعة.
    ///
    /// <para><b>ويمنع السالب في النطاق لا في الخدمة</b>: دفعةٌ برصيدٍ سالب تعني بضاعةً
    /// خرجت ولم تدخل، وهي حالةٌ لا معنى لها مادّياً — فلا يُترك منعُها لمستدعٍ قد يُنسى.</para>
    /// </summary>
    public void ApplyDelta(decimal delta)
    {
        var next = Math.Round(Quantity + delta, 4, MidpointRounding.AwayFromZero);
        if (next < 0)
            throw new AccountingException(
                $"رصيدُ الدفعة «{BatchNumber}» غير كافٍ — المتوفر {Quantity:N4} والمطلوب {-delta:N4}.");
        Quantity = next;
    }

    /// <summary>
    /// يُثبّت تاريخَ الصلاحية عند أوّل إسنادٍ له.
    ///
    /// <para><b>ولا يُغيَّر بعد ذلك من استلامٍ لاحق</b>: دفعةٌ بالرقم نفسِه وتاريخٍ مختلف
    /// إمّا خطأُ إدخالٍ أو دفعتان مختلفتان أُعطيتا رقماً واحداً — <b>وكلاهما يُقال ولا
    /// يُبتلع</b>. وابتلاعُه يجعل صلاحيةَ ما في المستودع تتغيّر بعد دخوله.</para>
    /// </summary>
    public void EnsureExpiry(DateOnly? expiryDate)
    {
        if (expiryDate is null) return;

        if (ExpiryDate is null)
        {
            ExpiryDate = expiryDate;
            return;
        }

        if (ExpiryDate != expiryDate)
            throw new AccountingException(
                $"الدفعة «{BatchNumber}» مسجَّلةٌ بصلاحية {ExpiryDate:yyyy-MM-dd} "
                + $"ووردت بصلاحية {expiryDate:yyyy-MM-dd} — راجعْ رقمَ الدفعة أو تاريخَها.");
    }

    /// <summary>منتهيةٌ في التاريخ المذكور — والمساواةُ ليست انتهاءً: يومُ الانتهاء صالح.</summary>
    public bool IsExpiredOn(DateOnly date) => ExpiryDate is DateOnly d && d < date;

    public void AssignBranch(long branchId)
    {
        if (BranchId is null) BranchId = branchId;
    }
}
