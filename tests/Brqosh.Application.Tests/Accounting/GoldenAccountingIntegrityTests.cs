using Brqosh.Domain.Accounting;
using Brqosh.Domain.Assets;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Security;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Assets;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Treasury;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Accounting;

/// <summary>Golden Accounting Integrity — R-05 (7 سيناريوهات).</summary>
public class GoldenAccountingIntegrityTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _customerId;
    private long _supplierId;
    private long _productId;
    private long _warehouseId;
    private readonly DateOnly _today = new(2026, 7, 9);

    public GoldenAccountingIntegrityTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new AuditLogImmutabilityInterceptor())
            .Options);
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task SetupAsync(BusinessProfile profile = BusinessProfile.Retail)
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة Golden", "310123456700003", profile);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        var customer = Customer.Create(_companyId, "عميل Golden", "311122233300003");
        var supplier = Supplier.Create(_companyId, "مورد Golden", "312222222200003");
        var product = Product.Create(_companyId, "G-P", "صنف Golden", "قطعة", ProductType.Stockable, 100m);
        _db.Customers.Add(customer);
        _db.Suppliers.Add(supplier);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;
        _supplierId = supplier.Id;
        _productId = product.Id;
        _warehouseId = (await new InventoryService(_db).GetDefaultWarehouseAsync(_companyId)).Id;
    }

    [Fact]
    public async Task Golden_AssetAcquisition_IncreasesAssetAndBalancesTb()
    {
        await SetupAsync();
        var assetAcctId = await _db.Accounts
            .Where(a => a.CompanyId == _companyId && a.Code.StartsWith("12") && !a.Code.StartsWith("1207") && !a.IsGroup)
            .Select(a => a.Id).FirstAsync();
        var depAcctId = await _db.Accounts
            .Where(a => a.CompanyId == _companyId && a.Code.StartsWith("1207"))
            .Select(a => a.Id).FirstAsync();

        var asset = FixedAsset.Create(_companyId, "FA-G1", "أصل Golden", _today.AddMonths(-3),
            30_000m, 3_000m, 36, assetAcctId, depAcctId);
        _db.FixedAssets.Add(asset);
        await _db.SaveChangesAsync();

        var entry = await new FixedAssetAcquisitionPostingService(_db).PostAcquisitionAsync(asset);
        Assert.NotNull(entry.EntryNumber);
        Assert.Equal(2, entry.Lines.Count);

        var tb = await TrialBalanceAsync();
        Assert.Equal(tb.Debit, tb.Credit);
        var assetBalance = tb.Rows.First(r => r.Code.StartsWith("12") && !r.Code.StartsWith("1207")).Debit;
        Assert.True(assetBalance >= 30_000m);
    }

    [Fact]
    public async Task Golden_AssetDisposal_ReducesAssetAndRecordsGainLoss()
    {
        await SetupAsync();
        var assetAcctId = await AssetAccountIdAsync();
        var depAcctId = await DepAccountIdAsync();
        var asset = FixedAsset.Create(_companyId, "FA-G2", "أصل بيع", _today.AddMonths(-12),
            10_000m, 10_000m, 24, assetAcctId, depAcctId);
        _db.FixedAssets.Add(asset);
        await _db.SaveChangesAsync();
        await new FixedAssetAcquisitionPostingService(_db).PostAcquisitionAsync(asset);

        var entry = await new FixedAssetDisposalPostingService(_db)
            .PostDisposalAsync(asset, 8_000m, _today, "admin");
        Assert.NotNull(entry.EntryNumber);
        Assert.False(asset.IsActive);

        var tb = await TrialBalanceAsync();
        Assert.Equal(tb.Debit, tb.Credit);
    }

    [Fact]
    public async Task Golden_Depreciation_CreatesAccumulatedDepreciationJe()
    {
        await SetupAsync();
        var asset = FixedAsset.Create(_companyId, "FA-G3", "أصل إهلاك", _today.AddMonths(-6),
            12_000m, 0m, 12, await AssetAccountIdAsync(), await DepAccountIdAsync());
        _db.FixedAssets.Add(asset);
        await _db.SaveChangesAsync();
        await new FixedAssetAcquisitionPostingService(_db).PostAcquisitionAsync(asset);

        var count = await new DepreciationService(_db).RunMonthlyAsync(_companyId, _today, "admin");
        Assert.Equal(1, count);

        var depJe = await _db.JournalEntries.SingleAsync(e =>
            e.CompanyId == _companyId && e.SourceDocument == $"Depreciation:{asset.Code}");
        Assert.Equal(2, depJe.Lines.Count);
        Assert.True(asset.AccumulatedDepreciation > 0);
    }

    [Fact]
    public async Task Golden_StockAdjustment_Inbound_BalancesInventoryJe()
    {
        await SetupAsync();
        var movement = StockMovement.CreateDraft(
            _companyId, _today, StockMovementType.AdjustmentIn, _warehouseId, "جرد Golden");
        movement.AddLine(_productId, 15m, 50m);
        var posted = await new StockMovementPostingService(_db).PostAsync(movement);

        Assert.NotNull(posted.JournalEntryId);
        var onHand = await new InventoryService(_db).GetOnHandAsync(_companyId, _productId, _warehouseId);
        Assert.Equal(15m, onHand);

        var tb = await TrialBalanceAsync();
        Assert.Equal(tb.Debit, tb.Credit);
    }

    [Fact]
    public async Task Golden_CreditNote_ReversesVatAndReducesAr()
    {
        await SetupAsync();
        var movement = StockMovement.CreateDraft(
            _companyId, _today, StockMovementType.AdjustmentIn, _warehouseId, "تجهيز مخزون");
        movement.AddLine(_productId, 20m, 50m);
        await new StockMovementPostingService(_db).PostAsync(movement);

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, _today);
        invoice.AddLine("بيع Golden", 10m, 200m, _productId);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var arBefore = await ArBalanceAsync();
        var note = CreditNote.CreateDraft(_companyId, _customerId, invoice.Id, _today, "مرتجع Golden");
        note.AddLine("مرتجع", 2m, 200m, _productId);
        await new CreditNotePostingService(_db).PostAsync(note);

        var arAfter = await ArBalanceAsync();
        Assert.True(arAfter < arBefore);
        Assert.True(note.VatAmount > 0);

        var tb = await TrialBalanceAsync();
        Assert.Equal(tb.Debit, tb.Credit);
    }

    [Fact]
    public async Task Golden_VatReturn_NetMatchesOutputMinusInput()
    {
        await SetupAsync();
        var pi = PurchaseInvoice.CreateDraft(_companyId, _supplierId, _today, "G-PI-VAT", true);
        pi.AddLine("شراء ضريبة", 5m, 100m, _productId);
        await new PurchaseInvoicePostingService(_db).PostAsync(pi);

        var si = SalesInvoice.CreateDraft(_companyId, _customerId, _today);
        si.AddLine("بيع ضريبة", 3m, 200m, _productId);
        await new SalesInvoicePostingService(_db).PostAsync(si);

        var vat = await new VatReturnQuery(_db).ExecuteAsync(_companyId, _today.AddMonths(-1), _today);
        Assert.True(vat.OutputVat > 0);
        Assert.True(vat.DeductibleInputVat > 0);
        Assert.Equal(vat.NetOutputVat - vat.NetDeductibleInputVat, vat.NetVatPayable);
    }

    [Fact]
    public async Task Golden_FxRevaluation_CreatesGainLossJeAndBalancesTb()
    {
        await SetupAsync();
        var bankGlId = await _db.Accounts
            .Where(a => a.CompanyId == _companyId && a.Code == "1102" && !a.IsGroup)
            .Select(a => a.Id).FirstAsync();
        var bank = BankAccount.Create(_companyId, "FX-BNK", "بنك عملة", "بنك", "123", "SA0000000000000000000001", bankGlId);
        bank.SetCurrency("USD");
        _db.BankAccounts.Add(bank);
        await _db.SaveChangesAsync();

        var entry = await new FxRevaluationService(_db).RevalueAsync(
            _companyId, bank.Id, _today, newRate: 3.80m, username: "admin", foreignBalance: 10_000m);
        Assert.NotNull(entry);
        Assert.Equal(2, entry!.Lines.Count);

        var tb = await TrialBalanceAsync();
        Assert.Equal(tb.Debit, tb.Credit);
    }

    [Fact]
    public async Task OperationalMode_ReadOnly_BlocksPosting()
    {
        await SetupAsync();
        var company = await _db.Companies.SingleAsync(c => c.Id == _companyId);
        company.SetOperationalMode(CompanyOperationalMode.ReadOnly);
        await _db.SaveChangesAsync();

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, _today);
        invoice.AddLine("محظور", 1m, 100m);
        await Assert.ThrowsAsync<AccountingException>(() => new SalesInvoicePostingService(_db).PostAsync(invoice));
    }

    private async Task<long> AssetAccountIdAsync() =>
        await _db.Accounts.Where(a => a.CompanyId == _companyId && a.Code.StartsWith("12") && !a.Code.StartsWith("1207") && !a.IsGroup)
            .Select(a => a.Id).FirstAsync();

    private async Task<long> DepAccountIdAsync() =>
        await _db.Accounts.Where(a => a.CompanyId == _companyId && a.Code.StartsWith("1207"))
            .Select(a => a.Id).FirstAsync();

    private async Task<(decimal Debit, decimal Credit, List<(string Code, decimal Debit, decimal Credit)> Rows)> TrialBalanceAsync()
    {
        var rows = await new TrialBalanceQuery(_db).ExecuteAsync(_companyId, _today.AddYears(-1), _today);
        return (rows.Sum(r => r.TotalDebit), rows.Sum(r => r.TotalCredit),
            rows.Select(r => (r.AccountCode, r.TotalDebit, r.TotalCredit)).ToList());
    }

    private async Task<decimal> ArBalanceAsync()
    {
        var arId = await _db.Accounts
            .Where(a => a.CompanyId == _companyId && a.Code == SaudiChartOfAccounts.SystemCodes.AccountsReceivable)
            .Select(a => a.Id).SingleAsync();
        var lines = await _db.JournalEntries.AsNoTracking()
            .Where(e => e.CompanyId == _companyId && e.Status != JournalEntryStatus.Draft)
            .SelectMany(e => e.Lines)
            .Where(l => l.AccountId == arId)
            .ToListAsync();
        return lines.Sum(l => l.Debit - l.Credit);
    }
}
