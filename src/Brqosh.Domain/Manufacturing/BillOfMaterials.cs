using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;

namespace Brqosh.Domain.Manufacturing;

/// <summary>بند في قائمة المواد — مكوّن وكميته لإنتاج وحدة واحدة من المنتج النهائي.</summary>
public class BomLine
{
    public long ComponentProductId { get; private set; }
    public decimal Quantity { get; private set; }

    private BomLine() { }

    internal BomLine(long componentProductId, decimal quantity)
    {
        ComponentProductId = componentProductId;
        Quantity = quantity;
    }
}

/// <summary>قائمة مواد (BOM) — تربط منتجاً نهائياً بمكوّناته.</summary>
public class BillOfMaterials : IBranchScoped, ISoftDeletable
{
    private readonly List<BomLine> _lines = [];

    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }
    public string Code { get; private set; }
    public string NameAr { get; private set; }
    public long FinishedProductId { get; private set; }
    public decimal OutputQuantity { get; private set; } = 1m;
    public bool IsActive { get; private set; } = true;
    public DateTime? DeletedAtUtc { get; private set; }

    public IReadOnlyList<BomLine> Lines => _lines.AsReadOnly();

    private BillOfMaterials()
    {
        Code = null!;
        NameAr = null!;
    }

    private BillOfMaterials(long companyId, string code, string nameAr, long finishedProductId, decimal outputQuantity)
    {
        CompanyId = companyId;
        Code = code;
        NameAr = nameAr;
        FinishedProductId = finishedProductId;
        OutputQuantity = outputQuantity;
    }

    public static BillOfMaterials Create(long companyId, string code, string nameAr, long finishedProductId,
        decimal outputQuantity = 1m)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new AccountingException("كود BOM مطلوب.");
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new AccountingException("اسم BOM مطلوب.");
        if (finishedProductId <= 0)
            throw new AccountingException("المنتج النهائي مطلوب.");
        if (outputQuantity <= 0)
            throw new AccountingException("كمية الإنتاج يجب أن تكون موجبة.");

        return new BillOfMaterials(companyId, code.Trim().ToUpperInvariant(), nameAr.Trim(),
            finishedProductId, outputQuantity);
    }

    public void AddLine(long componentProductId, decimal quantity)
    {
        if (componentProductId <= 0)
            throw new AccountingException("المكوّن مطلوب.");
        if (quantity <= 0)
            throw new AccountingException("كمية المكوّن يجب أن تكون موجبة.");
        if (componentProductId == FinishedProductId)
            throw new AccountingException("لا يمكن أن يكون المكوّن هو المنتج النهائي.");

        _lines.Add(new BomLine(componentProductId, quantity));
    }

    public void ClearLines() => _lines.Clear();

    public void ReplaceLines(IEnumerable<(long ComponentProductId, decimal Quantity)> lines)
    {
        _lines.Clear();
        foreach (var (productId, qty) in lines)
            AddLine(productId, qty);
    }

    public void Rename(string nameAr)
    {
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new AccountingException("اسم BOM مطلوب.");
        NameAr = nameAr.Trim();
    }

    /// <summary>
    /// إيقاف قائمة المواد — <b>حالةٌ تشغيلية لا واقعةُ حذف</b>.
    ///
    /// <para>(CEEF-229 · م2-١٧) كان يضبط <c>DeletedAtUtc</c> أيضاً، ومرشّح الاستعلام العامّ
    /// يُخفي كلَّ ما له تاريخ حذف — <b>فيختفي من قوائمه تماماً</b> لا يظهر موقوفاً. وزرُّ
    /// الشاشة مكتوبٌ عليه «إيقاف» ورسالةُ التأكيد تسأل «إيقاف …؟»، فكان الوعد إيقافاً
    /// والفعلُ حذفاً. ولا رجعة: لا <c>Reactivate</c> في الكيان أصلاً.</para>
    ///
    /// <para>أُصلح هذا في <see cref="Brqosh.Domain.Accounting.Account"/> ضمن م0-١٣، وبقيت
    /// سبعةُ كياناتٍ أخرى بالجسم القديم حرفاً بحرف — فالإصلاح طُبِّق على كيانٍ واحد ولم يُعمَّم.</para>
    /// </summary>
    public void Deactivate() => IsActive = false;

    /// <summary>إعادة تفعيل قائمة المواد الموقوف.</summary>
    public void Reactivate() => IsActive = true;
    public void AssignBranch(long branchId)
    {
        if (branchId <= 0) throw new AccountingException("الفرع غير صالح.");
        BranchId = branchId;
    }

}
