using Brqosh.Application.Accounting;
using Brqosh.Application.Reporting;
using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Accounting;

/// <summary>إقفال شهري بفحص ميزان مراجعة + تحذير تسويات بنكية معلّقة.</summary>
public sealed class MonthEndCloseOrchestrator(BrqoshDbContext db, ITrialBalanceQuery trialBalance)
{
    public async Task<MonthEndCloseResult> ClosePeriodAsync(
        long companyId,
        long periodId,
        string username,
        CancellationToken ct = default)
    {
        // ‏(م-٢ · ن-٠٣ تبعيّة) ‏**الفحصُ والإقفالُ في معاملةٍ واحدةٍ تحت حاجزِ الدفتر.**
        //
        // ‏وكانا منفصلين: يُقرأ ميزانُ المراجعة وتُعدّ التسوياتُ المعلّقة خارجَ أيِّ قفل،
        // ‏ثمّ يُقفَل. **فالرقمان اللذان يُعيدهما هذا التقرير يصفان لحظةً قد تكون مضت
        // ‏قبل الإقفال** — وتقريرُ إقفالٍ شهريٍّ يجب أن يصف الحالةَ التي أقفلها لا واحدةً سبقتها.
        //
        // ‏و`CloseAsync` ينضمّ إلى هذه المعاملة ويطلب الحاجزَ نفسَه — **والجلسةُ لا تحجب نفسَها**،
        // ‏وهي فرضيّةٌ مقيسةٌ لا مأخوذةٌ من توثيق (انظر `ASessionHoldingTheExclusiveLock_MayStillTakeItShared`).
        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var period = await db.FiscalPeriods.SingleAsync(
                p => p.CompanyId == companyId && p.Id == periodId, token);

            await LedgerYearLock.AcquireForClosingAsync(
                db.Database, companyId, await FiscalYearResolver.StartYearForAsync(db, companyId, period.StartDate, token), token);

            var rows = await trialBalance.ExecuteAsync(companyId, period.StartDate, period.EndDate, token);
            var totalDebit = rows.Sum(r => r.TotalDebit);
            var totalCredit = rows.Sum(r => r.TotalCredit);
            var difference = totalDebit - totalCredit;

            // ‏بوابةٌ حقيقية: لا فرقَ مسموحاً في الميزان — ولو هللة.
            if (difference != 0m)
                throw new AccountingException(
                    $"ميزان المراجعة غير متوازن للفترة {period.Code}: فرق {difference:N2}.");

            var unreconciled = await CountUnreconciledBankLinesAsync(
                companyId, period.StartDate, period.EndDate, token);
            if (unreconciled > 0)
                throw new AccountingException(
                    $"لا تُقفل الفترة {period.Code}: {unreconciled} سطر بنكي غير مسوّى — أكمل التسوية البنكية أولاً.");

            var drafts = await db.JournalEntries.CountAsync(e => e.CompanyId == companyId
                && e.Status == JournalEntryStatus.Draft
                && e.EntryDate >= period.StartDate && e.EntryDate <= period.EndDate, token);
            if (drafts > 0)
                throw new AccountingException(
                    $"لا تُقفل الفترة {period.Code}: {drafts} قيد مسودة معلّق — رحّله أو احذفه أولاً.");

            // ‏مطابقةُ الدفتر المساعد للمخزون مع حسابه الرقابي شرطٌ للإقفال (حين تكون المقارنة دقيقة: FIFO وحسابٌ موجود).
            var stock = await new Brqosh.Infrastructure.Reporting.InventoryGlReconciliationQuery(db)
                .RunAsync(companyId, ct: token);
            if (stock.Warning is null && !stock.IsReconciled)
                throw new AccountingException(
                    $"لا تُقفل الفترة {period.Code}: تقييم المخزون {stock.LayerValuation:N2} لا يطابق رصيد الحساب {stock.InventoryAccountCode} ({stock.GlBalance:N2}).");

            await new FiscalPeriodService(db).CloseAsync(companyId, periodId, username, token);

            return new MonthEndCloseResult(
                period.Code,
                true,
                difference,
                unreconciled,
                true);
        }, ct);
    }

    private async Task<int> CountUnreconciledBankLinesAsync(
        long companyId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var banks = await db.BankAccounts
            .Where(b => b.CompanyId == companyId && b.IsActive)
            .Select(b => b.Id)
            .ToListAsync(ct);

        var recon = new BankReconciliationQuery(db);
        var count = 0;
        foreach (var bankId in banks)
        {
            var lines = await recon.GetBookLinesAsync(companyId, bankId, from, to);
            count += lines.Count(l => !l.IsReconciled);
        }

        return count;
    }
}
