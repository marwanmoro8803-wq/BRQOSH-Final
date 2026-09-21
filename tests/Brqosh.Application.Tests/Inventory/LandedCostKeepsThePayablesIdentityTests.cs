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

namespace Brqosh.Application.Tests.Inventory;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٢٧) <b>توزيعُ التكلفة الإضافيّة على الحساب كان يُدائن حسابَ
/// مراقبة الموردين بلا مورِّد.</b>
///
/// <para><b>والهويّةُ هـ-٢ تقضي</b>: <c>مجموعُ أرصدة الموردين == رصيدُ 2101</c>.
/// فقيدٌ يُدائن <c>2101</c> <b>بلا طرفٍ يُنسب إليه</b> يرفع الدفترَ <b>ولا يستطيع الدفترُ
/// المساعدُ أن يتبعه</b> — <b>فتنكسر الهويّةُ كسراً دائماً</b>.</para>
///
/// <para><b>ولا يشكو الميزان</b>: القيدُ متوازن. ويظهر العطلُ في <b>مطابقة كشف المورّد</b>
/// وحدَها — إجماليُّ الذمم في الميزانية أعلى من مجموع ما على الموردين، <b>والفرقُ بلا
/// اسمٍ يُسأل عنه</b>.</para>
///
/// <para><b>والصوابُ أنّ شحناً أو جمركاً لم تَرِد فاتورتُه التزامٌ مستحقّ</b>
/// (<c>2104</c> «مصروفات مستحقة» — في الدليل منذ نشأته ولا يكتبه أحد) <b>لا ذمّةُ
/// مورِّد</b>. فإذا وردت الفاتورةُ نُقل إلى ذمّة صاحبها بمستندها.</para>
/// </summary>
public class LandedCostKeepsThePayablesIdentityTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _productId;
    private long _warehouseId;
    private long _supplierId;

    public LandedCostKeepsThePayablesIdentityTests()
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

    // ── الهويّةُ الحاكمة ────────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ الحاكم:</b> تكلفةٌ إضافيّةٌ على الحساب ⟵ <b>هـ-٢ سليمة</b>.
    /// ويُقاس <b>بمُدقِّق الهويّات في المستودع نفسِه</b>، لا بمقارنةٍ يدويّة.
    /// </summary>
    [Fact]
    public async Task LandedCostOnCredit_KeepsThePayablesIdentity()
    {
        await SetupAsync();
        await ReceiveStockAsync();

        await AllocateAsync(amount: 900m, creditCash: false);

        LedgerIdentities.Assert(await LedgerIdentities.PayablesAsync(_db, _companyId));
        await LedgerIdentities.AssertAllAsync(_db, _companyId);
    }

    /// <summary>
    /// <b>والمقابلُ مصروفٌ مستحقٌّ صريح</b> — <c>2104</c> — <b>ولا ريالَ على حساب
    /// مراقبة الموردين</b>.
    /// </summary>
    [Fact]
    public async Task TheCreditLands_OnAccruedExpenses_NotOnThePayablesControl()
    {
        await SetupAsync();
        await ReceiveStockAsync();
        var payablesBefore = await LedgerAsync(SaudiChartOfAccounts.SystemCodes.AccountsPayable);

        await AllocateAsync(amount: 900m, creditCash: false);

        Assert.Equal(-900m, await LedgerAsync(SaudiChartOfAccounts.SystemCodes.AccruedExpenses));
        Assert.Equal(payablesBefore, await LedgerAsync(SaudiChartOfAccounts.SystemCodes.AccountsPayable));
    }

    // ── شواهدُ الضبط ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>شاهدُ ضبطٍ أوّل — التكلفةُ تصل المخزونَ كما كانت:</b> الإصلاحُ مسّ طرفَ
    /// الدائن وحدَه. <b>ولولاه لجاز أن يكون «الإصلاح» تعطيلاً للتوزيع.</b>
    /// </summary>
    [Fact]
    public async Task TheCostStillReachesInventory()
    {
        await SetupAsync();
        await ReceiveStockAsync();
        var before = await LedgerAsync(SaudiChartOfAccounts.SystemCodes.Inventory);

        await AllocateAsync(amount: 900m, creditCash: false);

        Assert.Equal(before + 900m, await LedgerAsync(SaudiChartOfAccounts.SystemCodes.Inventory));
    }

    /// <summary>
    /// <b>وشاهدُ ضبطٍ ثانٍ — الدفعُ نقداً لم يتغيّر:</b> ما زال يُدائن الصندوق،
    /// <b>ولا يمسّ المستحقّات</b>.
    /// </summary>
    [Fact]
    public async Task PayingCash_IsUnchanged()
    {
        await SetupAsync();
        await ReceiveStockAsync();

        await AllocateAsync(amount: 900m, creditCash: true);

        Assert.Equal(-900m, await LedgerAsync(SaudiChartOfAccounts.SystemCodes.CashOnHand));
        Assert.Equal(0m, await LedgerAsync(SaudiChartOfAccounts.SystemCodes.AccruedExpenses));
        await LedgerIdentities.AssertAllAsync(_db, _companyId);
    }

    /// <summary>
    /// <b>وشاهدُ ضبطٍ ثالث — وسمُ «نظاميّ» يلحق القاعدةَ القائمة.</b>
    ///
    /// <para><c>DatabaseInitializer</c> <b>يخرج مبكّراً إن وُجد حسابٌ واحد</b> — فالدليلُ
    /// يُزرع مرّةً <b>ولا يُعاد مطابقتُه بالقالب أبداً</b>. فحسابٌ رُقّي اليومَ يبقى
    /// عاديّاً على كلّ تنصيبٍ قائم، <b>قابلاً للحذف بينما صار الترحيلُ يعتمد عليه</b>.</para>
    ///
    /// <para>فيُحاكى ذلك حرفاً: <b>يُخفَّض الوسمُ ثمّ يُشغَّل المُرقِّع</b> ⟵ يعود.
    /// <b>وهذا هو المسارُ الذي يسلكه كلُّ عميلٍ قائم</b>، لا مسارُ التأسيس.</para>
    /// </summary>
    [Fact]
    public async Task AnExistingDatabase_GetsTheSystemFlag_WhenThePatcherRuns()
    {
        await SetupAsync();

        // ‏قاعدةٌ زُرعت قبل الترقية: الحسابُ موجودٌ وغيرُ معلَّمٍ نظاميّاً.
        var accrued = await _db.Accounts.SingleAsync(
            a => a.CompanyId == _companyId && a.Code == SaudiChartOfAccounts.SystemCodes.AccruedExpenses);
        _db.Entry(accrued).Property(nameof(Account.IsSystem)).CurrentValue = false;
        await _db.SaveChangesAsync();

        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        _db.ChangeTracker.Clear();
        var repaired = await _db.Accounts.SingleAsync(
            a => a.CompanyId == _companyId && a.Code == SaudiChartOfAccounts.SystemCodes.AccruedExpenses);

        Assert.True(repaired.IsSystem,
            "‏حسابٌ يعتمد عليه الترحيلُ بقي عاديّاً على قاعدةٍ قائمة — فهو قابلٌ للحذف.");
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("متجر الشحن", "310123456700044", BusinessProfile.Retail);
        _companyId = company.Id;

        var supplier = Supplier.Create(_companyId, "مورد", "312222222200003");
        _db.Suppliers.Add(supplier);
        var product = Product.Create(_companyId, "P-L", "بضاعة", "قطعة", ProductType.Stockable);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        _supplierId = supplier.Id;
        _productId = product.Id;
        _warehouseId = (await _db.Warehouses.SingleAsync(w => w.CompanyId == _companyId && w.IsDefault)).Id;
    }

    /// <summary>بضاعةٌ في المستودع — فالتوزيعُ يحتاج رصيداً.</summary>
    private async Task ReceiveStockAsync()
    {
        var order = PurchaseOrder.CreateDraft(_companyId, _supplierId, new DateOnly(2026, 8, 1));
        order.AddLine("بضاعة", 100m, 10m, _productId);
        await new PurchaseOrderService(_db).ApproveAsync(order);

        var receipt = GoodsReceipt.CreateDraft(
            _companyId, _supplierId, order.Id, _warehouseId, new DateOnly(2026, 8, 2));
        receipt.AddLine("بضاعة", 100m, 100m, 10m, _productId);
        await new GoodsReceiptPostingService(_db).PostAsync(receipt);

        var invoice = PurchaseInvoice.CreateDraft(
            _companyId, _supplierId, new DateOnly(2026, 8, 3), "SUP-L1", vatDeductible: true);
        invoice.LinkPurchaseOrder(order.Id);
        invoice.AddLine("بضاعة", 100m, 10m, _productId);
        await new PurchaseInvoicePostingService(_db).PostAsync(invoice);
    }

    private Task AllocateAsync(decimal amount, bool creditCash) =>
        new LandedCostAllocationService(_db).AllocateAsync(
            _companyId,
            new DateOnly(2026, 8, 5),
            "شحن وجمارك",
            amount,
            [(_productId, _warehouseId, 1m)],
            "admin",
            creditCash);

    /// <summary>رصيدٌ مُوقَّع: موجبٌ مدين، سالبٌ دائن.</summary>
    private async Task<decimal> LedgerAsync(string accountCode)
    {
        var rows = await new TrialBalanceQuery(_db)
            .ExecuteAsync(_companyId, new DateOnly(2000, 1, 1), new DateOnly(2100, 12, 31));

        var row = rows.FirstOrDefault(r => r.AccountCode == accountCode);
        return row is null ? 0m : Math.Round(row.TotalDebit - row.TotalCredit, 2, MidpointRounding.AwayFromZero);
    }
}
