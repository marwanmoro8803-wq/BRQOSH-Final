using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Setup;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Setup;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١٣٤ · <c>accounting-core#7</c>) <b>الذممُ المفتوحةُ عند بدء التشغيل — لكلّ عميلٍ ومورِّدٍ بفواتيره.</b>
///
/// <para>‏الميزانُ الافتتاحيُّ (ح-١٣٣) أوقف رصيدَي الذمم في 3107. <b>والمنشأةُ لا تُحصّل رصيداً بل فواتير</b>: من مدينٌ بكم،
/// ومنذ متى، وبأيّ رقم. فتُستورد الذممُ مستنداتٍ لأصحابها، <b>ويعود 3107 صفراً</b> — وذاك برهانُ أنّ ما في الميزان فُصِّل كلُّه.</para>
///
/// <para>‏<b>والقياسُ رحلةٌ كاملة</b>: ميزانٌ ⟵ عملاءُ ومورِّدون ⟵ ذممُهم ⟵ الأستاذُ والمساعدُ والكشفُ والأعمارُ متّفقةٌ كلُّها.</para>
/// </summary>
public class GoLiveOpenItemsTests : IDisposable
{
    private static readonly DateOnly GoLive = new(2026, 1, 1);
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _company;

    public GoLiveOpenItemsTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        TestActor.Administrator();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private const string TrialBalance = "1101,50000,0\n1104,20000,0\n2101,0,15000\n3101,0,55000";

    private const string Receivables = """
        CustomerName,DocumentNumber,DocumentDate,DueDate,Amount
        عميل قديم,INV-1001,2025-10-01,2025-10-31,12000
        عميل حديث,INV-1002,2025-12-15,2026-01-14,8000
        """;

    private const string Payables = "SupplierName,DocumentNumber,DocumentDate,DueDate,Amount\nمورد الانتقال,P-77,2025-11-20,2025-12-20,15000";

