using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Assets;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Infrastructure.Assets;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Assets;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٥٨ · <c>treasury-payroll-assets#12</c>) <b>الأصلُ يُشترى من مورّدٍ بفاتورته — آجلاً وبضريبة مدخلات.</b>
///
/// <para><b>ما كان:</b> الاقتناءُ نقداً أو من بنكٍ وحدَهما. فسيّارةٌ من وكيلٍ بفاتورةٍ آجلة: يُدان الصندوقُ بما لم يُدفع، وتضيع
/// ضريبةُ المدخلات (١٥٪ من التكلفة)، ولا يعرف دفترُ المورّد دَينَه.</para>
/// </summary>
public class AnAssetIsBoughtFromASupplierByInvoiceTests : IDisposable
{
    private static readonly DateOnly Day = new(2026, 7, 1);

    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _supplierId;
    private long _assetAcctId;
    private long _depAcctId;

    public AnAssetIsBoughtFromASupplierByInvoiceTests()
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

    /// <summary><b>القياسُ الحاكم:</b> الأصلُ ١٠٬٠٠٠ مدين، وضريبةُ المدخلات ١٬٥٠٠ مدينة، والمورّدُ ١١٬٥٠٠ دائن — والصندوقُ لا يُمسّ.</summary>
    [Fact]
    public async Task ADeductibleInvoice_DebitsTheAssetAndInputVat_AndCreditsTheSupplier()
    {
        await SetupAsync();
        var asset = await RegisterAsync("FA-1", 10_000m);

        var invoice = await PostInvoiceAsync(asset, 10_000m, vatDeductible: true);

        Assert.Equal(10_000m, await BalanceAsync(_assetAcctId));
        Assert.Equal(1_500m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.VatInput));
        Assert.Equal(-11_500m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsPayable));
        Assert.Equal(0m, await BalanceAsync("5102"));
        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.CashOnHand));

        _db.ChangeTracker.Clear();
        var acquired = await _db.FixedAssets.SingleAsync(a => a.Id == asset.Id);
        Assert.True(acquired.IsActive);
        Assert.False(acquired.AwaitingInvoice);
        Assert.Equal(invoice.JournalEntryId, acquired.AcquisitionJournalEntryId);
    }

    /// <summary>‏والمورّدُ غيرُ المسجَّل: الضريبةُ جزءٌ من التكلفة — فالأصلُ بالإجماليّ.</summary>
    [Fact]
    public async Task ANonDeductibleInvoice_CapitalizesTheTaxIntoTheAsset()
    {
        await SetupAsync();
        var asset = await RegisterAsync("FA-2", 11_500m);

        await PostInvoiceAsync(asset, 10_000m, vatDeductible: false);

        Assert.Equal(11_500m, await BalanceAsync(_assetAcctId));
        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.VatInput));
    }

    /// <summary><b>المنتظرُ فاتورتَه لا يُهلَك</b> — لا قيدَ له بعد، والإهلاكُ يبدأ من اقتنائه الحقيقيّ.</summary>
    [Fact]
    public async Task AnAssetAwaitingItsInvoice_IsNotDepreciated()
    {
        await SetupAsync();
        await RegisterAsync("FA-3", 12_000m);

        Assert.Equal(0, await new DepreciationService(_db).RunMonthlyAsync(_companyId, Day.AddMonths(2), "admin"));
    }

    [Fact]
    public async Task AnInvoiceThatDoesNotMatchTheAssetsCost_IsRefused_AndNothingIsPosted()
    {
        await SetupAsync();
        var asset = await RegisterAsync("FA-4", 9_000m);

        var ex = await Assert.ThrowsAsync<AccountingException>(() => PostInvoiceAsync(asset, 10_000m, vatDeductible: true));

        Assert.Contains("FA-4", ex.Message, StringComparison.Ordinal);
        _db.ChangeTracker.Clear();
        Assert.True((await _db.FixedAssets.SingleAsync(a => a.Id == asset.Id)).AwaitingInvoice);
        Assert.False(await _db.PurchaseInvoices.AnyAsync(i => i.Status == PurchaseInvoiceStatus.Posted));
    }

    [Fact]
    public async Task AnInvoiceOnAnotherDate_IsRefused()
    {
        await SetupAsync();
        var asset = await RegisterAsync("FA-5", 10_000m);

        await Assert.ThrowsAsync<AccountingException>(() =>
            PostInvoiceAsync(asset, 10_000m, vatDeductible: true, date: Day.AddDays(5)));
    }

    /// <summary><b>وإلغاءُ الفاتورة يُلغي اقتناءَ أصلها</b> — والدائنُ والضريبةُ والأصلُ تعود صفراً معاً.</summary>
    [Fact]
    public async Task CancellingTheInvoice_CancelsTheAcquisition()
    {
        await SetupAsync();
        var asset = await RegisterAsync("FA-6", 10_000m);
        var invoice = await PostInvoiceAsync(asset, 10_000m, vatDeductible: true);

        await new PurchaseInvoiceCancellationService(_db).CancelAsync(_companyId, invoice.Id, Day.AddDays(2), "admin");

        Assert.Equal(0m, await BalanceAsync(_assetAcctId));
        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsPayable));
        _db.ChangeTracker.Clear();
        var cancelled = await _db.FixedAssets.SingleAsync(a => a.Id == asset.Id);
        Assert.False(cancelled.IsActive);
        Assert.Equal(Day.AddDays(2), cancelled.CancelledOn);
    }

    /// <summary>‏وأصلٌ أُهلك يمنع إلغاءَ فاتورته — يُعكس إهلاكُه أوّلاً، كإلغاء الأصل نفسِه.</summary>
    [Fact]
    public async Task ADepreciatedAsset_BlocksCancellingItsInvoice()
    {
        await SetupAsync();
        var asset = await RegisterAsync("FA-7", 12_000m);
        var invoice = await PostInvoiceAsync(asset, 12_000m, vatDeductible: true);
        await new DepreciationService(_db).RunMonthlyAsync(_companyId, Day.AddMonths(1), "admin");

        await Assert.ThrowsAsync<AccountingException>(() =>
            new PurchaseInvoiceCancellationService(_db).CancelAsync(_companyId, invoice.Id, Day.AddMonths(1), "admin"));
        Assert.Equal(PurchaseInvoiceStatus.Posted, (await _db.PurchaseInvoices.SingleAsync(i => i.Id == invoice.Id)).Status);
    }

    /// <summary>‏<b>والمُقتنى بفاتورةٍ لا يُلغى من شاشة الأصل</b> — وإلا عُكس قيدُ الفاتورة وبقيت مرحَّلةً والمورّدُ دائناً بلا مقابل.</summary>
    [Fact]
    public async Task AnAssetBoughtByInvoice_IsCancelledThroughItsInvoiceOnly()
    {
        await SetupAsync();
        var asset = await RegisterAsync("FA-8", 10_000m);
        await PostInvoiceAsync(asset, 10_000m, vatDeductible: true);

        var ex = await Assert.ThrowsAsync<AccountingException>(() =>
            new FixedAssetCancellationService(_db).CancelAcquisitionAsync(_companyId, asset.Id, Day.AddDays(1), "admin"));
        Assert.Contains("ألغِ الفاتورة", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnInvoice_BuysAnAssetOrChargesAProject_NotBoth()
    {
        var invoice = PurchaseInvoice.CreateDraft(1, 1, Day, "SUP-1", vatDeductible: true);
        invoice.AssignProject(7);

        Assert.Throws<AccountingException>(() => invoice.AssignFixedAsset(9));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<FixedAsset> RegisterAsync(string code, decimal cost)
    {
        var asset = FixedAsset.CreateAwaitingInvoice(_companyId, code, "سيارة", Day, cost, 0m, 12, _assetAcctId, _depAcctId);
        await new FixedAssetAcquisitionPostingService(_db).RegisterAwaitingInvoiceAsync(asset);
        return asset;
    }

    private Task<PurchaseInvoice> PostInvoiceAsync(FixedAsset asset, decimal amount, bool vatDeductible, DateOnly? date = null)
    {
        var invoice = PurchaseInvoice.CreateDraft(
            _companyId, _supplierId, date ?? Day, $"SUP-{Guid.NewGuid():N}"[..12], vatDeductible: vatDeductible);
        invoice.AddLine("سيارة نقل", 1m, amount);
        invoice.AssignFixedAsset(asset.Id);
        return new PurchaseInvoicePostingService(_db).PostAsync(invoice);
    }

    private Task<decimal> BalanceAsync(long accountId) => BalanceWhereAsync(l => l.AccountId == accountId);

    private Task<decimal> BalanceAsync(string accountCode) => BalanceWhereAsync(l => l.AccountCode == accountCode);

    private async Task<decimal> BalanceWhereAsync(System.Linq.Expressions.Expression<Func<JournalLine, bool>> which) =>
        (await _db.JournalEntries
            .Where(e => e.CompanyId == _companyId && e.Status != JournalEntryStatus.Draft)
            .SelectMany(e => e.Lines)
            .Where(which)
            .Select(l => new { l.Debit, l.Credit })
            .ToListAsync())
        .Sum(l => l.Debit - l.Credit);

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700158", BusinessProfile.Retail);
        _companyId = company.Id;
        var supplier = Supplier.Create(_companyId, "وكيل السيّارات", "312222222200003");
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();
        _supplierId = supplier.Id;
        _assetAcctId = await _db.Accounts
            .Where(a => a.CompanyId == _companyId && a.Code.StartsWith("12") && !a.Code.StartsWith("1207") && !a.IsGroup)
            .Select(a => a.Id).FirstAsync();
        _depAcctId = await _db.Accounts
            .Where(a => a.CompanyId == _companyId && a.Code.StartsWith("1207"))
            .Select(a => a.Id).FirstAsync();
    }
}
