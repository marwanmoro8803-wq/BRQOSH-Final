using Brqosh.Application.Reporting;
using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Reporting;

/// <summary>
/// ‏<b>قائمةُ التغيّرات في حقوق الملكية</b> (IAS 1) من الدفتر نفسه — بلا جدولٍ جديد.
///
/// <para>المكوّناتُ حساباتُ حقوق الملكية، ومعها «أرباح غير مقفلة» (3999) كما في المركز المالي. وتصنيفُ حركة الفترة:
/// قيدُ الإقفال السنويّ وقيدٌ كلُّ سطوره حقوقُ ملكية ← تحويلاتٌ بين المكوّنات (مجموعها صفر)؛ واحتياطيُّ ترجمة العملات ←
/// دخلٌ شاملٌ آخر؛ وما سواهما (رأسُ مالٍ مدفوع، توزيعات، جاري الشركاء) ← معاملاتٌ مع الملّاك. وربحُ الفترة يدخل المكوِّنَ
/// غيرَ المقفل، وما أُقفل منه إلى حسابات حقوق الملكية يخرج منه تحويلاً.</para>
/// </summary>
public sealed class StatementOfChangesInEquityQuery(BrqoshDbContext db) : IStatementOfChangesInEquityQuery
{
    public const string UnclosedProfitCode = "3999";
    public const string UnclosedProfitNameAr = "أرباح غير مقفلة";

    public async Task<StatementOfChangesInEquityDto> ExecuteAsync(
        long companyId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        if (toDate < fromDate)
            throw new AccountingException("نهاية المدة قبل بدايتها.");

        var origin = new DateOnly(1900, 1, 1);
        var dayBefore = fromDate > origin ? fromDate.AddDays(-1) : origin;
        var hasOpeningWindow = fromDate > origin;

        var accounts = await db.Accounts
            .Where(a => a.CompanyId == companyId && a.Type == AccountType.Equity)
            .Select(a => new { a.Id, a.Code, a.NameAr })
            .ToListAsync(ct);
        var ids = accounts.Select(a => a.Id).ToList();
        var codeById = accounts.ToDictionary(a => a.Id, a => a.Code);

        var opening = new Dictionary<long, decimal>();
        if (hasOpeningWindow)
        {
            var before = await db.JournalEntries.WhereReportable(companyId, null, dayBefore)
                .SelectMany(e => e.Lines)
                .Where(l => ids.Contains(l.AccountId))
                .Select(l => new { l.AccountId, l.Debit, l.Credit })
                .ToListAsync(ct);
            foreach (var g in before.GroupBy(l => l.AccountId))
                opening[g.Key] = g.Sum(l => l.Credit - l.Debit);
        }

        var period = await db.JournalEntries.WhereReportable(companyId, fromDate, toDate)
            .Where(e => e.Lines.Any(l => ids.Contains(l.AccountId)))
            .Select(e => new
            {
                e.SourceDocument,
                Lines = e.Lines.Select(l => new { l.AccountId, l.Debit, l.Credit }).ToList(),
            })
            .ToListAsync(ct);

        var oci = new Dictionary<long, decimal>();
        var owners = new Dictionary<long, decimal>();
        var transfers = new Dictionary<long, decimal>();
        foreach (var entry in period)
        {
            var isClosing = entry.SourceDocument != null
                            && entry.SourceDocument.StartsWith(ReportingJournalEntries.YearEndClosePrefix, StringComparison.Ordinal);
            var allEquity = entry.Lines.All(l => codeById.ContainsKey(l.AccountId));
            foreach (var line in entry.Lines.Where(l => codeById.ContainsKey(l.AccountId)))
            {
                var amount = line.Credit - line.Debit;
                var bucket = isClosing || allEquity ? transfers
                    : codeById[line.AccountId] == SaudiChartOfAccounts.SystemCodes.TranslationReserve ? oci
                    : owners;
                bucket[line.AccountId] = bucket.GetValueOrDefault(line.AccountId) + amount;
            }
        }

        var components = accounts
            .Select(a => new EquityComponentMovementDto(
                a.Code, a.NameAr,
                opening.GetValueOrDefault(a.Id),
                0m,
                oci.GetValueOrDefault(a.Id),
                owners.GetValueOrDefault(a.Id),
                transfers.GetValueOrDefault(a.Id)))
            .Where(c => c.Opening != 0 || c.OtherComprehensiveIncome != 0 || c.TransactionsWithOwners != 0 || c.TransfersWithinEquity != 0)
            .ToList();

        // ‏المكوِّنُ غيرُ المقفل: أوّلُه وآخرُه كما في المركز المالي، وربحُ الفترة بلا قيود الإقفال، والباقي ما أُقفل منه.
        var income = new IncomeStatementQuery(db);
        var unclosedOpening = hasOpeningWindow
            ? (await income.ExecuteAsync(companyId, origin, dayBefore, excludeYearEndClosing: false)).NetIncome
            : 0m;
        var unclosedClosing = (await income.ExecuteAsync(companyId, origin, toDate, excludeYearEndClosing: false)).NetIncome;
        var profit = (await income.ExecuteAsync(companyId, fromDate, toDate, excludeYearEndClosing: true)).NetIncome;
        var closedOut = unclosedClosing - unclosedOpening - profit;
        if (unclosedOpening != 0 || profit != 0 || closedOut != 0)
            components.Add(new EquityComponentMovementDto(
                UnclosedProfitCode, UnclosedProfitNameAr, unclosedOpening, profit, 0m, 0m, closedOut));

        return new StatementOfChangesInEquityDto(fromDate, toDate,
            components.OrderBy(c => c.AccountCode, StringComparer.Ordinal).ToList());
    }
}
