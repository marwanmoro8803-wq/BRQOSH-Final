using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// (CEEF-228) قائمة التدفقات النقدية — ثلاثة أعطال كانت تجعلها غير متوازنة بنيوياً.
///
/// `CashFlowStatementTests` القائم يؤكّد `Assert.True(result.Operating.Subtotal != 0m)` —
/// وهو تأكيدٌ يُثبّت العطل بدل كشفه: في سيناريو الفاتورة الآجلة القيمة الصحيحة **صفر**
/// (لم يُقبض شيء)، والاختبار كان يمرّ لأنها كانت ‎-150‎.
/// هنا تُقاس **القيمة** لا مجرّد كونها غير صفرية.
/// </summary>
public class CashFlowStatementAccuracyTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public CashFlowStatementAccuracyTests()
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
            .InitializeAsync("شركة تدفق", "310123456700003", BusinessProfile.General);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();
        ManualJournals.LiftApprovalThreshold(_db);   // ‏(accounting-core#6) قيودٌ يدويّةٌ كبيرةٌ لسببٍ غيرِ الاعتماد — منشأةٌ بمستخدمٍ واحد
    }

    private async Task<Account> AccountAsync(string code) =>
        await _db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code == code);

    private async Task PostAsync(string description, params (string Code, decimal Debit, decimal Credit)[] lines)
    {
        var entry = JournalEntry.CreateDraft(_companyId, new DateOnly(2026, 6, 15), description);
        foreach (var (code, debit, credit) in lines)
            entry.AddLine(await AccountAsync(code), debit, credit, description);
        await new JournalPostingService(_db).PostAsync(entry);
    }

    private static decimal LineAmount(CashFlowSection section, string labelPart) =>
        section.Lines.Where(l => l.Label.Contains(labelPart)).Sum(l => l.Amount);

    private Task<CashFlowStatementResult> RunAsync() =>
        new CashFlowStatementService(_db).ExecuteAsync(
            _companyId, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30));

    /// <summary>
    /// الإهلاك مصروفٌ غير نقديّ: يُطرح داخل صافي الدخل ثم **يُردّ** في التشغيل.
    /// كان `invert: true` يقلب الإشارة مرّة ثانية فيظهر ‎-1,000‎ بدل ‎+1,000‎ —
    /// أي أن كل 1,000 إهلاك كانت تُنقص التدفق التشغيليّ 2,000.
    /// </summary>
    [Fact]
    public async Task Depreciation_IsAddedBack_NotSubtractedTwice()
    {
        await SetupAsync();
        await PostAsync("إهلاك الشهر",
            ("5211", 1000m, 0m),   // مصروف الإهلاك
            ("1207", 0m, 1000m));  // مجمّع إهلاك الأصول الثابتة

        var result = await RunAsync();

        Assert.Equal(1000m, LineAmount(result.Operating, "إهلاك"));
    }

    /// <summary>
    /// فاتورة آجلة 1,000 + ضريبة 150: لم يُقبض شيء، فالتدفق التشغيليّ **صفر**.
    /// كانت الضريبة (2106) خارج قائمة رأس المال العامل فتظهر ‎-150‎ لم تحدث قطّ.
    /// </summary>
    [Fact]
    public async Task CreditSale_WithVat_ProducesZeroOperatingCashFlow()
    {
        await SetupAsync();
        var customer = Customer.Create(_companyId, "عميل", "311122233300003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var invoice = SalesInvoice.CreateDraft(_companyId, customer.Id, new DateOnly(2026, 6, 15));
        invoice.AddLine("خدمة", 1m, 1000m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var result = await RunAsync();

        Assert.Equal(0m, result.Operating.Subtotal);
        Assert.Equal(0m, result.NetCashChange);
    }

    /// <summary>
    /// قيد الإقفال السنويّ يُدخل صافي الدخل في 3103. كان ذلك يظهر ثانيةً في التمويل،
    /// فمجموع الأقسام يصير ضعف التغيّر النقديّ الحقيقيّ.
    /// </summary>
    [Fact]
    public async Task YearEndCloseTransfer_DoesNotAppearAsFinancingInflow()
    {
        await SetupAsync();
        await PostAsync("إقفال إلى الأرباح المبقاة",
            (SaudiChartOfAccounts.SystemCodes.CurrentYearProfit, 200_000m, 0m),
            (SaudiChartOfAccounts.SystemCodes.RetainedEarnings, 0m, 200_000m));

        var result = await RunAsync();

        Assert.Equal(0m, result.Financing.Subtotal);
    }

    /// <summary>حركة الشركاء الحقيقية تبقى ظاهرة — الاستثناء انتقائيّ لا شامل.</summary>
    [Fact]
    public async Task PartnersAccountMovement_StillAppearsInFinancing()
    {
        await SetupAsync();
        await PostAsync("إيداع من الشركاء",
            (SaudiChartOfAccounts.SystemCodes.CashOnHand, 50_000m, 0m),
            ("3105", 0m, 50_000m));

        var result = await RunAsync();

        Assert.Equal(50_000m, result.Financing.Subtotal);
    }
}
