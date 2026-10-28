using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Accounting;

/// <summary>
/// ‏(Phase 7 · بديلُ المراجعة الخارجيّة) <b>سنةٌ ماليّةٌ كاملةٌ من أوّل قيدٍ حتى الإقفال والقوائم المالية</b> — بأرقامٍ محسوبةٍ يدويّاً:
/// ‏رأسُ مالٍ 500,000 في البنك؛ اثنتا عشرة فاتورةَ مبيعاتٍ آجلة شهريّة 10,000 + ضريبة 15%؛ إيجارٌ شهريّ 3,000 من البنك.
/// ‏المتوقَّع: إيرادات 120,000 · مصروفات 36,000 · صافي 84,000 · ذمم 138,000 · ضريبة مستحقّة 18,000 · بنك 464,000 · أصول 602,000.
/// ‏ثمّ الإقفالُ السنويّ: الإيراداتُ والمصروفاتُ تُصفَّر، والربحُ إلى حقوق الملكية، والسنةُ المقفلةُ ترفض قيداً جديداً،
/// ‏وكلُّ قائمةٍ (ميزان، دخل، مركز ماليّ، تغيّرات حقوق ملكية، تدفّقات نقديّة) تتّفق مع الأخرى.
/// </summary>
public class AFullFiscalYearRunsFromFirstEntryToClosingAndStatementsTests : IDisposable
{
    private static readonly DateOnly YearStart = new(2025, 1, 1);
    private static readonly DateOnly YearEnd = new(2025, 12, 31);
    private readonly BrqoshDbContext _db;

    public AFullFiscalYearRunsFromFirstEntryToClosingAndStatementsTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose() => _db.Dispose();

    private async Task PostManualAsync(long companyId, DateOnly date, string text, Account debit, Account credit, decimal amount)
    {
        var e = JournalEntry.CreateDraft(companyId, date, text);
        e.AddLine(debit, amount, 0m);
        e.AddLine(credit, 0m, amount);
        await new JournalPostingService(_db).PostAsync(e);
    }

