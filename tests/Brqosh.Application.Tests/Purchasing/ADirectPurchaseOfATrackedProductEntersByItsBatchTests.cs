using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Purchasing;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٠٨ · <c>order-to-cash-procure-to-pay-inventory#7</c>) <b>الشراءُ المباشر لصنفٍ متتبَّع يدخل بدفعته.</b>
///
/// <para>فاتورةٌ بلا أمر تُرحِّل حركةَ استلامها بنفسها، وكانت بالتحميل القديم الذي يُسقط الدفعةَ فيرفض المَخنَقُ الصنفَ المتتبَّع —
/// فلا تشتري صيدليةٌ دواءً بفاتورةٍ مباشرة. <b>والسطرُ يحمل الدفعةَ وانتهاءها الآن</b> إلى الحركة.</para>
/// </summary>
public sealed class ADirectPurchaseOfATrackedProductEntersByItsBatchTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _supplierId;
    private long _trackedId;
    private long _plainId;
    private int _references;

    public ADirectPurchaseOfATrackedProductEntersByItsBatchTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary><b>القياسُ الحاكم:</b> دواءٌ متتبَّع بدفعة LOT-A وانتهائها ⟵ فاتورةٌ مباشرة تُرحَّل، ودفعةُ المخزون كميّتُها عشر وانتهاؤها المسجَّل.</summary>
    [Fact]
    public async Task ATrackedLine_EntersStockUnderItsBatchWithItsExpiry()
    {
        await SetUpAsync();
        var invoice = Draft();
        invoice.AddLine("دواء", 10m, 10m, _trackedId, batchNumber: " LOT-A ", expiryDate: new DateOnly(2027, 1, 1));

        await new PurchaseInvoicePostingService(_db).PostAsync(invoice);

        _db.ChangeTracker.Clear();
        var batch = await _db.StockBatches.AsNoTracking().SingleAsync(b => b.ProductId == _trackedId);
        Assert.Equal("LOT-A", batch.BatchNumber);
        Assert.Equal(10m, batch.Quantity);
        Assert.Equal(new DateOnly(2027, 1, 1), batch.ExpiryDate);
        var saved = await _db.PurchaseInvoices.AsNoTracking().SingleAsync(i => i.Id == invoice.Id);
        Assert.Equal(new DateOnly(2027, 1, 1), saved.Lines[0].ExpiryDate);
        Assert.Equal("LOT-A", saved.Lines[0].BatchNumber);
    }

    /// <summary><b>دفعتان في فاتورةٍ واحدة</b> ⟵ دفعتان بكلٍّ كميّتُها وانتهاؤها.</summary>
    [Fact]
    public async Task TwoLines_EnterTwoBatches()
    {
        await SetUpAsync();
        var invoice = Draft();
        invoice.AddLine("دواء", 4m, 10m, _trackedId, batchNumber: "LOT-A", expiryDate: new DateOnly(2027, 1, 1));
        invoice.AddLine("دواء", 6m, 10m, _trackedId, batchNumber: "LOT-B", expiryDate: new DateOnly(2027, 6, 1));

        await new PurchaseInvoicePostingService(_db).PostAsync(invoice);

        var batches = await _db.StockBatches.AsNoTracking().Where(b => b.ProductId == _trackedId).ToListAsync();
        Assert.Equal(4m, batches.Single(b => b.BatchNumber == "LOT-A").Quantity);
        Assert.Equal(6m, batches.Single(b => b.BatchNumber == "LOT-B").Quantity);
        Assert.Equal(new DateOnly(2027, 6, 1), batches.Single(b => b.BatchNumber == "LOT-B").ExpiryDate);
    }

    /// <summary><b>سطرٌ متتبَّعٌ بلا دفعة</b> ⟵ يُرفض باسم الصنف ولا فاتورةَ تُرحَّل ولا رصيدَ يدخل.</summary>
    [Fact]
    public async Task ATrackedLineWithoutABatch_IsRefusedAndNothingIsPosted()
    {
        await SetUpAsync();
        var invoice = Draft();
        invoice.AddLine("دواء", 10m, 10m, _trackedId);

        var ex = await Assert.ThrowsAsync<AccountingException>(() => new PurchaseInvoicePostingService(_db).PostAsync(invoice));

        Assert.Contains("دواء", ex.Message, StringComparison.Ordinal);
        _db.ChangeTracker.Clear();
        Assert.False(await _db.PurchaseInvoices.AnyAsync(i => i.Status == PurchaseInvoiceStatus.Posted));
        Assert.False(await _db.StockBatches.AnyAsync());
    }

    /// <summary>انتهاءٌ بلا دفعةٍ لا معنى له ⟵ يُرفض عند إضافة السطر.</summary>
    [Fact]
    public async Task AnExpiryWithoutABatch_IsRefusedWhenTheLineIsAdded()
    {
        await SetUpAsync();
        var invoice = Draft();

        Assert.Throws<AccountingException>(() =>
            invoice.AddLine("دواء", 1m, 10m, _trackedId, batchNumber: "  ", expiryDate: new DateOnly(2027, 1, 1)));
    }

    /// <summary><b>وغيرُ المتتبَّع كما كان</b> — بلا دفعةٍ ولا دفعات في المخزون.</summary>
    [Fact]
    public async Task AnUntrackedLine_IsUnchanged()
    {
        await SetUpAsync();
        var invoice = Draft();
        invoice.AddLine("صنف", 10m, 10m, _plainId);

        await new PurchaseInvoicePostingService(_db).PostAsync(invoice);

        Assert.Equal(PurchaseInvoiceStatus.Posted, invoice.Status);
        Assert.False(await _db.StockBatches.AnyAsync());
    }

    /// <summary><b>وإلغاءُ الفاتورة يُخرج كميّةَ الدفعة</b> — الحركةُ تُعكس بدفعاتها.</summary>
    [Fact]
    public async Task CancellingTheInvoice_RemovesTheBatchQuantity()
    {
        await SetUpAsync();
        var invoice = Draft();
        invoice.AddLine("دواء", 10m, 10m, _trackedId, batchNumber: "LOT-A", expiryDate: new DateOnly(2027, 1, 1));
        await new PurchaseInvoicePostingService(_db).PostAsync(invoice);

        await new PurchaseInvoiceCancellationService(_db)
            .CancelAsync(_companyId, invoice.Id, new DateOnly(2026, 2, 2), "admin");

        _db.ChangeTracker.Clear();
        Assert.Equal(0m, (await _db.StockBatches.AsNoTracking().SingleAsync(b => b.BatchNumber == "LOT-A")).Quantity);
    }

    private PurchaseInvoice Draft() => PurchaseInvoice.CreateDraft(
        _companyId, _supplierId, new DateOnly(2026, 2, 1), $"SUP-{++_references}", vatDeductible: true);

    private async Task SetUpAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("صيدلية", "310123456700066", BusinessProfile.Healthcare);
        _companyId = company.Id;
        var supplier = Supplier.Create(_companyId, "مورد", "312222222200003");
        _db.Suppliers.Add(supplier);
        var tracked = Product.Create(_companyId, "P-LOT", "دواء", "علبة", ProductType.Stockable, salePrice: 20m);
        tracked.SetTracking(InventoryTracking.Batch, currentOnHand: 0m);
        var plain = Product.Create(_companyId, "P-PLAIN", "صنف", "قطعة", ProductType.Stockable, salePrice: 20m);
        _db.Products.AddRange(tracked, plain);
        await _db.SaveChangesAsync();
        _supplierId = supplier.Id;
        _trackedId = tracked.Id;
        _plainId = plain.Id;
    }
}
