using System.Data.Common;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Treasury;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Brqosh.Application.Tests.Treasury;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٩٣ · <c>treasury-payroll-assets#13</c>) <b>التسويةُ البنكيّةُ وإعادةُ تقييم العملة تقرآن سطورَ البنك وحدَها — وكانتا
/// تُحمّلان الدفترَ كلَّه.</b>
///
/// <para><c>GetOpeningBalanceAsync</c> يُحمّل كلَّ قيدٍ قبل الفترة، و<c>GetBookLinesAsync</c> كلَّ قيدٍ فيها، و<c>GetBookedFunctionalBalanceAsync</c>
/// كلَّ قيدٍ حتى تاريخه — <b>والسطورُ مملوكةٌ تُحمَّل مع كلّ قيد</b> — ثمّ يُرشَّح الحسابُ في الذاكرة. فدفترٌ بمليون قيد (ح-٥٩) يُقرأ كلُّه لرصيد
/// حسابٍ واحد، في كلّ فتحٍ لشاشة التسوية وكلّ مطابقةٍ آليّة.</para>
///
/// <para><b>والقياسُ بعدد الصفوف المُعادة لا بعدد المتتبَّع</b> — فكلُّ <c>SELECT</c> يُعاد بـ<c>COUNT(*)</c> على الاتّصال نفسِه. <b>والحكمُ ثباتُها</b>:
/// ستّون قيداً لا تمسّ البنك تُضاف، فلا يزيد ما يُقرأ صفّاً واحداً.</para>
/// </summary>
public class TheBankReadsOnlyItsOwnLinesTests : IDisposable
{
    private static readonly DateOnly Day = new(2026, 7, 8);
    private readonly SqliteConnection _connection;
    private readonly RowCounter _rows = new();
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private BankAccount _bank = null!;

    public TheBankReadsOnlyItsOwnLinesTests()
    {
        TestActor.Administrator();
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).AddInterceptors(_rows).Options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// <b>القياسُ الحاكم: ملخّصُ التسوية يقرأ ما يقرأ مهما كبر ما لا يمسّ البنك</b> — والأرقامُ صحيحة: افتتاحيٌّ ١٬٠٠٠ وحركةٌ ٥٬٠٠٠.
    /// </summary>
    [Fact]
    public async Task TheReconciliationSummary_ReadsTheSameRows_HoweverLargeTheRestOfTheLedger()
    {
        await SetupAsync(currency: null);
        await PostAsync(await BankGlAsync(), Day.AddDays(-10), 1_000m);
        await PostAsync(await BankGlAsync(), Day, 5_000m);
        await UnrelatedAsync(10);

        var (small, summary) = await MeasureAsync(() =>
            new BankReconciliationQuery(_db).GetSummaryAsync(_companyId, _bank.Id, Day, Day));
        await UnrelatedAsync(60);
        var (large, again) = await MeasureAsync(() =>
            new BankReconciliationQuery(_db).GetSummaryAsync(_companyId, _bank.Id, Day, Day));

        Assert.Equal((1_000m, 5_000m, 6_000m, 1), (summary.OpeningBalance, summary.PeriodMovement, summary.ClosingBalance, summary.TotalLines));
        Assert.Equal(summary, again);
        Assert.True(large == small, $"الصفوفُ المقروءةُ زادت مع قيودٍ لا تمسّ البنك: {small} ⟵ {large}");
    }

