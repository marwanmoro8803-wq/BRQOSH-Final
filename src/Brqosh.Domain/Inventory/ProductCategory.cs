using Brqosh.Domain.Accounting;

namespace Brqosh.Domain.Inventory;

/// <summary>
/// فئةُ الأصناف — تحمل حساباتِ الإيراد والمخزون وتكلفة المبيعات لأصنافها.
/// والحسابُ الفارغ يعني «الحسابَ العامّ» (النظاميّ)، فلا يتغيّر قيدٌ لمنشأةٍ لم تُعرّف فئات.
/// </summary>
public class ProductCategory
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public string Code { get; private set; } = "";
    public string NameAr { get; private set; } = "";
    public long? RevenueAccountId { get; private set; }
    public long? InventoryAccountId { get; private set; }
    public long? CogsAccountId { get; private set; }
    public bool IsActive { get; private set; } = true;

    private ProductCategory() { }

    public static ProductCategory Create(long companyId, string code, string nameAr,
        long? revenueAccountId = null, long? inventoryAccountId = null, long? cogsAccountId = null)
    {
        if (companyId <= 0) throw new AccountingException("المنشأة غير صالحة.");
        if (string.IsNullOrWhiteSpace(code)) throw new AccountingException("رمز الفئة مطلوب.");
        if (string.IsNullOrWhiteSpace(nameAr)) throw new AccountingException("اسم الفئة مطلوب.");
        var c = new ProductCategory { CompanyId = companyId, Code = code.Trim(), NameAr = nameAr.Trim() };
        c.SetAccounts(revenueAccountId, inventoryAccountId, cogsAccountId);
        return c;
    }

    public void Rename(string nameAr)
    {
        if (string.IsNullOrWhiteSpace(nameAr)) throw new AccountingException("اسم الفئة مطلوب.");
        NameAr = nameAr.Trim();
    }

    public void SetAccounts(long? revenueAccountId, long? inventoryAccountId, long? cogsAccountId)
    {
        if (revenueAccountId is <= 0 || inventoryAccountId is <= 0 || cogsAccountId is <= 0)
            throw new AccountingException("حساب الفئة غير صالح.");
        RevenueAccountId = revenueAccountId;
        InventoryAccountId = inventoryAccountId;
        CogsAccountId = cogsAccountId;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
