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
/// (جاهزيّةُ الإطلاق · ح-٩٠ · <c>found#4</c>) <b>السندُ المعكوسُ يخرج من التسوية البنكيّة — وكان يبقى «مُسوًّى» وسطرُ كشفه «مطابَقاً».</b>
///
/// <para><c>VoucherReversalService</c> يعكس قيدَ السند وتخصيصاتِه <b>ولا يسأل عن التسوية ولا عن سطر الكشف</b>، و<c>BankStatementLine.Unmatch()</c>
/// لم يكن يستدعيه شيء: فقبضٌ طابقته المطابقةُ الآليّةُ ثمّ عُكس <b>يبقى في «المُسوّى» برصيده</b> وعكسُه خارجه، <b>وسطرُ الكشف مقفولٌ عليه</b> —
/// فالسندُ الصحيحُ الذي يحلّ محلّه لا يُطابَق به أبداً.</para>
/// </summary>
public class AReversedVoucherLeavesTheBankReconciliationTests : IDisposable
{
    private static readonly DateOnly Day = new(2026, 7, 5);
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private BankAccount _bank = null!;

    public AReversedVoucherLeavesTheBankReconciliationTests()
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
    /// <b>القياسُ الحاكم: قبضٌ مُسوًّى لا يُعكس حتى تُلغى تسويتُه</b> — وكان يُعكس فيبقى في «المُسوّى» ٥٬٠٠٠ لقيدٍ معكوس.
    /// </summary>
    [Fact]
    public async Task AReconciledReceipt_IsRefused_UntilItIsUnreconciled()
    {
        await SetupAsync();
        var receipt = await ReceiptAsync();
        var statement = await MatchedStatementAsync("5000.00");
        var reversals = new VoucherReversalService(_db);

        await Assert.ThrowsAsync<AccountingException>(() =>
            reversals.ReverseReceiptAsync(_companyId, receipt.Id, Day.AddDays(1), "admin"));
        Assert.Equal(ReceiptVoucherStatus.Posted, (await _db.ReceiptVouchers.AsNoTracking().SingleAsync(v => v.Id == receipt.Id)).Status);

        await new BankReconciliationQuery(_db).UnreconcileAsync(_companyId, _bank.Id, receipt.JournalEntryId!.Value);
        await reversals.ReverseReceiptAsync(_companyId, receipt.Id, Day.AddDays(1), "admin");

        Assert.False((await LineAsync(statement.Id)).IsMatched);
    }

    /// <summary>
    /// <b>وسندُ الصرف المُسوّى كذلك، وسطرُ كشفه يُحرَّر بعكسه</b> — وكان مقفولاً على قيدٍ معكوسٍ أبداً، فلا يُطابَق بغيره ولا يُرحَّل فرقاً.
    /// </summary>
    [Fact]
    public async Task AReconciledPayment_IsRefused_ThenReversingItReleasesTheStatementLine()
    {
        await SetupAsync();
        var payment = await PaymentAsync();
        var statement = await MatchedStatementAsync("-3000.00");
        var reversals = new VoucherReversalService(_db);

        await Assert.ThrowsAsync<AccountingException>(() =>
            reversals.ReversePaymentAsync(_companyId, payment.Id, Day.AddDays(1), "admin"));

        await new BankReconciliationQuery(_db).UnreconcileAsync(_companyId, _bank.Id, payment.JournalEntryId!.Value);
        await reversals.ReversePaymentAsync(_companyId, payment.Id, Day.AddDays(1), "admin");

        Assert.False((await LineAsync(statement.Id)).IsMatched);
    }

