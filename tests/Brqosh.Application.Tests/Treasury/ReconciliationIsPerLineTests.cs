using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Treasury;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٥٥ · <c>treasury-payroll-assets#13</c>) <b>التسويةُ البنكيّةُ لكلّ سطرٍ لا لكلّ قيد.</b>
///
/// <para><b>ما كان:</b> قيدٌ فيه شيكان على البنك (١٠٠٠ و٥٠٠) — يُطابَق أحدُهما بالكشف فيُعدّ الآخرُ مُسوًّى ولم يظهر في البنك:
/// تختفي «مدفوعاتٌ معلّقة» من مذكّرة التسوية فتكذب، ولا يُطابَق سطرُ الشيك الثاني حين يظهر لأنّ قيدَه «مستعمَل».</para>
/// </summary>
public class ReconciliationIsPerLineTests : IDisposable
{
    private static readonly DateOnly Day = new(2026, 7, 5);

    private readonly BrqoshDbContext _db;
    private long _companyId;
    private BankAccount _bank = null!;

    public ReconciliationIsPerLineTests()
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

    /// <summary><b>الشيكُ الذي لم يُصرف يبقى معلّقاً</b> — والمذكّرةُ تراه فتتوازن.</summary>
    [Fact]
    public async Task MatchingOneChequeOfAnEntry_LeavesTheOtherOutstanding()
    {
        await SetupAsync();
        await TwoChequesAsync();

        var statement = await ImportAsync(opening: 0m, closing: -1_000m, "2026-07-05,شيك 1,-1000,CHQ-1");
        var report = await new BankStatementMatcherService(_db).AutoMatchAsync(_companyId, statement.Id);
        Assert.Equal(1, report.MatchedCount);

        var lines = await BookLinesAsync();
        Assert.Equal(2, lines.Count);
        Assert.True(lines.Single(l => l.Credit == 1_000m).IsReconciled);
        Assert.False(lines.Single(l => l.Credit == 500m).IsReconciled);

        var rec = await new BankReconciliationQuery(_db).GetStatementReconciliationAsync(_companyId, statement.Id);
        Assert.Equal(500m, rec.OutstandingPayments);
        Assert.Equal(-1_500m, rec.BookBalance);
        Assert.Equal(0m, rec.Difference);
    }

    /// <summary><b>وحين يظهر الشيكُ الثاني يُطابَق بسطره</b> — وكان قيدُه «مستعمَلاً» فيبقى سطرُ الكشف بلا مطابقةٍ أبداً.</summary>
    [Fact]
    public async Task BothChequesOnTheStatement_EachMatchesItsOwnLine()
    {
        await SetupAsync();
        await TwoChequesAsync();

        var statement = await ImportAsync(opening: 0m, closing: -1_500m,
            "2026-07-05,شيك 1,-1000,CHQ-1", "2026-07-05,شيك 2,-500,CHQ-2");
        var report = await new BankStatementMatcherService(_db).AutoMatchAsync(_companyId, statement.Id);

        Assert.Equal(2, report.MatchedCount);
        Assert.Equal(0, report.UnmatchedAfter);
        Assert.All(await BookLinesAsync(), l => Assert.True(l.IsReconciled));
    }

    /// <summary>
    /// <b>وإلغاءُ سطرٍ من قيدٍ سُوّي كلُّه يُبقي أخاه</b> — السجلُّ القائمُ قبل الهجرة (صفر = القيدُ كلُّه) يُستبدل بسجلٍّ لكلّ سطرٍ باقٍ.
    /// </summary>
    [Fact]
    public async Task UnreconcilingOneLineOfAWholeEntry_KeepsItsSibling()
    {
        await SetupAsync();
        var entryId = await TwoChequesAsync();
        var recon = new BankReconciliationQuery(_db);
        await recon.ReconcileAsync(_companyId, _bank.Id, entryId, Day);
        Assert.All(await BookLinesAsync(), l => Assert.True(l.IsReconciled));

        var small = (await BookLinesAsync()).Single(l => l.Credit == 500m);
        await recon.UnreconcileAsync(_companyId, _bank.Id, entryId, small.JournalLineId);

        var lines = await BookLinesAsync();
        Assert.False(lines.Single(l => l.Credit == 500m).IsReconciled);
        Assert.True(lines.Single(l => l.Credit == 1_000m).IsReconciled);
    }

