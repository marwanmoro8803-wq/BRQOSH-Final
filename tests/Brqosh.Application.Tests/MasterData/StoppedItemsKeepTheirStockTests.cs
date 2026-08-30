using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.MasterData;

/// <summary>
/// (CEEF-229 · م2-١٧ب) <b>الإيقافُ يوقف البيعَ، ولا يُخفي البضاعة.</b>
///
/// <para><b>العطل:</b> <c>Product.Deactivate()</c> يضبط <c>IsActive = false</c> بلا فحصٍ لرصيد،
/// و<c>StockBalanceQuery</c> يُرشّح <c>p.IsActive</c> — فالبضاعةُ القائمة في المستودع تختفي من
/// الدفتر المساعد لحظةَ الإيقاف، بينما يبقى حساب المخزون 1107 في الأستاذ كما هو. وهو
/// <b>انفصالُ دفترٍ مساعدٍ عن أستاذٍ عام</b>: كسرُ الهوية هـ-٣ بمقدار قيمة البضاعة كاملةً.</para>
///
/// <para><b>ولا يقع بحيلةٍ نادرة:</b> يقع بضغطة زرٍّ يفعلها كلُّ تاجرٍ في نهاية كلّ موسم —
/// «أوقِف المظلّات». والصندوق يسأله «إيقاف الصنف؟» فيوافق، فتختفي مئتا مظلّةٍ من تقرير
/// المخزون ومن ورقة الجرد، ويبقى ثمنُها في الميزانية. ولا رسالةَ ولا علامة.</para>
///
/// <para><b>والاتّجاهان مطلوبان معاً:</b> منعُ الإخفاء وحده يترك التاجرَ يبيع صنفاً أوقفه؛
/// ومنعُ الإيقاف وحده يمنعه من إيقاف صنفٍ عنده منه بضاعة — وهو عينُ ما يريد فعله. فالصواب:
/// <b>الإيقاف يمنع الحركة الجديدة ويُبقي الموجود مرئياً</b> حتى يُباع أو يُجرَد أو يُتلَف.</para>
///
/// <para>والاختبارُ القائم <c>DeactivationIsNotDeletionTests</c> يوقف صنفاً <b>بلا رصيد</b>،
/// فلا يبلغ هذا العطلَ إطلاقاً.</para>
/// </summary>
public class StoppedItemsKeepTheirStockTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _productId;
    private long _supplierId;
    private long _customerId;

    public StoppedItemsKeepTheirStockTests()
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

    /// <summary>مئتا مظلّةٍ بأربعين ريالاً — ثمانيةُ آلافٍ في المستودع وفي حساب 1107.</summary>
    private async Task SetupWithStockAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("متجر الموسم", "310123456700099", BusinessProfile.Retail);
        _companyId = company.Id;

        var product = Product.Create(_companyId, "P-1", "مظلات", "قطعة", ProductType.Stockable, 90m);
        _db.Products.Add(product);

        var supplier = Supplier.Create(_companyId, "مورّد المظلات");
        _db.Suppliers.Add(supplier);

        var customer = Customer.Create(_companyId, "زبون");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        _productId = product.Id;
        _supplierId = supplier.Id;
        _customerId = customer.Id;

        var purchase = PurchaseInvoice.CreateDraft(
            _companyId, _supplierId, new DateOnly(2026, 3, 1), "INV-SUP-1", true);
        purchase.AddLine("مظلات", 200m, 40m, _productId);
        await new PurchaseInvoicePostingService(_db).PostAsync(purchase);
    }

    private async Task StopTheProductAsync()
    {
        var product = await _db.Products.SingleAsync(p => p.Id == _productId);
        product.Deactivate();
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    /// <summary>
    /// <b>القياسُ المركزيّ.</b> الهويةُ هـ-٣ قائمةٌ قبل الإيقاف، ويجب أن تبقى بعده —
    /// فالبضاعةُ لم تتحرّك، وحدَها حالتُها التشغيلية تغيّرت.
    /// </summary>
    [Fact]
    public async Task StoppingAnItem_DoesNotBreakTheInventoryIdentity()
    {
        await SetupWithStockAsync();

        var before = await LedgerIdentities.InventoryAsync(_db, _companyId);
        Assert.Equal(8_000m, before.Subsidiary);
        LedgerIdentities.Assert(before);

        await StopTheProductAsync();

        var after = await LedgerIdentities.InventoryAsync(_db, _companyId);
        LedgerIdentities.Assert(after);
    }

    /// <summary>
    /// <b>والبضاعةُ تبقى مرئيةً باسمها</b> — لا في المجموع وحده. فورقةُ الجرد تُبنى من هذا
    /// الاستعلام، وما لا يظهر فيها لا يُعَدّ ولا يُسوّى ولا يخرج من المستودع أبداً.
    /// </summary>
    [Fact]
    public async Task AStoppedItem_StaysOnTheStockSheet_SoItCanBeCounted()
    {
        await SetupWithStockAsync();
        await StopTheProductAsync();

        var rows = await new StockBalanceQuery(_db).ExecuteAsync(_companyId);
        var row = rows.SingleOrDefault(r => r.ProductCode == "P-1");

        Assert.True(row is not null,
            "الصنفُ الموقوف اختفى من تقرير المخزون ومعه بضاعتُه — ولا سبيل إلى جردها.");
        Assert.Equal(200m, row!.Quantity);
        Assert.Equal(8_000m, row.TotalValue);
    }

    /// <summary>
    /// <b>وما نفدت بضاعتُه يختفي كالمعتاد</b> — وإلا لتضخّم التقريرُ بكلّ صنفٍ أُوقف يوماً.
    /// وهذا هو الحارسُ الذي يمنع الإصلاحَ من أن يصير تسييباً.
    /// </summary>
    [Fact]
    public async Task AStoppedItem_WithNoStock_StillDisappears()
    {
        await SetupWithStockAsync();

        var empty = Product.Create(_companyId, "P-2", "صنف بلا رصيد", "قطعة", ProductType.Stockable, 10m);
        empty.Deactivate();
        _db.Products.Add(empty);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var rows = await new StockBalanceQuery(_db).ExecuteAsync(_companyId);

        Assert.DoesNotContain(rows, r => r.ProductCode == "P-2");
    }

    /// <summary>
    /// <b>والاتّجاه الآخر: الإيقافُ يوقف فعلاً.</b> بيعُ صنفٍ موقوف يجب أن يُرفض — وإلا لم
    /// يكن الزرُّ إلا تسميةً. والرسالةُ تُسمّي الصنف، فالتاجرُ يعرف أيَّها أوقف.
    /// </summary>
    [Fact]
    public async Task SellingAStoppedItem_IsRefused()
    {
        await SetupWithStockAsync();
        await StopTheProductAsync();

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 6, 1));
        invoice.AddLine("مظلات", 1m, 90m, _productId);

        var ex = await Assert.ThrowsAsync<AccountingException>(
            () => new SalesInvoicePostingService(_db).PostAsync(invoice));

        Assert.Contains("مظلات", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>وشراؤه كذلك — فالإيقافُ قرارٌ بألّا يتحرّك الصنف، لا بألّا يُباع وحده.</summary>
    [Fact]
    public async Task BuyingAStoppedItem_IsRefused()
    {
        await SetupWithStockAsync();
        await StopTheProductAsync();

        var purchase = PurchaseInvoice.CreateDraft(
            _companyId, _supplierId, new DateOnly(2026, 6, 1), "INV-SUP-2", true);
        purchase.AddLine("مظلات", 10m, 40m, _productId);

        var ex = await Assert.ThrowsAsync<AccountingException>(
            () => new PurchaseInvoicePostingService(_db).PostAsync(purchase));

        Assert.Contains("مظلات", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>والصنفُ النشط يمرّ</b> — وإلا لكان الحارسُ يمنع كلَّ بيع. وهذا هو الحارسُ الذي
    /// يمنع الحارسَ من أن يبتلع الحالةَ السائدة.
    /// </summary>
    [Fact]
    public async Task AnActiveItem_StillSells()
    {
        await SetupWithStockAsync();

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 6, 1));
        invoice.AddLine("مظلات", 1m, 90m, _productId);

        var posted = await new SalesInvoicePostingService(_db).PostAsync(invoice);

        Assert.True(posted.Id > 0);
    }
}
