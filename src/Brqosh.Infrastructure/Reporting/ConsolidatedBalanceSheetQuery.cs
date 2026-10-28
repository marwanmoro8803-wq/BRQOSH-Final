using Brqosh.Domain.Accounting;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Reporting;

public record ConsolidatedBalanceSheetRow(
    string AccountCode,
    string AccountNameAr,
    AccountType Type,
    decimal Amount,
    int CompanyCount);

public record ConsolidatedBalanceSheetResult(
    long GroupId,
    string ReportingCurrencyCode,
    DateOnly AsOfDate,
    IReadOnlyList<ConsolidatedBalanceSheetRow> Assets,
    IReadOnlyList<ConsolidatedBalanceSheetRow> Liabilities,
    IReadOnlyList<ConsolidatedBalanceSheetRow> Equity)
{
    public decimal TotalAssets => Assets.Sum(a => a.Amount);
    public decimal TotalLiabilities => Liabilities.Sum(l => l.Amount);
    public decimal TotalEquity => Equity.Sum(e => e.Amount);
    public decimal TotalLiabilitiesAndEquity => TotalLiabilities + TotalEquity;
    public bool IsBalanced => TotalAssets == TotalLiabilitiesAndEquity;
}

/// <summary>قائمة مركز مالي موحّدة — الأرصدة بسعر إقفال.</summary>
public class ConsolidatedBalanceSheetQuery(BrqoshDbContext db, ExchangeRateService rateService)
{
    public async Task<ConsolidatedBalanceSheetResult> ExecuteAsync(
        long groupId,
        DateOnly asOfDate,
        IReadOnlyDictionary<long, decimal>? fxRatesFromFunctionalToReporting = null,
        CancellationToken ct = default)
    {
        Brqosh.Infrastructure.Licensing.LicenseGate.Ensure(Brqosh.Application.Licensing.LicensedCapability.Consolidation);
        var group = await db.CompanyGroups.SingleOrDefaultAsync(g => g.Id == groupId, ct)
            ?? throw new AccountingException("مجموعة الشركات غير موجودة.");

        var companies = await db.Companies.Where(c => c.CompanyGroupId == groupId).ToListAsync(ct);
        var assets = new Dictionary<string, (string NameAr, decimal Amount, HashSet<long> Companies)>(StringComparer.OrdinalIgnoreCase);
        var liabilities = new Dictionary<string, (string NameAr, decimal Amount, HashSet<long> Companies)>(StringComparer.OrdinalIgnoreCase);
        var equity = new Dictionary<string, (string NameAr, decimal Amount, HashSet<long> Companies)>(StringComparer.OrdinalIgnoreCase);

        decimal consolidatedUnclosedEarnings = 0m;
        // ‏(ح-٤٥) هل تُرجِم شيءٌ أصلاً؟ — فالفرقُ لا يكون «احتياطيَ ترجمة» إلّا حيث وقعت ترجمة.
        var anyTranslation = false;

        foreach (var company in companies)
        {
            var closingRate = await ConsolidatedFxRateResolver.ResolveAsync(
                rateService, company, group.ReportingCurrencyCode, asOfDate, null,
                FxRateKind.Closing, fxRatesFromFunctionalToReporting, ct);
            anyTranslation |= closingRate != 1m;

            var bs = await new BalanceSheetQuery(db).ExecuteAsync(company.Id, asOfDate);

            foreach (var row in bs.Assets)
                Accumulate(assets, row, closingRate, company.Id);
            foreach (var row in bs.Liabilities)
                Accumulate(liabilities, row, closingRate, company.Id);
            foreach (var row in bs.Equity.Where(e => e.AccountCode != "3999"))
                Accumulate(equity, row, closingRate, company.Id);

            var (earnings, translated) = await UnclosedEarningsAsync(
                company, group.ReportingCurrencyCode, asOfDate, fxRatesFromFunctionalToReporting, ct);
            consolidatedUnclosedEarnings += earnings;
            anyTranslation |= translated;
        }

        var eliminations = await IntercompanyEliminationHelper.ComputeByAccountAsync(
            db, rateService, group, companies, new DateOnly(1900, 1, 1), asOfDate, FxRateKind.Closing,
            fxRatesFromFunctionalToReporting, ct);
        ApplyBalanceSheetEliminations(assets, eliminations, AccountType.Asset);
        ApplyBalanceSheetEliminations(liabilities, eliminations, AccountType.Liability);
        ApplyBalanceSheetEliminations(equity, eliminations, AccountType.Equity);

        if (consolidatedUnclosedEarnings != 0)
        {
            if (!equity.TryGetValue("3999", out var cy))
                cy = ("أرباح غير مقفلة (موحّدة)", 0m, []);
            cy.Companies.UnionWith(companies.Select(c => c.Id));
            equity["3999"] = (cy.NameAr, cy.Amount + consolidatedUnclosedEarnings, cy.Companies);
        }

        var assetTotal = assets.Values.Sum(v => v.Amount);
        var liabilityTotal = liabilities.Values.Sum(v => v.Amount);
        var equityTotal = equity.Values.Sum(v => v.Amount);
        var ctaPlug = assetTotal - liabilityTotal - equityTotal;

        // ‏**والفرقُ «احتياطيُ ترجمة» حيث وقعت ترجمةٌ فقط.**
        //
        // ‏بطريقة السعر الجاري فرقُ الترجمة هو الموازنُ بتعريفه — الأصولُ بسعر الإقفال والأرباحُ
        // ‏بالمتوسّط — **فيبقى حيث تُرجِمت شركةٌ واحدةٌ على الأقلّ**. **أمّا مجموعةٌ لا ترجمةَ فيها
        // ‏فكلُّ فرقٍ خطأُ توحيدٍ لا ترجمة** — قيدٌ بين شركتين سجّلته إحداهما، مثلاً — **فلا يُدفن
        // ‏في 3106 ليقول التقريرُ «متوازن»**، بل يبقى ظاهراً: «✗ غير متوازن» على الشاشة.
        //
        // ‏**والحدُّ المعلَن:** في مجموعةٍ مُترجَمةٍ يبقى الخطأُ الحقيقيُّ داخل 3106 مع الترجمة —
        // ‏فصلُهما يحتاج حقوقَ الملكية بأسعارها التاريخيّة، **وهي تُترجَم هنا بسعر الإقفال**.
        if (ctaPlug != 0 && anyTranslation)
        {
            if (!equity.TryGetValue(SaudiChartOfAccounts.SystemCodes.TranslationReserve, out var cta))
                cta = ("احتياطي ترجمة العملات (موحّد)", 0m, []);
            cta.Companies.UnionWith(companies.Select(c => c.Id));
            equity[SaudiChartOfAccounts.SystemCodes.TranslationReserve] =
                (cta.NameAr, cta.Amount + ctaPlug, cta.Companies);
        }

        return new ConsolidatedBalanceSheetResult(
            groupId,
            group.ReportingCurrencyCode,
            asOfDate,
            ToRows(assets, AccountType.Asset),
            ToRows(liabilities, AccountType.Liability),
            ToRows(equity, AccountType.Equity));
    }

