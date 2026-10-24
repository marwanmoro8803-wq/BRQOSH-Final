using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Companies;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Setup;

/// <summary>
/// (أسئلةُ المالك 2026-10-24 · العملة) <b>عملةُ المنشأة لا تتغيّر بعد أن تُقيَّد دفاترُها بها.</b>
///
/// <para>كانت الشاشةُ تستدعي <c>SetCurrencies</c> مباشرةً: «SAR» ← «USD» على منشأةٍ فيها قيودٌ تُعيد تسميةَ كلِّ مبلغٍ قائمٍ بلا
/// أن يتغيّر رقم، بلا قيدٍ ولا أثر تدقيق. والحقلُ كان يقبل أيَّ ثلاثة أحرف («ABC»).</para>
/// </summary>
public class TheCompanyCurrencyIsLockedOnceBooksExistTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public TheCompanyCurrencyIsLockedOnceBooksExistTests()
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

    private async Task SetupAsync() =>
        _companyId = (await new DatabaseInitializer(_db).InitializeAsync("منشأة", "310123456700099", BusinessProfile.General)).Id;

    private Task<string> FunctionalAsync() =>
        _db.Companies.Where(c => c.Id == _companyId).Select(c => c.FunctionalCurrencyCode).SingleAsync();

    [Fact]
    public async Task ACompanyWithoutBooks_CanChangeItsCurrency_AndItIsAudited()
    {
        await SetupAsync();

        await new CompanyCurrencyChangeService(_db).ChangeAsync(_companyId, "usd", "usd");

        Assert.Equal("USD", await FunctionalAsync());
        Assert.True(await _db.AuditLogEntries.AnyAsync(a => a.CompanyId == _companyId && a.Details.Contains("SAR←USD")));
    }

    [Fact]
    public async Task ACompanyWithAJournalEntry_IsRefused_AndKeepsItsCurrency()
    {
        await SetupAsync();
        _db.JournalEntries.Add(JournalEntry.CreateDraft(_companyId, new DateOnly(2026, 5, 1), "قيدٌ أوّل"));
        await _db.SaveChangesAsync();

        var refused = await Assert.ThrowsAsync<CurrencyLockedException>(() =>
            new CompanyCurrencyChangeService(_db).ChangeAsync(_companyId, "USD", "USD"));

        Assert.Equal("SAR", refused.CurrentCurrency);
        Assert.Equal("SAR", await FunctionalAsync());
    }

    [Fact]
    public async Task SavingTheSameCurrencyAgain_IsNotARefusalEvenWithBooks()
    {
        // ‏الحفظُ العاديُّ للشاشة يمرّ بالخدمة كلَّ مرّة — ولا يكون رفضاً أن تُحفظ العملةُ كما هي.
        await SetupAsync();
        _db.JournalEntries.Add(JournalEntry.CreateDraft(_companyId, new DateOnly(2026, 5, 1), "قيدٌ أوّل"));
        await _db.SaveChangesAsync();

        await new CompanyCurrencyChangeService(_db).ChangeAsync(_companyId, "sar", " SAR ");

        Assert.Equal("SAR", await FunctionalAsync());
    }

    [Theory]
    [InlineData("ABC")]
    [InlineData("US")]
    [InlineData("12A")]
    [InlineData("")]
    public async Task ACodeThatIsNotAKnownCurrency_IsRefused(string code)
    {
        await SetupAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new CompanyCurrencyChangeService(_db).ChangeAsync(_companyId, code, "SAR"));
        Assert.Equal("SAR", await FunctionalAsync());
    }

    [Fact]
    public async Task TheAuditorName_IsKeptWithTheCompany_AndReachesThePrintedFooter()
    {
        await SetupAsync();
        var company = await _db.Companies.SingleAsync(c => c.Id == _companyId);
        company.SetAuditor("  مكتب الأمانة للمراجعة  ");
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var reloaded = await _db.Companies.SingleAsync(c => c.Id == _companyId);
        Assert.Equal("مكتب الأمانة للمراجعة", reloaded.AuditorName);

        var branding = Brqosh.Infrastructure.Reporting.PrintBranding.FromCompany(reloaded, "خالد العتيبي");
        var onReports = branding.PeopleLine(withAuditor: true);
        var onInvoices = branding.PeopleLine(withAuditor: false);
        Assert.Equal("أعدّه: خالد العتيبي   ·   المراجع: مكتب الأمانة للمراجعة", onReports);
        // ‏الفاتورةُ والسندُ ليسا مراجَعَين — يظهر فيهما من أعدّهما وحدَه.
        Assert.Equal("أعدّه: خالد العتيبي", onInvoices);
    }

    [Fact]
    public async Task WithoutAnAuditorOrAUser_TheFooterSaysNothingExtra()
    {
        await SetupAsync();
        var company = await _db.Companies.SingleAsync(c => c.Id == _companyId);

        var branding = Brqosh.Infrastructure.Reporting.PrintBranding.FromCompany(company);

        Assert.Null(branding.PeopleLine(withAuditor: true));
    }

    [Fact]
    public async Task ANonAdministrator_CannotChangeIt()
    {
        await SetupAsync();
        TestActor.Accountant();

        await Assert.ThrowsAsync<AccountingException>(() =>
            new CompanyCurrencyChangeService(_db).ChangeAsync(_companyId, "USD", "USD"));
        Assert.Equal("SAR", await FunctionalAsync());
    }
}
