using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Platform;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Platform;

/// <summary>
/// (CEEF-229 · م2-١٠ / D-6) <b>القياس السلوكيّ لفحص إتاحة الكود.</b>
///
/// <para>حارسا <c>DeletedCodeExplanationTests</c> نصّيّان: يُثبتان أنّ لكلّ كيانٍ فحصاً وأنّ
/// الشاشاتِ تستدعيه. <b>ولا يُثبتان أنّ الفحصَ يقول الصوابَ</b> — وهذا عملُ هذا الملفّ.</para>
///
/// <para><b>والحالةُ الحرجة هي الصفُّ المحذوف</b>: مُرشَّحُ الحذف الناعم يُخفيه عن كلّ
/// استعلامٍ عاديّ، <b>فيمرّ فحصٌ يعتمد على <c>AnyAsync</c> ثمّ يصطدم بالفهرس الفريد</b> —
/// وتلك هي الحالةُ التي تُنتج نصَّ المحرّك الإنجليزيّ في واجهةٍ عربية.</para>
/// </summary>
public class CodeAvailabilityTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public CodeAvailabilityTests()
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
    /// <b>القياس المركزيّ.</b> مستودعٌ يُحذف حذفاً ناعماً ⟵ كودُه <b>ما زال محجوزاً</b>،
    /// والفحصُ يقول ذلك بدل أن يُترك المحرّكُ يتحدّث.
    /// </summary>
    [Fact]
    public async Task ADeletedRow_StillHoldsItsCode_AndTheCheckSaysSo()
    {
        await SetupAsync();

        var warehouse = Warehouse.Create(_companyId, "W-9", "مستودع مؤقّت", isDefault: false);
        _db.Warehouses.Add(warehouse);
        await _db.SaveChangesAsync();

        await SoftDeleteAsync(warehouse);

        Assert.False(await _db.Warehouses.AnyAsync(w => w.Code == "W-9"),
            "القياسُ أجوف — الصفُّ ما زال ظاهراً للاستعلام العاديّ، فلا حذفَ ناعمَ هنا.");

        var availability = await new CodeAvailabilityQuery(_db)
            .CheckAsync(CodeAvailabilityScope.Warehouse, _companyId, "W-9");

        Assert.Equal(CodeAvailability.TakenByDeletedRow, availability);

        var reason = CodeAvailabilityQuery.Explain(
            availability, CodeAvailabilityScope.Warehouse, "W-9");

        Assert.NotNull(reason);
        Assert.Contains("محذوف", reason, StringComparison.Ordinal);
        Assert.Contains("W-9", reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>ويُفرَّق بين المحجوز لقائمٍ والمحجوز لمحذوف</b> — فالمخرجُ مختلف: الأوّلُ يُعثر
    /// عليه بإظهار الموقوف، والثاني لا يُرى بحال.
    /// </summary>
    [Fact]
    public async Task AnExistingRow_IsReportedDifferentlyFromADeletedOne()
    {
        await SetupAsync();

        _db.Warehouses.Add(Warehouse.Create(_companyId, "W-1", "مستودع قائم", isDefault: false));
        await _db.SaveChangesAsync();

        Assert.Equal(CodeAvailability.TakenByExistingRow, await new CodeAvailabilityQuery(_db)
            .CheckAsync(CodeAvailabilityScope.Warehouse, _companyId, "W-1"));
    }

    /// <summary>
    /// <b>والاتّجاه الآخر — وهو ما يجعل الفحص قابلاً للشحن.</b> كودٌ حرٌّ متاح، والصفُّ
    /// نفسُه لا يمنع كودَه عند تعديله.
    /// </summary>
    [Fact]
    public async Task AFreeCodeIsAvailable_AndARowDoesNotBlockItself()
    {
        await SetupAsync();

        Assert.Equal(CodeAvailability.Available, await new CodeAvailabilityQuery(_db)
            .CheckAsync(CodeAvailabilityScope.Warehouse, _companyId, "W-FREE"));

        var warehouse = Warehouse.Create(_companyId, "W-2", "مستودع", isDefault: false);
        _db.Warehouses.Add(warehouse);
        await _db.SaveChangesAsync();

        Assert.Equal(CodeAvailability.Available, await new CodeAvailabilityQuery(_db)
            .CheckAsync(CodeAvailabilityScope.Warehouse, _companyId, "W-2", excludingId: warehouse.Id));
    }

    /// <summary>
    /// <b>والفحصُ يعمل لكيانٍ آخر</b> — فالتعميمُ ليس دعوى: مركزُ تكلفةٍ محذوف يحجز كودَه
    /// كما يفعل المستودع.
    /// </summary>
    [Fact]
    public async Task TheCheckWorksForASecondEntity_NotJustWarehouses()
    {
        await SetupAsync();

        var center = Brqosh.Domain.Accounting.CostCenter.Create(_companyId, "CC-9", "مركز مؤقّت");
        _db.CostCenters.Add(center);
        await _db.SaveChangesAsync();

        await SoftDeleteAsync(center);

        Assert.Equal(CodeAvailability.TakenByDeletedRow, await new CodeAvailabilityQuery(_db)
            .CheckAsync(CodeAvailabilityScope.CostCenter, _companyId, "CC-9"));
    }

    /// <summary>
    /// ‏حذفٌ ناعم: يُضبط <c>DeletedAtUtc</c> عبر EF نفسِه.
    ///
    /// <para><b>ولا يوجد اليوم مسارُ حذفٍ ناعمٍ في النطاق لهذين الكيانين</b> — العمودُ
    /// والمرشّحُ قائمان ولا كاتبَ لهما. فالقياسُ يضع الحالةَ مباشرةً: <b>السؤالُ هل يراها
    /// الفحص، لا كيف نشأت</b>. ولو أُضيف مسارُ حذفٍ غداً لبقي هذا صادقاً.</para>
    /// </summary>
    private async Task SoftDeleteAsync(object entity)
    {
        _db.Entry(entity).Property("DeletedAtUtc").CurrentValue = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة الأكواد", "310123456700022", BusinessProfile.Retail);
        _companyId = company.Id;
    }
}
