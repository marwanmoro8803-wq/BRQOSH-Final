using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Reporting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Purchasing;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٠٧ · `found#7` · كشفه العمل) <b>مرتجعُ مشتريات مُسعَّرٌ دون تكلفته كان
/// مستحيلاً.</b>
///
/// <para><b>السيناريو:</b> استلامٌ وفوترةُ ١٠ وحداتٍ بعشرة ريالات (تكلفةُ الوحدة ١٠، مخزونٌ
/// ١٠٠)، ثمّ إشعارُ مدينٍ بإرجاع ٤ وحداتٍ <b>بسعر ٦</b> (المورّدُ يقبلها بائتةً بائتةً بثمنٍ
/// جزئيّ). الطبقاتُ تُستهلك بـ٤×١٠ = ٤٠، والإشعارُ يُدين المورّدَ بـ٢٤ + ضريبةٍ ٣٫٦ = ٢٧٫٦.</para>
///
/// <para><b>وكان</b> <c>nonInv = ٢٤ − ٤٠ = −١٦</c> فلا يُضاف سطرٌ يوازنه، <b>فيُرفض القيدُ
/// كلُّه</b>. والصوابُ: <b>دائنُ المخزون = ٤٠ (الطبقات)</b>، <b>ومدينُ «فروق أسعار
/// المشتريات» 5108 = ١٦</b> (خسارةُ المنشأة)، فالقيدُ: مدين ٢٧٫٦ + ١٦ = ٤٣٫٦ = دائن
/// ٤٠ + ٣٫٦.</para>
/// </summary>
public class AReturnBelowCostIsBalancedTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _supplierId;
    private long _productId;
    private long _warehouseId;

    public AReturnBelowCostIsBalancedTests()
    {
        TestActor.Administrator();
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// <b>القياسُ الحاكم — تحت FIFO وWAC:</b> إرجاعُ ٤ بسعر ٦ من ١٠ بتكلفة ١٠ <b>يُرحَّل</b>،
    /// والقيدُ متوازن بالأرقام الدقيقة: مدينُ المورّد ٢٧٫٦ · دائنُ المخزون ٤٠ (= الطبقات) ·
    /// دائنُ ضريبة المدخلات ٣٫٦ · <b>مدينُ فروق الأسعار ١٦</b>.
    /// </summary>
    [Theory]
    [InlineData(InventoryCostingMethod.Fifo)]
    [InlineData(InventoryCostingMethod.WeightedAverage)]
    public async Task AReturnPricedBelowCost_PostsAndBalances(InventoryCostingMethod method)
    {
        await SetupAsync(method);
        var invoice = await PostInvoiceAsync();

        var note = await ReturnAsync(invoice, quantity: 4m, unitPrice: 6m);

        var entry = await _db.JournalEntries.Include(e => e.Lines)
            .SingleAsync(e => e.Id == note.JournalEntryId);
        Assert.Equal(entry.Lines.Sum(l => l.Debit), entry.Lines.Sum(l => l.Credit));
        Assert.Equal(43.6m, entry.Lines.Sum(l => l.Debit));

        Assert.Equal(60m, await LedgerAsync("1107"));          // ‏١٠٠ − ٤٠: المخزونُ ينقص بالتكلفة لا بالسعر
        Assert.Equal(16m, await LedgerAsync("5108"));          // ‏خسارةُ المنشأة
        Assert.Equal(-(115m - 27.6m), await LedgerAsync("2101"));
        Assert.Equal(15m - 3.6m, await LedgerAsync("1111"));
        Assert.Equal(6m, await new InventoryService(_db).GetOnHandAsync(_companyId, _productId, _warehouseId));
    }

    /// <summary>
    /// <b>والمخزونُ في الأستاذ يطابق الطبقات بعد المرتجع</b> (FIFO): ٦ وحداتٍ متبقّيةٍ بعشرة
    /// = ٦٠ — <b>ولو دائنُ المخزون ٢٤ (السعر) لانفصل الأستاذُ عنها بـ١٦.</b>
    /// </summary>
    [Fact]
    public async Task TheInventoryLedger_StillEqualsTheLayers()
    {
        await SetupAsync(InventoryCostingMethod.Fifo);
        var invoice = await PostInvoiceAsync();

        await ReturnAsync(invoice, quantity: 4m, unitPrice: 6m);

        var layers = await _db.InventoryCostLayers.Where(l => l.CompanyId == _companyId).ToListAsync();
        Assert.Equal(60m, layers.Sum(l => l.RemainingQuantity * l.UnitCost));
        Assert.Equal(await LedgerAsync("1107"), layers.Sum(l => l.RemainingQuantity * l.UnitCost));
    }

    /// <summary>
    /// <b>شاهدُ ضبط — مرتجعٌ بسعر التكلفة أو فوقه لا يمسّ 5108:</b> ٤ بسعر ١٠ (يساوي) ثمّ
    /// ٢ بسعر ١٢ (فوق). <b>تبقى المعالجةُ الأصليّةُ</b>: المخزون بالتكلفة والفائضُ دائنٌ في
    /// المشتريات 5102، <b>وفروقُ الأسعار صفر</b>.
    /// </summary>
    [Fact]
    public async Task AReturnAtOrAboveCost_IsUnchanged()
    {
        await SetupAsync(InventoryCostingMethod.Fifo);
        var invoice = await PostInvoiceAsync();

        await ReturnAsync(invoice, quantity: 4m, unitPrice: 10m);
        Assert.Equal(0m, await LedgerAsync("5108"));
        Assert.Equal(60m, await LedgerAsync("1107"));
        Assert.Equal(0m, await LedgerAsync("5102"));

        await ReturnAsync(invoice, quantity: 2m, unitPrice: 12m);
        Assert.Equal(0m, await LedgerAsync("5108"));
        Assert.Equal(40m, await LedgerAsync("1107"));
        Assert.Equal(-4m, await LedgerAsync("5102"));          // ‏٢٤ − ٢٠ = ٤ فائضٌ دائن
    }

    /// <summary>
    /// <b>إلغاءُ المرتجع المُسعَّر دون التكلفة يُعيد كلَّ شيء:</b> عكسُ القيد يُصفّر 5108
    /// ويُعيد المخزون إلى ١٠٠ وذمةَ المورّد إلى ١١٥، <b>والطبقاتُ تعود</b> (١٠ بعشرة) —
    /// والعكسُ عامٌّ على سطور القيد فيشمل السطرَ الجديد.
    /// </summary>
    [Fact]
    public async Task CancellingTheBelowCostNote_RestoresEverythingAndNetsTheLossToZero()
    {
        await SetupAsync(InventoryCostingMethod.Fifo);
        var invoice = await PostInvoiceAsync();
        var note = await ReturnAsync(invoice, quantity: 4m, unitPrice: 6m);
        Assert.Equal(16m, await LedgerAsync("5108"));

        await new DebitNoteCancellationService(_db).CancelAsync(
            _companyId, note.Id, new DateOnly(2026, 2, 12), "admin");

        Assert.Equal(0m, await LedgerAsync("5108"));
        Assert.Equal(100m, await LedgerAsync("1107"));
        Assert.Equal(-115m, await LedgerAsync("2101"));
        Assert.Equal(15m, await LedgerAsync("1111"));
        Assert.Equal(10m, await new InventoryService(_db).GetOnHandAsync(_companyId, _productId, _warehouseId));
        var layers = await _db.InventoryCostLayers.Where(l => l.CompanyId == _companyId).ToListAsync();
        Assert.Equal(100m, layers.Sum(l => l.RemainingQuantity * l.UnitCost));
    }

    /// <summary>
    /// <b>وبلا تأكيدٍ صريحٍ يُرفض ما دون التكلفة ويُسمّي الفرق</b> — الرفضُ القديمُ كان الحارسَ الوحيدَ على خطأ كتابة السعر، فلا يصير
    /// خسارةً صامتة: ٢٤ مقابل ٤٠ والفرقُ ١٦، ولا إشعارَ ولا حركة.
    /// </summary>
    [Fact]
    public async Task BelowCost_WithoutAcceptance_IsRefusedNamingTheDifference()
    {
        await SetupAsync(InventoryCostingMethod.Fifo);
        var invoice = await PostInvoiceAsync();

        var ex = await Assert.ThrowsAsync<AccountingException>(() => ReturnAsync(invoice, 4m, 6m, accept: false));

        Assert.Contains("فروق أسعار المشتريات", ex.Message, StringComparison.Ordinal);
        _db.ChangeTracker.Clear();
        Assert.False(await _db.DebitNotes.AnyAsync(n => n.Status == DebitNoteStatus.Posted));
        Assert.Equal(0m, await LedgerAsync("5108"));
        Assert.Equal(10m, await new InventoryService(_db).GetOnHandAsync(_companyId, _productId, _warehouseId));
    }

    /// <summary>
    /// <b>وتحت FIFO تخرج طبقةُ الفاتورة المُرجَعة لا أقدمُ ما في المستودع</b> — فاتورةٌ أولى بعشرة وثانيةٌ بثمانية، ومرتجعُ أربعٍ من
    /// الثانية بسعرها (٨) ⟵ التكلفةُ ٣٢ والقيمةُ ٣٢ فلا فرقَ ولا تأكيد؛ <b>وكانت تُستهلك طبقةُ الأولى (٤٠) فتُقاس خسارةٌ لم تقع</b>.
    /// </summary>
    [Fact]
    public async Task UnderFifo_TheReturnedInvoicesOwnLayerLeaves()
    {
        await SetupAsync(InventoryCostingMethod.Fifo);
        await PostInvoiceAsync("SUP-A", 10m);
        var second = await PostInvoiceAsync("SUP-B", 8m);

        var note = await ReturnAsync(second, 4m, 8m, accept: false);

        Assert.Equal(0m, await LedgerAsync("5108"));
        Assert.Equal(180m - 32m, await LedgerAsync("1107"));
        var layers = await _db.InventoryCostLayers.AsNoTracking().Where(l => l.ProductId == _productId).ToListAsync();
        Assert.Equal(10m, layers.Single(l => l.UnitCost == 10m).RemainingQuantity);
        Assert.Equal(6m, layers.Single(l => l.UnitCost == 8m).RemainingQuantity);
        Assert.NotNull(note.JournalEntryId);
    }

    /// <summary><b>وإشعارٌ بعملةٍ غير عملة فاتورته يُرفض</b> — وإلّا قورن دولارٌ بطبقاتٍ بالريال فقيس فرقٌ لم يقع.</summary>
    [Fact]
    public async Task ANoteInAnotherCurrencyThanItsInvoice_IsRefused()
    {
        await SetupAsync(InventoryCostingMethod.Fifo);
        var invoice = await PostInvoiceAsync();
        var note = DebitNote.CreateDraft(_companyId, _supplierId, invoice.Id, new DateOnly(2026, 2, 10), "مرتجع");
        note.SetCurrency("USD", 3.75m);
        note.AddLine("صنف", 1m, 2m, _productId);

        var ex = await Assert.ThrowsAsync<AccountingException>(() =>
            new DebitNotePostingService(_db).PostAsync(note, acceptBelowCost: true));

        Assert.Contains("عملةُ الإشعار", ex.Message, StringComparison.Ordinal);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<DebitNote> ReturnAsync(PurchaseInvoice invoice, decimal quantity, decimal unitPrice, bool accept = true)
    {
        var note = await new DebitNoteDraftBuilder(_db).BuildAsync(
            _companyId, invoice.Id, new DateOnly(2026, 2, 10), "مرتجع",
            [new RequestedReturn(LineIndex: 0, Quantity: quantity, UnitPrice: unitPrice)]);
        return await new DebitNotePostingService(_db).PostAsync(note, acceptBelowCost: accept);
    }

    private async Task<PurchaseInvoice> PostInvoiceAsync(string reference = "SUP-1", decimal unitPrice = 10m)
    {
        var invoice = PurchaseInvoice.CreateDraft(
            _companyId, _supplierId, new DateOnly(2026, 2, 1), reference, vatDeductible: true);
        invoice.AddLine("صنف", 10m, unitPrice, _productId);
        return await new PurchaseInvoicePostingService(_db).PostAsync(invoice);
    }

    private async Task<decimal> LedgerAsync(string accountCode)
    {
        var rows = await new TrialBalanceQuery(_db)
            .ExecuteAsync(_companyId, new DateOnly(2000, 1, 1), new DateOnly(2100, 12, 31));
        var row = rows.FirstOrDefault(r => r.AccountCode == accountCode);
        return row is null ? 0m : Math.Round(row.TotalDebit - row.TotalCredit, 2, MidpointRounding.AwayFromZero);
    }

    private async Task SetupAsync(InventoryCostingMethod method)
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة المرتجع دون التكلفة", "310123456700141", BusinessProfile.General);
        company.SetCostingMethod(method);
        _companyId = company.Id;

        var supplier = Supplier.Create(_companyId, "مورد", "312233344400003");
        var product = Product.Create(_companyId, "P-1", "صنف", "قطعة", ProductType.Stockable);
        _db.Suppliers.Add(supplier);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        _supplierId = supplier.Id;
        _productId = product.Id;
        _warehouseId = (await _db.Warehouses
            .SingleAsync(w => w.CompanyId == _companyId && w.IsDefault)).Id;
    }
}
