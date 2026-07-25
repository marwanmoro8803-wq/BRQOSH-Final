using Brqosh.Domain.Accounting;
using Brqosh.Domain.Construction;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Reporting;

public record DashboardKpis(
    decimal SalesThisMonth,
    decimal PurchasesThisMonth,
    decimal CollectedThisMonth,
    decimal PaidThisMonth,
    decimal Receivables,
    decimal Payables,
    decimal NetVatThisMonth,
    decimal InventoryValue,
    int OpenPeriods,
    int ClosedPeriods);

public class DashboardQuery(BrqoshDbContext db)
{
    public async Task<DashboardKpis> ExecuteAsync(long companyId)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        // Subtotal/VatAmount مُخزَّنان — تجميع SQL مباشر
        var sales = await db.SalesInvoices
            .Where(i => i.CompanyId == companyId && i.Status == SalesInvoiceStatus.Posted
                        && i.InvoiceDate >= monthStart && i.InvoiceDate <= today)
            .SumGrandTotalAsync();

        var billings = (await db.ProgressBillings
            .Where(b => b.CompanyId == companyId && b.Status == ProgressBillingStatus.Posted
                        && b.BillingDate >= monthStart && b.BillingDate <= today)
            .ToListAsync())
            .Sum(b => b.NetBillableAmount);

        sales += billings;

        var purchases = await db.PurchaseInvoices
            .Where(i => i.CompanyId == companyId && i.Status == PurchaseInvoiceStatus.Posted
                        && i.InvoiceDate >= monthStart && i.InvoiceDate <= today)
            .SumGrandTotalAsync();

        var collected = await db.ReceiptVouchers
            .Where(v => v.CompanyId == companyId && v.Status == ReceiptVoucherStatus.Posted
                        && v.VoucherDate >= monthStart && v.VoucherDate <= today)
            .SumAsync(v => (decimal?)v.Amount) ?? 0m;

        var paid = await db.PaymentVouchers
            .Where(v => v.CompanyId == companyId && v.Status == PaymentVoucherStatus.Posted
                        && v.VoucherDate >= monthStart && v.VoucherDate <= today)
            .SumAsync(v => (decimal?)v.Amount) ?? 0m;

        var receivables = (await new CustomerBalanceQuery(db).AllBalancesAsync(companyId)).Values.Sum();
        var payables = (await new SupplierBalanceQuery(db).AllBalancesAsync(companyId)).Values.Sum();

        var vat = await new VatReturnQuery(db).ExecuteAsync(companyId, monthStart, today);

        var inventoryRows = await new StockBalanceQuery(db).ExecuteAsync(companyId);
        var inventoryValue = inventoryRows.Sum(r => r.TotalValue);

        var periods = await db.FiscalPeriods.Where(p => p.CompanyId == companyId).ToListAsync();
        var open = periods.Count(p => p.Status == FiscalPeriodStatus.Open);
        var closed = periods.Count - open;

        return new DashboardKpis(sales, purchases, collected, paid, receivables, payables,
            vat.NetVatPayable, inventoryValue, open, closed);
    }
}