    /// <summary>
    /// <b>أرباحُ الشركة غير المقفلة منذ التأسيس —
    /// كلُّ سنةٍ بمتوسّط سنتِها.</b>
    ///
    /// <para><b>وكان الموحَّدُ يأخذ السنةَ التقويميّةَ الجاريةَ وحدَها</b> ويُسقط 3999 المفرد
    /// (التراكميَّ منذ التأسيس)، <b>فيُرحَّل ربحُ كلّ سنةٍ سابقةٍ لم تُقفل إلى «احتياطي ترجمة»</b>
    /// — ولو كانت المجموعةُ كلُّها بالريال. <b>وهو عينُ ما أصلحه CEEF-201 (ع-R1) في المركز
    /// المفرد</b> ولم يبلغ الموحَّد.</para>
    ///
    /// <para><b>و<c>excludeYearEndClosing: false</c> كما في المركز المفرد حرفاً</b>: قيدُ الإقفال
    /// يُصفّر السنةَ المقفلة فتساهم بصفر، <b>وأرباحُها في حساب حقوق الملكية المجموع أعلاه</b> —
    /// فلا ازدواج. <b>وكان الافتراضيُّ يستثني قيدَ الإقفال</b>، فسنةٌ جاريةٌ أُقفلت في آخر يومها
    /// تُحسب مرّتين: في 3999 وفي الحساب الذي نُقلت إليه.</para>
    /// </summary>
    private async Task<(decimal Earnings, bool Translated)> UnclosedEarningsAsync(
        Brqosh.Domain.Companies.Company company, string reportingCurrency, DateOnly asOfDate,
        IReadOnlyDictionary<long, decimal>? manualRates, CancellationToken ct)
    {
        var first = await db.JournalEntries
            .Where(e => e.CompanyId == company.Id && e.EntryDate <= asOfDate)
            .MinAsync(e => (DateOnly?)e.EntryDate, ct);
        if (first is null)
            return (0m, false);

        var total = 0m;
        var translated = false;
        for (var year = first.Value.Year; year <= asOfDate.Year; year++)
        {
            var from = new DateOnly(year, 1, 1);
            var to = year == asOfDate.Year ? asOfDate : new DateOnly(year, 12, 31);
            var pnl = await new IncomeStatementQuery(db)
                .ExecuteAsync(company.Id, from, to, excludeYearEndClosing: false);
            if (pnl.NetIncome == 0m)
                continue;

            var average = await ConsolidatedFxRateResolver.ResolveAsync(
                rateService, company, reportingCurrency, to, from,
                FxRateKind.Average, manualRates, ct);
            translated |= average != 1m;
            total += FxConversion.Convert(pnl.NetIncome, average);
        }

        return (total, translated);
    }

