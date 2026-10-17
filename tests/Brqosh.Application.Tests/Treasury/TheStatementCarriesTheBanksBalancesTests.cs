using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Treasury;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٥٤ · <c>treasury-payroll-assets#13</c>) <b>الكشفُ يحمل رصيدَي البنك، والتسويةُ مذكّرةٌ تتوازن.</b>
///
/// <para><b>ما كان:</b> الكشفُ سطورٌ بلا رصيد — فلا يُعرف أكان الملفُّ كاملاً، ولا تُبنى مذكّرةُ تسويةٍ تُقارن رصيدَ البنك برصيد الدفتر.</para>
/// </summary>
public class TheStatementCarriesTheBanksBalancesTests : IDisposable
{
    private static readonly DateOnly Day = new(2026, 7, 5);

    private readonly BrqoshDbContext _db;
    private long _companyId;
    private BankAccount _bank = null!;

    public TheStatementCarriesTheBanksBalancesTests()
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

    [Fact]
    public async Task BalancesThatMatchTheLines_AreKept()
    {
        await SetupAsync();

        var statement = await ImportAsync(opening: 1_000m, closing: 1_700m, "2026-07-05,إيداع,800,R1", "2026-07-06,رسوم,-100,R2");

        Assert.Equal(1_000m, statement.OpeningBalance);
        Assert.Equal(1_700m, statement.ClosingBalance);
    }

    /// <summary><b>وكشفٌ لا تطابق سطورُه رصيدَيه يُرفض بفرقه</b> — سطرٌ ضاع في التصدير لا يُسوّى عليه.</summary>
    [Fact]
    public async Task BalancesThatDoNotMatchTheLines_AreRefusedWithTheGap()
    {
        await SetupAsync();

        var ex = await Assert.ThrowsAsync<AccountingException>(() =>
            ImportAsync(opening: 1_000m, closing: 1_800m, "2026-07-05,إيداع,800,R1", "2026-07-06,رسوم,-100,R2"));

        Assert.Contains("100.00", ex.Message, StringComparison.Ordinal);
        Assert.False(await _db.BankStatements.AnyAsync());
    }

    [Fact]
    public async Task OneBalanceWithoutTheOther_IsRefused()
    {
        await SetupAsync();

        await Assert.ThrowsAsync<AccountingException>(() =>
            new BankStatementImportService(_db).ImportCsvAsync(_companyId, _bank.Id, "july.csv",
                Csv("2026-07-05,إيداع,800,R1"), openingBalance: 1_000m));
    }

    /// <summary>
    /// <b>القياسُ الحاكم: مذكّرةُ التسوية تتوازن</b> — قبضٌ ٥٠٠٠ ظهر في البنك وطوبق، وصرفٌ ٣٠٠٠ لم يُصرف بعد (معلّق)، ورسومٌ ٤٠ في البنك
    /// لم تُقيَّد: البنكُ ٥٠٠٠−٤٠=٤٩٦٠، والدفترُ ٢٠٠٠؛ المعدَّلُ ٤٩٦٠−٣٠٠٠=١٩٦٠ = ٢٠٠٠−٤٠ — والفرقُ صفر.
    /// </summary>
    [Fact]
    public async Task TheReconciliationStatement_Balances()
    {
        await SetupAsync();
        var customer = await _db.Customers.SingleAsync(c => c.CompanyId == _companyId);
        var supplier = await _db.Suppliers.SingleAsync(s => s.CompanyId == _companyId);
        await new ReceiptVoucherPostingService(_db).PostAsync(ReceiptVoucher.CreateDraft(
            _companyId, customer.Id, Day, 5_000m, ReceiptMethod.Bank, "قبض بنكي", "RV-1", bankAccountId: _bank.Id));
        await new PaymentVoucherPostingService(_db).PostAsync(PaymentVoucher.CreateDraft(
            _companyId, supplier.Id, Day, 3_000m, ReceiptMethod.Bank, "صرف بنكي", "PV-1", bankAccountId: _bank.Id));

        var statement = await ImportAsync(opening: 0m, closing: 4_960m, "2026-07-05,قبض,5000,RV-1", "2026-07-06,رسوم,-40,FEE");
        await new BankStatementMatcherService(_db).AutoMatchAsync(_companyId, statement.Id);

        var rec = await new BankReconciliationQuery(_db).GetStatementReconciliationAsync(_companyId, statement.Id);

        Assert.Equal(4_960m, rec.StatementClosing);
        Assert.Equal(2_000m, rec.BookBalance);
        Assert.Equal(0m, rec.DepositsInTransit);
        Assert.Equal(3_000m, rec.OutstandingPayments);
        Assert.Equal(-40m, rec.UnbookedBankItems);
        Assert.Equal(1_960m, rec.AdjustedBank);
        Assert.Equal(1_960m, rec.AdjustedBook);
        Assert.Equal(0m, rec.Difference);
    }

    [Fact]
    public async Task AStatementWithoutBalances_HasNoReconciliationStatement()
    {
        await SetupAsync();
        var statement = await new BankStatementImportService(_db).ImportCsvAsync(_companyId, _bank.Id, "july.csv", Csv("2026-07-05,إيداع,800,R1"));

        await Assert.ThrowsAsync<AccountingException>(() =>
            new BankReconciliationQuery(_db).GetStatementReconciliationAsync(_companyId, statement.Id));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private Task<BankStatement> ImportAsync(decimal opening, decimal closing, params string[] rows) =>
        new BankStatementImportService(_db).ImportCsvAsync(_companyId, _bank.Id, "july.csv", Csv(rows),
            openingBalance: opening, closingBalance: closing);

    private static string Csv(params string[] rows) =>
        "Date,Description,Amount,Reference" + Environment.NewLine + string.Join(Environment.NewLine, rows) + Environment.NewLine;

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة", "310123456700154", BusinessProfile.General);
        _companyId = company.Id;
        _db.Customers.Add(Customer.Create(_companyId, "عميل", "311111111100003"));
        _db.Suppliers.Add(Supplier.Create(_companyId, "مورد", "312222222200003"));
        await _db.SaveChangesAsync();
        _bank = await new BankAccountService(_db).CreateAsync(
            _companyId, "BNK1", "حساب التشغيل", "الأهلي", "111", "SA0000000000000000000011", true);
    }
}
