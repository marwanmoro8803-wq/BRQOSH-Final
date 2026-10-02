using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Treasury;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٨٩ · <c>treasury-payroll-assets#10</c>) <b>التحويلُ البنكيُّ وقيدُ فرق الكشف يُعكسان — وكانا لا يُعكسان أبداً.</b>
///
/// <para>العكسُ العامُّ من دفتر اليوميّة يرفض كلَّ قيدٍ له مستند («يُصحَّح من المستند المصدر»)، <b>ولا خدمةَ تعكس التحويلَ ولا الفرق</b>:
/// فتحويلٌ أُدخل بمبلغٍ خطأ أو بين حسابين خطأ، وفرقٌ رُحِّل على حسابٍ خطأ — <b>يبقيان في الأستاذ إلى الأبد</b>.</para>
/// </summary>
public class ABankTransferAndABankDifferenceCanBeReversedTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private BankAccount _from = null!;
    private BankAccount _to = null!;

    public ABankTransferAndABankDifferenceCanBeReversedTests()
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
    /// <b>القياسُ الحاكم: عكسُ التحويل يُعيد الحسابين إلى ما كانا عليه</b> — ٢٬٥٠٠ خرجت من الأوّل ودخلت الثاني، وبعد العكس كلاهما صفر.
    /// </summary>
    [Fact]
    public async Task ReversingABankTransfer_ReturnsBothAccountsToTheirBalances()
    {
        await SetupAsync();
        var transfer = await TransferAsync();
        Assert.Equal(-2_500m, await BalanceAsync(_from));

        await new BankTransferService(_db).ReverseAsync(_companyId, transfer.Id, new DateOnly(2026, 7, 6), "admin");

        Assert.Equal(0m, await BalanceAsync(_from));
        Assert.Equal(0m, await BalanceAsync(_to));
    }

    /// <summary>
    /// <b>والقائمُ وحدَه يُعكس</b> — فالتحويلُ المعكوسُ لا يُعكس ثانيةً، <b>وقيدُ العكس نفسُه لا يُعكس</b> (وإلّا عاد التحويلُ من بابٍ خلفيّ
    /// بلا أثرٍ يدلّ عليه).
    /// </summary>
    [Fact]
    public async Task AReversedTransfer_AndItsReversal_AreNotReversedAgain()
    {
        await SetupAsync();
        var transfer = await TransferAsync();
        var service = new BankTransferService(_db);
        var reversal = await service.ReverseAsync(_companyId, transfer.Id, new DateOnly(2026, 7, 6), "admin");

        await Assert.ThrowsAsync<AccountingException>(() =>
            service.ReverseAsync(_companyId, transfer.Id, new DateOnly(2026, 7, 7), "admin"));
        await Assert.ThrowsAsync<AccountingException>(() =>
            service.ReverseAsync(_companyId, reversal.Id, new DateOnly(2026, 7, 7), "admin"));
        Assert.Equal(0m, await BalanceAsync(_from));
    }

    /// <summary>
    /// <b>والتحويلُ المُسوّى مع البنك يُرفض حتى تُلغى تسويتُه</b> — فعكسُه وهو مُسوًّى يترك في التسوية قيداً صار صفراً، ورصيدُ «المُسوّى»
    /// يكذب. <b>وبعد إلغاء التسوية يُعكس.</b>
    /// </summary>
    [Fact]
    public async Task AReconciledTransfer_IsRefused_UntilItIsUnreconciled()
    {
        await SetupAsync();
        var transfer = await TransferAsync();
        var recon = new BankReconciliationQuery(_db);
        await recon.ReconcileAsync(_companyId, _from.Id, transfer.Id, new DateOnly(2026, 7, 5));
        var service = new BankTransferService(_db);

        await Assert.ThrowsAsync<AccountingException>(() =>
            service.ReverseAsync(_companyId, transfer.Id, new DateOnly(2026, 7, 6), "admin"));
        Assert.Equal(-2_500m, await BalanceAsync(_from));

        await recon.UnreconcileAsync(_companyId, _from.Id, transfer.Id);
        await service.ReverseAsync(_companyId, transfer.Id, new DateOnly(2026, 7, 6), "admin");
        Assert.Equal(0m, await BalanceAsync(_from));
    }

    /// <summary>
    /// <b>وسطرُ الكشف المطابَقُ للتحويل يُحرَّر بعكسه</b> — وإلّا بقي «مطابَقاً» لقيدٍ صار صفراً، فلا يُطابَق بالتحويل الصحيح ولا يُرحَّل فرقاً.
    /// </summary>
    [Fact]
    public async Task ReversingAMatchedTransfer_ReleasesTheStatementLine()
    {
        await SetupAsync();
        var transfer = await TransferAsync();
        var statement = await new BankStatementImportService(_db).ImportCsvAsync(_companyId, _from.Id, "july.csv", """
            Date,Description,Amount,Reference
            2026-07-05,تحويل صادر,-2500.00,TR-1
            """);
        var report = await new BankStatementMatcherService(_db).AutoMatchAsync(_companyId, statement.Id);
        Assert.Equal(1, report.MatchedCount);
        await new BankReconciliationQuery(_db).UnreconcileAsync(_companyId, _from.Id, transfer.Id);

        await new BankTransferService(_db).ReverseAsync(_companyId, transfer.Id, new DateOnly(2026, 7, 6), "admin");

        Assert.False((await LineAsync(statement.Id)).IsMatched);
    }

    /// <summary>
    /// <b>وقيدٌ ليس تحويلاً لا يُعكس من باب التحويل</b> — فالبابُ لا يصير عكساً عامّاً يتجاوز مستندَ القيد. <b>وقيدُ الفرق تُلغى تسويتُه
    /// أوّلاً</b>: فهو مُسوًّى عند ترحيله، وحارسُ التسوية كان يرفضه قبل سؤال النوع — فيمرّ الاختبارُ ولو سقط سؤالُ النوع.
    /// </summary>
    [Fact]
    public async Task AnEntryThatIsNotATransfer_IsRefused()
    {
        await SetupAsync();
        var statement = await FeeStatementAsync();
        var difference = await new BankStatementDifferencePostingService(_db).PostUnmatchedLineAsync(_companyId, statement.Id, 0);
        await new BankReconciliationQuery(_db).UnreconcileAsync(_companyId, _from.Id, difference.JournalEntryId);

        await Assert.ThrowsAsync<AccountingException>(() =>
            new BankTransferService(_db).ReverseAsync(_companyId, difference.JournalEntryId, new DateOnly(2026, 7, 11), "admin"));
        Assert.Equal(75m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.BankCharges));
    }

    /// <summary>
    /// <b>القياسُ الحاكم للفرق: عكسُه يُعيد المصروفَ صفراً ويُحرّر السطرَ ويُلغي تسويتَه — ثمّ يُرحَّل من جديد.</b> فالفرقُ الذي رُحِّل
    /// على حسابٍ خطأٍ يُصحَّح، وكان لا سبيلَ إلى ذلك.
    /// </summary>
    [Fact]
    public async Task ReversingABankDifference_ReleasesTheLine_AndItCanBePostedAgain()
    {
        await SetupAsync();
        var statement = await FeeStatementAsync();
        var differences = new BankStatementDifferencePostingService(_db);
        var first = await differences.PostUnmatchedLineAsync(_companyId, statement.Id, 0);

        await differences.ReverseAsync(_companyId, statement.Id, 0, new DateOnly(2026, 7, 11), "admin");

        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.BankCharges));
        Assert.Equal(0m, await BalanceAsync(_from));
        Assert.False((await LineAsync(statement.Id)).IsMatched);
        Assert.False(await _db.ReconciledBankEntries.AnyAsync(r => r.JournalEntryId == first.JournalEntryId));

        await differences.PostUnmatchedLineAsync(_companyId, statement.Id, 0);
        Assert.Equal(75m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.BankCharges));
    }

    /// <summary>
    /// <b>وسطرٌ مطابَقٌ لسندٍ لا يُعكس «فرقاً»</b> — فعكسُ قيد السند من هنا يتجاوز عكسَ السند نفسِه (وتخصيصاتِه)؛ <b>ولا فرقَ ⟵ رفضٌ
    /// باسمه</b>.
    /// </summary>
    [Fact]
    public async Task ALineThatIsNotADifference_IsRefused()
    {
        await SetupAsync();
        var transfer = await TransferAsync();
        var statement = await new BankStatementImportService(_db).ImportCsvAsync(_companyId, _from.Id, "july.csv", """
            Date,Description,Amount,Reference
            2026-07-05,تحويل صادر,-2500.00,TR-1
            """);
        await new BankStatementMatcherService(_db).AutoMatchAsync(_companyId, statement.Id);
        var differences = new BankStatementDifferencePostingService(_db);

        await Assert.ThrowsAsync<AccountingException>(() =>
            differences.ReverseAsync(_companyId, statement.Id, 0, new DateOnly(2026, 7, 6), "admin"));
        Assert.Equal(-2_500m, await BalanceAsync(_from));

        var fees = await FeeStatementAsync();
        await Assert.ThrowsAsync<AccountingException>(() =>
            differences.ReverseAsync(_companyId, fees.Id, 0, new DateOnly(2026, 7, 11), "admin"));
        Assert.Equal(transfer.Id, (await LineAsync(statement.Id)).MatchedJournalEntryId);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private Task<JournalEntry> TransferAsync() =>
        new BankTransferService(_db).TransferAsync(_companyId, _from.Id, _to.Id, 2_500m, new DateOnly(2026, 7, 5), "TR-1");

    private Task<BankStatement> FeeStatementAsync() =>
        new BankStatementImportService(_db).ImportCsvAsync(_companyId, _from.Id, "fees.csv", """
            Date,Description,Amount,Reference
            2026-07-10,رسوم بنكية,-75.00,FEE-1
            """);

    private async Task<BankStatementLine> LineAsync(long statementId)
    {
        _db.ChangeTracker.Clear();
        return (await _db.BankStatements.AsNoTracking().Include(s => s.Lines).SingleAsync(s => s.Id == statementId)).Lines.Single();
    }

    private async Task<decimal> BalanceAsync(BankAccount bank) =>
        await BalanceAsync((await _db.Accounts.AsNoTracking().SingleAsync(a => a.Id == bank.GlAccountId)).Code);

    private async Task<decimal> BalanceAsync(string code) =>
        (await _db.JournalEntries
            .Where(e => e.CompanyId == _companyId && e.Status != JournalEntryStatus.Draft)
            .SelectMany(e => e.Lines)
            .Where(l => l.AccountCode == code)
            .Select(l => new { l.Debit, l.Credit })
            .ToListAsync())
        .Sum(l => l.Debit - l.Credit);

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة", "310123456700077", BusinessProfile.General);
        _companyId = company.Id;
        var banks = new BankAccountService(_db);
        _from = await banks.CreateAsync(_companyId, "BNK1", "حساب التشغيل", "الأهلي", "111", "SA0000000000000000000011", true);
        _to = await banks.CreateAsync(_companyId, "BNK2", "حساب الرواتب", "الراجحي", "222", "SA0000000000000000000022", false);
    }
}
