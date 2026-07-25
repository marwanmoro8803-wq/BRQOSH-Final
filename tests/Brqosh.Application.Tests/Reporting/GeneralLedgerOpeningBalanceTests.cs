using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// (CEEF-206) الأرصدة الافتتاحية في دفتر الأستاذ العام.
///
/// كانت تُحسب بتحميل كل تاريخ القيود بسطوره إلى الذاكرة؛ صارت تُجمَّع في قاعدة البيانات.
/// هذه الاختبارات تجمّد **النتيجة** حتى لا يغيّر تحسين الأداء الأرقام:
/// طبيعة الحساب (مدين/دائن)، واستبعاد ما بعد الفترة، والرصيد المتحرك.
/// </summary>
public class GeneralLedgerOpeningBalanceTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public GeneralLedgerOpeningBalanceTests()
    {
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة الأستاذ", "310123456700003", BusinessProfile.General);
        _companyId = company.Id;
    }

    private async Task PostAsync(DateOnly date, decimal amount, string description)
    {
        var cash = await _db.Accounts.FirstAsync(a => a.CompanyId == _companyId && a.Code == "1101" && !a.IsGroup);
        var capital = await _db.Accounts.FirstAsync(a => a.CompanyId == _companyId && a.Code == "3101" && !a.IsGroup);

        var entry = JournalEntry.CreateDraft(_companyId, date, description);
        entry.AddLine(cash, amount, 0m);
        entry.AddLine(capital, 0m, amount);
        await new JournalPostingService(_db).PostAsync(entry);
    }

    /// <summary>حركة ما قبل الفترة تظهر رصيداً افتتاحياً، لا سطراً داخل الفترة.</summary>
    [Fact]
    public async Task PriorMovement_BecomesOpeningBalance_NotAPeriodLine()
    {
        await SetupAsync();
        await PostAsync(new DateOnly(2026, 1, 15), 1000m, "قبل الفترة");
        await PostAsync(new DateOnly(2026, 6, 10), 250m, "داخل الفترة");

        var ledger = await new GeneralLedgerQuery(_db).ExecuteAsync(
            _companyId, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30));

        var cash = ledger.Single(a => a.AccountCode == "1101");

        var opening = cash.Lines.Single(l => l.Description == "رصيد افتتاحي");
        Assert.Equal(1000m, opening.Debit);          // النقدية مدينة الطبيعة
        Assert.Equal(1000m, opening.RunningBalance);

        Assert.Equal(1250m, cash.ClosingBalance);    // 1000 افتتاحي + 250 حركة
        Assert.Equal(2, cash.Lines.Count);           // افتتاحي + حركة واحدة فقط
    }

    /// <summary>الحساب الدائن الطبيعة (حقوق ملكية) يُحسب رصيده بالاتجاه المعاكس.</summary>
    [Fact]
    public async Task CreditNatureAccount_OpeningBalance_UsesCreditDirection()
    {
        await SetupAsync();
        await PostAsync(new DateOnly(2026, 1, 15), 800m, "رأس مال قبل الفترة");

        var ledger = await new GeneralLedgerQuery(_db).ExecuteAsync(
            _companyId, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30));

        // لا حركة داخل الفترة على رأس المال — فلا يظهر أصلاً في الأستاذ.
        Assert.DoesNotContain(ledger, a => a.AccountCode == "3101");

        // وبحركة داخل الفترة يظهر رصيده الافتتاحي دائناً موجباً.
        await PostAsync(new DateOnly(2026, 6, 20), 200m, "زيادة داخل الفترة");
        var withMovement = await new GeneralLedgerQuery(_db).ExecuteAsync(
            _companyId, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30));

        var capital = withMovement.Single(a => a.AccountCode == "3101");
        var opening = capital.Lines.Single(l => l.Description == "رصيد افتتاحي");
        Assert.Equal(800m, opening.Credit);
        Assert.Equal(1000m, capital.ClosingBalance);   // 800 + 200
    }

    /// <summary>حركة بعد نهاية الفترة لا تُحتسب في الرصيد الختامي.</summary>
    [Fact]
    public async Task MovementAfterPeriod_IsExcluded()
    {
        await SetupAsync();
        await PostAsync(new DateOnly(2026, 6, 10), 500m, "داخل الفترة");
        await PostAsync(new DateOnly(2026, 7, 5), 9_999m, "بعد الفترة");

        var ledger = await new GeneralLedgerQuery(_db).ExecuteAsync(
            _companyId, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30));

        Assert.Equal(500m, ledger.Single(a => a.AccountCode == "1101").ClosingBalance);
    }
}
