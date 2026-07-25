using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Accounting;

/// <summary>
/// ترجمة إقفال الفترة — قيد CTA (3106) بناءً على فرق سعر المتوسط والإقفال على صافي الدخل.
/// </summary>
public class FxClosingTranslationService(BrqoshDbContext db, ExchangeRateService rateService)
{
    public Task<JournalEntry?> TranslatePeriodAsync(
        long companyId,
        DateOnly periodStart,
        DateOnly periodEnd,
        string username,
        CancellationToken ct = default) =>
        UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            SecurityContext.EnsureCanPostAccounting();
            username = SecurityContext.ResolveUsername(username);

            var company = await db.Companies.SingleAsync(c => c.Id == companyId, token);
            if (company.CompanyGroupId is null)
                return null;

            var group = await db.CompanyGroups.SingleAsync(g => g.Id == company.CompanyGroupId, token);
            var reporting = group.ReportingCurrencyCode;

            if (string.Equals(company.FunctionalCurrencyCode, reporting, StringComparison.OrdinalIgnoreCase))
                return null;

            var sourceDoc = $"FxClosingTranslation:{periodEnd:yyyy-MM-dd}";
            var exists = await db.JournalEntries.AnyAsync(e =>
                e.CompanyId == companyId
                && e.SourceDocument == sourceDoc
                && e.Status != JournalEntryStatus.Draft, token);
            if (exists)
                return null;

            var pnl = await new IncomeStatementQuery(db).ExecuteAsync(companyId, periodStart, periodEnd);
            var avgRate = await rateService.GetAverageRateAsync(
                companyId, company.FunctionalCurrencyCode, reporting, periodStart, periodEnd, token);
            var closingRate = await rateService.GetRateAsync(
                companyId, company.FunctionalCurrencyCode, reporting, periodEnd, token);

            var cta = FxTranslationCalculator.ComputePeriodCtaAdjustment(pnl.NetIncome, avgRate, closingRate);
            if (cta == 0)
                return null;

            await EnsureTranslationReserveAccountAsync(companyId, token);
            await new FxRevaluationService(db).EnsureFxAccountsAsync(companyId, token);

            var ctaAccount = await db.Accounts.SingleAsync(a =>
                a.CompanyId == companyId && a.Code == SaudiChartOfAccounts.SystemCodes.TranslationReserve, token);
            var gainAccount = await db.Accounts.SingleAsync(a =>
                a.CompanyId == companyId && a.Code == SaudiChartOfAccounts.SystemCodes.UnrealizedFxGain, token);
            var lossAccount = await db.Accounts.SingleAsync(a =>
                a.CompanyId == companyId && a.Code == SaudiChartOfAccounts.SystemCodes.UnrealizedFxLoss, token);

            var amount = Math.Abs(cta);
            var entry = JournalEntry.CreateDraft(
                companyId, periodEnd,
                $"ترجمة إقفال — {company.FunctionalCurrencyCode} → {reporting}",
                sourceDoc);

            if (cta > 0)
            {
                entry.AddLine(gainAccount, amount, 0m, "فروق ترجمة إقفال");
                entry.AddLine(ctaAccount, 0m, amount, "احتياطي ترجمة العملات");
            }
            else
            {
                entry.AddLine(ctaAccount, amount, 0m, "احتياطي ترجمة العملات");
                entry.AddLine(lossAccount, 0m, amount, "فروق ترجمة إقفال");
            }

            return await new JournalPostingService(db).PostAsync(entry);
        }, ct);

    private async Task EnsureTranslationReserveAccountAsync(long companyId, CancellationToken ct)
    {
        if (await db.Accounts.AnyAsync(
                a => a.CompanyId == companyId && a.Code == SaudiChartOfAccounts.SystemCodes.TranslationReserve, ct))
            return;

        var parent = await db.Accounts.SingleAsync(
            a => a.CompanyId == companyId && a.Code == "3", ct);

        var account = Account.Create(
            SaudiChartOfAccounts.SystemCodes.TranslationReserve,
            "احتياطي ترجمة العملات",
            "Currency Translation Reserve (CTA)",
            AccountType.Equity,
            parent.Id,
            isGroup: false,
            isSystem: true,
            companyId);
        db.Accounts.Add(account);
        await db.SaveChangesAsync(ct);
    }
}
