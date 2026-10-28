using Brqosh.Application.Treasury;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Treasury;

/// <summary>
/// <b>التدفّقُ المتوقَّع — والسببُ الأوّلُ لتعثّر منشآتٍ رابحة.</b>
///
/// <para><b>وكان مبنيّاً منذ يوليو، وفيه أربعةُ أعطالٍ تجعله يُطمئن كذباً:</b></para>
///
/// <para><b>الأوّل — لا رصيدَ متوقَّع أصلاً.</b> كان يُخرج داخلاً وخارجاً لكلّ أسبوع
/// <b>ولا يقول ما الرصيدُ آخرَه</b>. والمالكُ لا يسأل «كم يدخل» بل <b>«هل أستطيع
/// الدفع»</b> — وذاك لا يُجاب إلا برصيدٍ تراكميٍّ يبدأ من نقدٍ حقيقيّ. <b>فكان التقريرُ
/// يعرض أرقاماً صحيحةً لا تُجيب السؤال.</b></para>
///
/// <para><b>الثاني — ما بعد الأفق يُحشر في الأسبوع الأخير.</b> كان
/// <c>weekIndex = weeks.Count - 1</c> لكلّ ما يتجاوز ثلاثةَ عشرَ أسبوعاً: <b>فاتورةٌ
/// تستحقّ بعد سنةٍ تظهر داخلاً في الأسبوع الثالثَ عشر</b>. <b>وذاك أسوأُ من إسقاطها</b>
/// — يُظهر ذيلَ الأفق مليئاً بمالٍ لا يصل، <b>فيُبنى عليه التزام</b>.</para>
///
/// <para><b>الثالث — مهلةُ السداد ثابتةٌ ثلاثون يوماً للجميع</b>، ثابتٌ في الشيفرة لا
/// خاصيّةٌ للعميل. <b>فمَن يدفع نقداً ومَن يمهل تسعين يوماً سواء</b>.</para>
///
/// <para><b>الرابع — الشيكاتُ مُهمَلة.</b> وهي <b>أوثقُ ما يُتنبَّأ به</b>: ورقةٌ بتاريخِ
/// استحقاقٍ مكتوبٍ عليها. <b>فيُبنى التنبّؤ على المقدَّر ويُترك المؤكَّد.</b></para>
///
/// <para><b>وهويّةُ هـ-٢٢:</b> <c>رصيدُ آخرِ أسبوع = الافتتاحيُّ + Σالداخل − Σالخارج</c>
/// تراكمياً — <b>ولا يسقط مستندٌ صامتاً</b>: كلُّ مفتوحٍ إمّا في أسبوعٍ وإمّا مُعلَنٌ
/// «بعد الأفق».</para>
/// </summary>
public sealed class CashFlowForecastService(BrqoshDbContext db) : ICashFlowForecastService
{
    private const int WeekCount = 13;

    public async Task<CashFlowForecastResult> ForecastAsync(
        long companyId, DateOnly? asOfDate = null, CancellationToken ct = default)
    {
        var anchor = asOfDate ?? DateOnly.FromDateTime(DateTime.Today);
        var horizonEnd = anchor.AddDays(WeekCount * 7 - 1);

        var inflows = await ExpectedInflowsAsync(companyId, ct);
        var outflows = await ExpectedOutflowsAsync(companyId, ct);

        var weeks = new List<CashFlowForecastWeek>(WeekCount);
        var running = await CashBalanceAsync(companyId, anchor, ct);
        var opening = running;
        var lowest = running;
        DateOnly? shortfall = null;

        for (var i = 0; i < WeekCount; i++)
        {
            var start = anchor.AddDays(i * 7);
            var end = start.AddDays(6);

            // ‏**والأسبوعُ الأوّلُ يبتلع المتأخّر**: ما استحقّ قبل الأفق **مستحقٌّ الآن**،
            // ‏وإسقاطُه يُخفي أثقلَ ما على المنشأة.
            var isFirst = i == 0;
            var weekIn = Sum(inflows, start, end, isFirst);
            var weekOut = Sum(outflows, start, end, isFirst);

            running = Round(running + weekIn - weekOut);
            if (running < lowest) lowest = running;
            if (running < 0m && shortfall is null) shortfall = end;

            weeks.Add(new CashFlowForecastWeek(i + 1, start, end, weekIn, weekOut,
                Round(weekIn - weekOut), running));
        }

        // ‏**وما بعد الأفق يُعلَن ولا يُحشر** — وهو العطلُ الذي كان.
        var beyondIn = Round(inflows.Where(d => d.Due > horizonEnd).Sum(d => d.Amount));
        var beyondOut = Round(outflows.Where(d => d.Due > horizonEnd).Sum(d => d.Amount));

        return new CashFlowForecastResult(
            anchor,
            weeks,
            Round(weeks.Sum(w => w.ProjectedInflows)),
            Round(weeks.Sum(w => w.ProjectedOutflows)),
            opening,
            beyondIn,
            beyondOut,
            lowest,
            shortfall);
    }

