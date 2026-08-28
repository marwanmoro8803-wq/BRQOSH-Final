using Brqosh.Infrastructure.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Construction;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Sales;

public record CustomerBalance(long CustomerId, decimal Invoiced, decimal Credited, decimal Received)
{
    /// <summary>الرصيد المدين المستحق على العميل.</summary>
    public decimal Balance => Invoiced - Credited - Received;
}

public record CustomerStatementLine(DateOnly Date, string DocumentNumber, string DocumentType,
    decimal Debit, decimal Credit, decimal RunningBalance);

/// <summary>
/// دفتر مساعد للعملاء: الرصيد وكشف الحساب يُبنيان من المستندات المرحّلة
/// (فواتير آجلة + مستخلصات − إشعارات دائنة − سندات قبض − شيكات واردة محصّلة/قيد التحصيل).
/// تُستبعد فواتير نقاط البيع النقدية لأنها لا تُقيَّد على ذمم العملاء.
/// </summary>
public class CustomerBalanceQuery(BrqoshDbContext db)
{
    /// <summary>شيك وارد سجّل تخفيض ذمة العميل عند الاستلام.</summary>
    private static readonly ChequeStatus[] ReceivedChequeCreditStatuses =
    [
        ChequeStatus.InPortfolio,
        ChequeStatus.Deposited,
        ChequeStatus.Cleared,
    ];

    private static IQueryable<SalesInvoice> ArSalesInvoices(BrqoshDbContext db, long companyId, long? customerId = null)
    {
        var q = db.SalesInvoices.Where(i =>
            i.CompanyId == companyId
            && i.Status == SalesInvoiceStatus.Posted
            && i.PosShiftId == null);
        return customerId is long cid ? q.Where(i => i.CustomerId == cid) : q;
    }

    /// <summary>
    /// (CEEF-229 · م0-٩) إشعارات الدائن التي تمسّ <b>الذمم</b> — دون إشعارات نقاط البيع.
    ///
    /// <para>الفواتير كانت تستثني نقاط البيع (<c>PosShiftId == null</c>) والإشعاراتُ لا تستثنيها،
    /// فيُطرح من الذمم ما لم يُضَف إليها قط. و<c>CreditNotePostingService</c> يوجّه إشعار فاتورة
    /// نقطة البيع إلى <b>الصندوق</b> لا إلى حساب العملاء — بحقّ، فالبيع كان نقدياً.</para>
    ///
    /// <para><b>الأثر:</b> عميل مسجَّل يشتري من الكاشير بـ1,150 نقداً ثم يُرجع البضاعة ⟵ الأستاذ
    /// يقول ذمم العميل صفر، والدفتر المساعد يقول <b>سالب 1,150</b>. رصيدٌ دائنٌ وهميّ يظهر في
    /// كشف الحساب وتقرير الأعمار، ومؤشّر «المدينون» أقلّ من الحقيقة عن كل حالة مماثلة. ويصيب
    /// قطاع التجزئة تحديداً — الشريحة المستهدفة الأولى.</para>
    /// </summary>
    private static IQueryable<CreditNote> ArCreditNotes(BrqoshDbContext db, long companyId, long? customerId = null)
    {
        var posInvoiceIds = db.SalesInvoices
            .Where(i => i.CompanyId == companyId && i.PosShiftId != null)
            .Select(i => i.Id);

        var q = db.CreditNotes.Where(n =>
            n.CompanyId == companyId
            && n.Status == CreditNoteStatus.Posted
            && !posInvoiceIds.Contains(n.OriginalInvoiceId));
        return customerId is long cid ? q.Where(n => n.CustomerId == cid) : q;
    }

    public async Task<CustomerBalance> ForCustomerAsync(long companyId, long customerId)
    {
        var invoiced = await ArSalesInvoices(db, companyId, customerId).SumGrandTotalAsync();

        var billings = (await db.ProgressBillings
            .Where(b => b.CompanyId == companyId && b.CustomerId == customerId && b.Status == ProgressBillingStatus.Posted)
            .ToListAsync())
            .Sum(b => b.GrandTotal);

        var credited = await ArCreditNotes(db, companyId, customerId).SumGrandTotalAsync();

        var received = await db.ReceiptVouchers
            .Where(v => v.CompanyId == companyId && v.CustomerId == customerId && v.Status == ReceiptVoucherStatus.Posted)
            .SumAsync(v => (decimal?)v.Amount) ?? 0m;

        received += await db.Cheques
            .Where(c => c.CompanyId == companyId && c.CustomerId == customerId
                        && c.Type == ChequeType.Received
                        && ReceivedChequeCreditStatuses.Contains(c.Status))
            .SumAsync(c => (decimal?)c.Amount) ?? 0m;

        return new CustomerBalance(customerId, invoiced + billings, credited, received);
    }

