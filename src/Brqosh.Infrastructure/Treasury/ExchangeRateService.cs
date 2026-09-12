using Brqosh.Application.Treasury;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Treasury;

public class ExchangeRateService(BrqoshDbContext db) : IExchangeRateService
{
    public async Task<decimal> GetRateAsync(
        long companyId,
        string fromCurrency,
        string toCurrency,
        DateOnly asOfDate,
        CancellationToken ct = default)
    {
        var from = ExchangeRate.NormalizeCurrency(fromCurrency);
        var to = ExchangeRate.NormalizeCurrency(toCurrency);

        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
            return 1m;

        var direct = await db.ExchangeRates
            .Where(r => r.CompanyId == companyId
                && r.FromCurrencyCode == from
                && r.ToCurrencyCode == to
                && r.EffectiveDate <= asOfDate)
            .OrderByDescending(r => r.EffectiveDate)
            .ThenByDescending(r => r.Id)
            .Select(r => (decimal?)r.Rate)
            .FirstOrDefaultAsync(ct);

        if (direct is not null)
            return direct.Value;

        var inverse = await db.ExchangeRates
            .Where(r => r.CompanyId == companyId
                && r.FromCurrencyCode == to
                && r.ToCurrencyCode == from
                && r.EffectiveDate <= asOfDate)
            .OrderByDescending(r => r.EffectiveDate)
            .ThenByDescending(r => r.Id)
            .Select(r => (decimal?)r.Rate)
            .FirstOrDefaultAsync(ct);

        if (inverse is not null && inverse.Value > 0)
            return Math.Round(1m / inverse.Value, 6, MidpointRounding.AwayFromZero);

        throw new AccountingException(
            $"لا يوجد سعر صرف مخزّن للشركة {companyId} ({from} → {to}) بتاريخ {asOfDate:yyyy-MM-dd}.");
    }

    public async Task<decimal> GetAverageRateAsync(
        long companyId,
        string fromCurrency,
        string toCurrency,
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken ct = default)
    {
        var from = ExchangeRate.NormalizeCurrency(fromCurrency);
        var to = ExchangeRate.NormalizeCurrency(toCurrency);

        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
            return 1m;

        if (periodEnd < periodStart)
            throw new AccountingException("نهاية الفترة يجب أن تكون بعد بدايتها.");

        var rates = await db.ExchangeRates
            .Where(r => r.CompanyId == companyId
                && r.FromCurrencyCode == from
                && r.ToCurrencyCode == to
                && r.EffectiveDate >= periodStart
                && r.EffectiveDate <= periodEnd)
            .Select(r => r.Rate)
            .ToListAsync(ct);

        if (rates.Count > 0)
            return Math.Round(rates.Average(), 6, MidpointRounding.AwayFromZero);

        var opening = await TryGetRateAsync(companyId, from, to, periodStart, ct);
        var closing = await TryGetRateAsync(companyId, from, to, periodEnd, ct);
        if (opening is not null && closing is not null)
            return Math.Round((opening.Value + closing.Value) / 2m, 6, MidpointRounding.AwayFromZero);

        if (closing is not null)
            return closing.Value;

        throw new AccountingException(
            $"لا يوجد سعر صرف متوسط للشركة {companyId} ({from} → {to}) للفترة {periodStart:yyyy-MM-dd}–{periodEnd:yyyy-MM-dd}.");
    }

    private async Task<decimal?> TryGetRateAsync(
        long companyId, string from, string to, DateOnly asOfDate, CancellationToken ct)
    {
        try
        {
            return await GetRateAsync(companyId, from, to, asOfDate, ct);
        }
        catch (AccountingException)
        {
            return null;
        }
    }

    public async Task RecordRateAsync(
        long companyId,
        string fromCurrency,
        string toCurrency,
        DateOnly effectiveDate,
        decimal rate,
        string? source = null,
        CancellationToken ct = default)
    {
        // ‏(م-٤ · ن-١٦) ‏**طبقةٌ ثانيةٌ في الخدمة — والأولى في المسار.**
        //
        // ‏الواجهةُ ترفض بـ403 قبل بلوغ هذا السطر، **وهذا يُغطّي ما لا مسارَ له**: سطحُ
        // ‏المكتب يستدعي هذه الدالّةَ عبر `FxRateSyncService`، ومن يُضيف مستدعياً غداً
        // ‏لا يمرّ بحارس الواجهة. **وعرفُ المستودع أن تفحص الخدمةُ بنفسها** — يُطبَّق في
        // ‏`FxClosingTranslationService:25` — وكان غائباً هنا كلّيّاً.
        SecurityContext.EnsureCanPostAccounting();

        var entity = ExchangeRate.Create(companyId, fromCurrency, toCurrency, effectiveDate, rate, source);
        db.ExchangeRates.Add(entity);
        await db.SaveChangesAsync(ct);
    }
}
