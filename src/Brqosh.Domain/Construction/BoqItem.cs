namespace Brqosh.Domain.Construction;

/// <summary>بند في جدول الكميات (BOQ) — مملوك للمشروع.</summary>
public class BoqItem
{
    public string ItemNumber { get; private set; }
    public string Description { get; private set; }
    public string Unit { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal Amount => Math.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);

    private BoqItem()
    {
        ItemNumber = null!;
        Description = null!;
        Unit = null!;
    }

    internal BoqItem(string itemNumber, string description, string unit, decimal quantity, decimal unitPrice)
    {
        ItemNumber = itemNumber;
        Description = description;
        Unit = unit;
        Quantity = quantity;
        UnitPrice = Math.Round(unitPrice, 2, MidpointRounding.AwayFromZero);
    }
}
