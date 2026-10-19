using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Workflow;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Accounting;

/// <summary>
/// (جاهزيّةُ الإطلاق · <c>accounting-core#6</c> · قرارُ المالك 2026-10-18) <b>القيدُ اليدويُّ فوق حدّ المنشأة لا يُرحَّل بلا اعتماد.</b>
///
/// <para><b>ما كان:</b> سلسلةُ الموافقات قائمةٌ ولا يعتمد المرءُ ما قدّمه — لكنّها اختياريّة: من يملك صلاحيةَ الترحيل رحّل قيداً يدويّاً
/// بمئة ألفٍ وحدَه. والحدُّ الافتراضيُّ ١٠٬٠٠٠ ريال يغيّره المدير.</para>
/// </summary>
public class AManualEntryAboveTheThresholdNeedsApprovalTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public AManualEntryAboveTheThresholdNeedsApprovalTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>().UseSqlite(_connection).Options);
        SecurityContext.CurrentUsername = "preparer";
        SecurityContext.CurrentRole = UserRole.Accountant;
    }

    public void Dispose()
    {
        SecurityContext.CurrentUsername = null;
        SecurityContext.CurrentRole = null;
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task SetupAsync()
    {
        _companyId = (await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700012", BusinessProfile.General)).Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();
    }

    private async Task<JournalEntry> DraftAsync(decimal amount, string? source = null)
    {
        var cash = await _db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code == SaudiChartOfAccounts.SystemCodes.CashOnHand);
        var expense = await _db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code == SaudiChartOfAccounts.SystemCodes.BankCharges);
        var entry = JournalEntry.CreateDraft(_companyId, new DateOnly(2026, 7, 9), "قيد", sourceDocument: source);
        entry.AddLine(expense, amount, 0m);
        entry.AddLine(cash, 0m, amount);
        _db.JournalEntries.Add(entry);
        await _db.SaveChangesAsync();
        return entry;
    }

    private Task<JournalEntry> PostAsync(JournalEntry entry) => new JournalPostingService(_db).PostAsync(entry);

    [Fact]
    public async Task AtOrBelowTheThreshold_ItPostsDirectly()
    {
        await SetupAsync();

        var entry = await PostAsync(await DraftAsync(10_000m));

        Assert.Equal(JournalEntryStatus.Posted, entry.Status);
    }

    [Fact]
    public async Task AboveTheThreshold_ItIsRefused_NamingBothAmounts()
    {
        await SetupAsync();
        var entry = await DraftAsync(10_000.01m);

        var refused = await Assert.ThrowsAsync<AccountingException>(() => PostAsync(entry));

        Assert.Contains("10,000.01", refused.Message, StringComparison.Ordinal);
        Assert.Contains("10,000.00", refused.Message, StringComparison.Ordinal);
        Assert.Equal(JournalEntryStatus.Draft, entry.Status);
    }

    [Fact]
    public async Task AboveTheThreshold_ItPostsOnceAnotherUserCompletesTheChain()
    {
        await SetupAsync();
        var entry = await DraftAsync(50_000m);
        var svc = new ManualJournalEntryWorkflowService(_db, new WorkflowEngine(_db));

        await svc.SubmitAsync(entry);
        await Assert.ThrowsAsync<AccountingException>(() => PostAsync(entry));

        SecurityContext.CurrentUsername = "reviewer";
        await svc.ApproveAsync(entry.Id);
        SecurityContext.CurrentUsername = "admin";
        SecurityContext.CurrentRole = UserRole.Administrator;
        await svc.ApproveAsync(entry.Id);

        Assert.Equal(JournalEntryStatus.Posted, (await PostAsync(entry)).Status);
    }

    [Fact]
    public async Task AZeroThreshold_IsKept_AndThenEveryManualEntryNeedsApproval()
    {
        await SetupAsync();
        (await _db.Companies.SingleAsync(c => c.Id == _companyId)).SetManualJournalApprovalThreshold(0m);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        Assert.Equal(0m, await _db.Companies.Where(c => c.Id == _companyId).Select(c => c.ManualJournalApprovalThreshold).SingleAsync());
        await Assert.ThrowsAsync<AccountingException>(async () => await PostAsync(await DraftAsync(1m)));
    }

    /// <summary>
    /// <b>وصفرٌ يُكتب لمنشأةٍ جديدةٍ قبل حفظها الأوّل يبقى صفراً</b> — والمسارُ هو الإدراج: لو أُعلن الافتراضُ في EF
    /// (<c>HasDefaultValue</c>) لأسقط EF الصفرَ (قيمةَ CLR الافتراضيّة) من جملة الإدراج فوضعت القاعدةُ ١٠٬٠٠٠ مكانه.
    /// </summary>
    [Fact]
    public async Task AZeroThreshold_SetBeforeTheFirstSave_IsInserted()
    {
        await SetupAsync();
        var company = Brqosh.Domain.Companies.Company.Create("منشأة ثانية", "310123456700020", BusinessProfile.General);
        company.SetManualJournalApprovalThreshold(0m);
        _db.Companies.Add(company);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        Assert.Equal(0m, await _db.Companies.Where(c => c.Id == company.Id).Select(c => c.ManualJournalApprovalThreshold).SingleAsync());
    }

    [Fact]
    public async Task ADocumentEntry_IsNotManual_AndIsNotHeld()
    {
        await SetupAsync();

        var entry = await PostAsync(await DraftAsync(500_000m, source: "SalesInvoice:1"));

        Assert.Equal(JournalEntryStatus.Posted, entry.Status);
    }

    [Fact]
    public async Task OnlyTheAdministrator_ChangesTheThreshold_AndTheChangeIsAudited()
    {
        await SetupAsync();
        var policy = new ManualJournalApprovalPolicy(_db);

        await Assert.ThrowsAnyAsync<Exception>(() => policy.SetThresholdAsync(_companyId, 1_000_000m));
        Assert.Equal(10_000m, await _db.Companies.Where(c => c.Id == _companyId).Select(c => c.ManualJournalApprovalThreshold).SingleAsync());

        SecurityContext.CurrentUsername = "admin";
        SecurityContext.CurrentRole = UserRole.Administrator;
        await policy.SetThresholdAsync(_companyId, 25_000m);

        Assert.Equal(25_000m, await _db.Companies.Where(c => c.Id == _companyId).Select(c => c.ManualJournalApprovalThreshold).SingleAsync());
        Assert.True(await _db.AuditLogEntries.AnyAsync(a => a.CompanyId == _companyId && a.Details.Contains("25,000.00")));
    }

    [Fact]
    public void ANegativeThreshold_IsRefused() =>
        Assert.Throws<AccountingException>(() =>
            Brqosh.Domain.Companies.Company.Create("ش", "310123456700012", BusinessProfile.General)
                .SetManualJournalApprovalThreshold(-1m));
}
