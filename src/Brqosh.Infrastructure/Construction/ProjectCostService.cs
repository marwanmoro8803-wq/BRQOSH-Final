using Brqosh.Domain.Accounting;
using Brqosh.Domain.Construction;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Construction;

/// <summary>تسجيل تكلفة مشروع مع قيد اختياري (مدين 5105 / دائن صندوق) وتتبع IFRS 15.</summary>
public class ProjectCostService(BrqoshDbContext db)
{
    public async Task<ProjectCostEntry> RecordAsync(
        long companyId, long projectId, DateOnly costDate, string description, decimal amount,
        string username, bool postJournal = true, string sourceDocument = "",
        CancellationToken ct = default, string? creditAccountCode = null)
    {
        SecurityContext.EnsureCanPostAccounting();
        username = SecurityContext.ResolveUsername(username);

        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            _ = await db.ConstructionProjects.SingleAsync(
                p => p.Id == projectId && p.CompanyId == companyId, token);

            var entry = ProjectCostEntry.Create(companyId, projectId, costDate, description, amount, sourceDocument);
            db.ProjectCostEntries.Add(entry);
            await db.SaveChangesAsync(token);

            if (postJournal)
            {
                await new JournalPostingService(db).EnsurePeriodOpenAsync(companyId, costDate);
                var expense = await db.Accounts.SingleAsync(
                    a => a.CompanyId == companyId && a.Code == "5105", token);
                var cash = await CreditAccountAsync(companyId, creditAccountCode, token);

                var je = JournalEntry.CreateDraft(companyId, costDate,
                    $"تكلفة مشروع — {description}",
                    string.IsNullOrWhiteSpace(sourceDocument) ? $"ProjectCost:{entry.Id}" : sourceDocument);
                je.AddLine(expense, amount, 0m, description);
                je.AddLine(cash, 0m, amount, cash.Type == AccountType.Liability ? "تكلفة مشروع مستحقة" : "صرف تكلفة مشروع");
                var num = await new JournalEntryNumberGenerator(db).NextAsync(companyId, costDate, token);
                je.Post(num);
                db.JournalEntries.Add(je);
                await db.SaveChangesAsync(token);
                entry.LinkJournalEntry(je.Id);
                await db.SaveChangesAsync(token);
            }

            // ‏(جاهزيّةُ الإطلاق · ح-٨٠) ‏**والتكلفةُ تُغيّر نسبةَ الإنجاز فيُسوّى إيرادُ العقد** — قُيّدت هنا أو جاءت من مستندٍ آخر.
            await new ContractRevenueTrueUp(db).ApplyAsync(companyId, projectId, costDate, token);

            await new AuditService(db).LogAsync(companyId, username, "تسجيل تكلفة مشروع",
                "ConstructionProject", projectId.ToString(), $"{amount:N2} — {description}");

            return entry;
        }, ct);
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-١٤٥ · <c>sector-modules#9</c>) <b>الحسابُ الدائنُ يُختار — لا الصندوقُ دائماً.</b>
    /// </summary>
    /// <remarks>
    /// كان الدائنُ «الصندوق» في كلّ تكلفةٍ يدويّة: تكلفةٌ دُفعت من البنك أو استُحقّت ولم تُدفع تُخرج
    /// الصندوقَ سالباً وتترك البنكَ والمستحقّاتِ كما هي. <b>والمقبولُ نقديّةٌ أو بنكٌ (أصل) أو مستحقٌّ
    /// (التزام) قابلٌ للترحيل</b>؛ <b>والموردون والعملاءُ والمخزونُ مرفوضةٌ</b> — أرصدتُها من دفاترها المساعدة،
    /// وتكلفةُ المورّد طريقُها فاتورةُ مشترياتٍ مربوطةٌ بالمشروع (ح-١٤٤) لا قيدٌ على الذمّة بلا مستند.
    /// </remarks>
    public async Task<Account> CreditAccountAsync(long companyId, string? code, CancellationToken ct = default)
    {
        var wanted = string.IsNullOrWhiteSpace(code) ? SaudiChartOfAccounts.SystemCodes.CashOnHand : code.Trim();
        var account = await db.Accounts.SingleOrDefaultAsync(a => a.CompanyId == companyId && a.Code == wanted, ct)
            ?? throw new AccountingException($"الحساب الدائن {wanted} غير موجود.");

        if (!IsCreditable(account))
            throw new AccountingException(
                $"الحساب {account.Code} — {account.NameAr} لا يُقيَّد عليه مقابلُ تكلفة مشروع: "
                + (IsSubsidiaryControl(account.Code)
                    ? "رصيدُه من دفترٍ مساعد؛ وتكلفةُ المورّد بفاتورة مشترياتٍ مربوطةٍ بالمشروع."
                    : "المقبولُ صندوقٌ أو بنكٌ أو مصروفٌ مستحقٌّ قابلٌ للترحيل."));

        return account;
    }

    /// <summary>الحساباتُ التي تُعرض في حوار التكلفة — ما يقبله <see cref="CreditAccountAsync"/> وحدَه.</summary>
    public async Task<IReadOnlyList<Account>> CreditableAccountsAsync(long companyId, CancellationToken ct = default) =>
        (await db.Accounts.AsNoTracking()
            .Where(a => a.CompanyId == companyId && (a.Type == AccountType.Asset || a.Type == AccountType.Liability))
            .OrderBy(a => a.Code)
            .ToListAsync(ct))
        .Where(IsCreditable)
        .ToList();

    private static bool IsCreditable(Account account) =>
        account.IsPostable
        && account.Type is AccountType.Asset or AccountType.Liability
        && !IsSubsidiaryControl(account.Code);

    private static bool IsSubsidiaryControl(string code) =>
        code is SaudiChartOfAccounts.SystemCodes.AccountsReceivable
            or SaudiChartOfAccounts.SystemCodes.AccountsPayable
            or SaudiChartOfAccounts.SystemCodes.Inventory;

    public async Task<decimal> IncurredCostAsync(long companyId, long projectId, CancellationToken ct = default) =>
        await db.ProjectCostEntries
            .Where(c => c.CompanyId == companyId && c.ProjectId == projectId && !c.IsReversed)
            .SumAsync(c => (decimal?)c.Amount, ct) ?? 0m;
}
