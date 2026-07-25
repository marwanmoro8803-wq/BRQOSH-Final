namespace Brqosh.Domain.Inventory;

/// <summary>طريقة تسعير تكلفة المخزون عند الصرف.</summary>
public enum InventoryCostingMethod
{
    /// <summary>متوسط التكلفة المرجح على مستوى الشركة.</summary>
    WeightedAverage = 0,

    /// <summary>الوارد أولاً يُصرف أولاً — طبقات تكلفة.</summary>
    Fifo = 1,
}
