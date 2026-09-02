using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.RealEstate;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.RealEstate;

/// <summary>حصيلةُ ترحيل أمر صيانة — والمستنداتُ مُعلَنةٌ لا مطويّة.</summary>
public record MaintenancePostingResult(
    long OrderId,
    decimal TotalCost,
    decimal OwnerCost,
    decimal TenantCharge,
    long JournalEntryId,
    long? TenantInvoiceId);

/// <summary>ربحيّةُ وحدةٍ — <b>وهي ما لم يكن يُرى قبل ربط الصيانة بالوحدة</b>.</summary>
public record UnitMaintenanceCost(long? UnitId, string UnitLabel, int Orders, decimal OwnerCost);

/// <summary>
/// (CEEF-230 · ط3-١ب) <b>يُقيّد تكلفةَ الصيانة على وحدتها — ويُحوّل ما على المستأجر ديناً.</b>
///
/// <para><b>وحصّةُ المستأجر لا تُقيَّد قيداً</b>: الدفترُ المساعد يُبنى من المستندات
/// (<c>ط1-٤</c>)، <b>وقيدٌ يزيد <c>1104</c> يُصلح الميزانيةَ ويترك كشفَ حساب المستأجر
/// كما كان</b> — فيُطالَب بما لا يراه. <b>فتُصدَر له فاتورةٌ</b>: يراها، ويُطالَب بها،
/// وتظهر في أعمار ديونه.</para>
///
/// <para><b>وبها وحدَها تصير المصادرةُ ممكنة:</b> التأمينُ يُصادَر <b>في مقابل ما يُدين
/// به المستأجر</b> — ولا دَينَ بلا مستند. <b>وهذا هو الرابطُ الذي كان مفقوداً</b>: كان
/// مبلغُ المصادرة رقماً يكتبه محاسبٌ بلا شيءٍ خلفه.</para>
/// </summary>
public class MaintenanceOrderService(BrqoshDbContext db)
{
    /// <summary>حسابُ الصيانة والإصلاحات.</summary>
    public const string MaintenanceExpenseCode = "5206";