    public async Task<IReadOnlyDictionary<long, decimal>> AllBalancesAsync(long companyId)
    {
        var invoiced = await db.SalesInvoices
            .Where(i => i.CompanyId == companyId
                        && i.Status == SalesInvoiceStatus.Posted
                        && i.PosShiftId == null)
            .GroupBy(i => i.CustomerId)
            .Select(g => new { CustomerId = g.Key, Total = g.Sum(i => i.Subtotal + i.VatAmount) })
            .ToDictionaryAsync(x => x.CustomerId, x => x.Total);

        var billings = (await db.ProgressBillings
            .Where(b => b.CompanyId == companyId && b.Status == ProgressBillingStatus.Posted)
            .ToListAsync())
            .GroupBy(b => b.CustomerId)
            .ToDictionary(g => g.Key, g => g.Sum(b => b.GrandTotal));

        var credited = await ArCreditNotes(db, companyId)
            .GroupBy(n => n.CustomerId)
            .Select(g => new { CustomerId = g.Key, Total = g.Sum(n => n.Subtotal + n.VatAmount) })
            .ToDictionaryAsync(x => x.CustomerId, x => x.Total);

        var received = await db.ReceiptVouchers
            .Where(v => v.CompanyId == companyId && v.Status == ReceiptVoucherStatus.Posted)
            .GroupBy(v => v.CustomerId)
            .Select(g => new { CustomerId = g.Key, Total = g.Sum(v => v.Amount) })
            .ToDictionaryAsync(x => x.CustomerId, x => x.Total);

        var chequeReceived = await db.Cheques
            .Where(c => c.CompanyId == companyId && c.CustomerId != null
                        && c.Type == ChequeType.Received
                        && ReceivedChequeCreditStatuses.Contains(c.Status))
            .GroupBy(c => c.CustomerId!.Value)
            .Select(g => new { CustomerId = g.Key, Total = g.Sum(c => c.Amount) })
            .ToDictionaryAsync(x => x.CustomerId, x => x.Total);

        var all = invoiced.Keys
            .Union(billings.Keys)
            .Union(credited.Keys)
            .Union(received.Keys)
            .Union(chequeReceived.Keys);

        return all.ToDictionary(id =>
            id,
            id => invoiced.GetValueOrDefault(id) + billings.GetValueOrDefault(id)
                  - credited.GetValueOrDefault(id) - received.GetValueOrDefault(id)
                  - chequeReceived.GetValueOrDefault(id));
    }

