using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Payroll;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Payroll;

/// <summary>رصيدُ سُلفةٍ واحدة — مُشتقٌّ لا مُخزَّن.</summary>
public record AdvanceBalance(
    long AdvanceId,
    long EmployeeId,
    string AdvanceNumber,
    decimal Amount,
    decimal Deducted,
    decimal Outstanding,
    decimal InstallmentAmount,
    AdvanceStatus Status);

/// <summary>
/// (CEEF-230 · ط2-٥) <b>صرفُ السُّلَف واشتقاقُ أرصدتها — والاستقطاعُ يُعفيها.</b>
///
/// <para><b>وهويّةُ هـ-١٧:</b> <c>الرصيدُ = المصروفُ − المُستقطَع</c> ولا يكون سالباً.
/// <b>ومجموعُ أرصدة السُّلَف = رصيدُ حساب <c>1116</c></b> — وهي الهويّةُ نفسُها التي
/// تحكم الذممَ المدينة والدائنة.</para>
///
/// <para><b>والمُستقطَعُ يُشتقّ من دورات الرواتب المرحّلة</b> لا من عدّادٍ في السُّلفة.
/// <b>وعدّادٌ مخزَّنٌ ينحرف عند أوّل عكسِ دورة</b>: الدورةُ ترتدّ ويبقى العدّادُ ناقصاً،
/// <b>فتُقفَل سُلفةٌ لم تُسدَّد</b> ويسقط الدَّينُ بلا قبض.</para>
/// </summary>
public class EmployeeAdvanceService(BrqoshDbContext db)
{
    /// <summary>
    /// يصرف السُّلفةَ نقداً أو من البنك، ويُقيّدها <b>أصلاً لا مصروفاً</b>.
    /// </summary>
    /// <remarks>
    /// <b>ولولا هذا القيد لخرج المالُ مصروفَ رواتب</b> — فيُضخَّم المصروفُ بما سيُستردّ،
    /// <b>ويختفي الأصلُ من الميزانية</b>، ثمّ يُنقَص المصروفُ في شهرٍ آخر عند الاستقطاع
    /// فيتذبذب بلا سببٍ تشغيليّ.
    /// </remarks>
    public async Task<EmployeeAdvance> DisburseAsync(long companyId, long advanceId,
        DateOnly on, string cashAccountCode, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPostAccounting();

        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var advance = await db.EmployeeAdvances
                .SingleOrDefaultAsync(a => a.Id == advanceId && a.CompanyId == companyId, token)
                ?? throw new AccountingException("السُّلفة غير موجودة.");

            var employee = await db.Employees
                .SingleOrDefaultAsync(e => e.Id == advance.EmployeeId, token)
                ?? throw new AccountingException("الموظّف غير موجود.");

            var advances = await SystemAccountProvisioning.EnsureAsync(
                db, companyId, SaudiChartOfAccounts.SystemCodes.EmployeeAdvances, token);
            var cash = await db.Accounts
                .SingleOrDefaultAsync(a => a.CompanyId == companyId && a.Code == cashAccountCode, token)
                ?? throw new AccountingException($"حساب الصرف «{cashAccountCode}» غير موجود.");

            await FinancialPostingSupport.ValidateAsync(
                db, companyId, on, PostingDocumentKind.JournalEntry,
                accountIds: [advances.Id, cash.Id], ct: token);

            var entry = JournalEntry.CreateDraft(companyId, on,
                $"سُلفة {advance.AdvanceNumber} — {employee.NameAr}",
                sourceDocument: $"EmployeeAdvance:{advance.AdvanceNumber}");
            entry.AddLine(advances, advance.Amount, 0m, $"سُلفة {employee.NameAr}");
            entry.AddLine(cash, 0m, advance.Amount, $"صرف سُلفة {advance.AdvanceNumber}");

            var number = await new JournalEntryNumberGenerator(db).NextAsync(companyId, on);
            entry.Post(number);
            db.JournalEntries.Add(entry);
            await db.SaveChangesAsync(token);

            advance.MarkDisbursed(on, entry.Id);
            await db.SaveChangesAsync(token);

            return advance;
        }, ct);
    }

    /// <summary>
    /// أرصدةُ السُّلَف المفتوحة لموظّفٍ — أو للمنشأة كلِّها.
    /// </summary>
    /// <remarks>
    /// <b>والمُستقطَعُ من الدورات المرحّلة وحدَها</b>: مسودّةٌ لم تُرحَّل لم تأخذ من
    /// الموظّف شيئاً، <b>وعدُّها سداداً يُقفل سُلفةً قائمة</b>.
    /// </remarks>
    public async Task<IReadOnlyList<AdvanceBalance>> BalancesAsync(long companyId,
        long? employeeId = null, CancellationToken ct = default)
    {
        var advances = await db.EmployeeAdvances
            .Where(a => a.CompanyId == companyId
                        && (employeeId == null || a.EmployeeId == employeeId)
                        && a.Status != AdvanceStatus.Cancelled
                        && a.Status != AdvanceStatus.Approved)
            .OrderBy(a => a.DisbursedOn)
            .ToListAsync(ct);

        if (advances.Count == 0) return [];

        var employeeIds = advances.Select(a => a.EmployeeId).Distinct().ToList();

        var deductions = await db.PayrollRuns
            .Where(r => r.CompanyId == companyId && r.Status == PayrollRunStatus.Posted)
            .SelectMany(r => r.Lines)
            .Where(l => l.EmployeeId != null && employeeIds.Contains(l.EmployeeId!.Value)
                        && l.AdvanceDeduction > 0m)
            .GroupBy(l => l.EmployeeId!.Value)
            .Select(g => new { EmployeeId = g.Key, Total = g.Sum(l => l.AdvanceDeduction) })
            .ToDictionaryAsync(x => x.EmployeeId, x => x.Total, ct);

        // (جاهزيّةُ الإطلاق · ح-٨٧) ‏**والمستردُّ في المخالصة النهائيّة سدادٌ كالمستقطَع من الراتب** — دائنُ `1116` في قيد التصفية.
        // ‏وكان الرصيدُ يُشتقّ من سطور الرواتب وحدَها، فسُلفةٌ تُستردّ من مستحقّات المغادر تبقى «قائمةً» ديناً على موظّفٍ رحل.
        var settled = await db.JournalEntries
            .WhereReportable(companyId)
            .Where(e => e.SourceDocument != null
                        && e.SourceDocument.StartsWith(EndOfServiceSettlementService.SourceDocumentPrefix))
            .SelectMany(e => e.Lines, (e, l) => new { e.SourceDocument, l.AccountCode, l.Debit, l.Credit })
            .Where(x => x.AccountCode == SaudiChartOfAccounts.SystemCodes.EmployeeAdvances)
            .ToListAsync(ct);

        foreach (var line in settled)
        {
            var key = line.SourceDocument![EndOfServiceSettlementService.SourceDocumentPrefix.Length..];
            if (long.TryParse(key, out var settledEmployee) && employeeIds.Contains(settledEmployee))
                deductions[settledEmployee] = deductions.GetValueOrDefault(settledEmployee) + line.Credit - line.Debit;
        }

        // ‏**والسدادُ يُطفئ الأقدمَ أوّلاً**: موظّفٌ بسُلفتين، والمُستقطَعُ إجماليٌّ في سطر
        // ‏الرواتب. **وتوزيعُه بالتساوي يُبقي الأقدمَ مفتوحةً إلى الأبد**، والأقدمُ هي
        // ‏التي يُقاس بها التأخّر.
        var result = new List<AdvanceBalance>();
        var remaining = new Dictionary<long, decimal>(deductions);

        foreach (var advance in advances)
        {
            var pool = remaining.GetValueOrDefault(advance.EmployeeId);
            var applied = Math.Min(pool, advance.Amount);
            remaining[advance.EmployeeId] = pool - applied;

            result.Add(new AdvanceBalance(advance.Id, advance.EmployeeId, advance.AdvanceNumber,
                advance.Amount, applied, advance.Amount - applied,
                advance.InstallmentAmount, advance.Status));
        }

        return result;
    }

    /// <summary>
    /// القسطُ الواجبُ على موظّفٍ هذا الشهر — <b>مجموعُ أقساط سُلَفه المفتوحة</b>.
    /// </summary>
    /// <remarks>
    /// <b>وآخرُ قسطٍ هو الباقي لا القسطَ الكامل</b> — وبغيره يُستقطَع أكثرُ من الدَّين
    /// فيصير الرصيدُ سالباً، <b>وتصير المنشأةُ مدينةً لموظّفها بفرقٍ لا يُعرف سببُه</b>.
    /// </remarks>
    public async Task<decimal> DueInstallmentAsync(long companyId, long employeeId,
        CancellationToken ct = default)
    {
        var balances = await BalancesAsync(companyId, employeeId, ct);

        return Math.Round(
            balances.Where(b => b.Outstanding > 0m).Sum(b => b.InstallmentFor()),
            2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// يُقفل ما سُدِّد ويُعيد ما ارتدّ سدادُه — <b>ويُستدعى بعد ترحيل الدورة وبعد عكسها</b>.
    /// </summary>
    /// <remarks>
    /// <b>والاتّجاهان معاً في كاتبٍ واحد</b>: كاتبٌ يُقفل ولا يُعيد <b>يُسقط دَيناً عند
    /// أوّل عكسِ دورة</b> — تُقفل السُّلفةُ ثمّ يرتدّ الاستقطاع، فيبقى المالُ عند الموظّف
    /// والسُّلفةُ «مسدَّدة».
    /// </remarks>
    public async Task<int> RefreshStatusesAsync(long companyId, CancellationToken ct = default)
    {
        var balances = await BalancesAsync(companyId, ct: ct);
        if (balances.Count == 0) return 0;

        var ids = balances.Select(b => b.AdvanceId).ToList();
        var advances = await db.EmployeeAdvances
            .Where(a => ids.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);

        var changed = 0;
        foreach (var balance in balances)
        {
            var advance = advances[balance.AdvanceId];

            if (balance.Outstanding <= 0m && advance.Status == AdvanceStatus.Outstanding)
            {
                advance.MarkSettled();
                changed++;
            }
            else if (balance.Outstanding > 0m && advance.Status == AdvanceStatus.Settled)
            {
                advance.Reopen();
                changed++;
            }
        }

        if (changed > 0) await db.SaveChangesAsync(ct);
        return changed;
    }
}

/// <summary>امتدادٌ يقرأ القسطَ الواجب من الرصيد.</summary>
public static class AdvanceBalanceExtensions
{
    public static decimal InstallmentFor(this AdvanceBalance balance) =>
        balance.Outstanding <= 0m ? 0m : Math.Min(balance.InstallmentAmount, balance.Outstanding);
}
