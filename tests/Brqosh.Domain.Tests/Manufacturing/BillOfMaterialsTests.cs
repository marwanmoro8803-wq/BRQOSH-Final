using Brqosh.Domain.Accounting;
using Brqosh.Domain.Manufacturing;

namespace Brqosh.Domain.Tests.Manufacturing;

public class BillOfMaterialsTests
{
    [Fact]
    public void ReplaceLines_UpdatesComponents()
    {
        var bom = BillOfMaterials.Create(1, "BOM-01", "قائمة مواد", finishedProductId: 10);
        bom.AddLine(20, 2m);
        bom.AddLine(30, 1m);
        Assert.Equal(2, bom.Lines.Count);

        bom.ReplaceLines([(40, 5m), (50, 3m)]);
        Assert.Equal(2, bom.Lines.Count);
        Assert.Equal(40, bom.Lines[0].ComponentProductId);
        Assert.Equal(5m, bom.Lines[0].Quantity);
    }

    [Fact]
    public void AddLine_RejectsFinishedProductAsComponent()
    {
        var bom = BillOfMaterials.Create(1, "BOM-01", "قائمة", finishedProductId: 10);
        Assert.Throws<AccountingException>(() => bom.AddLine(10, 1m));
    }
}
