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

    /// <summary>
    /// (CEEF-229 · م2-١٧ج) <b>والمستودعُ الموقوف يُخفي بضاعتَه كما كان الصنف — والأثرُ أكبر.</b>
    ///
    /// <para>الاستعلامُ نفسُه يُرشّح <c>w.IsActive</c> على المستودعات. وإيقافُ مستودعٍ يُخفي
    /// <b>كلَّ</b> ما فيه دفعةً واحدة — لا صنفاً واحداً. ومتجرٌ يوقف مستودعَ الموسم يفقد
    /// من دفتره المساعد كلَّ بضاعته بينما يبقى ثمنُها في 1107.</para>
    ///
    /// <para>وهذا هو نمطُ «نصف الإصلاح» الذي تكرّر في هذا المستودع مراراً: يُصلَح الكيانُ
    /// الذي أُمسك، ويُترك نظيرُه في السطر المجاور.</para>
    /// </summary>
    [Fact]
    public async Task StoppingAWarehouse_DoesNotHideItsStock()
    {
        await SetupWithStockAsync();

        var warehouse = await new InventoryService(_db).GetDefaultWarehouseAsync(_companyId);
        var stored = await _db.Warehouses.SingleAsync(w => w.Id == warehouse.Id);
        stored.Deactivate();
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        LedgerIdentities.Assert(await LedgerIdentities.InventoryAsync(_db, _companyId));

        var rows = await new StockBalanceQuery(_db).ExecuteAsync(_companyId);
        Assert.Contains(rows, r => r.ProductCode == "P-1" && r.Quantity == 200m);
    }

    /// <summary>
    /// (CEEF-229 · م2-١٧د) <b>ولا يُخترَع مستودعٌ في الخفاء.</b>
    ///
    /// <para><c>GetDefaultWarehouseAsync</c> يبحث عن مستودعٍ افتراضيّ نشط، ثمّ عن أيّ نشط،
    /// <b>ثمّ يُنشئ واحداً</b>. والإنشاءُ وُضع لأوّل تشغيلٍ في منشأةٍ لا مستودعَ لها — وهو
    /// صواب. لكنّه يقع أيضاً حين <b>توجد</b> مستودعاتٌ وكلُّها موقوفة.</para>
    ///
    /// <para><b>وهذا حالُ العميل التجريبيّ بعينه:</b> متجرٌ بمستودعٍ واحد يوقفه صاحبُه لسببٍ ما،
    /// فتذهب كلُّ حركةٍ تالية إلى مستودعٍ <b>لم يطلبه أحد</b>، وتنقسم بضاعتُه بين اثنين: القديمُ
    /// فيه ما اشتراه، والجديدُ فيه ما بعده. ولا رسالةَ ولا سؤال.</para>
    ///
    /// <para>والصواب: الاختراعُ لمن لا مستودعَ له، والرفضُ لمن أوقف مستودعاته — فالإيقافُ
    /// قرارٌ صريح، وتجاوزُه بالاختراع إبطالٌ له.</para>
    /// </summary>
    [Fact]
    public async Task WithEveryWarehouseStopped_NoWarehouseIsInvented()
    {
        await SetupWithStockAsync();

        foreach (var w in await _db.Warehouses.Where(w => w.CompanyId == _companyId).ToListAsync())
            w.Deactivate();
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var before = await _db.Warehouses.CountAsync(w => w.CompanyId == _companyId);

        var purchase = PurchaseInvoice.CreateDraft(
            _companyId, _supplierId, new DateOnly(2026, 6, 1), "INV-SUP-3", true);
        purchase.AddLine("مظلات", 5m, 40m, _productId);

        await Assert.ThrowsAsync<AccountingException>(
            () => new PurchaseInvoicePostingService(_db).PostAsync(purchase));

        Assert.Equal(before, await _db.Warehouses.CountAsync(w => w.CompanyId == _companyId));
    }

    /// <summary>
    /// <b>والاختراعُ يبقى لمن يستحقّه</b> — منشأةٌ جديدة لا مستودعَ لها إطلاقاً تحصل على
    /// مستودعها الأوّل بلا أن يُسأل المالكُ عن شيءٍ في أوّل يوم.
    /// </summary>
    [Fact]
    public async Task ACompanyWithNoWarehouseAtAll_StillGetsItsFirstOne()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة جديدة", "310123456700088", BusinessProfile.Retail);
        foreach (var w in await _db.Warehouses.Where(w => w.CompanyId == company.Id).ToListAsync())
            _db.Warehouses.Remove(w);
        await _db.SaveChangesAsync();

        var warehouse = await new InventoryService(_db).GetDefaultWarehouseAsync(company.Id);

        Assert.NotNull(warehouse);
        Assert.True(warehouse.IsActive);
    }
}
