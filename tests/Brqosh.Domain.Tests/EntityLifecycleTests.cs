using Brqosh.Domain.Accounting;
using Brqosh.Domain.Assets;
using Brqosh.Domain.Manufacturing;
using Brqosh.Domain.Treasury;

namespace Brqosh.Domain.Tests;

public class EntityLifecycleTests
{
    [Fact]
    public void BankAccount_Update_ChangesFields()
    {
        var bank = BankAccount.Create(1, "B01", "حساب الراجحي", "الراجحي", "123", "SA123", 10);
        bank.Update("حساب محدّث", "الأهلي", "456", "SA456", true);
        Assert.Equal("حساب محدّث", bank.NameAr);
        Assert.Equal("الأهلي", bank.BankName);
        Assert.True(bank.IsDefault);
    }

    [Fact]
    public void FixedAsset_MarkDisposed_WhenFullyDepreciated()
    {
        var asset = FixedAsset.Create(1, "FA01", "سيارة", new DateOnly(2024, 1, 1),
            12000m, 0m, 12, 100, 101);
        for (var i = 0; i < 12; i++)
            asset.ApplyDepreciation();
        asset.EnsureReadyForDisposal();
        asset.MarkDisposed();
        Assert.False(asset.IsActive);
    }

    [Fact]
    public void FixedAsset_EnsureReadyForDisposal_Throws_WhenBookValueRemains()
    {
        var asset = FixedAsset.Create(1, "FA02", "معدات", new DateOnly(2024, 1, 1),
            10000m, 0m, 60, 100, 101);
        Assert.Throws<AccountingException>(() => asset.EnsureReadyForDisposal());
    }

    [Fact]
    public void BillOfMaterials_Rename_And_Deactivate()
    {
        var bom = BillOfMaterials.Create(1, "BOM1", "قائمة 1", 5);
        bom.Rename("قائمة محدّثة");
        bom.Deactivate();
        Assert.Equal("قائمة محدّثة", bom.NameAr);
        Assert.False(bom.IsActive);
    }

    [Fact]
    public void WorkOrder_Cancel_FromDraft()
    {
        var wo = WorkOrder.CreateDraft(1, 1, DateOnly.FromDateTime(DateTime.Today), 10);
        wo.Cancel();
        Assert.Equal(WorkOrderStatus.Cancelled, wo.Status);
    }
}
