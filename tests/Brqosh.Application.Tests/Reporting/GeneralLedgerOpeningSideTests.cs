using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// (CEEF-228) عمودا المدين والدائن لسطر «رصيد افتتاحي» في دفتر الأستاذ.
///
/// `GeneralLedgerOpeningBalanceTests` القائم يغطّي حالتين فقط — حساباً مدين الطبيعة
/// برصيد موجب، وحساباً دائن الطبيعة برصيد موجب. الحالة الرابعة (دائن الطبيعة برصيد
/// افتتاحيّ **مدين**) لم تكن مغطّاة، وهي التي كان التعبير الثلاثيّ يُخطئها.
/// </summary>
public class GeneralLedgerOpeningSideTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public GeneralLedgerOpeningSideTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";
    }

    public void Dispose() { _db.Dispose(); _connection.Dispose(); }

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة أستاذ", "310123456700003", BusinessProfile.General);
        _companyId = company.Id;
    }

    private async Task PostAsync(DateOnly date, string description,
        params (string Code, decimal Debit, decimal Credit)[] lines)
    {
        // ‏(ح-١٥٣) ‏تُمثّل قيودَ مستنداتٍ (دفعةٌ مقدّمةٌ وفاتورة) — فلها مصدر؛ والقيدُ اليدويُّ على الذمم صار ممنوعاً.
        var entry = JournalEntry.CreateDraft(_companyId, date, description, sourceDocument: $"Doc:{description}");
        foreach (var (code, debit, credit) in lines)
        {
            var account = await _db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code == code);
            entry.AddLine(account, debit, credit, description);
        }
        await new JournalPostingService(_db).PostAsync(entry);
    }

    /// <summary>
    /// مورد دُفع له مقدماً 5,000 في 2025 (مدين 2101 وهو دائن الطبيعة)، ثم وردت فاتورته
    /// 3,000 في يناير 2026. أستاذ يناير 2026 يجب أن يبدأ بـ**مدين 5,000**.
    /// كان يُطبع «دائن 5,000» — فيصير مجموع الأعمدة 0/8,000 والحقيقة 5,000/3,000،
    /// ويفترق الأستاذ عن ميزان المراجعة بـ10,000.
    /// </summary>
    [Fact]
    public async Task CreditNatureAccount_WithDebitOpeningBalance_UsesDebitColumn()
    {
        await SetupAsync();
        await PostAsync(new DateOnly(2025, 12, 1), "دفعة مقدمة لمورد",
            (SaudiChartOfAccounts.SystemCodes.AccountsPayable, 5000m, 0m),
            (SaudiChartOfAccounts.SystemCodes.CashOnHand, 0m, 5000m));

        await PostAsync(new DateOnly(2026, 1, 20), "فاتورة المورد",
            (SaudiChartOfAccounts.SystemCodes.SalariesExpense, 3000m, 0m),
            (SaudiChartOfAccounts.SystemCodes.AccountsPayable, 0m, 3000m));

        var result = await new GeneralLedgerQuery(_db).ExecuteAsync(
            _companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31),
            SaudiChartOfAccounts.SystemCodes.AccountsPayable);

        var account = result.Single(a => a.AccountCode == SaudiChartOfAccounts.SystemCodes.AccountsPayable);
        var opening = account.Lines.First();

        Assert.Equal("رصيد افتتاحي", opening.Description);
        Assert.Equal(5000m, opening.Debit);
        Assert.Equal(0m, opening.Credit);

        // ومجموع الأعمدة يجب أن يطابق الواقع: 5,000 مدين مقابل 3,000 دائن.
        Assert.Equal(5000m, account.Lines.Sum(l => l.Debit));
        Assert.Equal(3000m, account.Lines.Sum(l => l.Credit));
    }

    /// <summary>الحالة المقابلة: حسابٌ مدين الطبيعة برصيد افتتاحيّ **دائن** يقع في عمود الدائن.</summary>
    [Fact]
    public async Task DebitNatureAccount_WithCreditOpeningBalance_UsesCreditColumn()
    {
        await SetupAsync();
        await PostAsync(new DateOnly(2025, 12, 1), "تحصيل مقدم من عميل",
            (SaudiChartOfAccounts.SystemCodes.CashOnHand, 2000m, 0m),
            (SaudiChartOfAccounts.SystemCodes.AccountsReceivable, 0m, 2000m));

        await PostAsync(new DateOnly(2026, 1, 20), "فاتورة العميل",
            (SaudiChartOfAccounts.SystemCodes.AccountsReceivable, 500m, 0m),
            (SaudiChartOfAccounts.SystemCodes.SalesRevenue, 0m, 500m));

        var result = await new GeneralLedgerQuery(_db).ExecuteAsync(
            _companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31),
            SaudiChartOfAccounts.SystemCodes.AccountsReceivable);

        var opening = result
            .Single(a => a.AccountCode == SaudiChartOfAccounts.SystemCodes.AccountsReceivable)
            .Lines.First();

        Assert.Equal(0m, opening.Debit);
        Assert.Equal(2000m, opening.Credit);
    }
}
