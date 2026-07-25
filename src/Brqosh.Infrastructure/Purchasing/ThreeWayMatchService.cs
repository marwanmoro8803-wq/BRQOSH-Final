using Brqosh.Domain.Accounting;
using Brqosh.Domain.Purchasing;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Purchasing;

public record ThreeWayMatchIssue(string Code, string Message);

public record ThreeWayMatchResult(bool IsMatch, IReadOnlyList<ThreeWayMatchIssue> Issues);

/// <summary>
/// مطابقة ثلاثية: أمر شراء ↔ استلامات مرحّلة ↔ فاتورة مشتريات.
/// تسامح الكمية الافتراضي 0، تسامح السعر 2%.
/// </summary>
public class ThreeWayMatchService(BrqoshDbContext db)
{
    public const decimal DefaultPriceTolerancePercent = 2m;

    public async Task<ThreeWayMatchResult> ValidateAsync(
        PurchaseInvoice invoice, decimal priceTolerancePercent = DefaultPriceTolerancePercent,
        CancellationToken ct = default)
    {
        var issues = new List<ThreeWayMatchIssue>();
        if (invoice.PurchaseOrderId is not long poId)
            return new ThreeWayMatchResult(true, issues); // بدون PO لا تُفرض مطابقة

        var po = await db.PurchaseOrders
            .SingleOrDefaultAsync(p => p.Id == poId && p.CompanyId == invoice.CompanyId, ct)
            ?? throw new AccountingException("أمر الشراء المرتبط بالفاتورة غير موجود.");

        if (po.SupplierId != invoice.SupplierId)
            issues.Add(new("SUPPLIER", "مورد الفاتورة لا يطابق أمر الشراء."));

        var poTotal = po.Lines.Sum(l => l.LineTotal);
        var invoiceTotal = invoice.Lines.Sum(l => l.LineTotal);
        if (invoiceTotal > poTotal * (1m + priceTolerancePercent / 100m))
        {
            issues.Add(new("TOTAL",
                $"إجمالي الفاتورة ({invoiceTotal:N2}) يتجاوز أمر الشراء ({poTotal:N2}) مع التسامح {priceTolerancePercent}%."));
        }

        var receipts = await db.GoodsReceipts
            .Where(g => g.CompanyId == invoice.CompanyId
                        && g.PurchaseOrderId == poId
                        && g.Status == GoodsReceiptStatus.Posted)
            .ToListAsync(ct);

        if (receipts.Count == 0)
        {
            issues.Add(new("GRN", "لا يوجد إذن استلام مرحّل لأمر الشراء — أكمل الاستلام قبل ترحيل الفاتورة، أو أزل ربط أمر الشراء."));
            return new ThreeWayMatchResult(false, issues);
        }

        // كميات مستلمة لكل منتج
        var receivedByProduct = receipts
            .SelectMany(r => r.Lines)
            .Where(l => l.ProductId is not null)
            .GroupBy(l => l.ProductId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.ReceivedQuantity));

        var orderedByProduct = po.Lines
            .Where(l => l.ProductId is not null)
            .GroupBy(l => l.ProductId!.Value)
            .ToDictionary(g => g.Key, g => (Qty: g.Sum(l => l.Quantity), Price: g.Average(l => l.UnitPrice)));

        foreach (var line in invoice.Lines.Where(l => l.ProductId is not null))
        {
            var pid = line.ProductId!.Value;
            if (!orderedByProduct.TryGetValue(pid, out var ordered))
            {
                issues.Add(new("PO_LINE", $"الصنف {line.Description} غير موجود في أمر الشراء."));
                continue;
            }

            var received = receivedByProduct.GetValueOrDefault(pid, 0m);
            if (line.Quantity > received + 0.0001m)
            {
                issues.Add(new("QTY",
                    $"كمية فاتورة «{line.Description}» ({line.Quantity:N4}) تتجاوز المستلم ({received:N4})."));
            }

            if (ordered.Price > 0)
            {
                var drift = Math.Abs(line.UnitPrice - ordered.Price) / ordered.Price * 100m;
                if (drift > priceTolerancePercent)
                {
                    issues.Add(new("PRICE",
                        $"سعر «{line.Description}» ينحرف {drift:N1}% عن أمر الشراء (تسامح {priceTolerancePercent}%)."));
                }
            }
        }

        return new ThreeWayMatchResult(issues.Count == 0, issues);
    }

    public async Task EnsureMatchOrThrowAsync(PurchaseInvoice invoice, CancellationToken ct = default)
    {
        var result = await ValidateAsync(invoice, ct: ct);
        if (result.IsMatch) return;
        throw new AccountingException(
            "فشل المطابقة الثلاثية:\n" + string.Join("\n", result.Issues.Select(i => $"• {i.Message}")));
    }
}
