using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Treasury;

/// <summary>
/// (CEEF-230 · ط6-٩) <b>دورةُ الشيك — والشيكُ المرتجع الذي يبقى العميلُ به «مسدِّداً».</b>
///
/// <para><b>وكُشف بالقياس</b>: مسحُ ١٧٣ دالّةً عامّةً في البنية التحتية، فخرجت اثنتان
/// وخمسون <b>لا يستدعيها اختبارٌ مباشرةً</b>. ومن أخطرها دورةُ الشيك: <c>DepositAsync</c>
/// و<c>ClearAsync</c> و<c>BounceReceivedAsync</c> و<c>CancelIssuedAsync</c> — <b>ولم
/// يُستدعَ منها شيءٌ قطّ</b>، والمُستدعى (التسجيلُ) لم يُقَس إلا بأنّ حساباً استقبل سطراً.</para>
///
/// <para><b>والشيكُ المؤجَّل عمادُ التعامل في السوق السعودية</b>: يُستلم فيُعفى الدَّين،
/// <b>ثمّ يرتدّ</b>. <b>وإن لم يعد الدَّينُ لحظةَ ارتداده صار العميلُ «مسدِّداً» في كشفه
/// وهو لم يدفع</b> — فلا يُطالَب، ولا يظهر في أعمار الديون، <b>ويُباع له آجلاً من جديد</b>.</para>
///
/// <para><b>وهويّةُ هـ-٢٣:</b> <c>رصيدُ العميل بعد ارتداد الشيك = رصيدُه قبل استلامه</c>
/// — <b>لا أقلّ ولا أكثر</b>.</para>
/// </summary>
public class ChequeLifecycleTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _customerId;
    private long _bankId;

    public ChequeLifecycleTests()
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
    /// <b>القياسُ الحاكم (هـ-٢٣): الارتدادُ يُعيد الدَّينَ كما كان بالضبط.</b>
    ///
    /// <para><b>والقياسُ على كشف الحساب لا على الأستاذ</b>: هو ما يراه المحصِّلُ ويُطالب
    /// به. <b>ورصيدٌ صحيحٌ في الميزانية مع كشفٍ يقول «سدَّد» لا يُلاحَق فيه أحد.</b></para>
    /// </summary>
    [Fact]
    public async Task ABouncedCheque_RestoresTheDebtExactly()
    {
        await SetupAsync();
        await InvoiceAsync(10_000m);

        var before = await BalanceAsync();
        Assert.Equal(11_500m, before);   // ‏١٠٬٠٠٠ + ١٥٪

        var cheque = await ReceiveChequeAsync(11_500m);
        Assert.Equal(0m, await BalanceAsync());

        await new ChequePostingService(_db).BounceReceivedAsync(
            _companyId, cheque.Id, new DateOnly(2026, 4, 20));

        Assert.Equal(before, await BalanceAsync());
    }

    /// <summary>
    /// <b>والارتدادُ بعد الإيداع يُخرج المالَ من البنك ويُعيد الدَّين.</b>
    ///
    /// <para><b>والمقابلُ يختلف بالحالة</b>: قبل الإيداع يُعكس «شيكاتٌ برسم التحصيل»،
    /// <b>وبعده يُعكس البنك</b> — لأنّ المالَ دخله فعلاً. <b>وعكسُ الحساب الخطأ يترك
    /// رصيداً وهمياً في أحدهما</b>.</para>
    /// </summary>
    [Fact]
    public async Task BouncingAfterDeposit_TakesTheMoneyBackFromTheBank()
    {
        await SetupAsync();
        await InvoiceAsync(10_000m);

        var cheque = await ReceiveChequeAsync(11_500m);
        var service = new ChequePostingService(_db);

        await service.DepositAsync(_companyId, cheque.Id, new DateOnly(2026, 4, 10));
        var bankAfterDeposit = await AccountAsync(SaudiChartOfAccounts.SystemCodes.CashAtBanks);
        Assert.Equal(11_500m, bankAfterDeposit);

        await service.BounceReceivedAsync(_companyId, cheque.Id, new DateOnly(2026, 4, 20));

        Assert.Equal(0m, await AccountAsync(SaudiChartOfAccounts.SystemCodes.CashAtBanks));
        Assert.Equal(11_500m, await BalanceAsync());
    }

    /// <summary>
    /// <b>وشيكاتُ التحصيل تُفرَّغ بالإيداع.</b>
    ///
    /// <para><b>وبقاؤها فيه بعد الإيداع يُظهر المالَ مرّتين</b>: في البنك وفي
    /// «برسم التحصيل» معاً — <b>وهو أصلٌ موهومٌ في الميزانية</b>.</para>
    /// </summary>
    [Fact]
    public async Task Depositing_EmptiesTheChequesUnderCollectionAccount()
    {
        await SetupAsync();
        await InvoiceAsync(10_000m);

        var cheque = await ReceiveChequeAsync(11_500m);
        Assert.Equal(11_500m,
            await AccountAsync(SaudiChartOfAccounts.SystemCodes.ChequesUnderCollection));

        await new ChequePostingService(_db).DepositAsync(
            _companyId, cheque.Id, new DateOnly(2026, 4, 10));

        Assert.Equal(0m,
            await AccountAsync(SaudiChartOfAccounts.SystemCodes.ChequesUnderCollection));
    }

    /// <summary>
    /// <b>ولا يُرتجَع شيكٌ صادر — للوارد وحدَه.</b>
    ///
    /// <para><b>وارتجاعُ الصادر يُعيد ديناً على المنشأة سُدِّد</b> — فتظهر مدينةً لمورّدٍ
    /// قبض شيكَه.</para>
    /// </summary>
    [Fact]
    public async Task BouncingAnIssuedCheque_IsRefused()
    {
        await SetupAsync();

        var supplier = Domain.Purchasing.Supplier.Create(_companyId, "مورّد");
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        var issued = Cheque.CreateIssued(_companyId, "CHQ-OUT", new DateOnly(2026, 4, 1),
            2_000m, "دفعة", _bankId, supplier.Id);
        await new ChequePostingService(_db).RegisterIssuedAsync(issued);

        var error = await Assert.ThrowsAsync<AccountingException>(() =>
            new ChequePostingService(_db).BounceReceivedAsync(
                _companyId, issued.Id, new DateOnly(2026, 4, 20)));

        Assert.Contains("للشيكات الواردة فقط", error.Message);
    }

    /// <summary>
    /// <b>وكشفُ الحساب يُظهر الشيكَ وارتدادَه سطرين لا سطراً واحداً.</b>
    ///
    /// <para><b>وطيُّهما في صافٍ واحدٍ يُخفي الواقعة</b>: العميلُ يرى رصيداً لم يتغيّر
    /// <b>ولا يعرف أنّ شيكَه ارتدّ</b> — فيُفاجأ بالمطالبة، وتضيع لحظةُ التفاهم.</para>
    /// </summary>
    [Fact]
    public async Task TheStatement_ShowsBothTheChequeAndItsBounce()
    {
        await SetupAsync();
        await InvoiceAsync(10_000m);

        var cheque = await ReceiveChequeAsync(11_500m);
        await new ChequePostingService(_db).BounceReceivedAsync(
            _companyId, cheque.Id, new DateOnly(2026, 4, 20));

        var statement = await new CustomerBalanceQuery(_db)
            .StatementAsync(_companyId, _customerId);

        Assert.Contains(statement, r => r.DocumentType == "شيك وارد");
        Assert.Contains(statement, r => r.DocumentType == "ارتجاع شيك");
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<decimal> BalanceAsync() =>
        (await new CustomerBalanceQuery(_db).ForCustomerAsync(_companyId, _customerId)).Balance;

    private async Task<decimal> AccountAsync(string code)
    {
        var trial = await new TrialBalanceQuery(_db)
            .ExecuteAsync(_companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        return Math.Round(trial.Where(l => l.AccountCode == code)
            .Sum(l => l.TotalDebit - l.TotalCredit), 2);
    }

    private async Task InvoiceAsync(decimal amount)
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 4, 1));
        invoice.AddLine("بيع", 1m, amount);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);
    }

    private async Task<Cheque> ReceiveChequeAsync(decimal amount)
    {
        var cheque = Cheque.CreateReceived(_companyId, "CHQ-IN", new DateOnly(2026, 4, 5),
            amount, "شيك عميل", _bankId, _customerId, dueDate: new DateOnly(2026, 4, 30));
        await new ChequePostingService(_db).RegisterReceivedAsync(cheque);
        return cheque;
    }

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;

        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة الشيكات", "310123456700132", BusinessProfile.Retail);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        var customer = Customer.Create(_companyId, "عميل");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;

        var gl = await _db.Accounts.FirstAsync(a => a.CompanyId == _companyId
            && a.Code == SaudiChartOfAccounts.SystemCodes.CashAtBanks);
        var bank = BankAccount.Create(_companyId, "BK-1", "بنك", "بنك",
            "0001", "SA0000000000000000000002", gl.Id, isDefault: true);
        _db.BankAccounts.Add(bank);
        await _db.SaveChangesAsync();
        _bankId = bank.Id;
    }
}
