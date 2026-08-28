using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Sales;

/// <summary>
/// (CEEF-229 · م2-٢٢) <b>هوية التغطية: كلُّ ريالٍ في المدينين له عميلٌ يمكن تحصيلُه.</b>
///
/// <para><b>العطل:</b> حوار سند القبض يبني قائمته بشرط <c>IsActive</c> وحده، وإيقافُ العميل
/// لا يفحص رصيده رغم أن الصفحة نفسها حمّلت الأرصدة. فبعد الإيقاف تختفي منه طريقُ التحصيل
/// <b>بينما ذمّتُه باقيةٌ في حساب المدينين</b> — مالٌ مسجَّل بلا سبيلٍ لإقفاله من داخل البرنامج.</para>
///
/// <para><b>ولماذا هوية لا تأكيدُ قيمة:</b> «مجموع أرصدة العملاء القابلين للاختيار == رصيد
/// المدينين في ميزان المراجعة» تربط <b>الشاشة بالأستاذ</b>. وهي تصدق مهما تغيّرت قواعد
/// الإيقاف، وتنكسر لحظةَ يصير ريالٌ واحد غير قابلٍ للتحصيل.</para>
/// </summary>
public class CollectableCustomersTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private readonly long _companyId;

    public CollectableCustomersTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _companyId = new DatabaseInitializer(_db)
            .InitializeAsync("منشأة", "310123456700099", BusinessProfile.General)
            .GetAwaiter().GetResult().Id;
    }

    private async Task<Customer> CustomerAsync(string name, string vat)
    {
        var customer = Customer.Create(_companyId, name, vat);
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        return customer;
    }

    private async Task SellAsync(long customerId, decimal amount)
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, customerId, new DateOnly(2026, 2, 1));
        invoice.AddLine("خدمة", 1m, amount);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);
    }

    private async Task<decimal> ReceivablesInLedgerAsync()
    {
        var rows = await new TrialBalanceQuery(_db).ExecuteAsync(
            _companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var row = rows.SingleOrDefault(r =>
            r.AccountCode == SaudiChartOfAccounts.SystemCodes.AccountsReceivable);
        return row is null ? 0m : row.BalanceDebit - row.BalanceCredit;
    }

    private async Task AssertCoverageAsync()
    {
        var selectable = await new CollectableCustomersQuery(_db).ExecuteAsync(_companyId);
        var covered = selectable.Sum(c => c.Balance);
        var ledger = await ReceivablesInLedgerAsync();

        Assert.True(covered == ledger,
            $"هوية التغطية مكسورة: العملاء القابلون للاختيار يغطّون {covered} " +
            $"بينما المدينون في الأستاذ {ledger} — الفرق {ledger - covered} ريالاً لا سبيل لتحصيله.");
    }

    /// <summary>
    /// <b>القياس المركزيّ.</b> عميلٌ عليه رصيد يُوقَف — ويبقى قابلاً للتحصيل. هذه هي
    /// الاختبارة التي كانت تسقط.
    /// </summary>
    [Fact]
    public async Task ADeactivatedCustomerWithABalance_StaysCollectable()
    {
        var customer = await CustomerAsync("عميل مدين", "311111111100003");
        await SellAsync(customer.Id, 10_000m);

        customer.Deactivate();
        await _db.SaveChangesAsync();

        var selectable = await new CollectableCustomersQuery(_db).ExecuteAsync(_companyId);

        Assert.Contains(selectable, c => c.Id == customer.Id);
        Assert.False(selectable.Single(c => c.Id == customer.Id).IsActive);
        await AssertCoverageAsync();
    }

    /// <summary>
    /// <b>حارسٌ للحارس — ولولاه لكان «أعِد الجميع» يُمرّر ما سبق:</b> عميلٌ موقوفٌ <b>بلا
    /// رصيد</b> لا يزحم القائمة. فالقاعدة «من عليه رصيد» لا «الجميع».
    /// </summary>
    [Fact]
    public async Task ADeactivatedCustomerWithNoBalance_IsNotListed()
    {
        var settled = await CustomerAsync("عميل مسدَّد", "311111111100003");
        var active = await CustomerAsync("عميل نشط", "322222222200003");
        settled.Deactivate();
        await _db.SaveChangesAsync();

        var selectable = await new CollectableCustomersQuery(_db).ExecuteAsync(_companyId);

        Assert.DoesNotContain(selectable, c => c.Id == settled.Id);
        Assert.Contains(selectable, c => c.Id == active.Id);   // النشط يظهر ولو بلا رصيد
    }

    /// <summary>
    /// والهوية تصدق على منشأةٍ فيها الحالات الثلاث معاً — نشطٌ مدين، وموقوفٌ مدين،
    /// وموقوفٌ مسدَّد.
    /// </summary>
    [Fact]
    public async Task CoverageHolds_AcrossAllThreeStates()
    {
        var activeDebtor = await CustomerAsync("نشط مدين", "311111111100003");
        var suspendedDebtor = await CustomerAsync("موقوف مدين", "322222222200003");
        var suspendedSettled = await CustomerAsync("موقوف مسدَّد", "333333333300003");

        await SellAsync(activeDebtor.Id, 4_000m);
        await SellAsync(suspendedDebtor.Id, 6_000m);

        suspendedDebtor.Deactivate();
        suspendedSettled.Deactivate();
        await _db.SaveChangesAsync();

        await AssertCoverageAsync();
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
