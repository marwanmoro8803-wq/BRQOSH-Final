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

/// <summary>نقطة شهرية حقيقية في اتجاه النقد .</summary>
public record CashTrendPoint(int Year, int Month, decimal Collected, decimal Paid)
{
    public decimal Net => Collected - Paid;
    public string Label => $"{Month:D2}/{Year % 100:D2}";
}

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
    int ClosedPeriods,
    // مؤشرات محاسبية حقيقية من قائمة الدخل — لا من إجماليات المستندات.
    // المبيعات والمشتريات أعلاه شاملة الضريبة وتصلح لمؤشرات حجم النشاط فقط.
    decimal RevenueThisMonth = 0m,
    decimal ExpensesThisMonth = 0m,
    decimal NetProfitThisMonth = 0m,
    // اتجاه نقد حقيقي من القاعدة — كان يُصطنع بضرب رقم الشهر الجاري
    // في معاملات ثابتة (0.7 و0.85) ويُعرض كأنه تاريخ فعلي.
    IReadOnlyList<CashTrendPoint>? CashTrend = null);

public class DashboardQuery(BrqoshDbContext db)
{
    public async Task<DashboardKpis> ExecuteAsync(long companyId)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        // الإيراد والمصروف والربح تُقرأ من قائمة الدخل — المصدر المحاسبي
        // الموثوق — فلا يمكن أن تتناقض لوحة التحكم معها كما كان يحدث.
        var pnl = await new IncomeStatementQuery(db).ExecuteAsync(companyId, monthStart, today);

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

        var cashTrend = await CashTrendAsync(companyId, today, months: 6);

        var receivables = (await new CustomerBalanceQuery(db).AllBalancesAsync(companyId)).Values.Sum();
        var payables = (await new SupplierBalanceQuery(db).AllBalancesAsync(companyId)).Values.Sum();

        var vat = await new VatReturnQuery(db).ExecuteAsync(companyId, monthStart, today);

        var inventoryRows = await new StockBalanceQuery(db).ExecuteAsync(companyId);
        var inventoryValue = inventoryRows.Sum(r => r.TotalValue);

        var periods = await db.FiscalPeriods.Where(p => p.CompanyId == companyId).ToListAsync();
        var open = periods.Count(p => p.Status == FiscalPeriodStatus.Open);
        var closed = periods.Count - open;

        return new DashboardKpis(sales, purchases, collected, paid, receivables, payables,
            vat.NetVatPayable, inventoryValue, open, closed,
            pnl.TotalRevenue, pnl.TotalExpenses, pnl.NetIncome, cashTrend);
    }

    /// <summary>
    /// اتجاه النقد لآخر عدة أشهر — تحصيلات وسدادات فعلية من القاعدة.
    /// تجميع واحد لكل جدول ثم تجميع في الذاكرة على عدد أشهر صغير — لا استعلام لكل شهر.
    /// </summary>
    private async Task<IReadOnlyList<CashTrendPoint>> CashTrendAsync(
        long companyId, DateOnly today, int months)
    {
        var firstMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(-(months - 1));

        var receipts = await db.ReceiptVouchers
            .Where(v => v.CompanyId == companyId && v.Status == ReceiptVoucherStatus.Posted
                        && v.VoucherDate >= firstMonth && v.VoucherDate <= today)
            .GroupBy(v => new { v.VoucherDate.Year, v.VoucherDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(v => v.Amount) })
            .ToListAsync();

        var payments = await db.PaymentVouchers
            .Where(v => v.CompanyId == companyId && v.Status == PaymentVoucherStatus.Posted
                        && v.VoucherDate >= firstMonth && v.VoucherDate <= today)
            .GroupBy(v => new { v.VoucherDate.Year, v.VoucherDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(v => v.Amount) })
            .ToListAsync();

        var trend = new List<CashTrendPoint>(months);
        for (var i = 0; i < months; i++)
        {
            var m = firstMonth.AddMonths(i);
            // شهر بلا حركة يبقى نقطة بصفر — لا يُحذف، وإلا انكمش المحور وأوهم بنشاط متصل.
            trend.Add(new CashTrendPoint(
                m.Year, m.Month,
                receipts.FirstOrDefault(r => r.Year == m.Year && r.Month == m.Month)?.Total ?? 0m,
                payments.FirstOrDefault(p => p.Year == m.Year && p.Month == m.Month)?.Total ?? 0m));
        }

        return trend;
    }
}