    /// <summary>
    /// <b>وإعادةُ تقييم البنك الأجنبيّ كذلك</b> — رصيدُه الدفتريُّ ٣٬٧٥٠ (١٬٠٠٠ دولارٍ بـ٣٫٧٥) يُقيَّم بـ٣٫٨٠ فيُقيَّد ٥٠، وما يُقرأ لا يزيد
    /// بستّين قيداً لا تمسّه (١٢٠ سطراً). <b>وزيادةُ سطرين مقبولة</b>: التقييمُ الأوّلُ وعكسُه يمسّان البنكَ نفسَه فيُقرآن في الثاني.
    /// </summary>
    [Fact]
    public async Task TheFxRevaluation_ReadsTheSameRows_HoweverLargeTheRestOfTheLedger()
    {
        await SetupAsync(currency: "USD");
        await PostAsync(await BankGlAsync(), new DateOnly(2026, 1, 1), 3_750m);
        await UnrelatedAsync(10);

        var (small, first) = await MeasureAsync(() =>
            new FxRevaluationService(_db).RevalueAsync(_companyId, _bank.Id, Day, 3.80m, "admin", foreignBalance: 1_000m));
        await new JournalReversalService(_db).ReverseAsync(_companyId, first!.Id, Day, "admin", fromSourceDocument: true);
        await UnrelatedAsync(60);
        var (large, second) = await MeasureAsync(() =>
            new FxRevaluationService(_db).RevalueAsync(_companyId, _bank.Id, Day, 3.80m, "admin", foreignBalance: 1_000m));

        Assert.Equal(50m, first.TotalDebit);
        Assert.Equal(50m, second!.TotalDebit);
        Assert.True(large - small <= 4, $"الصفوفُ المقروءةُ زادت مع قيودٍ لا تمسّ البنك: {small} ⟵ {large}");
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<(int Rows, T Result)> MeasureAsync<T>(Func<Task<T>> act)
    {
        _db.ChangeTracker.Clear();
        _rows.Start();
        var result = await act();
        return (_rows.Stop(_connection), result);
    }

    private Task<Account> BankGlAsync() => _db.Accounts.SingleAsync(a => a.Id == _bank.GlAccountId);

    private async Task PostAsync(Account bankGl, DateOnly on, decimal amount)
    {
        var capital = await _db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code == "3101");
        var entry = JournalEntry.CreateDraft(_companyId, on, "إيداع", null);
        entry.AddLine(bankGl, amount, 0m, "إيداع");
        entry.AddLine(capital, 0m, amount, "رأس المال");
        await new JournalPostingService(_db).PostAsync(entry);
    }

    /// <summary>قيودٌ لا تمسّ البنك — نصفُها قبل الفترة ونصفُها فيها، فتطال القارئَين كليهما.</summary>
    private async Task UnrelatedAsync(int count)
    {
        var charges = await _db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code == SaudiChartOfAccounts.SystemCodes.BankCharges);
        var capital = await _db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code == "3101");
        for (var i = 0; i < count; i++)
        {
            var entry = JournalEntry.CreateDraft(_companyId, i % 2 == 0 ? Day.AddDays(-5) : Day, "قيدٌ لا يمسّ البنك", null);
            entry.AddLine(charges, 10m, 0m, "مصروف");
            entry.AddLine(capital, 0m, 10m, "رأس المال");
            await new JournalPostingService(_db).PostAsync(entry);
        }
        _db.ChangeTracker.Clear();
    }

    private async Task SetupAsync(string? currency)
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة", "310123456700077", BusinessProfile.General);
        _companyId = company.Id;
        _bank = await new BankAccountService(_db).CreateAsync(
            _companyId, "BNK1", "حساب التشغيل", "الأهلي", "111", "SA0000000000000000000011", true);
        if (currency is not null)
        {
            _bank.SetCurrency(currency);
            await _db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// يلتقط كلَّ <c>SELECT</c> أثناء القياس، ثمّ يُعيده بـ<c>COUNT(*)</c> بمعاملاته نفسِها — فيُعرف ما أُعيد من صفوفٍ لا ما تُتبِّع.
    /// </summary>
    private sealed class RowCounter : DbCommandInterceptor
    {
        private readonly List<(string Sql, (string Name, object? Value)[] Parameters)> _selects = [];
        private bool _armed;

        public void Start()
        {
            _selects.Clear();
            _armed = true;
        }

        public int Stop(SqliteConnection connection)
        {
            _armed = false;
            var total = 0;
            foreach (var (sql, parameters) in _selects)
            {
                using var count = connection.CreateCommand();
                count.CommandText = "SELECT COUNT(*) FROM (" + sql.Trim().TrimEnd(';') + ")";
                foreach (var (name, value) in parameters)
                    count.Parameters.AddWithValue(name, value ?? DBNull.Value);
                total += Convert.ToInt32(count.ExecuteScalar());
            }
            return total;
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Capture(command);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Capture(command);
            return ValueTask.FromResult(result);
        }

        private void Capture(DbCommand command)
        {
            if (_armed && command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                _selects.Add((command.CommandText,
                    command.Parameters.Cast<DbParameter>().Select(p => (p.ParameterName, p.Value)).ToArray()));
        }
    }
}
