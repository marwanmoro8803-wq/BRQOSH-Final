using Brqosh.Domain.Accounting;

namespace Brqosh.Domain.Inventory;

/// <summary>
/// صنف/منتج — الوحدة الأساسية للمخزون والفوترة.
/// التكلفة المرجحة (WAC) تُحدَّث عند كل إدخال مخزني.
/// </summary>
public class Product : ISoftDeletable
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public string Code { get; private set; }
    public string NameAr { get; private set; }
    public string Unit { get; private set; }
    public ProductType Type { get; private set; }
    public decimal SalePrice { get; private set; }

    /// <summary>متوسط التكلفة المرجح — يُستخدم لحساب تكلفة المبيعات.</summary>
    public decimal WeightedAverageCost { get; private set; }

    public string Barcode { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? DeletedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    // EF Core
    private Product()
    {
        Code = null!;
        NameAr = null!;
        Unit = null!;
        Barcode = null!;
    }

    private Product(long companyId, string code, string nameAr, string unit, ProductType type,
        decimal salePrice, string barcode)
    {
        CompanyId = companyId;
        Code = code;
        NameAr = nameAr;
        Unit = unit;
        Type = type;
        SalePrice = salePrice;
        Barcode = barcode;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static Product Create(long companyId, string code, string nameAr, string unit,
        ProductType type, decimal salePrice = 0m, string barcode = "")
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new AccountingException("كود الصنف مطلوب.");
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new AccountingException("اسم الصنف مطلوب.");
        if (string.IsNullOrWhiteSpace(unit))
            throw new AccountingException("وحدة القياس مطلوبة (قطعة، كرتون، كيلو...).");
        if (salePrice < 0)
            throw new AccountingException("سعر البيع لا يكون سالباً.");

        return new Product(companyId, code.Trim().ToUpperInvariant(), nameAr.Trim(), unit.Trim(),
            type, Math.Round(salePrice, 2, MidpointRounding.AwayFromZero), (barcode ?? "").Trim());
    }

    public void Update(string nameAr, string unit, ProductType type, decimal salePrice, string barcode)
    {
        if (string.IsNullOrWhiteSpace(nameAr) || string.IsNullOrWhiteSpace(unit))
            throw new AccountingException("اسم الصنف ووحدة القياس مطلوبان.");
        NameAr = nameAr.Trim();
        Unit = unit.Trim();
        Type = type;
        SalePrice = Math.Round(salePrice, 2, MidpointRounding.AwayFromZero);
        Barcode = (barcode ?? "").Trim();
    }

    /// <summary>تحديث المتوسط المرجح بعد إدخال كمية جديدة.</summary>
    public void ApplyReceipt(decimal quantityIn, decimal unitCost)
    {
        if (!this.IsStockable())
            throw new AccountingException($"الصنف '{NameAr}' ليس مخزنياً.");
        if (quantityIn <= 0)
            throw new AccountingException("كمية الإدخال يجب أن تكون موجبة.");
        if (unitCost < 0)
            throw new AccountingException("تكلفة الوحدة لا تكون سالبة.");

        // WAC = (رصيد قديم × تكلفة قديمة + كمية جديدة × تكلفة جديدة) ÷ الرصيد الجديد
        // الرصيد الكلي يُمرَّر من الخدمة — هنا نحدّث التكلفة فقط بناءً على الكمية الإجمالية المعروفة
        WeightedAverageCost = unitCost; // placeholder — InventoryService يحسب WAC الصحيح
    }

    public void SetWeightedAverageCost(decimal wac) =>
        WeightedAverageCost = Math.Round(wac, 4, MidpointRounding.AwayFromZero);

    public void Deactivate()
    {
        if (DeletedAtUtc is not null) return;
        DeletedAtUtc = DateTime.UtcNow;
        IsActive = false;
    }
}
