using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Accounting;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٥٣ · <c>accounting-core#6</c>) <b>القيدُ اليدويُّ لا يمسّ حسابَ مراقبةٍ يُبنى من دفترٍ مساعد.</b>
///
/// <para><b>ما كان:</b> قيدٌ يدويٌّ يُرحَّل على العملاء والموردين والمخزون بلا طرفٍ ولا صنف — فيختلف الحسابُ عن مجموع دفتره
/// المساعد صامتاً، ويكشفه ح-٢٨ بعد وقوعه ولا يمنعه.</para>
/// </summary>
public class AManualEntryLeavesSubledgerControlsAloneTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public AManualEntryLeavesSubledgerControlsAloneTests()
    {
        TestActor.Accountant();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Database.CloseConnection();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Theory]
    [InlineData(SaudiChartOfAccounts.SystemCodes.AccountsReceivable)]
    [InlineData(SaudiChartOfAccounts.SystemCodes.AccountsPayable)]
    [InlineData(SaudiChartOfAccounts.SystemCodes.Inventory)]
    public async Task AManualEntryOnAControlAccount_IsRefused_AndNothingIsPosted(string control)
    {
        await SetupAsync();

        var ex = await Assert.ThrowsAsync<AccountingException>(() => PostManualAsync(control, "5101"));

        Assert.Contains(control, ex.Message, StringComparison.Ordinal);
        Assert.False(await _db.JournalEntries.AnyAsync(e => e.Status == JournalEntryStatus.Posted));
    }

    /// <summary><b>وما سواها يُرحَّل كما كان</b> — مصروفٌ من الصندوق، وتسويةُ ضريبةٍ يدويّةٌ يقرؤها الإقرار.</summary>
    [Theory]
    [InlineData("5101", SaudiChartOfAccounts.SystemCodes.CashOnHand)]
    [InlineData(SaudiChartOfAccounts.SystemCodes.VatInput, SaudiChartOfAccounts.SystemCodes.CashOnHand)]
    public async Task OtherManualEntries_StillPost(string debit, string credit)
    {
        await SetupAsync();

        await PostManualAsync(debit, credit);

        Assert.True(await _db.JournalEntries.AnyAsync(e => e.Status == JournalEntryStatus.Posted));
    }

    /// <summary><b>والمستندُ يمسّها كما كان</b> — القيدُ ذو المصدر ليس يدويّاً.</summary>
    [Fact]
    public async Task ADocumentEntry_StillPostsToAControlAccount()
    {
        await SetupAsync();

        var entry = JournalEntry.CreateDraft(_companyId, new DateOnly(2026, 5, 1), "من مستند", sourceDocument: "SalesInvoice:INV-1");
        entry.AddLine(await AccountAsync(SaudiChartOfAccounts.SystemCodes.AccountsReceivable), 100m, 0m);
        entry.AddLine(await AccountAsync("4101"), 0m, 100m);
        await new JournalPostingService(_db).PostAsync(entry);

        Assert.True(await _db.JournalEntries.AnyAsync(e => e.Status == JournalEntryStatus.Posted));
    }

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة القيود", "300000000000153", BusinessProfile.General);
        _companyId = company.Id;
    }

    private async Task PostManualAsync(string debitCode, string creditCode)
    {
        var entry = JournalEntry.CreateDraft(_companyId, new DateOnly(2026, 5, 1), "قيدٌ يدويّ");
        entry.AddLine(await AccountAsync(debitCode), 250m, 0m);
        entry.AddLine(await AccountAsync(creditCode), 0m, 250m);
        await new JournalPostingService(_db).PostAsync(entry);
    }

    private Task<Account> AccountAsync(string code) =>
        _db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code == code);
}
