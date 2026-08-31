using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Platform;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Platform;

/// <summary>
/// (CEEF-229) <b>الصفُّ المحذوف حذفاً ناعماً: يُرى ويُسترجَع.</b>
///
/// <para>كان يبقى في الجدول، <b>محجوباً عن كلّ شاشة</b>، حاجزاً كودَه، بلا سبيلٍ إلى إعادته.
/// وسُجّل بنداً مفتوحاً منذ <c>م2-٢٧</c>.</para>
///
/// <para><b>وأخطرُ ممّا يبدو:</b> تعبئةٌ في المُرقِّع كانت تُنفَّذ في كلّ إقلاع فتُحوّل كلَّ
/// صفٍّ <b>موقوف</b> إلى <b>محذوف</b>. أُوقفت في <c>م0-١٣</c> <b>ولم يُسترجَع ما حوّلته</b> —
/// فحسابٌ أوقفه المحاسبُ غاب عن شجرة الحسابات بلا رجعة.</para>
///
/// <para><b>ولا يُصلَح آلياً</b>: الصفُّ الذي حوّلته التعبئةُ والمحذوفُ قصداً لا يفترقان في
/// البيانات. <b>فالإظهارُ والقرارُ بيد المالك</b> — مذهبُ م2-١١ نفسُه.</para>
/// </summary>
public class SoftDeletedRowTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public SoftDeletedRowTests()
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
    /// <b>القياس المركزيّ.</b> صفٌّ محذوفٌ يُرى في القائمة، ويُسترجَع، فيعود إلى الاستعلام
    /// العاديّ — <b>ويصير كودُه متاحاً بمعناه الصحيح: مستعمَلٌ لصفٍّ قائم</b>.
    /// </summary>
    [Fact]
    public async Task ADeletedRow_IsListedAndCanBeRestored()
    {
        await SetupAsync();
        var warehouse = await DeletedWarehouseAsync("W-9", "مستودع محجوب");

        var listed = await new SoftDeletedRowService(_db)
            .ListAsync(CodeAvailabilityScope.Warehouse, _companyId);

        Assert.Single(listed);
        Assert.Equal("W-9", listed[0].Code);

        await new SoftDeletedRowService(_db)
            .RestoreAsync(CodeAvailabilityScope.Warehouse, _companyId, warehouse, "admin");

        Assert.True(await _db.Warehouses.AnyAsync(w => w.Code == "W-9"),
            "استُرجع الصفُّ ولم يعد يظهر في الاستعلام العاديّ — فالاسترجاعُ لم يقع.");

        Assert.Equal(CodeAvailability.TakenByExistingRow, await new CodeAvailabilityQuery(_db)
            .CheckAsync(CodeAvailabilityScope.Warehouse, _companyId, "W-9"));
    }

    /// <summary>
    /// <b>ولماذا لا يحتاج الاسترجاعُ حارسَ تصادم — وهو ما قاله القياس.</b>
    ///
    /// <para>بنيتُ فحصاً يرفض الاسترجاعَ إن صار الكودُ مستعمَلاً، <b>فسقط الاختبارُ عند
    /// تهيئته</b>: إنشاءُ صفٍّ حيٍّ بكود المحذوف <b>مستحيلٌ أصلاً</b> — الفهرسُ الفريد يمنعه،
    /// وهو نفسُه سببُ هذا البند كلِّه.</para>
    ///
    /// <para>فهذا الاختبارُ يُثبّت <b>السبب</b>: ما دام الكودُ محجوزاً فلا صفَّ حيٌّ يحمله،
    /// <b>فالاسترجاعُ لا يتصادم أبداً</b>. ولو جُزّئ الفهرسُ يوماً لسقط هذا الاختبارُ —
    /// وحينها يلزم الحارسُ الذي أزلتُه.</para>
    /// </summary>
    [Fact]
    public async Task ADeletedCodeCannotBeTakenByALiveRow_SoRestoreNeverConflicts()
    {
        await SetupAsync();
        await DeletedWarehouseAsync("W-9", "مستودع محجوب");

        _db.Warehouses.Add(Warehouse.Create(_companyId, "W-9", "مستودع جديد", isDefault: false));

        await Assert.ThrowsAnyAsync<Exception>(() => _db.SaveChangesAsync());
        _db.ChangeTracker.Clear();

        Assert.Equal(CodeAvailability.TakenByDeletedRow, await new CodeAvailabilityQuery(_db)
            .CheckAsync(CodeAvailabilityScope.Warehouse, _companyId, "W-9"));
    }

    /// <summary>
    /// <b>والاسترجاعُ يترك أثراً.</b> إعادةُ صفٍّ إلى الدفاتر تغييرٌ لا يقلّ عن حذفه —
    /// <b>وحذفُه نفسُه كان بلا أثرٍ في بعض المسارات</b>.
    /// </summary>
    [Fact]
    public async Task RestoringLeavesAnAuditTrail()
    {
        await SetupAsync();
        var warehouse = await DeletedWarehouseAsync("W-7", "مستودع محجوب");

        await new SoftDeletedRowService(_db)
            .RestoreAsync(CodeAvailabilityScope.Warehouse, _companyId, warehouse, "admin");

        var trail = await _db.AuditLogEntries.AsNoTracking()
            .Where(a => a.CompanyId == _companyId && a.Action.Contains("استرجاع"))
            .ToListAsync();

        Assert.Single(trail);
        Assert.Contains("W-7", trail[0].Details, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(trail[0].Reason));
    }

    /// <summary>
    /// <b>والاتّجاه الآخر:</b> منشأةٌ لا محذوفَ فيها تُعيد قائمةً فارغة — <b>فلا يُوهَم
    /// المالكُ بأنّ في دفاتره ما يُسترجَع</b>.
    /// </summary>
    [Fact]
    public async Task NothingDeleted_ListsNothing()
    {
        await SetupAsync();
        _db.Warehouses.Add(Warehouse.Create(_companyId, "W-1", "مستودع", isDefault: false));
        await _db.SaveChangesAsync();

        Assert.Empty(await new SoftDeletedRowService(_db)
            .ListAsync(CodeAvailabilityScope.Warehouse, _companyId));
    }

    /// <summary>
    /// <b>ويُشترط دورُ مديرِ النظام.</b> استرجاعُ صفٍّ من الدفاتر ليس عملاً روتينياً —
    /// <b>وهو درسُ م2-١ بعينه</b>: عمليةٌ حسّاسة بلا حارسِ دور.
    /// </summary>
    [Fact]
    public async Task RestoringRequiresAnAdministrator()
    {
        await SetupAsync();
        var warehouse = await DeletedWarehouseAsync("W-5", "مستودع محجوب");

        TestActor.Accountant();
        await Assert.ThrowsAnyAsync<Exception>(() =>
            new SoftDeletedRowService(_db)
                .RestoreAsync(CodeAvailabilityScope.Warehouse, _companyId, warehouse, "acc"));

        TestActor.Administrator();
        Assert.Single(await new SoftDeletedRowService(_db)
            .ListAsync(CodeAvailabilityScope.Warehouse, _companyId));
    }

    private async Task<long> DeletedWarehouseAsync(string code, string name)
    {
        var warehouse = Warehouse.Create(_companyId, code, name, isDefault: false);
        _db.Warehouses.Add(warehouse);
        await _db.SaveChangesAsync();

        // ‏صورةُ ما فعلته تعبئةُ المُرقِّع: يُضبط الحقلُ مباشرةً، فلا مسارَ نطاقٍ يفعله.
        _db.Entry(warehouse).Property("DeletedAtUtc").CurrentValue = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        return warehouse.Id;
    }

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة المحذوف", "310123456700066", BusinessProfile.Retail);
        _companyId = company.Id;
    }
}
