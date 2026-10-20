using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Hospitality;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Companies;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Setup;

/// <summary>
/// (جاهزيّةُ الإطلاق · <c>ui-ux-i18n-printing#7</c> · قرارُ المالك 2026-10-18) <b>ملفُّ النشاط يغيّره المديرُ — ولا يُخفي دفاترَ قائمة.</b>
///
/// <para><b>ما كان:</b> الملفُّ يُختار مرّةً في المعالج ولا طريقَ لتغييره. <b>والحارس</b>: الانتقالُ إلى ملفٍّ يُخفي وحدةً فيها بياناتٌ
/// يُرفض بأسمائها، والتوسّعُ يمرّ، والتغييرُ للمدير وحده ويُدقَّق.</para>
/// </summary>
public class TheBusinessProfileChangesOnlyWithoutHidingDataTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public TheBusinessProfileChangesOnlyWithoutHidingDataTests()
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
    }

    private async Task SetupAsync(BusinessProfile profile) =>
        _companyId = (await new DatabaseInitializer(_db).InitializeAsync("منشأة", "310123456700099", profile)).Id;

    private Task<BusinessProfile> ProfileAsync() =>
        _db.Companies.Where(c => c.Id == _companyId).Select(c => c.Profile).SingleAsync();

    [Fact]
    public async Task WideningTheProfile_IsAllowed_AndAudited()
    {
        await SetupAsync(BusinessProfile.General);

        await new BusinessProfileChangeService(_db).ChangeAsync(_companyId, BusinessProfile.Hospitality);

        Assert.Equal(BusinessProfile.Hospitality, await ProfileAsync());
        Assert.True(await _db.AuditLogEntries.AnyAsync(a => a.CompanyId == _companyId && a.Details.Contains("Hospitality")));
    }

    [Fact]
    public async Task AProfileThatHidesAModuleWithData_IsRefused_NamingIt()
    {
        await SetupAsync(BusinessProfile.Hospitality);
        _db.Rooms.Add(Room.Create(_companyId, "101", RoomKind.Single, 300m, floor: 1));
        await _db.SaveChangesAsync();

        // ‏الرفضُ يحمل الملفَّ والوحداتِ نفسَها — فالشاشةُ تعرضها بأسمائها المعروضة لا بأسمائها في الكود.
        var refused = await Assert.ThrowsAsync<ProfileHidesModulesWithDataException>(() =>
            new BusinessProfileChangeService(_db).ChangeAsync(_companyId, BusinessProfile.General));

        Assert.Equal(BusinessProfile.General, refused.Profile);
        Assert.Equal([ModuleKey.Hospitality], refused.Modules);
        Assert.Contains(nameof(ModuleKey.Hospitality), refused.Message, StringComparison.Ordinal);
        Assert.Equal(BusinessProfile.Hospitality, await ProfileAsync());
    }

    [Fact]
    public async Task AProfileThatHidesOnlyEmptyModules_IsAllowed()
    {
        await SetupAsync(BusinessProfile.Hospitality);

        await new BusinessProfileChangeService(_db).ChangeAsync(_companyId, BusinessProfile.General);

        Assert.Equal(BusinessProfile.General, await ProfileAsync());
    }

    [Fact]
    public async Task ANonAdministrator_CannotChangeIt()
    {
        await SetupAsync(BusinessProfile.General);
        TestActor.Accountant();

        await Assert.ThrowsAsync<AccountingException>(() =>
            new BusinessProfileChangeService(_db).ChangeAsync(_companyId, BusinessProfile.Retail));
        Assert.Equal(BusinessProfile.General, await ProfileAsync());
    }

    [Fact]
    public async Task EveryModuleWithItsOwnTable_IsAskedForData()
    {
        await SetupAsync(BusinessProfile.General);
        var customer = Customer.Create(_companyId, "عميل", "");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        var service = new BusinessProfileChangeService(_db);

        foreach (var module in Enum.GetValues<ModuleKey>())
            Assert.False(await service.HasDataAsync(_companyId, module), $"{module} يُعدّ ذا بياناتٍ في منشأةٍ فارغة");
    }
}