    /// <summary>كشف حساب العميل زمنياً مع رصيد متحرك.</summary>
    public async Task<IReadOnlyList<CustomerStatementLine>> StatementAsync(long companyId, long customerId)
    {
        var rows = new List<(DateOnly Date, string Number, string Type, decimal Debit, decimal Credit)>();

        foreach (var i in await ArSalesInvoices(db, companyId, customerId).ToListAsync())
            rows.Add((i.InvoiceDate, i.InvoiceNumber ?? "", "فاتورة مبيعات", i.GrandTotal(), 0m));

        foreach (var i in await db.SalesInvoices
            .Where(i => i.CompanyId == companyId && i.CustomerId == customerId
                        && i.Status == SalesInvoiceStatus.Cancelled && i.PosShiftId == null)
            .ToListAsync())
        {
            await AppendCancellationRowAsync(rows, i.InvoiceDate, i.InvoiceNumber ?? "", "فاتورة مبيعات",
                i.GrandTotal(), 0m, i.JournalEntryId);
        }

        foreach (var b in await db.ProgressBillings
            .Where(b => b.CompanyId == companyId && b.CustomerId == customerId && b.Status == ProgressBillingStatus.Posted)
            .ToListAsync())
            rows.Add((b.BillingDate, b.BillingNumber ?? "", "مستخلص أعمال", b.GrandTotal, 0m));

        foreach (var b in await db.ProgressBillings
            .Where(b => b.CompanyId == companyId && b.CustomerId == customerId && b.Status == ProgressBillingStatus.Cancelled)
            .ToListAsync())
        {
            await AppendCancellationRowAsync(rows, b.BillingDate, b.BillingNumber ?? "", "مستخلص أعمال",
                b.GrandTotal, 0m, b.JournalEntryId);
        }

        // (CEEF-229 · م0-٩) كشف الحساب يتبع الرصيد: إشعار نقطة البيع لا يمسّ الذمم فلا يظهر فيه.
        // وإلا اختلف الرصيد الجاري في الكشف عن الرصيد في رأسه — وهو أوّل ما يفحصه العميل.
        foreach (var n in await ArCreditNotes(db, companyId, customerId).ToListAsync())
            rows.Add((n.NoteDate, n.NoteNumber ?? "", "إشعار دائن", 0m, n.GrandTotal()));

        var posInvoiceIdsForStatement = db.SalesInvoices
            .Where(i => i.CompanyId == companyId && i.PosShiftId != null)
            .Select(i => i.Id);

        foreach (var n in await db.CreditNotes
            .Where(n => n.CompanyId == companyId && n.CustomerId == customerId
                        && n.Status == CreditNoteStatus.Cancelled
                        && !posInvoiceIdsForStatement.Contains(n.OriginalInvoiceId))
            .ToListAsync())
        {
            await AppendCancellationRowAsync(rows, n.NoteDate, n.NoteNumber ?? "", "إشعار دائن",
                0m, n.GrandTotal(), n.JournalEntryId);
        }

        foreach (var v in await db.ReceiptVouchers
            .Where(v => v.CompanyId == companyId && v.CustomerId == customerId
                        && (v.Status == ReceiptVoucherStatus.Posted || v.Status == ReceiptVoucherStatus.Reversed))
            .ToListAsync())
        {
            rows.Add((v.VoucherDate, v.VoucherNumber ?? "", "سند قبض", 0m, v.Amount));
            if (v.Status == ReceiptVoucherStatus.Reversed && v.JournalEntryId is long jeId)
            {
                var original = await db.JournalEntries.SingleOrDefaultAsync(e => e.Id == jeId);
                if (original?.ReversalEntryId is long revId)
                {
                    var reversal = await db.JournalEntries.SingleAsync(e => e.Id == revId);
                    rows.Add((reversal.EntryDate, v.VoucherNumber ?? "", "عكس سند قبض", v.Amount, 0m));
                }
            }
        }

        foreach (var c in await db.Cheques
            .Where(c => c.CompanyId == companyId && c.CustomerId == customerId && c.Type == ChequeType.Received)
            .ToListAsync())
        {
            if (ReceivedChequeCreditStatuses.Contains(c.Status))
                rows.Add((c.ChequeDate, c.ChequeNumber, "شيك وارد", 0m, c.Amount));
            else if (c.Status == ChequeStatus.Bounced)
            {
                rows.Add((c.ChequeDate, c.ChequeNumber, "شيك وارد", 0m, c.Amount));
                rows.Add((c.ChequeDate, c.ChequeNumber, "ارتجاع شيك", c.Amount, 0m));
            }
        }

        var running = 0m;
        return rows
            .OrderBy(r => r.Date).ThenBy(r => r.Number)
            .Select(r =>
            {
                running += r.Debit - r.Credit;
                return new CustomerStatementLine(r.Date, r.Number, r.Type, r.Debit, r.Credit, running);
            })
            .ToList();
    }

    private async Task AppendCancellationRowAsync(
        List<(DateOnly Date, string Number, string Type, decimal Debit, decimal Credit)> rows,
        DateOnly docDate, string number, string typeLabel,
        decimal debit, decimal credit, long? journalEntryId)
    {
        rows.Add((docDate, number, typeLabel, debit, credit));
        if (journalEntryId is not long jeId) return;

        var original = await db.JournalEntries.SingleOrDefaultAsync(e => e.Id == jeId);
        if (original?.ReversalEntryId is not long revId) return;

        var reversal = await db.JournalEntries.SingleAsync(e => e.Id == revId);
        rows.Add((reversal.EntryDate, number, $"إلغاء {typeLabel}", credit, debit));
    }
}
