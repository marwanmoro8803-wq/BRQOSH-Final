using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Inventory;

/// <summary>
/// (CEEF-229 · م1-٢٢ / I-3) <b>تبديلُ طريقة التكلفة على مخزونٍ قائم.</b>
///
/// <para><c>Company.SetCostingMethod</c> كانت <b>سطراً واحداً بلا حارس</b>، وشاشةُ إعدادات
/// المنشأة تستدعيها وتحفظ. والطريقةُ تُقرأ في كلّ استلامٍ وكلّ صرف — <b>فتبديلُها يُغيّر
/// معنى البيانات القائمة بأثرٍ رجعيّ</b>.</para>
///
/// <para><b>والاتّجاهان مختلفان في العطل:</b></para>
///
/// <para>‏<b>متوسط ⟵ FIFO:</b> الطبقاتُ لا تُنشأ إلا في وضع FIFO، فالبضاعةُ القائمة بلا
/// طبقاتٍ إطلاقاً. <b>وأوّلُ بيعةٍ تسقط</b> بـ«طبقات FIFO غير كافية» — والمتجرُ يتوقّف عن
/// البيع كلّياً. عطلٌ صاخبٌ لا صامت، لكنّه يُقعِد المنشأة.</para>
///
/// <para>‏<b>FIFO ⟵ متوسط ثمّ عودة:</b> الطبقاتُ تتجمّد — تبقى كما هي بينما تُصرَف البضاعةُ
/// بالمتوسط. فالعودةُ إلى FIFO تجد طبقاتٍ <b>تدّعي كمّيةً لم تعد موجودة</b>، فتُستهلك بضاعةٌ
/// بيعت مرّةً أخرى بتكلفتها القديمة. <b>وهذا هو الصامت</b>.</para>
///
/// <para><b>والقرار: المنعُ لا الترميم.</b> بناءُ الطبقات بأثرٍ رجعيّ يحتاج تاريخَ استلامٍ
/// وتكاليفَ لا يملكها النظامُ بعد أن مضى في المتوسط — <b>فأيُّ ترميمٍ اختراعٌ لأرقامٍ لم تقع</b>.
/// والمنعُ مشروطٌ بوجود مخزون، فيبقى التبديلُ متاحاً في اليوم الأوّل حيث لا شيءَ يُفسده.</para>
/// </summary>
public class CostingMethodSwitchTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _warehouseId;
    private long _productId;

    public CostingMethodSwitchTests()
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

    /// <summary>
    /// <b>القياس المركزيّ.</b> مخزونٌ قائم ⟵ التبديلُ <b>يُرفض</b> برسالةٍ تقول لماذا وما
    /// المخرج، لا بسقوطِ أوّل بيعةٍ بعد أسبوع.
    /// </summary>
    [Fact]
    public async Task SwitchingWithStockOnHand_IsRefused()
    {
        await SetupAsync(InventoryCostingMethod.WeightedAverage);
        await ReceiveAsync(10m, 10m);

        var company = await _db.Companies.SingleAsync(c => c.Id == _companyId);

        var refusal = await Record.ExceptionAsync(() =>
            new CostingMethodService(_db).ChangeAsync(_companyId, InventoryCostingMethod.Fifo));

        Assert.True(refusal is AccountingException,
            "بُدّلت طريقةُ التكلفة ومخزونُ المنشأة قائم — والبضاعةُ القائمة تصير بلا طبقاتٍ "
            + "فتسقط أوّلُ بيعة، أو تُستهلك طبقاتٌ متجمّدة لبضاعةٍ بيعت.");

        await _db.Entry(company).ReloadAsync();
        Assert.Equal(InventoryCostingMethod.WeightedAverage, company.CostingMethod);
    }

    /// <summary>
    /// <b>والرسالةُ تقول المخرج.</b> «لا يمكن التبديل» وحدها تترك المالكَ بلا طريق؛ والصوابُ
    /// أن يعرف أنّ التصفية أو الجرد الافتتاحيّ يفتحان البابَ من جديد.
    /// </summary>
    [Fact]
    public async Task TheRefusal_NamesTheReasonAndTheWayOut()
    {
        await SetupAsync(InventoryCostingMethod.WeightedAverage);
        await ReceiveAsync(10m, 10m);

        var refusal = await Assert.ThrowsAsync<AccountingException>(() =>
            new CostingMethodService(_db).ChangeAsync(_companyId, InventoryCostingMethod.Fifo));

        Assert.Contains("مخزون", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("10", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>والاتّجاه الآخر — وهو ما يجعل الحارس قابلاً للشحن.</b> منشأةٌ لا مخزونَ لها تُبدّل
    /// بحرّية: هو اليومُ الأوّل، ولا شيءَ يُفسده التبديل.
    /// </summary>
    [Fact]
    public async Task SwitchingWithNoStock_IsAllowed()
    {
        await SetupAsync(InventoryCostingMethod.WeightedAverage);

        await new CostingMethodService(_db).ChangeAsync(_companyId, InventoryCostingMethod.Fifo);

        var company = await _db.Companies.SingleAsync(c => c.Id == _companyId);
        Assert.Equal(InventoryCostingMethod.Fifo, company.CostingMethod);
    }

    /// <summary>
    /// <b>ومخزونٌ صُفّي يفتح البابَ من جديد</b> — فالحارسُ يقيس الحالةَ الآن لا التاريخ.
    /// وحارسٌ يُغلق البابَ إلى الأبد بعد أوّل استلامٍ يُصبح قيداً لا حمايةً.
    /// </summary>
    [Fact]
    public async Task AfterTheStockIsCleared_SwitchingIsAllowedAgain()
    {
        await SetupAsync(InventoryCostingMethod.WeightedAverage);
        await ReceiveAsync(10m, 10m);
        await IssueAsync(10m);

        await new CostingMethodService(_db).ChangeAsync(_companyId, InventoryCostingMethod.Fifo);

        var company = await _db.Companies.SingleAsync(c => c.Id == _companyId);
        Assert.Equal(InventoryCostingMethod.Fifo, company.CostingMethod);
    }

    /// <summary>
    /// <b>وتبديلٌ إلى الطريقة نفسِها ليس تبديلاً</b> — فحفظُ شاشة الإعدادات يمرّ على هذا
    /// المسار في كلّ مرّة، ولو رُفض لصار المالكُ عاجزاً عن تغيير شعارِ منشأته لأنّ عنده بضاعة.
    /// </summary>
    [Fact]
    public async Task SettingTheSameMethod_IsNotAChange()
    {
        await SetupAsync(InventoryCostingMethod.Fifo);
        await ReceiveAsync(10m, 10m);

        await new CostingMethodService(_db).ChangeAsync(_companyId, InventoryCostingMethod.Fifo);

        var company = await _db.Companies.SingleAsync(c => c.Id == _companyId);
        Assert.Equal(InventoryCostingMethod.Fifo, company.CostingMethod);
    }

    /// <summary>
    /// <b>الضررُ نفسُه — مقيسٌ لا مُدّعى.</b> يتجاوز هذا الاختبارُ الحارسَ ويُبدّل الطريقةَ
    /// من النطاق مباشرةً، ليُظهر <b>لماذا</b> يمنع الحارسُ ما يمنع.
    ///
    /// <para>مخزونٌ قائمٌ بُني في وضع المتوسط ⟵ لا طبقاتِ FIFO له إطلاقاً ⟵ <b>أوّلُ صرفٍ
    /// بعد التبديل يسقط</b>. والمتجرُ لا يستطيع بيع شيءٍ حتى يُصفّى مخزونُه كلُّه.</para>
    ///
    /// <para><b>ويبقى نافعاً بعد الإصلاح</b>: النطاقُ ما زال يسمح بالتبديل (‏<c>SetCostingMethod</c>
    /// دالّةٌ عامّة)، فهذا يوثّق ثمنَ تجاوز الخدمة. ولو صار المنعُ في النطاق نفسِه سقط هذا
    /// الاختبارُ — وهو حينئذٍ خبرٌ سارّ يُقرأ من رسالته.</para>
    /// </summary>
    [Fact]
    public async Task SwitchingBehindTheGuard_BricksEveryIssue()
    {
        await SetupAsync(InventoryCostingMethod.WeightedAverage);
        await ReceiveAsync(10m, 10m);

        var company = await _db.Companies.SingleAsync(c => c.Id == _companyId);
        company.SetCostingMethod(InventoryCostingMethod.Fifo);
        await _db.SaveChangesAsync();

        var failure = await Assert.ThrowsAsync<AccountingException>(() => IssueAsync(3m));

        Assert.Contains("طبقات FIFO", failure.Message, StringComparison.Ordinal);
        Assert.Equal(10m, await new InventoryService(_db)
            .GetOnHandAsync(_companyId, _productId, _warehouseId));
    }

    private async Task SetupAsync(InventoryCostingMethod method)
    {
        var profile = method == InventoryCostingMethod.Fifo
            ? BusinessProfile.Retail
            : BusinessProfile.General;

        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة التكلفة", "310123456700044", profile);
        _companyId = company.Id;
        company.SetCostingMethod(method);
        await _db.SaveChangesAsync();

        var product = Product.Create(
            _companyId, "P-1", "صنف", "قطعة", ProductType.Stockable, 100m);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        _productId = product.Id;

        _warehouseId = _db.Warehouses.First(w => w.CompanyId == _companyId).Id;
    }

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
