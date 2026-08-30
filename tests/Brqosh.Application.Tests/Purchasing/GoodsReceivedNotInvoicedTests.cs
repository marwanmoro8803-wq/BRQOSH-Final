using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Purchasing;

/// <summary>
/// (CEEF-229 · م1-١٩ / I-5) <b>بضاعةٌ استُلمت ولم تُفوتَر — غائبةٌ عن الميزانية أصلاً والتزاماً.</b>
///
/// <para><b>العطل:</b> <c>GoodsReceiptPostingService</c> عمليةُ دفترٍ مساعدٍ خالصة: تُحرّك
/// المخزون وتضبط متوسطَ التكلفة، <b>ولا تكتب قيداً واحداً</b>. فبضاعةٌ بـ500,000 تُستلم في
/// 28 ديسمبر وتُفوتَر في 5 يناير <b>لا تظهر في ميزانية ديسمبر لا أصلاً ولا التزاماً</b> —
/// والقوائمُ تُقدَّم ناقصةً بقيمة البضاعة كلِّها.</para>
///
/// <para><b>ولا يكشفه ميزانُ المراجعة:</b> إذنُ الاستلام لا يكتب شيئاً، <b>والعدمُ متوازنٌ
/// تماماً</b>. ولا تكشفه هويّتا الذمم (هـ-١، هـ-٢) لأنّهما تقيسان 1104 و2101 ولا تمسّان 1107.</para>
///
/// <para><b>والهويةُ الوحيدةُ التي تقيس الطرفين هي هـ-٣:</b> مجموعُ قيمة المخزون من الدفتر
/// المساعد == رصيدُ 1107 في الأستاذ. ووثيقتُها تُسمّي «إذن الاستلام بلا قيد» بين ما وُجدت
/// له. <b>ولم يكن في المستودع اختبارٌ واحد يقيسها عند نقطة الاستلام</b> — الاختباراتُ كلُّها
/// تقيس بعد الفاتورة، حين يكون العطلُ قد اختفى.</para>
/// </summary>
public class GoodsReceivedNotInvoicedTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public GoodsReceivedNotInvoicedTests()
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
    /// <b>القياس المركزيّ — عند نقطة الاستلام، قبل أيّ فاتورة.</b>
    ///
    /// <para>بضاعةٌ بـ1,000 تُستلم: المخزونُ في الأستاذ 1,000 مديناً، والتزامُ «بضاعة مستلمة
    /// غير مفوترة» 1,000 دائناً، <b>وذمّةُ المورد صفر</b> — فالفاتورة لم تصل بعد.</para>
    ///
    /// <para>وصفرُ الذمم حمولةٌ لا زينة: هو ما يفرّق بين استحقاقِ استلامٍ ومديونيةٍ مؤكَّدة،
    /// وهو ما يُبقي هوية هـ-٢ قائمة.</para>
    /// </summary>
    [Fact]
    public async Task ReceivedGoods_AppearOnTheBalanceSheet_BeforeAnyInvoice()
    {
        var (companyId, supplierId, productId, warehouseId) = await SetupAsync();
        await ReceiveAsync(companyId, supplierId, productId, warehouseId, quantity: 10m, unitCost: 100m);

        Assert.Equal(1_000m, await LedgerAsync(companyId, SaudiChartOfAccounts.SystemCodes.Inventory));
        Assert.Equal(-1_000m, await LedgerAsync(companyId, "2112"));
        Assert.Equal(0m, await LedgerAsync(companyId, SaudiChartOfAccounts.SystemCodes.AccountsPayable));

        await LedgerIdentities.AssertAllAsync(_db, companyId);
    }

    /// <summary>
    /// <b>والفاتورةُ تُقفل الاستحقاق ولا تُضاعف المخزون.</b>
    ///
    /// <para>هذا هو نصفُ الإصلاح الذي لا يجوز أن يُشحن وحده: قيدٌ عند الاستلام بلا تعديلِ
    /// الفاتورة يجعل 1107 يُمدَّن مرّتين — مرّةً بالاستلام ومرّةً بالفاتورة — فيصير ضِعفَ
    /// الدفتر المساعد. <b>فيُقاس 1107 مساواةً تامّة لا «أكبر من صفر».</b></para>
    /// </summary>
    [Fact]
    public async Task TheInvoice_ClearsTheAccrual_WithoutDoublingInventory()
    {
        var (companyId, supplierId, productId, warehouseId) = await SetupAsync();
        var order = await ReceiveAsync(companyId, supplierId, productId, warehouseId, 10m, 100m);

        // ‏حارسٌ ضدّ المرور الفارغ: لولاه لمرّ هذا الاختبارُ اليوم — إذ لا حساب 2112 أصلاً
        // ‏فيقرأ صفراً، والفاتورةُ تُمدِّن 1107 بألف. فيُثبَّت أنّ الاستحقاق **وُجد** ثمّ أُقفل.
        Assert.Equal(-1_000m, await LedgerAsync(companyId, "2112"));

        var invoice = PurchaseInvoice.CreateDraft(
            companyId, supplierId, new DateOnly(2026, 8, 5), "SUP-001", vatDeductible: true);
        invoice.LinkPurchaseOrder(order.Id);
        invoice.AddLine("مواد", 10m, 100m, productId);
        await new PurchaseInvoicePostingService(_db).PostAsync(invoice);

        Assert.Equal(1_000m, await LedgerAsync(companyId, SaudiChartOfAccounts.SystemCodes.Inventory));
        Assert.Equal(0m, await LedgerAsync(companyId, "2112"));
        Assert.Equal(-1_150m, await LedgerAsync(companyId, SaudiChartOfAccounts.SystemCodes.AccountsPayable));

        await LedgerIdentities.AssertAllAsync(_db, companyId);
    }

    /// <summary>
    /// <b>وإلغاءُ الاستلام يعكس قيدَه.</b> فقيدٌ يُكتب بلا طريقِ عكسٍ يترك التزاماً خالداً
    /// على الميزانية عن بضاعةٍ أُعيدت إلى المورد.
    /// </summary>
    [Fact]
    public async Task CancellingTheReceipt_ReversesItsEntry()
    {
        var (companyId, supplierId, productId, warehouseId) = await SetupAsync();
        var order = await ReceiveAsync(companyId, supplierId, productId, warehouseId, 10m, 100m);
        var receipt = await _db.GoodsReceipts.SingleAsync(g => g.PurchaseOrderId == order.Id);

        // ‏وكذلك هنا: يُثبَت القيدُ قبل عكسه، وإلا كان «صفرٌ بعد الإلغاء» صفراً لم يتغيّر.
        Assert.Equal(-1_000m, await LedgerAsync(companyId, "2112"));
        Assert.NotNull(receipt.JournalEntryId);

        await new GoodsReceiptCancellationService(_db)
            .CancelAsync(companyId, receipt.Id, new DateOnly(2026, 8, 3), "مدير");

        Assert.Equal(0m, await LedgerAsync(companyId, SaudiChartOfAccounts.SystemCodes.Inventory));
        Assert.Equal(0m, await LedgerAsync(companyId, "2112"));

        await LedgerIdentities.AssertAllAsync(_db, companyId);
    }

    // ── التهيئة ──────────────────────────────────────────────────────────────

    private async Task<(long CompanyId, long SupplierId, long ProductId, long WarehouseId)> SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة الاستلام", "310123456700003", BusinessProfile.Retail);

        var supplier = Supplier.Create(company.Id, "مورد", "312222222200003");
        _db.Suppliers.Add(supplier);
        var product = Product.Create(company.Id, "P-GRN", "صنف مخزني", "قطعة", ProductType.Stockable);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var warehouse = await _db.Warehouses.SingleAsync(w => w.CompanyId == company.Id && w.IsDefault);
        return (company.Id, supplier.Id, product.Id, warehouse.Id);
    }

    private async Task<PurchaseOrder> ReceiveAsync(
        long companyId, long supplierId, long productId, long warehouseId,
        decimal quantity, decimal unitCost)
    {
        var order = PurchaseOrder.CreateDraft(companyId, supplierId, new DateOnly(2026, 8, 1));
        order.AddLine("مواد", quantity, unitCost, productId);
        await new PurchaseOrderService(_db).ApproveAsync(order);

        var receipt = GoodsReceipt.CreateDraft(
            companyId, supplierId, order.Id, warehouseId, new DateOnly(2026, 8, 2));
        receipt.AddLine("مواد", quantity, quantity, unitCost, productId);
        await new GoodsReceiptPostingService(_db).PostAsync(receipt);

        return order;
    }

    /// <summary>
    /// رصيدٌ مُوقَّع من <b>ميزان المراجعة نفسِه</b>: موجبٌ مدين، سالبٌ دائن.
    ///
    /// <para><b>ولا يُصنَع المرشّحُ باليد.</b> أوّلُ صياغةٍ لهذا التابع رشّحت
    /// <c>Status == Posted</c> وحدَها، فأسقطت القيدَ الأصليّ بعد أن صار <c>Reversed</c>
    /// وأبقت عكسَه — فقرأت 1107 بـ<b>−1,000</b> وأبلغت عن عطلٍ لا وجودَ له. والعكسُ
    /// المزدوج يُبقي الطرفين في الدفتر ويُلغي أحدُهما الآخر؛ والحالةُ علامةٌ لا استبعاد.</para>
    ///
    /// <para>فيُستعمل <c>TrialBalanceQuery</c> — مصدرُ الحقيقة نفسُه الذي تقيس به الهويات.</para>
    /// </summary>
    private async Task<decimal> LedgerAsync(long companyId, string accountCode)
    {
        var rows = await new Brqosh.Infrastructure.Reporting.TrialBalanceQuery(_db)
            .ExecuteAsync(companyId, new DateOnly(2000, 1, 1), new DateOnly(2100, 12, 31));

        var row = rows.FirstOrDefault(r => r.AccountCode == accountCode);
        return row is null ? 0m : Math.Round(row.TotalDebit - row.TotalCredit, 2, MidpointRounding.AwayFromZero);
    }
}
