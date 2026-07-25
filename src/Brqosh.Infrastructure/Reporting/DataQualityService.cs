using Brqosh.Application.Reporting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Reporting;

public class DataQualityService(BrqoshDbContext db) : IDataQualityService
{
    public async Task<DataQualityReport> RunAsync(long companyId, CancellationToken ct = default)
    {
        var findings = new List<DataQualityFinding>();

        var unbalancedCount = await db.JournalEntries
            .Where(e => e.CompanyId == companyId && e.Status == JournalEntryStatus.Posted)
            .Select(e => new
            {
                Debit = e.Lines.Sum(l => l.Debit),
                Credit = e.Lines.Sum(l => l.Credit),
            })
            .Where(x => x.Debit != x.Credit)
            .CountAsync(ct);

        if (unbalancedCount > 0)
        {
            findings.Add(new DataQualityFinding(
                "JE_UNBALANCED_POSTED",
                "Error",
                $"{unbalancedCount} قيد يوميّة مرحّل مجموع مدينه لا يساوي مجموع دائنه.",
                $"{unbalancedCount} posted journal entr{(unbalancedCount == 1 ? "y" : "ies")} with TotalDebit != TotalCredit."));
        }

        var orphanInvoices = await db.SalesInvoices
            .Where(i => i.CompanyId == companyId
                        && i.Status == SalesInvoiceStatus.Posted
                        && i.JournalEntryId == null)
            .CountAsync(ct);

        if (orphanInvoices > 0)
        {
            findings.Add(new DataQualityFinding(
                "SI_POSTED_NO_JE",
                "Warning",
                $"{orphanInvoices} فاتورة مبيعات مرحّلة بلا قيد يوميّة مرتبط.",
                $"{orphanInvoices} posted sales invoice(s) without JournalEntryId."));
        }

        var negativeStock = await (
            from balance in db.InventoryBalances
            join product in db.Products on balance.ProductId equals product.Id
            where balance.CompanyId == companyId
                  && product.CompanyId == companyId
                  && product.IsActive
                  && product.Type == ProductType.Stockable
                  && balance.QuantityOnHand < 0
            select balance.Id).CountAsync(ct);

        if (negativeStock > 0)
        {
            findings.Add(new DataQualityFinding(
                "INV_NEGATIVE_ON_HAND",
                "Warning",
                $"{negativeStock} رصيد صنف مخزنيّ بكمية سالبة.",
                $"{negativeStock} stockable product balance(s) with negative on-hand quantity."));
        }

        var hasHq = await db.Branches
            .AnyAsync(b => b.CompanyId == companyId && b.IsHeadquarters && b.IsActive, ct);

        if (!hasHq)
        {
            findings.Add(new DataQualityFinding(
                "BRANCH_HQ_MISSING",
                "Error",
                "لا يوجد فرع رئيسيّ (مقرّ) نشط للمنشأة.",
                "Company has no active headquarters branch."));
        }

        var since = DateTime.UtcNow.AddHours(-24);
        var eventCount = await db.StoredDomainEvents
            .CountAsync(e => e.CompanyId == companyId && e.OccurredAtUtc >= since, ct);

        findings.Add(new DataQualityFinding(
            "DOMAIN_EVENTS_24H",
            "Info",
            $"{eventCount} حدث نطاق مسجّل خلال آخر 24 ساعة.",
            $"{eventCount} domain event(s) recorded in the last 24 hours."));

        return new DataQualityReport(companyId, DateTime.UtcNow, findings);
    }
}
