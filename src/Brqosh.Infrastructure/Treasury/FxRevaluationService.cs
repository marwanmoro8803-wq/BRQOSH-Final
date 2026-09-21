using Brqosh.Domain.Accounting;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Treasury;

/// <summary>
/// إعادة تقييم رصيد حساب بنكي بعملة أجنبية إلى العملة الوظيفية.
/// foreignAmount غير مخزّن — يُمرَّر foreignBalance صراحةً عند التقييم.
/// </summary>
public class FxRevaluationService(BrqoshDbContext db)
{
    /// <summary>
    /// يعيد قيد التقييم أو null إذا لم يكن هناك فرق (gainOrLoss == 0).
    /// newRate = وحدات العملة الوظيفية لكل 1 وحدة أجنبية.
    /// </summary>
    public Task<JournalEntry?> RevalueAsync(
        long companyId,
        long bankAccountId,
        DateOnly asOfDate,
        decimal newRate,
        string username,
        decimal? foreignBalance = null,
        CancellationToken ct = default) =>
        UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            SecurityContext.EnsureCanPostAccounting();

            if (newRate <= 0)
                throw new AccountingException("سعر الصرف يجب أن يكون موجباً.");
            if (foreignBalance is null or <= 0)
                throw new AccountingException("رصيد العملة الأجنبية مطلوب لإعادة التقييم.");

            var bank = await db.BankAccounts.SingleAsync(
                b => b.CompanyId == companyId && b.Id == bankAccountId, token);

            await EnsureFxAccountsAsync(companyId, token);

            var bookedFunctional = await GetBookedFunctionalBalanceAsync(
                db, companyId, bank.GlAccountId, asOfDate, token);

            var functionalAtNewRate = FxConversion.Convert(foreignBalance.Value, newRate);
            var gainOrLoss = FxConversion.GainOrLoss(functionalAtNewRate, bookedFunctional);

            if (gainOrLoss == 0)
                return null;

            var bankGl = await db.Accounts.SingleAsync(a => a.Id == bank.GlAccountId, token);
            var gainAccount = await db.Accounts.SingleAsync(a =>
                a.CompanyId == companyId && a.Code == SaudiChartOfAccounts.SystemCodes.UnrealizedFxGain, token);
            var lossAccount = await db.Accounts.SingleAsync(a =>
                a.CompanyId == companyId && a.Code == SaudiChartOfAccounts.SystemCodes.UnrealizedFxLoss, token);

            var amount = Math.Abs(gainOrLoss);
            var description = $"إعادة تقييم عملة — {bank.NameAr} ({bank.CurrencyCode})";
            var sourceDoc = $"FxRevaluation:{bankAccountId}:{asOfDate:yyyy-MM-dd}";

            var entry = JournalEntry.CreateDraft(companyId, asOfDate, description, sourceDoc);

            if (gainOrLoss > 0)
            {
                entry.AddLine(bankGl, amount, 0m, "تعديل رصيد بنك");
                entry.AddLine(gainAccount, 0m, amount, "أرباح فروق عملة غير محققة");
            }
            else
            {
                entry.AddLine(lossAccount, amount, 0m, "خسائر فروق عملة غير محققة");
                entry.AddLine(bankGl, 0m, amount, "تعديل رصيد بنك");
            }

            return await new JournalPostingService(db).PostAsync(entry);
        }, ct);

    public Task<JournalEntry?> RevalueWithStoredRateAsync(
        long companyId,
        long bankAccountId,
        DateOnly asOfDate,
        string username,
        decimal? foreignBalance = null,
        CancellationToken ct = default) =>
        UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var bank = await db.BankAccounts.SingleAsync(
                b => b.CompanyId == companyId && b.Id == bankAccountId, token);
            var company = await db.Companies.SingleAsync(c => c.Id == companyId, token);
            var rate = await new ExchangeRateService(db).GetRateAsync(
                companyId, bank.CurrencyCode, company.FunctionalCurrencyCode, asOfDate, token);
            return await RevalueAsync(
                companyId, bankAccountId, asOfDate, rate, username, foreignBalance, token);
        }, ct);

    private static async Task<decimal> GetBookedFunctionalBalanceAsync(
        BrqoshDbContext db, long companyId, long glAccountId, DateOnly asOfDate, CancellationToken ct)
    {
        // (جاهزيّةُ الإطلاق · ح-٢٥) ‏**القيدُ المعكوسُ يبقى في الرصيد مع عكسه — أو يخرجان معاً.**
        //
        // ‏كان المُرشِّحُ `Status == Posted` وحدَه. و`CreateReversal` **يضع الأصلَ في حالة
        // ‏`Reversed`** ويُرحّل العكسيَّ `Posted`. فالأصلُ **يخرج** من الجمع **وعكسُه يبقى**:
        // ‏**الرصيدُ الدفتريُّ يصير مقلوبَ القيد الملغى**، لا صفراً ولا صحيحاً.
        //
        // ‏**والفرقُ يُقيَّد ربحاً أو خسارةَ صرفٍ غيرَ محقَّقة** — أي **مبلغٌ في قائمة الدخل
        // ‏عن واقعةٍ لم تقع**، ويتكرّر مع كلّ إعادة تقييمٍ بعد أيّ عكسٍ يمسّ بنكاً أجنبيّاً.
        //
        // ‏**والعلاجُ موجودٌ في المستودع لهذا السبب بعينه**: `WhereReportable` تضمّ
        // ‏`Posted` و`Reversed` معاً — وتعليقُها يقول: «استبعادُ المعكوسة كان يشوّه
        // ‏التقاريرَ التاريخية بعد الإلغاء لاحقاً (ACC-001)». **وثمانيةُ قُرّاءٍ يستعملونها،
        // ‏وهذا لم يكن منهم.**
        var entries = await db.JournalEntries
            .WhereReportable(companyId, to: asOfDate)
            .ToListAsync(ct);

        decimal balance = 0m;
        foreach (var entry in entries)
        {
            foreach (var line in entry.Lines.Where(l => l.AccountId == glAccountId))
                balance += line.Debit - line.Credit;
        }

        return balance;
    }

    internal async Task EnsureFxAccountsAsync(long companyId, CancellationToken ct = default)
    {
        await EnsureSystemAccountAsync(companyId,
            SaudiChartOfAccounts.SystemCodes.UnrealizedFxGain,
            "أرباح فروق عملة غير محققة", "Unrealized FX Gain",
            AccountType.Revenue, "4", ct);

        await EnsureSystemAccountAsync(companyId,
            SaudiChartOfAccounts.SystemCodes.UnrealizedFxLoss,
            "خسائر فروق عملة غير محققة", "Unrealized FX Loss",
            AccountType.Expense, "52", ct);
    }

    private async Task EnsureSystemAccountAsync(
        long companyId, string code, string nameAr, string nameEn,
        AccountType type, string parentCode, CancellationToken ct)
    {
        if (await db.Accounts.AnyAsync(a => a.CompanyId == companyId && a.Code == code, ct))
            return;

        var parent = await db.Accounts.SingleAsync(
            a => a.CompanyId == companyId && a.Code == parentCode, ct);

        var account = Account.Create(code, nameAr, nameEn, type, parent.Id,
            isGroup: false, isSystem: true, companyId);
        db.Accounts.Add(account);
        await db.SaveChangesAsync(ct);
    }
}
