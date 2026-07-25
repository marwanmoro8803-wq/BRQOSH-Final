using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Treasury;

/// <summary>إعادة تقييم مجمّعة لكل الحسابات البنكية بعملة أجنبية باستخدام أسعار مخزّنة.</summary>
public class FxRevaluationBatchService(
    BrqoshDbContext db,
    ExchangeRateService rateService,
    FxRevaluationService revaluationService)
{
    public async Task<IReadOnlyList<JournalEntry>> RevalueAllForeignBanksAsync(
        long companyId,
        DateOnly asOfDate,
        string username,
        IReadOnlyDictionary<long, decimal>? foreignBalancesByBankId = null,
        CancellationToken ct = default)
    {
        var company = await db.Companies.SingleAsync(c => c.Id == companyId, ct);
        var functional = company.FunctionalCurrencyCode;

        var foreignBanks = await db.BankAccounts
            .Where(b => b.CompanyId == companyId && b.IsActive)
            .ToListAsync(ct);

        var entries = new List<JournalEntry>();
        foreach (var bank in foreignBanks)
        {
            if (string.Equals(bank.CurrencyCode, functional, StringComparison.OrdinalIgnoreCase))
                continue;

            if (foreignBalancesByBankId is null || !foreignBalancesByBankId.TryGetValue(bank.Id, out var balance))
                throw new AccountingException(
                    $"رصيد العملة الأجنبية مطلوب للحساب {bank.NameAr} ({bank.CurrencyCode}).");

            var rate = await rateService.GetRateAsync(
                companyId, bank.CurrencyCode, functional, asOfDate, ct);

            var entry = await revaluationService.RevalueAsync(
                companyId, bank.Id, asOfDate, rate, username, balance, ct);

            if (entry is not null)
                entries.Add(entry);
        }

        return entries;
    }
}
