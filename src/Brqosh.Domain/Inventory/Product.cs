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

    /// <summary>
    /// (CEEF-230 · ط1-٣) نمطُ التتبّع — <b>والأصلُ <c>None</c></b>، فلا يتغيّر سلوكُ أيّ
    /// صنفٍ قائم بإضافة هذه الميزة.
    /// </summary>
    public InventoryTracking Tracking { get; private set; } = InventoryTracking.None;

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

    /// <summary>
    /// إيقاف الصنف — <b>حالةٌ تشغيلية لا واقعةُ حذف</b>.
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

    /// <summary>إعادة تفعيل الصنف الموقوف.</summary>
    public void Reactivate() => IsActive = true;

    /// <summary>
    /// (CEEF-230 · ط1-٣) <b>يضبط نمطَ التتبّع — ويُرفض ما دام للصنف رصيد.</b>
    ///
    /// <para><b>ولماذا يُرفض:</b> صنفٌ في المستودع منه مئةُ حبّةٍ بلا دفعات، فإن صار
    /// «يُتتبَّع بالدفعة» صارت المئةُ <b>بضاعةً بلا هويّة</b>: لا رقمَ لها ولا صلاحية،
    /// فتنكسر <c>هـ-٩</c> في اللحظة نفسِها — مجموعُ الدفعات صفرٌ والرصيد مئة. والعكسُ
    /// كذلك: إلغاءُ التتبّع يترك دفعاتٍ برصيدٍ لا يقابله شيء.</para>
    ///
    /// <para><b>وهو مذهبُ <c>م1-٢٢</c> بعينه</b>: طريقةُ التكلفة لا تُبدَّل والمخزونُ قائم،
    /// لأنّ التبديلَ يُعيد تفسيرَ ما مضى. <b>والمخرجُ للمستخدم واحد</b>: يُصفّى الرصيدُ
    /// جرداً ثمّ يُضبط النمطُ ثمّ يُدخَل بدفعاته — لا التفافَ على الحارس.</para>
    /// </summary>
    public void SetTracking(InventoryTracking tracking, decimal currentOnHand)
    {
        if (Tracking == tracking) return;

        if (currentOnHand != 0m)
            throw new AccountingException(
                $"لا يمكن تغيير نمط تتبّع '{NameAr}' وله رصيدٌ قائم ({currentOnHand:N4}). "
                + "صفِّ الرصيدَ بجردٍ أوّلاً، ثمّ غيّر النمط، ثمّ أدخِلْه بدفعاته.");

        Tracking = tracking;
    }

    /// <summary>يُتتبَّع بدفعةٍ أو رقمٍ تسلسليّ — فكلُّ حركةٍ له تلزمها هويّة.</summary>
    public bool IsTracked => Tracking != InventoryTracking.None;
}
