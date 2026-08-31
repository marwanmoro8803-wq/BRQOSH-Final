using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Inventory;

/// <summary>
/// (CEEF-229 · م1-١٢ / A-5) <b>الجردُ يُرحَّل على لقطةٍ بائتة — فيُحسم العجزُ مرّتين.</b>
///
/// <para><b>ما يقع:</b> <c>PostVariancesAsync</c> تأخذ <c>SystemQty</c> <b>من المستدعي</b> —
/// أيْ من لقطةِ الشاشة لحظةَ فُتحت ورقةُ الجرد — ولا تُعيد قراءتَها داخل المعاملة. والفرقُ
/// يُرحَّل <b>حركةً مطلقة</b>: <c>SystemQty − CountedQty</c>.</para>
///
/// <para><b>فأمينان يفتحان الجردَ على المستودع نفسه</b> صباحاً، كلاهما يرى ١٠٠. الأوّل يعدّ
/// ٩٥ ويُرحّل عجزَ ٥ ⟵ الرصيد ٩٥. والثاني يُرحّل بعده بدقيقة، <b>ولقطتُه ما زالت ١٠٠</b>،
/// فيُرحّل عجزَ ٥ مرّةً أخرى ⟵ <b>الرصيد ٩٠</b>. ونقصت البضاعةُ عشرةً وما نقص إلا خمسة.</para>
///
/// <para><b>وليس سباقاً</b> — لا يُصلحه قفلٌ ولا رمزُ تزامن. الترحيلان متباعدان في الزمن،
/// وكلُّ معاملةٍ منهما سليمةٌ بذاتها. <b>العطلُ أنّ الرقمَ الذي بُني عليه الفرق قديم</b>.
/// ولهذا يقع بصمتٍ تامّ: لا رسالةَ ولا تعارضَ ولا استثناء — <b>رقمٌ خاطئٌ في دفترٍ صحيح</b>.</para>
///
/// <para><b>والأثرُ مزدوج:</b> الدفترُ المساعد ينزل ضعفَ ما نقص، <b>والأستاذُ ينزل معه</b> —
/// فالهوية هـ-٣ تبقى صادقة ولا يكشفه حارسُها. الخسارةُ حقيقيةٌ ومتّسقة.</para>
/// </summary>
public class StaleStockCountTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private readonly long _companyId;
    private readonly long _warehouseId;
    private readonly long _productId;

    public StaleStockCountTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _companyId = new DatabaseInitializer(_db)
            .InitializeAsync("متجر الجرد", "310123456700077", BusinessProfile.Retail)
            .GetAwaiter().GetResult().Id;
        new BrqoshSchemaUpdater(_db).ApplyAsync().GetAwaiter().GetResult();

        var product = Product.Create(
            _companyId, "P-1", "صنف الجرد", "قطعة", ProductType.Stockable, 100m);
        _db.Products.Add(product);
        _db.SaveChanges();
        _productId = product.Id;

        _warehouseId = _db.Warehouses.First(w => w.CompanyId == _companyId).Id;

        // ‏رصيدٌ افتتاحيّ ١٠٠ بتكلفةٍ حقيقية — فالجردُ اللاحق عجزٌ لا تأسيس.
        ReceiveAsync(100m, 10m).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// <b>القياس المركزيّ.</b> ترحيلان بلقطةٍ واحدة: الثاني يجب أن <b>يُرفض</b>، لا أن
    /// يحسم العجزَ ثانيةً.
    /// </summary>
    [Fact]
    public async Task SecondPost_WithTheSameStaleSnapshot_IsRefused()
    {
        var snapshot = await OnHandAsync();
        Assert.Equal(100m, snapshot);

        await PostAsync(systemQty: snapshot, countedQty: 95m);
        Assert.Equal(95m, await OnHandAsync());

        // ‏الأمينُ الثاني يُرحّل ورقتَه — ولقطتُها ما زالت ١٠٠.
        var refusal = await Record.ExceptionAsync(() => PostAsync(systemQty: snapshot, countedQty: 95m));

        Assert.True(refusal is AccountingException,
            $"رُحّل جردٌ على لقطةٍ بائتة بلا اعتراض — والرصيد {await OnHandAsync()} بعد أن نقص خمساً "
            + "لا عشراً. فالبضاعةُ ضاعت من الدفتر ومن الأستاذ معاً، بصمتٍ تامّ.");

        Assert.Equal(95m, await OnHandAsync());
    }

    /// <summary>
    /// <b>والرسالةُ تُسمّي ما تغيّر.</b> «تغيّر الرصيد» وحدها تترك الأمينَ يعيد العدّ كلَّه؛
    /// أمّا الصنفُ والرقمان فتقول له أين ينظر.
    /// </summary>
    [Fact]
    public async Task TheRefusal_NamesTheProductAndBothNumbers()
    {
        var snapshot = await OnHandAsync();
        await PostAsync(systemQty: snapshot, countedQty: 95m);

        var refusal = await Assert.ThrowsAsync<AccountingException>(
            () => PostAsync(systemQty: snapshot, countedQty: 95m));

        Assert.Contains("صنف الجرد", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("100", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("95", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>والاتّجاه الآخر — وهو ما يجعل الحارس قابلاً للشحن.</b> جردٌ بلقطةٍ صادقة يمرّ.
    /// وحارسٌ يرفض الجردَ السليم يُعطّل الوظيفةَ كلَّها، ويُعلّم المستخدمَ تجاهلَ رسائله.
    /// </summary>
    [Fact]
    public async Task AFreshSnapshot_PostsNormally()
    {
        await PostAsync(systemQty: await OnHandAsync(), countedQty: 95m);
        Assert.Equal(95m, await OnHandAsync());

        // ‏ورقةٌ ثانية تُفتح **بعد** الأولى: لقطتُها ٩٥، فتمرّ.
        await PostAsync(systemQty: await OnHandAsync(), countedQty: 90m);
        Assert.Equal(90m, await OnHandAsync());
    }

    /// <summary>
    /// <b>وحركةُ بيعٍ بين العدّ والترحيل تُبطل الورقة أيضاً</b> — وهي الصورةُ الأشيع: يُعَدّ
    /// المستودعُ صباحاً وتُرحَّل الورقةُ ظهراً، وبينهما بيعٌ. فلو مرّ الترحيلُ لأعاد البضاعةَ
    /// المباعة إلى الدفتر.
    /// </summary>
    [Fact]
    public async Task ASaleBetweenCountingAndPosting_InvalidatesTheSheet()
    {
        var snapshot = await OnHandAsync();

        await IssueAsync(3m);
        Assert.Equal(97m, await OnHandAsync());

        await Assert.ThrowsAsync<AccountingException>(
            () => PostAsync(systemQty: snapshot, countedQty: 100m));

        Assert.Equal(97m, await OnHandAsync());
    }

    private Task<int> PostAsync(decimal systemQty, decimal countedQty) =>
        new StockCountAdjustmentService(_db).PostVariancesAsync(
            _companyId, _warehouseId, new DateOnly(2026, 3, 1),
            [(_productId, systemQty, countedQty, null)], "admin");

    private Task<decimal> OnHandAsync() =>
        new InventoryService(_db).GetOnHandAsync(_companyId, _productId, _warehouseId);

    private async Task ReceiveAsync(decimal quantity, decimal unitCost)
    {
        var movement = StockMovement.CreateDraft(
            _companyId, new DateOnly(2026, 1, 1), StockMovementType.AdjustmentIn,
            _warehouseId, "رصيد افتتاحيّ");
        movement.AddLine(_productId, quantity, unitCost);
        await new StockMovementPostingService(_db).PostAsync(movement);
    }

    private async Task IssueAsync(decimal quantity)
    {
        var movement = StockMovement.CreateDraft(
            _companyId, new DateOnly(2026, 2, 1), StockMovementType.AdjustmentOut,
            _warehouseId, "صرف");
        movement.AddLine(_productId, quantity, 10m);
        await new StockMovementPostingService(_db).PostAsync(movement);
    }
}