    /// <summary>
    /// <b>والحارسُ في الدفتر لا في السند</b> — فقيدُ يوميّةٍ يدويٌّ على البنك مُسوًّى يُرفض عكسُه كذلك؛ وكلُّ عكسٍ يمرّ بـ
    /// <c>JournalReversalService</c> وحدَه (رواتبُ وتحويلاتٌ وسنداتٌ ويدويّ).
    /// </summary>
    [Fact]
    public async Task AReconciledManualBankEntry_IsRefused()
    {
        await SetupAsync();
        var bankGl = await _db.Accounts.SingleAsync(a => a.Id == _bank.GlAccountId);
        var charges = await _db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code == SaudiChartOfAccounts.SystemCodes.BankCharges);
        var entry = JournalEntry.CreateDraft(_companyId, Day, "رسوم بنكيّة يدويّة", null);
        entry.AddLine(charges, 40m, 0m, "رسوم");
        entry.AddLine(bankGl, 0m, 40m, "رسوم");
        var posted = await new Brqosh.Infrastructure.Accounting.JournalPostingService(_db).PostAsync(entry);
        await new BankReconciliationQuery(_db).ReconcileAsync(_companyId, _bank.Id, posted.Id, Day);

        await Assert.ThrowsAsync<AccountingException>(() =>
            new Brqosh.Infrastructure.Accounting.JournalReversalService(_db).ReverseAsync(_companyId, posted.Id, Day.AddDays(1), "admin"));
    }

    /// <summary><b>والسندُ الذي لم يُسَوَّ يُعكس كما كان</b> — فالحارسُ لا يمسّ إلّا المُسوّى.</summary>
    [Fact]
    public async Task AnUnreconciledReceipt_IsReversedAsBefore()
    {
        await SetupAsync();
        var receipt = await ReceiptAsync();

        await new VoucherReversalService(_db).ReverseReceiptAsync(_companyId, receipt.Id, Day.AddDays(1), "admin");

        Assert.Equal(ReceiptVoucherStatus.Reversed, (await _db.ReceiptVouchers.AsNoTracking().SingleAsync(v => v.Id == receipt.Id)).Status);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<ReceiptVoucher> ReceiptAsync()
    {
        var customer = await _db.Customers.SingleAsync(c => c.CompanyId == _companyId);
        return await new ReceiptVoucherPostingService(_db).PostAsync(ReceiptVoucher.CreateDraft(
            _companyId, customer.Id, Day, 5_000m, ReceiptMethod.Bank, "قبض بنكي", "RV-1", bankAccountId: _bank.Id));
    }

    private async Task<PaymentVoucher> PaymentAsync()
    {
        var supplier = await _db.Suppliers.SingleAsync(s => s.CompanyId == _companyId);
        return await new PaymentVoucherPostingService(_db).PostAsync(PaymentVoucher.CreateDraft(
            _companyId, supplier.Id, Day, 3_000m, ReceiptMethod.Bank, "صرف بنكي", "PV-1", bankAccountId: _bank.Id));
    }

    private async Task<BankStatement> MatchedStatementAsync(string amount)
    {
        var statement = await new BankStatementImportService(_db).ImportCsvAsync(_companyId, _bank.Id, "july.csv",
            "Date,Description,Amount,Reference" + Environment.NewLine + "2026-07-05,حركة," + amount + ",REF-1" + Environment.NewLine);
        var report = await new BankStatementMatcherService(_db).AutoMatchAsync(_companyId, statement.Id);
        Assert.Equal(1, report.MatchedCount);
        return statement;
    }

    private async Task<BankStatementLine> LineAsync(long statementId)
    {
        _db.ChangeTracker.Clear();
        return (await _db.BankStatements.AsNoTracking().Include(s => s.Lines).SingleAsync(s => s.Id == statementId)).Lines.Single();
    }

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة", "310123456700077", BusinessProfile.General);
        _companyId = company.Id;
        _db.Customers.Add(Customer.Create(_companyId, "عميل", "311111111100003"));
        _db.Suppliers.Add(Supplier.Create(_companyId, "مورد", "312222222200003"));
        await _db.SaveChangesAsync();
        _bank = await new BankAccountService(_db).CreateAsync(
            _companyId, "BNK1", "حساب التشغيل", "الأهلي", "111", "SA0000000000000000000011", true);
    }
}