    private static decimal Sum(IReadOnlyList<DueAmount> items, DateOnly from, DateOnly to,
        bool absorbOverdue) =>
        Round(items
            .Where(d => d.Due <= to && (d.Due >= from || absorbOverdue))
            .Sum(d => d.Amount));

    /// <summary>
    /// المقبوضاتُ المتوقَّعة — <b>بمهلةِ كلّ عميلٍ لا بثابتٍ في الشيفرة</b>.
    /// </summary>
    private async Task<IReadOnlyList<DueAmount>> ExpectedInflowsAsync(long companyId,
        CancellationToken ct)
    {
        var result = new List<DueAmount>();

        // ‏(أسئلةُ المالك 2026-10-24 · الأداء) ‏كانت تمرّ على كلّ عميلٍ وتسأل القاعدةَ عن كلّ فاتورة — استعلاماً لكلّ مستند. صارت أربعةَ استعلاماتٍ
        // ‏بالجملة؛ والمهلةُ تُطبَّق بحسب عميل المستند، وعملاؤه المحذوفون ناعماً خارجَ القائمة كما كانوا.
        var terms = await db.Customers.AsNoTracking()
            .Where(c => c.CompanyId == companyId)
            .Select(c => new { c.Id, c.PaymentTermDays })
            .ToDictionaryAsync(c => c.Id, c => c.PaymentTermDays, ct);

        foreach (var (customerId, doc) in await new PaymentAllocationService(db).OpenSalesDocumentsForAllAsync(companyId, ct))
        {
            if (doc.RemainingAmount <= 0m || !terms.TryGetValue(customerId, out var days)) continue;
            result.Add(new DueAmount(doc.DocumentDate.AddDays(days), Round(doc.RemainingAmount)));
        }

        // ‏**والشيكاتُ بتاريخها المكتوب** — أوثقُ ما يُتنبَّأ به، وكانت مُهمَلة.
        var cheques = await db.Cheques.AsNoTracking()
            .Where(c => c.CompanyId == companyId
                        && c.Type == ChequeType.Received
                        && (c.Status == ChequeStatus.InPortfolio || c.Status == ChequeStatus.Deposited)
                        && c.DueDate != null)
            .Select(c => new { c.DueDate, c.Amount })
            .ToListAsync(ct);

        result.AddRange(cheques.Select(c => new DueAmount(c.DueDate!.Value, Round(c.Amount))));
        return result;
    }

    private async Task<IReadOnlyList<DueAmount>> ExpectedOutflowsAsync(long companyId,
        CancellationToken ct)
    {
        var result = new List<DueAmount>();

        var terms = await db.Suppliers.AsNoTracking()
            .Where(s => s.CompanyId == companyId)
            .Select(s => new { s.Id, s.PaymentTermDays })
            .ToDictionaryAsync(s => s.Id, s => s.PaymentTermDays, ct);

        foreach (var (supplierId, doc) in await new PaymentAllocationService(db).OpenPurchaseDocumentsForAllAsync(companyId, ct))
        {
            if (doc.RemainingAmount <= 0m || !terms.TryGetValue(supplierId, out var days)) continue;
            result.Add(new DueAmount(doc.DocumentDate.AddDays(days), Round(doc.RemainingAmount)));
        }

        var cheques = await db.Cheques.AsNoTracking()
            .Where(c => c.CompanyId == companyId
                        && c.Type == ChequeType.Issued
                        && c.Status == ChequeStatus.InPortfolio
                        && c.DueDate != null)
            .Select(c => new { c.DueDate, c.Amount })
            .ToListAsync(ct);

        result.AddRange(cheques.Select(c => new DueAmount(c.DueDate!.Value, Round(c.Amount))));
        return result;
    }

    /// <summary>النقدُ المتاح — صندوقاً وبنكاً.</summary>
    private async Task<decimal> CashBalanceAsync(long companyId, DateOnly asOf, CancellationToken ct)
    {
        var codes = new[]
        {
            SaudiChartOfAccounts.SystemCodes.CashOnHand,
            SaudiChartOfAccounts.SystemCodes.CashAtBanks,
        };

        var total = await db.JournalEntries
            .Where(e => e.CompanyId == companyId && e.EntryDate <= asOf)
            .SelectMany(e => e.Lines)
            .Where(l => codes.Contains(l.AccountCode))
            .SumAsync(l => l.Debit - l.Credit, ct);

        return Round(total);
    }

    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private sealed record DueAmount(DateOnly Due, decimal Amount);
}