    [Fact]
    public async Task TheYear_ClosesWithHandComputedFigures_AndEveryStatementAgrees()
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync("شركة السنة الكاملة", "310000000000003", BusinessProfile.General);
        var id = company.Id;
        var accounts = await _db.Accounts.Where(a => a.CompanyId == id).ToListAsync();
        var bank = accounts.Single(a => a.Code == "1102");
        var capital = accounts.Single(a => a.Code == "3101");
        var rent = accounts.Where(a => a.Type == AccountType.Expense && a.IsPostable && a.Code.StartsWith("52"))
            .OrderBy(a => a.Code).First();
        var customer = Customer.Create(id, "عميل السنة", "311122233300003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        // ‏حدُّ اعتماد القيد اليدويّ (10,000) يُرسل قيدَ رأس المال للموافقة — والموافقةُ مقيسةٌ في اختباراتها؛ هنا يُرفع الحدّ.
        await _db.Database.ExecuteSqlRawAsync("UPDATE companies SET \"ManualJournalApprovalThreshold\" = 1000000000 WHERE \"Id\" = {0}", id);

        // ‏١) أوّلُ قيد: رأسُ المال.
        await PostManualAsync(id, YearStart, "إيداع رأس المال", bank, capital, 500_000m);

        // ‏٢) اثنا عشر شهراً: فاتورةٌ آجلة وإيجار.
        for (var m = 1; m <= 12; m++)
        {
            var invoice = SalesInvoice.CreateDraft(id, customer.Id, new DateOnly(2025, m, 10));
            invoice.AddLine("خدمة شهرية", 1m, 10_000m);
            await new SalesInvoicePostingService(_db).PostAsync(invoice);
            await PostManualAsync(id, new DateOnly(2025, m, 25), $"إيجار {m}", rent, bank, 3_000m);
        }
        var arAccount = (await new TrialBalanceQuery(_db).ExecuteAsync(id, YearStart, YearEnd))
            .Where(r => r.Type == AccountType.Asset && r.AccountCode != "1102" && r.BalanceDebit > 0)
            .OrderByDescending(r => r.BalanceDebit).First();
        Assert.Equal(138_000m, arAccount.BalanceDebit);

        // ‏٣) ميزانُ المراجعة قبل الإقفال.
        var tb = await new TrialBalanceQuery(_db).ExecuteAsync(id, YearStart, YearEnd);
        Assert.Equal(tb.Sum(r => r.TotalDebit), tb.Sum(r => r.TotalCredit));

        // ‏٤) قائمةُ الدخل.
        var income = await new IncomeStatementQuery(_db).ExecuteAsync(id, YearStart, YearEnd);
        Assert.Equal(120_000m, income.TotalRevenue);
        Assert.Equal(36_000m, income.TotalExpenses);
        Assert.Equal(84_000m, income.NetIncome);

        // ‏٥) المركزُ الماليّ قبل الإقفال.
        var before = await new BalanceSheetQuery(_db).ExecuteAsync(id, YearEnd);
        Assert.True(before.IsBalanced, $"{before.TotalAssets} ≠ {before.TotalLiabilitiesAndEquity}");
        Assert.Equal(602_000m, before.TotalAssets);          // بنك 464,000 + ذمم 138,000
        Assert.Equal(18_000m, before.TotalLiabilities);      // ضريبة مخرجات
        Assert.Equal(584_000m, before.TotalEquity);          // 500,000 + 84,000

        // ‏٦) الإقفالُ السنويّ.
        var close = await new YearEndCloseService(_db).CloseYearAsync(id, 2025, "admin");
        Assert.Equal(84_000m, close.NetIncome);

        var afterIncome = await new IncomeStatementQuery(_db).ExecuteAsync(id, YearStart, YearEnd, excludeYearEndClosing: false);
        Assert.Equal(0m, afterIncome.NetIncome);
        Assert.All(afterIncome.RevenueRows.Concat(afterIncome.ExpenseRows), r => Assert.Equal(0m, r.Amount));
        Assert.Equal(84_000m, (await new IncomeStatementQuery(_db).ExecuteAsync(id, YearStart, YearEnd)).NetIncome);

        var after = await new BalanceSheetQuery(_db).ExecuteAsync(id, YearEnd);
        Assert.True(after.IsBalanced);
        Assert.Equal(before.TotalAssets, after.TotalAssets);
        Assert.Equal(before.TotalEquity, after.TotalEquity);   // لا عدَّ مزدوجاً للربح

        // ‏٧) قائمةُ التغيّرات في حقوق الملكية تتّفق مع المركز الماليّ.
        var equity = await new StatementOfChangesInEquityQuery(_db).ExecuteAsync(id, YearStart, YearEnd);
        Assert.Equal(84_000m, equity.TotalProfitOrLoss);
        Assert.Equal(after.TotalEquity, equity.TotalClosing);

        // ‏٨) التدفّقاتُ النقديّة: البنكُ 0 ← 464,000.
        var cash = await new CashFlowStatementService(_db).ExecuteAsync(id, YearStart, YearEnd);
        Assert.Equal(0m, cash.OpeningCash);
        Assert.Equal(464_000m, cash.ClosingCash);
        Assert.Equal(cash.ClosingCash - cash.OpeningCash, cash.NetCashChange);

        // ‏٩) السنةُ المقفلةُ ترفض قيداً جديداً، والسنةُ التالية تبدأ متوازنة.
        await Assert.ThrowsAnyAsync<Exception>(() => PostManualAsync(id, new DateOnly(2025, 12, 30), "متأخّر", rent, bank, 100m));
        _db.ChangeTracker.Clear();
        var next = await new BalanceSheetQuery(_db).ExecuteAsync(id, new DateOnly(2026, 1, 31));
        Assert.True(next.IsBalanced);
        Assert.Equal(after.TotalEquity, next.TotalEquity);
    }
}