    /// <summary>
    /// ‏<b>القياسُ الحاكم — الرحلةُ كلُّها:</b> بعد الذمم يعود 3107 صفراً، و1104 و2101 بأرصدة الميزان، وهويّتا المطابقة صادقتان،
    /// ورصيدُ كلّ عميلٍ رصيدُ فواتيره.
    /// </summary>
    [Fact]
    public async Task AfterTheOpenItems_TheClearingAccountIsEmpty_AndTheLedgersAgree()
    {
        await GoLiveAsync();

        Assert.Equal(0m, await BalanceAsync(CsvDataMigrationService.OpeningClearingCode));
        Assert.Equal(20_000m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsReceivable));
        Assert.Equal(-15_000m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsPayable));
        await LedgerIdentities.AssertAllAsync(_db, _company);

        Assert.Equal(12_000m, (await new CustomerBalanceQuery(_db).ForCustomerAsync(_company, await CustomerAsync("عميل قديم"))).Balance);
        Assert.Equal(15_000m, (await new SupplierBalanceQuery(_db).ForSupplierAsync(_company, await SupplierAsync("مورد الانتقال"))).Balance);
    }

    /// <summary>‏<b>والكشفُ يُطابق رأسَه</b>: الذمّةُ سطرٌ برقم فاتورتها الأصليّ.</summary>
    [Fact]
    public async Task TheStatement_ShowsTheOriginalInvoiceNumber()
    {
        await GoLiveAsync();

        var statement = await new CustomerBalanceQuery(_db).StatementAsync(_company, await CustomerAsync("عميل قديم"));

        var line = Assert.Single(statement);
        Assert.Equal("INV-1001", line.DocumentNumber);
        Assert.Equal(StandingOpeningBalanceItems.StatementType, line.DocumentType);
        Assert.Equal(12_000m, line.RunningBalance);

        var supplierLine = Assert.Single(await new SupplierBalanceQuery(_db).StatementAsync(_company, await SupplierAsync("مورد الانتقال")));
        Assert.Equal("P-77", supplierLine.DocumentNumber);
        Assert.Equal(15_000m, supplierLine.RunningBalance);
    }

    /// <summary>
    /// ‏<b>والأعمارُ من تاريخ الفاتورة الأصليّ</b> — فدَينُ أكتوبر يظهر متأخّراً يومَ بدء التشغيل، لا جارياً وُلد للتوّ.
    /// </summary>
    [Fact]
    public async Task Aging_CountsFromTheOriginalInvoiceDate()
    {
        await GoLiveAsync();

        var receivables = await new ReceivableAgingQuery(_db).ExecuteAsync(_company, GoLive);
        var old = Assert.Single(receivables, r => r.CustomerNameAr == "عميل قديم");
        Assert.Equal(12_000m, old.Over90);
        var recent = Assert.Single(receivables, r => r.CustomerNameAr == "عميل حديث");
        Assert.Equal(8_000m, recent.Current);

        var payables = await new PayableAgingQuery(_db).ExecuteAsync(_company, GoLive);
        Assert.Equal(15_000m, Assert.Single(payables).Days31To60);
    }

    /// <summary>‏<b>وقبل الميزان لا ذمم</b> — فلا تاريخَ بدءٍ يُعرف، ولا رصيدَ في 3107 يُفصَّل.</summary>
    [Fact]
    public async Task BeforeTheTrialBalance_OpenItemsAreRefused()
    {
        await StartAsync();

        var result = await Service().ImportOpeningReceivablesCsvAsync(_company, Receivables);

        Assert.Equal(0, result.Imported);
        Assert.Contains(result.Details, d => d.Error!.Contains("الميزانَ الافتتاحيَّ أوّلاً", StringComparison.Ordinal));
    }

    /// <summary>‏<b>وسطرٌ معيبٌ يُسقط الملفَّ كلَّه</b> — طرفٌ مجهول، أو تاريخٌ غيرُ مقروء، أو فاتورةٌ بعد بدء التشغيل.</summary>
    [Theory]
    [InlineData("عميل غائب,INV-9,2025-10-01,2025-10-31,100", "غير موجود")]
    [InlineData("عميل قديم,INV-9,01/10/2025,2025-10-31,100", "yyyy-MM-dd")]
    [InlineData("عميل قديم,INV-9,2026-02-01,2026-03-01,100", "سبقت بدءَ التشغيل")]
    [InlineData("عميل قديم,INV-9,2025-10-01,2025-09-01,100", "لا يسبق")]
    [InlineData("عميل قديم,INV-9,2025-10-01,2025-10-31,-5", "موجب")]
    public async Task ABadLine_SinksTheWholeFile(string badLine, string reason)
    {
        await PrepareAsync();

        var result = await Service().ImportOpeningReceivablesCsvAsync(_company, $"{Receivables}\n{badLine}");

        Assert.Equal(0, result.Imported);
        Assert.Contains(reason, Assert.Single(result.Details).Error, StringComparison.Ordinal);
        Assert.False(await _db.OpeningBalanceItems.AnyAsync());
        Assert.Equal(5_000m, await BalanceAsync(CsvDataMigrationService.OpeningClearingCode));
    }

    /// <summary>‏<b>والفاتورةُ مرّةً</b> — إعادةُ الملفّ لا تُضاعف الذمّة.</summary>
    [Fact]
    public async Task TheSameInvoice_IsNotImportedTwice()
    {
        await GoLiveAsync();

        var again = await Service().ImportOpeningReceivablesCsvAsync(_company, Receivables);

        Assert.Equal(0, again.Imported);
        Assert.Equal(20_000m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsReceivable));
    }

    /// <summary>
    /// ‏<b>وقيدُ الذمم إن عُكس سقطت ذممُه من المساعد</b> — وإلّا بقيت هناك وفارقت الأستاذَ بقيمتها.
    /// </summary>
    [Fact]
    public async Task ReversingTheOpenItemsEntry_TakesItsItemsOutOfTheSubledger()
    {
        await GoLiveAsync();
        var entry = await _db.JournalEntries.SingleAsync(e => e.CompanyId == _company && e.SourceDocument == "OpeningReceivables");

        var reversedOn = GoLive.AddDays(10);
        await new JournalReversalService(_db).ReverseAsync(_company, entry.Id, reversedOn, "admin", fromSourceDocument: true);
        var customer = await CustomerAsync("عميل قديم");

        Assert.Equal(0m, (await new CustomerBalanceQuery(_db).ForCustomerAsync(_company, customer)).Balance);
        await LedgerIdentities.AssertAllAsync(_db, _company);

        // ‏**وقبل تاريخ العكس تبقى** — كالأستاذ الذي يحفظ القيدَ ويُقابله عكسُه بتاريخه.
        Assert.Equal(12_000m, (await new CustomerBalanceQuery(_db).AllBalancesAsync(_company, GoLive))[customer]);
        Assert.Equal(12_000m, Assert.Single(await new ReceivableAgingQuery(_db).ExecuteAsync(_company, GoLive),
            r => r.CustomerId == customer).Over90);

        // ‏**والكشفُ يكتبها ثمّ عكسَها** — فيُطابق الرصيدَ في كلّ سطر.
        var statement = await new CustomerBalanceQuery(_db).StatementAsync(_company, customer);
        Assert.Equal([12_000m, 0m], statement.Select(l => l.RunningBalance));
        Assert.Equal(reversedOn, statement[^1].Date);
    }

    /// <summary>
    /// ‏<b>وعلى PostgreSQL حقيقيّ عبر الهجرات</b> — فالجدولُ هناك من الهجرة لا من النموذج، والاستعلاماتُ تُترجَم لمزوّدٍ آخر.
    /// </summary>
    [PostgresFact]
    public async Task TheJourney_HoldsOnPostgres()
    {
        await using var scope = await PostgresTestDatabase.CreateAsync("go_live_open_items");
        await new DatabaseSchemaService(scope.Db).EnsureSchemaAsync();
        var company = (await new DatabaseInitializer(scope.Db)
            .InitializeAsync("منشأة الانتقال", "300000000000003", BusinessProfile.General)).Id;
        var service = new CsvDataMigrationService(scope.Db);
        Assert.Equal(4, (await service.ImportOpeningTrialBalanceCsvAsync(company, GoLive, TrialBalance)).Imported);
        scope.Db.Customers.AddRange(Customer.Create(company, "عميل قديم", ""), Customer.Create(company, "عميل حديث", ""));
        scope.Db.Suppliers.Add(Supplier.Create(company, "مورد الانتقال"));
        await scope.Db.SaveChangesAsync();

        Assert.Equal(2, (await service.ImportOpeningReceivablesCsvAsync(company, Receivables)).Imported);
        Assert.Equal(1, (await service.ImportOpeningPayablesCsvAsync(company, Payables)).Imported);

        await using var check = scope.OpenIndependentContext();
        await LedgerIdentities.AssertAllAsync(check, company);
        Assert.Equal(12_000m, Assert.Single(await new ReceivableAgingQuery(check).ExecuteAsync(company, GoLive),
            r => r.CustomerNameAr == "عميل قديم").Over90);
    }

    private async Task GoLiveAsync()
    {
        await PrepareAsync();
        Assert.Equal(2, (await Service().ImportOpeningReceivablesCsvAsync(_company, Receivables)).Imported);
        Assert.Equal(1, (await Service().ImportOpeningPayablesCsvAsync(_company, Payables)).Imported);
    }

    private async Task PrepareAsync()
    {
        await StartAsync();
        Assert.Equal(4, (await Service().ImportOpeningTrialBalanceCsvAsync(_company, GoLive, TrialBalance)).Imported);
        _db.Customers.AddRange(Customer.Create(_company, "عميل قديم", ""), Customer.Create(_company, "عميل حديث", ""));
        _db.Suppliers.Add(Supplier.Create(_company, "مورد الانتقال"));
        await _db.SaveChangesAsync();
    }

    private async Task StartAsync() =>
        _company = (await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة الانتقال", "300000000000003", BusinessProfile.General)).Id;

    private CsvDataMigrationService Service() => new(_db);

    private Task<long> CustomerAsync(string name) =>
        _db.Customers.Where(c => c.CompanyId == _company && c.NameAr == name).Select(c => c.Id).SingleAsync();

    private Task<long> SupplierAsync(string name) =>
        _db.Suppliers.Where(s => s.CompanyId == _company && s.NameAr == name).Select(s => s.Id).SingleAsync();

    private async Task<decimal> BalanceAsync(string code)
    {
        var rows = await new TrialBalanceQuery(_db).ExecuteAsync(_company, new DateOnly(2000, 1, 1), new DateOnly(2100, 12, 31));
        var row = rows.FirstOrDefault(r => r.AccountCode == code);
        return row is null ? 0m : row.TotalDebit - row.TotalCredit;
    }
}