    private static void ApplyBalanceSheetEliminations(
        Dictionary<string, (string NameAr, decimal Amount, HashSet<long> Companies)> bucket,
        IReadOnlyDictionary<string, (decimal Debit, decimal Credit)> eliminations,
        AccountType type)
    {
        foreach (var (code, (elimDebit, elimCredit)) in eliminations)
        {
            if (!bucket.TryGetValue(code, out var row))
                continue;

            var elimAmount = type switch
            {
                AccountType.Asset => elimDebit - elimCredit,
                AccountType.Liability or AccountType.Equity => elimCredit - elimDebit,
                _ => 0m,
            };
            bucket[code] = (row.NameAr, row.Amount - elimAmount, row.Companies);
        }
    }

    private static void Accumulate(
        Dictionary<string, (string NameAr, decimal Amount, HashSet<long> Companies)> bucket,
        BalanceSheetRow row,
        decimal rate,
        long companyId)
    {
        var amount = FxConversion.Convert(row.Amount, rate);
        if (!bucket.TryGetValue(row.AccountCode, out var existing))
            existing = (row.AccountNameAr, 0m, []);
        existing.Companies.Add(companyId);
        bucket[row.AccountCode] = (existing.NameAr, existing.Amount + amount, existing.Companies);
    }

    private static IReadOnlyList<ConsolidatedBalanceSheetRow> ToRows(
        Dictionary<string, (string NameAr, decimal Amount, HashSet<long> Companies)> bucket,
        AccountType type) =>
        bucket
            .Where(kv => kv.Value.Amount != 0)
            .Select(kv => new ConsolidatedBalanceSheetRow(
                kv.Key, kv.Value.NameAr, type, kv.Value.Amount, kv.Value.Companies.Count))
            .OrderBy(r => r.AccountCode)
            .ToList();
}
