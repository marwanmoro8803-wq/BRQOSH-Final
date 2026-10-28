using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Hospitality;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Hospitality;

/// <summary>حصيلةُ إقفال وعاءٍ — والمستنداتُ مُعلَنةٌ لا مطويّة.</summary>
public record TipPoolPostingResult(
    long PoolId,
    decimal CollectedCash,
    decimal CollectedCard,
    decimal TotalCollected,
    long JournalEntryId);

/// <summary>
/// <b>يُقيّد الإكرامياتِ التزاماً لا إيراداً — ثمّ يدفعها.</b>
///
/// <para><b>وهذا هو الباب كلُّه:</b> بلا حساب <c>2116</c> لا يجد النظامُ للإكرامية إلا
/// الإيرادَ موضعاً — <b>فتُدفع ضريبةُ قيمةٍ مضافةٍ وزكاةٌ على مالٍ ليس للمنشأة</b>.</para>
///
/// <para><b>والتحصيلُ والدفعُ حدثان لا حدثٌ واحد</b>: الوعاءُ يُقفل حين تُوزَّع الحصص —
/// <b>وذاك تخصيصٌ لا صرف</b>. والنُّدُلُ يُدفعون لاحقاً مع الرواتب أو نقداً، <b>ويبقى
/// <c>2116</c> بينهما يحمل ما لهم</b>. ودمجُهما في قيدٍ واحدٍ يُخفي الالتزامَ تماماً:
/// يدخل ويخرج في اللحظة نفسِها، <b>فلا يظهر في الميزانية قطُّ أنّ على المنشأة مالاً
/// لعامليها</b>.</para>
///
/// <para><b>وهويّةُ هـ-١٩ تُقاس على الحساب</b>: <c>رصيدُ 2116 = المحصَّلُ − المدفوع</c>،
/// ولا يكون سالباً — وسالبُه يعني دفعَ ما لم يُحصَّل.</para>
/// </summary>
public class TipPoolService(BrqoshDbContext db)
{
    /// <summary>
    /// يُقفل الوعاءَ ويُقيّد المحصَّلَ التزاماً.
    /// </summary>
    /// <remarks>
    /// <para><b>والنقديُّ يدخل الصندوقَ والبطاقةُ تدخل البنك</b> — لأنّ المالَ وصل
    /// فعلاً بمسارين. <b>وجمعُهما على الصندوق يُظهر فيه مالاً لم يدخله</b>، فيفترق
    /// رصيدُ الدفتر عن الدرج ويُطارَد الفرقُ شهرياً.</para>
    ///
    /// <para><b>والإقفالُ يشترط توزيعَ الحصص</b> — والنطاقُ يحرسه: بقيّةٌ بلا صاحبٍ
    /// تبقى في <c>2116</c> إلى الأبد ثمّ تُشطب إيراداً.</para>
    /// </remarks>
    public async Task<TipPoolPostingResult> CloseAndPostAsync(long companyId, long poolId,
        DateOnly on, string cashAccountCode = SaudiChartOfAccounts.SystemCodes.CashOnHand,
        string? bankAccountCode = null,
        CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPostAccounting();

        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var pool = await db.TipPools
                .SingleOrDefaultAsync(p => p.Id == poolId && p.CompanyId == companyId, token)
                ?? throw new AccountingException("وعاء الإكراميات غير موجود.");

            pool.Close();

            var tips = await SystemAccountProvisioning.EnsureAsync(
                db, companyId, SaudiChartOfAccounts.SystemCodes.TipsPayable, token);

            var accountIds = new List<long> { tips.Id };
            var entry = JournalEntry.CreateDraft(companyId, on,
                $"إكرامياتُ {pool.PoolDate:yyyy-MM-dd} — تحصيل",
                sourceDocument: $"TipPool:{pool.Id}");

            if (pool.CollectedCash > 0m)
            {
                var cash = await ResolveAsync(companyId, cashAccountCode, token);
                accountIds.Add(cash.Id);
                entry.AddLine(cash, pool.CollectedCash, 0m, "إكرامياتٌ نقدية");
            }

            if (pool.CollectedCard > 0m)
            {
                // ‏ ‏**البنكُ من مالكه لا من قيمةٍ
                // ‏افتراضيّة.** كان `= CashAtBanks` — **الأبَ `1102`** — والشاشةُ لا تمرّر حساباً،
                // ‏فتقع كلُّ إكراميةِ بطاقةٍ على الأب والكشفُ في الابن: عينُ انشقاق م2-١٥.
                var bank = bankAccountCode is null
                    ? await BankGlAccountResolver.ResolveAsync(db, companyId, ct: token)
                    : await ResolveAsync(companyId, bankAccountCode, token);
                accountIds.Add(bank.Id);
                entry.AddLine(bank, pool.CollectedCard, 0m, "إكرامياتٌ على البطاقة");
            }

            entry.AddLine(tips, 0m, pool.TotalCollected, "إكرامياتٌ مستحقّةٌ للعاملين");

            await FinancialPostingSupport.ValidateAsync(
                db, companyId, on, PostingDocumentKind.JournalEntry,
                accountIds: [.. accountIds], ct: token);

            var number = await new JournalEntryNumberGenerator(db).NextAsync(companyId, on);
            entry.Post(number);
            db.JournalEntries.Add(entry);
            await db.SaveChangesAsync(token);

            pool.LinkJournalEntry(entry.Id);
            await db.SaveChangesAsync(token);

            return new TipPoolPostingResult(pool.Id, pool.CollectedCash, pool.CollectedCard,
                pool.TotalCollected, entry.Id);
        }, ct);
    }

    /// <summary>
    /// يدفع حصصَ وعاءٍ مُقفَل — <b>وهنا يخرج المال</b>.
    /// </summary>
    /// <remarks>
    /// <b>ولا يُدفع إلا وعاءٌ أُقفل وقُيّد</b>: الدفعُ قبل القيد يُنقص <c>2116</c> قبل أن
    /// يُزاد، <b>فيصير الرصيدُ سالباً</b> — وهو ما تمنعه <c>هـ-١٩</c>.
    /// </remarks>
    public async Task<long> PayOutAsync(long companyId, long poolId, DateOnly on,
        string cashAccountCode = SaudiChartOfAccounts.SystemCodes.CashOnHand,
        CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPostAccounting();

        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var pool = await db.TipPools
                .SingleOrDefaultAsync(p => p.Id == poolId && p.CompanyId == companyId, token)
                ?? throw new AccountingException("وعاء الإكراميات غير موجود.");

            if (pool.Status != TipPoolStatus.Closed || pool.JournalEntryId is null)
                throw new AccountingException(
                    "لا يُدفع إلا وعاءٌ أُقفل وقُيّد — والدفعُ قبل القيد يُنقص الالتزامَ قبل أن يُزاد.");

            var tips = await SystemAccountProvisioning.EnsureAsync(
                db, companyId, SaudiChartOfAccounts.SystemCodes.TipsPayable, token);
            var cash = await ResolveAsync(companyId, cashAccountCode, token);

            await FinancialPostingSupport.ValidateAsync(
                db, companyId, on, PostingDocumentKind.JournalEntry,
                accountIds: [tips.Id, cash.Id], ct: token);

            var entry = JournalEntry.CreateDraft(companyId, on,
                $"إكرامياتُ {pool.PoolDate:yyyy-MM-dd} — صرفٌ للعاملين",
                sourceDocument: $"TipPayout:{pool.Id}");
            entry.AddLine(tips, pool.TotalDistributed, 0m, "صرفُ إكرامياتٍ مستحقّة");
            entry.AddLine(cash, 0m, pool.TotalDistributed, "صرفُ إكرامياتٍ نقداً");

            var number = await new JournalEntryNumberGenerator(db).NextAsync(companyId, on);
            entry.Post(number);
            db.JournalEntries.Add(entry);
            await db.SaveChangesAsync(token);

            return entry.Id;
        }, ct);
    }

    private async Task<Account> ResolveAsync(long companyId, string code, CancellationToken ct) =>
        await db.Accounts.SingleOrDefaultAsync(a => a.CompanyId == companyId && a.Code == code, ct)
        ?? throw new AccountingException($"الحساب «{code}» غير موجود.");
}
