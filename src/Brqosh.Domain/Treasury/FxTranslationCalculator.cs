namespace Brqosh.Domain.Treasury;

/// <summary>حساب فروق الترجمة — صافي الدخل × (سعر الإقفال − سعر المتوسط).</summary>
public static class FxTranslationCalculator
{
    public static decimal ComputePeriodCtaAdjustment(
        decimal periodNetIncomeFunctional,
        decimal averageRate,
        decimal closingRate)
    {
        if (periodNetIncomeFunctional == 0 || averageRate == closingRate)
            return 0m;

        return Math.Round(
            periodNetIncomeFunctional * (closingRate - averageRate),
            2,
            MidpointRounding.AwayFromZero);
    }
}
