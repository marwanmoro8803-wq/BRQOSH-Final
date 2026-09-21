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
/// (جاهزيّةُ الإطلاق · ح-١٤) <b>فاتورةٌ بلا أمرِ شراءٍ كانت تُدخل بضاعةَ إذنِ الاستلام مرّةً ثانية.</b>
///
/// <para>مسارُ «بلا إذن» في <c>PurchaseInvoicePostingService</c> يُضيف الكمّيةَ إلى
/// المستودع — <b>وهو الصوابُ لشراءٍ مباشر</b>. لكنّ البضاعةَ التي دخلت بإذنِ استلامٍ
/// <b>دخلت المخزونَ يومَها</b>، فإن فُوتِرت بفاتورةٍ غيرِ مربوطةٍ بأمرها: <b>تُضاف كمّيتُها
/// ثانيةً ويبقى <c>2112</c> دائناً إلى الأبد</b> — كمّيةٌ وهميّةٌ في الجرد والتزامٌ لا
/// يُقفله شيء.</para>
///
/// <para><b>ولا يشكو الميزان</b>: القيدان متوازنان. والجردُ وحدَه يكشفه — <b>بعد شهور</b>.</para>
///
/// <para><b>والحكمُ من الدفتر لا من النيّة:</b> لا رابطَ في الفاتورة يقول لمن هي، فيُسأل
/// عن استحقاقٍ مفتوحٍ لهذا المورِّد على هذا الصنف. <b>وشراءٌ مباشرٌ من مورِّدٍ بلا استحقاقٍ
/// مفتوحٍ يمرّ كما كان</b> — وحظرُه كان سيُغلق باباً سليماً لأجل بابٍ معطوب.</para>
/// </summary>
public class UnlinkedInvoiceDoesNotDoubleStockTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _supplierId;
    private long _otherSupplierId;
    private long _productId;
    private long _warehouseId;

    public UnlinkedInvoiceDoesNotDoubleStockTests()
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

    // ── العطلُ نفسُه ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ الحاكم:</b> بضاعةٌ استُلمت بإذن، ثمّ فاتورةٌ غيرُ مربوطةٍ لها ⟵ <b>تُرفض</b>،
    /// <b>والرصيدُ يبقى عشرةً لا عشرين</b>.
    /// </summary>
    [Fact]
    public async Task AnUnlinkedInvoice_ForReceivedGoods_IsRefused()
    {
        await SetupAsync();
        await ReceiveAsync(quantity: 10m, unitCost: 100m);

        var error = await Assert.ThrowsAsync<AccountingException>(
            () => PostUnlinkedAsync(_supplierId, quantity: 10m, unitPrice: 100m));

        Assert.Contains("اربط الفاتورةَ بأمر الشراء", error.Message, StringComparison.Ordinal);
        Assert.Equal(10m, await OnHandAsync());
    }

    /// <summary>
    /// <b>وحسابُ الاستحقاق يبقى كما تركه الإذن</b> — فالرفضُ منع الازدواجَ ولم يُقفل
    /// استحقاقاً بغير فاتورته.
    /// </summary>
    [Fact]
    public async Task TheRefusal_LeavesTheAccrualUntouched()
    {
        await SetupAsync();
        await ReceiveAsync(quantity: 10m, unitCost: 100m);

        await Assert.ThrowsAsync<AccountingException>(
            () => PostUnlinkedAsync(_supplierId, quantity: 10m, unitPrice: 100m));

        Assert.Equal(-1_000m, await LedgerAsync("2112"));
    }

    // ── شواهدُ الضبط ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>شاهدُ ضبطٍ أوّل — الشراءُ المباشر:</b> مورِّدٌ بلا إذنِ استلامٍ مفتوح، فاتورتُه
    /// غيرُ المربوطة تمرّ <b>وتُدخل البضاعةَ مرّةً واحدة</b>. <b>ولولاه لكان «الإصلاح»
    /// إغلاقاً لأشيع مسارٍ في المتاجر الصغيرة.</b>
    /// </summary>
    [Fact]
    public async Task ADirectPurchase_WithNoOpenReceipt_StillPosts()
    {
        await SetupAsync();

        await PostUnlinkedAsync(_otherSupplierId, quantity: 4m, unitPrice: 50m);

        Assert.Equal(4m, await OnHandAsync());
    }

    /// <summary>
    /// <b>وشاهدُ ضبطٍ ثانٍ — الاستحقاقُ يخصّ مورِّدَه:</b> إذنُ استلامٍ من مورِّدٍ لا يمنع
    /// فاتورةَ مورِّدٍ آخر على الصنف نفسِه. <b>فالحارسُ يسأل عن المورِّد لا عن الصنف وحدَه.</b>
    /// </summary>
    [Fact]
    public async Task AnotherSuppliersInvoice_IsNotBlockedByThisSuppliersReceipt()
    {
        await SetupAsync();
        await ReceiveAsync(quantity: 10m, unitCost: 100m);

        await PostUnlinkedAsync(_otherSupplierId, quantity: 3m, unitPrice: 90m);

        Assert.Equal(13m, await OnHandAsync());   // ‏عشرةٌ بالإذن وثلاثةٌ بالشراء المباشر
    }

    /// <summary>
    /// <b>وشاهدُ ضبطٍ ثالث — الاستحقاقُ المُقفَل لا يمنع:</b> استُلمت عشرةٌ وفُوتِرت كلُّها
    /// بفاتورةٍ مربوطة، <b>فلا استحقاقَ مفتوحاً</b> — وفاتورةٌ لاحقةٌ غيرُ مربوطةٍ لشراءٍ
    /// جديدٍ تمرّ. ولولاه لصار الحارسُ أبديّاً على كلّ صنفٍ وصل يوماً بإذن.
    /// </summary>
    [Fact]
    public async Task AFullyInvoicedReceipt_NoLongerBlocks()
    {
        await SetupAsync();
        var order = await ReceiveAsync(quantity: 10m, unitCost: 100m);
        await PostLinkedAsync(order, quantity: 10m, unitPrice: 100m);

        await PostUnlinkedAsync(_supplierId, quantity: 2m, unitPrice: 105m);

        Assert.Equal(12m, await OnHandAsync());
        Assert.Equal(0m, await LedgerAsync("2112"));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("متجر", "310123456700066", BusinessProfile.Retail);
        _companyId = company.Id;

        var supplier = Supplier.Create(_companyId, "مورد الإذن", "312222222200003");
        var other = Supplier.Create(_companyId, "مورد مباشر", "312333333300003");
        _db.Suppliers.AddRange(supplier, other);

        var product = Product.Create(_companyId, "P-U", "بضاعة", "قطعة", ProductType.Stockable);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        _supplierId = supplier.Id;
        _otherSupplierId = other.Id;
        _productId = product.Id;
        _warehouseId = (await _db.Warehouses.SingleAsync(w => w.CompanyId == _companyId)).Id;
    }

    private async Task<PurchaseOrder> ReceiveAsync(decimal quantity, decimal unitCost)
    {
        var order = PurchaseOrder.CreateDraft(_companyId, _supplierId, new DateOnly(2026, 8, 1));
        order.AddLine("بضاعة", quantity, unitCost, _productId);
        await new PurchaseOrderService(_db).ApproveAsync(order);

        var receipt = GoodsReceipt.CreateDraft(
            _companyId, _supplierId, order.Id, _warehouseId, new DateOnly(2026, 8, 2));
        receipt.AddLine("بضاعة", quantity, quantity, unitCost, _productId);
        await new GoodsReceiptPostingService(_db).PostAsync(receipt);

        return order;
    }

    private Task PostLinkedAsync(PurchaseOrder order, decimal quantity, decimal unitPrice)
    {
        var invoice = PurchaseInvoice.CreateDraft(
            _companyId, _supplierId, new DateOnly(2026, 8, 5), $"SUP-L-{quantity:0}", vatDeductible: true);
        invoice.LinkPurchaseOrder(order.Id);
        invoice.AddLine("بضاعة", quantity, unitPrice, _productId);
        return new PurchaseInvoicePostingService(_db).PostAsync(invoice);
    }

    private Task PostUnlinkedAsync(long supplierId, decimal quantity, decimal unitPrice)
    {
        var invoice = PurchaseInvoice.CreateDraft(
            _companyId, supplierId, new DateOnly(2026, 8, 6),
            $"SUP-U-{supplierId}-{quantity:0}", vatDeductible: true);
        invoice.AddLine("بضاعة", quantity, unitPrice, _productId);
        return new PurchaseInvoicePostingService(_db).PostAsync(invoice);
    }

    private Task<decimal> OnHandAsync() =>
        new InventoryService(_db).GetOnHandAsync(_companyId, _productId, _warehouseId);

    /// <summary>رصيدٌ مُوقَّع: موجبٌ مدين، سالبٌ دائن.</summary>
    private async Task<decimal> LedgerAsync(string accountCode)
    {
        var rows = await new TrialBalanceQuery(_db)
            .ExecuteAsync(_companyId, new DateOnly(2000, 1, 1), new DateOnly(2100, 12, 31));

        var row = rows.FirstOrDefault(r => r.AccountCode == accountCode);
        return row is null ? 0m : Math.Round(row.TotalDebit - row.TotalCredit, 2, MidpointRounding.AwayFromZero);
    }
}
