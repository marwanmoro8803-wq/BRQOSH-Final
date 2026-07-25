using Brqosh.Domain.Accounting;

namespace Brqosh.Domain.Treasury;

/// <summary>
/// تحويل عملة بسيط (سعر صرف يدوي) — أساس multi-currency.
/// إعادة التقييم الدورية والتراجم تُضاف لاحقاً.
/// </summary>
public static class FxConversion
{
    public static decimal Convert(decimal amount, decimal rateFromFunctional, int decimals = 2)
    {
        if (rateFromFunctional <= 0)
            throw new AccountingException("سعر الصرف يجب أن يكون موجباً.");
        return Math.Round(amount * rateFromFunctional, decimals, MidpointRounding.AwayFromZero);
    }

    public static decimal GainOrLoss(decimal functionalAmount, decimal bookedFunctionalAmount) =>
        Math.Round(functionalAmount - bookedFunctionalAmount, 2, MidpointRounding.AwayFromZero);
}