    /// <summary><b>ورقمُ سطرٍ ليس من القيد على هذا البنك يُرفض</b> — فلا تُسوّى سطورُ المصروف ولا سطورُ قيدٍ آخر.</summary>
    [Fact]
    public async Task ALineThatIsNotOnTheBank_IsRefused()
    {
        await SetupAsync();
        var entryId = await TwoChequesAsync();
        var expenseLineId = await _db.JournalEntries
            .Where(e => e.Id == entryId)
            .SelectMany(e => e.Lines, (e, l) => new { l.Debit, Id = EF.Property<long>(l, "id") })
            .Where(x => x.Debit > 0m)
            .Select(x => x.Id)
            .SingleAsync();

        await Assert.ThrowsAsync<AccountingException>(() =>
            new BankReconciliationQuery(_db).ReconcileAsync(_companyId, _bank.Id, entryId, Day, expenseLineId));
        Assert.False(await _db.ReconciledBankEntries.AnyAsync());
    }

    /// <summary><b>والعكسُ ما زال محجوزاً</b> بسطرٍ واحدٍ مُسوًّى — حارسُ ح-٩٠ يقرأ القيدَ لا السطر.</summary>
    [Fact]
    public async Task AnEntryWithOneReconciledLine_StillCannotBeReversed()
    {
        await SetupAsync();
        var entryId = await TwoChequesAsync();
        var big = (await BookLinesAsync()).Single(l => l.Credit == 1_000m);
        await new BankReconciliationQuery(_db).ReconcileAsync(_companyId, _bank.Id, entryId, Day, big.JournalLineId);

        await Assert.ThrowsAsync<AccountingException>(() =>
            new JournalReversalService(_db).ReverseAsync(_companyId, entryId, Day.AddDays(1), "admin"));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    /// <summary>قيدٌ واحدٌ بشيكين على البنك: مصروفٌ ١٥٠٠ مدين، والبنكُ دائنٌ ١٠٠٠ ثمّ ٥٠٠.</summary>
    private async Task<long> TwoChequesAsync()
    {
        var bankGl = await _db.Accounts.SingleAsync(a => a.Id == _bank.GlAccountId);
        var charges = await _db.Accounts.SingleAsync(a =>
            a.CompanyId == _companyId && a.Code == SaudiChartOfAccounts.SystemCodes.BankCharges);
        var entry = JournalEntry.CreateDraft(_companyId, Day, "شيكان في قيد", null);
        entry.AddLine(charges, 1_500m, 0m, "مصروف");
        entry.AddLine(bankGl, 0m, 1_000m, "شيك 1");
        entry.AddLine(bankGl, 0m, 500m, "شيك 2");
        return (await new JournalPostingService(_db).PostAsync(entry)).Id;
    }

    private Task<IReadOnlyList<BankBookLine>> BookLinesAsync() =>
        new BankReconciliationQuery(_db).GetBookLinesAsync(_companyId, _bank.Id, Day, Day);

    private Task<BankStatement> ImportAsync(decimal opening, decimal closing, params string[] rows) =>
        new BankStatementImportService(_db).ImportCsvAsync(_companyId, _bank.Id, "july.csv",
            "Date,Description,Amount,Reference" + Environment.NewLine + string.Join(Environment.NewLine, rows) + Environment.NewLine,
            openingBalance: opening, closingBalance: closing);

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة", "310123456700155", BusinessProfile.General);
        _companyId = company.Id;
        _bank = await new BankAccountService(_db).CreateAsync(
            _companyId, "BNK1", "حساب التشغيل", "الأهلي", "111", "SA0000000000000000000011", true);
    }
}