    /// <summary>
    /// يُرحّل أمرَ صيانةٍ منتهياً.
    /// </summary>
    /// <remarks>
    /// <para><b>والتكلفةُ كلُّها مصروفٌ على المالك أوّلاً</b> — ثمّ يُستردّ ما على
    /// المستأجر بفاتورة. <b>وقيدُ حصّةِ المالك وحدَها يُخفي نصفَ التكلفة</b>: العملُ
    /// وقع بكامله ودُفع بكامله، <b>وما يُستردّ إيرادٌ لا نقصٌ في المصروف</b>.</para>
    ///
    /// <para><b>ولا يُرحَّل إلا أمرٌ أُنهي</b>: تكلفتُه لم تثبت بعد، <b>وترحيلُ أمرٍ
    /// قائمٍ يُقيّد مصروفاً يزيد غداً</b> فيفترق القيدُ عن سنده.</para>
    /// </remarks>
    public async Task<MaintenancePostingResult> PostAsync(long companyId, long orderId, DateOnly on,
        string cashAccountCode = SaudiChartOfAccounts.SystemCodes.CashAtBanks,
        CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPostAccounting();

        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var order = await db.MaintenanceOrders
                .SingleOrDefaultAsync(m => m.Id == orderId && m.CompanyId == companyId, token)
                ?? throw new AccountingException("أمر الصيانة غير موجود.");

            if (order.Status != MaintenanceStatus.Completed)
                throw new AccountingException(
                    "لا يُرحَّل إلا أمرٌ أُنهي — وأمرٌ قائمٌ تكلفتُه تزيد غداً فيفترق القيدُ عن سنده.");
            if (order.JournalEntryId is not null)
                throw new AccountingException("أمرُ الصيانة مُرحَّلٌ سلفاً — ولا يُقيَّد مرّتين.");

            var expense = await SystemAccountProvisioning.EnsureAsync(
                db, companyId, MaintenanceExpenseCode, token);
            var cash = await SystemAccountProvisioning.EnsureAsync(db, companyId, cashAccountCode, token);

            await FinancialPostingSupport.ValidateAsync(
                db, companyId, on, PostingDocumentKind.JournalEntry,
                accountIds: [expense.Id, cash.Id], ct: token);

            var entry = JournalEntry.CreateDraft(companyId, on,
                $"صيانة {order.OrderNumber} — {order.Description}",
                sourceDocument: $"MaintenanceOrder:{order.OrderNumber}");
            entry.AddLine(expense, order.TotalCost, 0m,
                order.VendorName is null ? "صيانة" : $"صيانة — {order.VendorName}");
            entry.AddLine(cash, 0m, order.TotalCost, $"دفعُ صيانة {order.OrderNumber}");

            var number = await new JournalEntryNumberGenerator(db).NextAsync(companyId, on);
            entry.Post(number);
            db.JournalEntries.Add(entry);
            await db.SaveChangesAsync(token);

            order.LinkJournalEntry(entry.Id);

            long? invoiceId = null;
            if (order.TenantChargeAmount > 0m)
            {
                var lease = await db.LeaseContracts
                    .SingleOrDefaultAsync(l => l.Id == order.LeaseContractId, token)
                    ?? throw new AccountingException("عقدُ الإيجار غير موجود — ولا تحميلَ بلا عقد.");

                var invoice = SalesInvoice.CreateDraft(companyId, lease.TenantCustomerId, on);
                invoice.AddLine(
                    $"إعادةُ تحميل صيانة {order.OrderNumber} — {order.Description}",
                    1m, order.TenantChargeAmount);

                await new SalesInvoicePostingService(db).PostCoreAsync(invoice, token);
                invoiceId = invoice.Id;
            }

            await db.SaveChangesAsync(token);

            return new MaintenancePostingResult(order.Id, order.TotalCost, order.OwnerCost,
                order.TenantChargeAmount, entry.Id, invoiceId);
        }, ct);
    }

    /// <summary>
    /// تكلفةُ الصيانة على المالك لكلّ وحدة — <b>وهو الرقمُ الذي كان مستحيلاً</b>.
    /// </summary>
    /// <remarks>
    /// <b>ومحفظةٌ بأربعين وحدةً تُظهر ربحاً إجمالياً ولا تقول أيَّ وحدةٍ تخسر</b> — وثلاثُ
    /// وحداتٍ قد تأكل ربحَ السبعةِ والثلاثين. <b>والترتيبُ بالأعلى تكلفةً يضع الآكلةَ
    /// أوّلاً</b>: مَن يفتح الشاشةَ يراها قبل أيّ شيء.
    /// </remarks>
    public async Task<IReadOnlyList<UnitMaintenanceCost>> CostByUnitAsync(long companyId,
        DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var orders = await db.MaintenanceOrders
            .Where(m => m.CompanyId == companyId
                        && m.Status == MaintenanceStatus.Completed
                        && m.RequestDate >= from && m.RequestDate <= to)
            .ToListAsync(ct);

        if (orders.Count == 0) return [];

        var unitIds = orders.Where(o => o.UnitId != null).Select(o => o.UnitId!.Value)
            .Distinct().ToList();

        var units = await db.PropertyUnits.AsNoTracking()
            .Where(u => unitIds.Contains(u.Id))
            .Select(u => new { u.Id, u.UnitNumber })
            .ToDictionaryAsync(u => u.Id, u => u.UnitNumber, ct);

        return [.. orders
            .GroupBy(o => o.UnitId)
            .Select(g => new UnitMaintenanceCost(
                g.Key,
                g.Key is long id && units.TryGetValue(id, out var n) ? n : "مرافق مشتركة",
                g.Count(),
                Math.Round(g.Sum(o => o.OwnerCost), 2, MidpointRounding.AwayFromZero)))
            .OrderByDescending(u => u.OwnerCost)];
    }
}
