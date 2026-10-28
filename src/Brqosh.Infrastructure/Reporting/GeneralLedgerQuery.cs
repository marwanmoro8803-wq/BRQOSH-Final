using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Reporting;

public record GeneralLedgerLine(
    DateOnly EntryDate,
    string EntryNumber,
    string Description,
    decimal Debit,
    decimal Credit,
    decimal RunningBalance);

public record GeneralLedgerAccount(
    string AccountCode,
    string AccountNameAr,
    AccountType Type,
    IReadOnlyList<GeneralLedgerLine> Lines,
    decimal ClosingBalance);

/// <summary>دفتر الأستاذ العام — حركة كل حساب مع رصيد متحرك (القيود المرحّلة فقط).</summary>
public class GeneralLedgerQuery(BrqoshDbContext db)
{
    public async Task<IReadOnlyList<GeneralLedgerAccount>> ExecuteAsync(
        long companyId, DateOnly from, DateOnly to, string? accountCodeFilter = null)
    {
        var entries = await db.JournalEntries
            .WhereReportable(companyId, from, to)
            .OrderBy(e => e.EntryDate).ThenBy(e => e.EntryNumber)
            .ToListAsync();

        // الأرصدة الافتتاحية تُجمَّع في قاعدة البيانات لا في الذاكرة.
        // كان يُحمَّل كل تاريخ القيود منذ التأسيس بسطوره المملوكة لمجرّد جمع مدين/دائن
        // لكل حساب — مئات آلاف الصفوف في كل فتح للتقرير. نفس نمط TrialBalanceQuery.
        var priorTotals = await db.JournalEntries
            .WhereReportable(companyId, to: from.AddDays(-1))
            .SelectMany(e => e.Lines)
            .GroupBy(l => l.AccountId)
            .Select(g => new
            {
                AccountId = g.Key,
                Debit = g.Sum(l => l.Debit),
                Credit = g.Sum(l => l.Credit),
            })
            .ToListAsync();

        var accountIds = entries.SelectMany(e => e.Lines).Select(l => l.AccountId)
            .Concat(priorTotals.Select(t => t.AccountId))
            .Distinct().ToList();
        // كما في TrialBalanceQuery: الحساب المعطَّل ما زال له سطور مرحّلة تاريخياً.
        var accounts = await db.Accounts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id);

        // الرصيد الافتتاحي: الفرق حسب طبيعة الحساب — مشتقّ من المجاميع المُجمَّعة في SQL.
        var openingByAccount = priorTotals.ToDictionary(
            t => t.AccountId,
            t => accounts[t.AccountId].Type.IsDebitNature()
                ? t.Debit - t.Credit
                : t.Credit - t.Debit);

        var byAccount = entries
            .SelectMany(e => e.Lines.Select(l => new { Entry = e, Line = l }))
            .GroupBy(x => x.Line.AccountId);

        var result = new List<GeneralLedgerAccount>();
        foreach (var group in byAccount.OrderBy(g => accounts[g.Key].Code))
        {
            var account = accounts[group.Key];
            if (accountCodeFilter is not null && !account.Code.StartsWith(accountCodeFilter, StringComparison.Ordinal))
                continue;

            var opening = openingByAccount.GetValueOrDefault(account.Id, 0m);
            var running = opening;
            var lines = new List<GeneralLedgerLine>();

            if (opening != 0)
            {
                // `opening` موجبٌ إذا كان على الجانب الطبيعيّ للحساب، وسالبٌ إذا
                // كان على الجانب المقابل. التعبير السابق غطّى ثلاث حالات وأخطأ الرابعة:
                // حسابٌ دائن الطبيعة برصيدٍ افتتاحيٍّ **مدين** (مورد دُفع له مقدماً) كان يُطبع
                // في عمود الدائن — فأعمدة الحساب تصير 0/8,000 والحقيقة 5,000/3,000، ويفترق
                // مجموع الأستاذ عن ميزان المراجعة بضعف المبلغ.
                //  الحالات الأربع:  مدين الطبيعة/موجب -> مدين  ·  مدين الطبيعة/سالب -> دائن
                //                    دائن الطبيعة/موجب -> دائن  ·  دائن الطبيعة/سالب -> مدين
                var isDebitNature = account.Type.IsDebitNature();
                var onNaturalSide = opening > 0;
                var goesToDebitColumn = isDebitNature == onNaturalSide;
                var openingDebit = goesToDebitColumn ? Math.Abs(opening) : 0m;
                var openingCredit = goesToDebitColumn ? 0m : Math.Abs(opening);

                lines.Add(new GeneralLedgerLine(
                    from,
                    "",
                    "رصيد افتتاحي",
                    openingDebit,
                    openingCredit,
                    opening));
            }

            lines.AddRange(group
                .OrderBy(x => x.Entry.EntryDate).ThenBy(x => x.Entry.EntryNumber)
                .Select(x =>
                {
                    var delta = account.Type.IsDebitNature()
                        ? x.Line.Debit - x.Line.Credit
                        : x.Line.Credit - x.Line.Debit;
                    running += delta;
                    return new GeneralLedgerLine(
                        x.Entry.EntryDate,
                        x.Entry.EntryNumber ?? "",
                        string.IsNullOrWhiteSpace(x.Line.Description) ? x.Entry.Description : x.Line.Description,
                        x.Line.Debit, x.Line.Credit, running);
                }));

            result.Add(new GeneralLedgerAccount(account.Code, account.NameAr, account.Type, lines, running));
        }

        return result;
    }

}
