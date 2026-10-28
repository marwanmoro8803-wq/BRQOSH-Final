using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Accounting;

/// <summary>
/// فرقُ ترجمة الفترة لشركةٍ تابعةٍ بعملةٍ غير
/// عملة عرض المجموعة — <b>محسوباً للاطّلاع، لا مُرحَّلاً.</b>
/// </summary>
public sealed record FxClosingTranslationFigure(
    string FunctionalCurrency,
    string ReportingCurrency,
    decimal NetIncome,
    decimal AverageRate,
    decimal ClosingRate,
    decimal Cta);

/// <summary>
/// ترجمة إقفال الفترة — <b>فرقُ الترجمة على صافي الدخل</b> بين سعر المتوسّط وسعر الإقفال.
///
/// <para> <b>وكان يُرحَّل قيداً في دفاتر الشركة
/// التابعة</b>: مدين 4107 «أرباح فروق عملة» / دائن 3106 — <b>مبلغاً بعملة العرض في دفاترٍ بعملتها
/// الوظيفيّة</b>. <b>ومعيارُ المحاسبة الدوليّ 21 يجعل فروقَ الترجمة في القوائم المُترجَمة وحدَها</b>
/// (الدخل الشامل الآخر)، <b>لا ربحاً ولا خسارةً في دفاتر المنشأة أبداً</b>. وقِيس: دخلُ تابعةٍ
/// بالدولار صار ٩٦٫٦٧ بدل ١٠٠ — <b>٣٫٣٣ ريالاً في دفترٍ بالدولار، وبالإشارة المقلوبة</b> (ربحُ
/// الترجمة يُدين حسابَ الأرباح) — <b>والموحَّدُ طبّقها مرّتين</b>: موازِناً في المركز، وبنداً مزيّفاً
/// في قائمة الدخل.</para>
///
/// <para><b>فصارت تُحسب وتُعاد ولا تمسّ الدفاتر</b>، والمركزُ الموحَّدُ يشتقّ احتياطيَ الترجمة
/// بنفسه (ح-٤٥). <b>وحارسُ الدور باقٍ كما كان</b> — المسارُ في الواجهة البرمجيّة مسارُ كتابةٍ
/// تاريخيّاً، <b>وتخفيفُ صلاحيّته قرارٌ غيرُ هذا</b>.</para>
///
/// <para><b>والباقي المعلوم:</b> قاعدةٌ رُحّلت فيها قيودٌ بالخدمة القديمة (<c>FxClosingTranslation:*</c>)
/// تبقى فيها — والواجهةُ البرمجيّة لا تُشحن في المُنصِّب (OPEN_ITEMS ت-١٠)، فلا يُتوقَّع وجودُها.</para>
/// </summary>
public class FxClosingTranslationService(BrqoshDbContext db, ExchangeRateService rateService)
{
    public async Task<FxClosingTranslationFigure?> TranslatePeriodAsync(
        long companyId,
        DateOnly periodStart,
        DateOnly periodEnd,
        string username,
        CancellationToken ct = default)
    {
        Brqosh.Infrastructure.Licensing.LicenseGate.Ensure(Brqosh.Application.Licensing.LicensedCapability.MultiCurrency);
        SecurityContext.EnsureCanPostAccounting();
        _ = SecurityContext.ResolveUsername(username);

        var company = await db.Companies.SingleAsync(c => c.Id == companyId, ct);
        if (company.CompanyGroupId is null)
            return null;

        var group = await db.CompanyGroups.SingleAsync(g => g.Id == company.CompanyGroupId, ct);
        var reporting = group.ReportingCurrencyCode;

        if (string.Equals(company.FunctionalCurrencyCode, reporting, StringComparison.OrdinalIgnoreCase))
            return null;

        var pnl = await new IncomeStatementQuery(db).ExecuteAsync(companyId, periodStart, periodEnd);
        var avgRate = await rateService.GetAverageRateAsync(
            companyId, company.FunctionalCurrencyCode, reporting, periodStart, periodEnd, ct);
        var closingRate = await rateService.GetRateAsync(
            companyId, company.FunctionalCurrencyCode, reporting, periodEnd, ct);

        var cta = FxTranslationCalculator.ComputePeriodCtaAdjustment(pnl.NetIncome, avgRate, closingRate);
        return new FxClosingTranslationFigure(
            company.FunctionalCurrencyCode, reporting, pnl.NetIncome, avgRate, closingRate, cta);
    }
}
