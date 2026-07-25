using Brqosh.Domain.Construction;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Retail;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Construction;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Treasury;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Sales;

/// <summary>دقة دفاتر العملاء والموردين المساعدة مقابل القيود.</summary>
public class SubLedgerAccuracyTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _customerId;
    private long _supplierId;
    private long _bankAccountId;

    public SubLedgerAccuracyTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;

        var customer = Customer.Create(_companyId, "عميل");
        _db.Customers.Add(customer);
        var supplier = Supplier.Create(_companyId, "مورد", "300000000000003");
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;
        _supplierId = supplier.Id;

        var bank = await new BankAccountService(_db).CreateAsync(
            _companyId, "BNK1", "حساب رئيسي", "البنك", "123", "SA0000000000000000000000", true);
        _bankAccountId = bank.Id;
    }

    [Fact]
    public async Task PosSale_IsExcludedFromCustomerBalance()
    {
        await SetupAsync();

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 5));
        invoice.AddLine("بيع POS", 1m, 200m);
        invoice.MarkPosSale(1, PosPaymentMethod.Cash);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var balance = await new CustomerBalanceQuery(_db).ForCustomerAsync(_companyId, _customerId);
        Assert.Equal(0m, balance.Balance);
        Assert.Equal(0m, balance.Invoiced);
    }

    [Fact]
    public async Task CreditSale_IncludedInCustomerBalance()
    {
        await SetupAsync();

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 5));
        invoice.AddLine("بيع آجل", 1m, 100m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var balance = await new CustomerBalanceQuery(_db).ForCustomerAsync(_companyId, _customerId);
        Assert.Equal(115m, balance.Balance);
    }

    [Fact]
    public async Task ReceivedCheque_ReducesCustomerBalance()
    {
        await SetupAsync();

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 1));
        invoice.AddLine("بيع", 1m, 1000m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var cheque = Cheque.CreateReceived(_companyId, "CH-001", new DateOnly(2026, 7, 3),
            500m, "شيك عميل", _bankAccountId, _customerId);
        await new ChequePostingService(_db).RegisterReceivedAsync(cheque);

        var balance = await new CustomerBalanceQuery(_db).ForCustomerAsync(_companyId, _customerId);
        Assert.Equal(650m, balance.Balance); // 1150 - 500
    }

    [Fact]
    public async Task ReversedReceipt_AppearsOnStatementWithNetZero()
    {
        await SetupAsync();

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 1));
        invoice.AddLine("بيع", 1m, 100m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var voucher = ReceiptVoucher.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 2),
            115m, ReceiptMethod.Cash, "تحصيل");
        var posted = await new ReceiptVoucherPostingService(_db).PostAsync(voucher);

        await new VoucherReversalService(_db).ReverseReceiptAsync(
            _companyId, posted.Id, new DateOnly(2026, 7, 4), "admin");

        var balance = await new CustomerBalanceQuery(_db).ForCustomerAsync(_companyId, _customerId);
        Assert.Equal(115m, balance.Balance);

        var statement = await new CustomerBalanceQuery(_db).StatementAsync(_companyId, _customerId);
        Assert.Contains(statement, l => l.DocumentType == "عكس سند قبض");
        Assert.Equal(115m, statement.Last().RunningBalance);
    }

    [Fact]
    public async Task IssuedCheque_ReducesSupplierBalance()
    {
        await SetupAsync();

        var pi = PurchaseInvoice.CreateDraft(_companyId, _supplierId, new DateOnly(2026, 7, 1), "SUP-001", true);
        pi.AddLine("شراء", 1m, 1000m);
        await new PurchaseInvoicePostingService(_db).PostAsync(pi);

        var cheque = Cheque.CreateIssued(_companyId, "CH-OUT", new DateOnly(2026, 7, 3),
            400m, "شيك مورد", _bankAccountId, _supplierId);
        await new ChequePostingService(_db).RegisterIssuedAsync(cheque);

        var balance = await new SupplierBalanceQuery(_db).ForSupplierAsync(_companyId, _supplierId);
        Assert.Equal(750m, balance.Balance); // 1150 - 400
    }

    [Fact]
    public async Task SubcontractRetentionWithhold_ReducesSupplierBalance()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("مقاولات", "310123456700003", BusinessProfile.Construction);
        var customer = Customer.Create(company.Id, "عميل");
        var supplier = Supplier.Create(company.Id, "باطن", "300000000000003");
        _db.Customers.Add(customer);
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        var project = ConstructionProject.Create(
            company.Id, "PRJ-1", "مشروع", customer.Id, contractValue: 1_000_000m);
        project.Activate();
        await new ConstructionProjectService(_db).CreateAsync(project);

        var agreement = SubcontractAgreement.Create(
            company.Id, project.Id, supplier.Id, "SUB-01", new DateOnly(2026, 7, 1), 500_000m);
        agreement.Activate();
        _db.SubcontractAgreements.Add(agreement);
        await _db.SaveChangesAsync();

        var pi = PurchaseInvoice.CreateDraft(company.Id, supplier.Id, new DateOnly(2026, 7, 2), "SUP-200", true);
        pi.AddLine("مواد", 1m, 1000m);
        await new PurchaseInvoicePostingService(_db).PostAsync(pi);

        await new SubcontractRetentionWithholdService(_db).WithholdAsync(
            company.Id, agreement.Id, 50_000m, new DateOnly(2026, 7, 5), "محتجز", "admin");

        var balance = await new SupplierBalanceQuery(_db).ForSupplierAsync(company.Id, supplier.Id);
        Assert.Equal(-48_850m, balance.Balance); // 1150 invoiced - 50000 withhold
    }

    [Fact]
    public async Task CancelledSalesInvoice_AppearsOnStatementWithNetZero()
    {
        await SetupAsync();

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 1));
        invoice.AddLine("بيع", 1m, 100m);
        var posted = await new SalesInvoicePostingService(_db).PostAsync(invoice);

        await new SalesInvoiceCancellationService(_db).CancelAsync(
            _companyId, posted.Id, new DateOnly(2026, 7, 3), "admin");

        var balance = await new CustomerBalanceQuery(_db).ForCustomerAsync(_companyId, _customerId);
        Assert.Equal(0m, balance.Balance);

        var statement = await new CustomerBalanceQuery(_db).StatementAsync(_companyId, _customerId);
        Assert.Contains(statement, l => l.DocumentType == "إلغاء فاتورة مبيعات");
        Assert.Equal(0m, statement.Last().RunningBalance);
    }
}
