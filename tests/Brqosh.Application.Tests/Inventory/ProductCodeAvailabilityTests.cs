using Brqosh.Infrastructure.Platform;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Inventory;

/// <summary>
/// (CEEF-229 · م2-٢٧) <b>لماذا يُرفض كودٌ لا يراه المستخدم في أيّ مكان.</b>
///
/// <para><b>العطل:</b> الفهرس الفريد على <c>(CompanyId, Code)</c> لا يعرف الحذف الناعم — فالصفّ
/// المحذوف يبقى في الجدول ويحجز كوده. وشاشةُ الصنف لا تفحص التكرار مسبقاً إطلاقاً، فيصطدم
/// الحفظُ بانتهاك تفرّدٍ من قاعدة البيانات ويُعرض نصُّ <c>DbUpdateException</c> <b>الإنجليزيّ</b>
/// كما هو داخل حوارٍ عربيّ. فالمالك لا يعرف السبب، ولا يرى الصنف الحاجز في أيّ قائمة.</para>
///
/// <para><b>ولم يُغيَّر الفهرس عمداً:</b> جعلُه جزئياً يسمح بكودَين متطابقَين أحدهما محذوف،
/// فيصير سجلّ الحركة القديم ملتبساً بين صنفَين — <b>والالتباس في الدفاتر أسوأ من رفضٍ مفهوم</b>.
/// فالإصلاح رسالةٌ تقول ما الذي يحجز الكود، لا فتحُ باب الازدواج.</para>
/// </summary>
public class ProductCodeAvailabilityTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private readonly long _companyId;

    public ProductCodeAvailabilityTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _companyId = new DatabaseInitializer(_db)
            .InitializeAsync("متجر", "310123456700099", BusinessProfile.Retail)
            .GetAwaiter().GetResult().Id;
    }

    private async Task<Product> AddAsync(string code)
    {
        var product = Product.Create(_companyId, code, "صنف", "قطعة", ProductType.Stockable, 10m);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return product;
    }

    private Task<CodeAvailability> CheckAsync(string code) =>
        new CodeAvailabilityQuery(_db).CheckAsync(CodeAvailabilityScope.Product, _companyId, code);

    /// <summary>كودٌ جديد متاح — وإلا لكان الفحص يمنع كل إنشاء.</summary>
    [Fact]
    public async Task AFreshCode_IsAvailable() =>
        Assert.Equal(CodeAvailability.Available, await CheckAsync("P-NEW"));

    /// <summary>وكودٌ لصنفٍ قائم يُرفض برسالةٍ تدلّ على مكان الصنف.</summary>
    [Fact]
    public async Task ACodeOfAnExistingProduct_IsReported()
    {
        await AddAsync("P-1");

        var availability = await CheckAsync("P-1");

        Assert.Equal(CodeAvailability.TakenByExistingRow, availability);
        Assert.Contains("أظهِر الموقوف", CodeAvailabilityQuery.Explain(availability, CodeAvailabilityScope.Product, "P-1")!);
    }

    /// <summary>
    /// <b>القياس المركزيّ.</b> صنفٌ <b>محذوف</b> يحجز كوده — والمالك لا يراه في أيّ قائمة.
    /// الرسالة تقول ذلك صراحةً بدل نصٍّ إنجليزيّ من قاعدة البيانات.
    /// </summary>
    [Fact]
    public async Task ACodeOfADeletedProduct_IsReportedAsSuch()
    {
        var product = await AddAsync("P-2");
        var sql = $"UPDATE products SET DeletedAtUtc = '2026-01-01 00:00:00' WHERE Id = {product.Id}";
        await _db.Database.ExecuteSqlRawAsync(sql);
        _db.ChangeTracker.Clear();

        var availability = await CheckAsync("P-2");

        Assert.Equal(CodeAvailability.TakenByDeletedRow, availability);
        Assert.Contains("محذوف", CodeAvailabilityQuery.Explain(availability, CodeAvailabilityScope.Product, "P-2")!);
    }

    /// <summary>
    /// <b>حارسٌ للحارس:</b> صنفٌ <b>موقوف</b> ليس محذوفاً — فيُبلَّغ عنه بوصفه قائماً، والرسالة
    /// تدلّ المالك على «أظهِر الموقوف» بدل أن تُوهمه أن الكود ضاع. ولولا هذا التمييز لاختلط
    /// الإصلاحان (م2-١٧ و م2-٢٧) في رسالةٍ واحدة مضلِّلة.
    /// </summary>
    [Fact]
    public async Task ACodeOfADeactivatedProduct_IsReportedAsExisting_NotDeleted()
    {
        var product = await AddAsync("P-3");
        product.Deactivate();
        await _db.SaveChangesAsync();

        Assert.Equal(CodeAvailability.TakenByExistingRow, await CheckAsync("P-3"));
    }

    /// <summary>وتعديلُ الصنف نفسه لا يصطدم بكوده — وإلا استحال حفظُ أيّ تعديل.</summary>
    [Fact]
    public async Task AProductDoesNotBlockItsOwnCode()
    {
        var product = await AddAsync("P-4");

        Assert.Equal(CodeAvailability.Available,
            await new CodeAvailabilityQuery(_db)
                .CheckAsync(CodeAvailabilityScope.Product, _companyId, "P-4", excludingId: product.Id));
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
