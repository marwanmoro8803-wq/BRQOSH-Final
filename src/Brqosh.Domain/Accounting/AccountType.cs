namespace Brqosh.Domain.Accounting;

/// <summary>
/// التصنيف المحاسبي الرئيسي للحساب — يحدد الطبيعة المدينة/الدائنة وموقعه في القوائم المالية.
/// </summary>
public enum AccountType
{
    /// <summary>الأصول — طبيعة مدينة، تظهر في قائمة المركز المالي.</summary>
    Asset = 1,

    /// <summary>الالتزامات (الخصوم) — طبيعة دائنة، تظهر في قائمة المركز المالي.</summary>
    Liability = 2,

    /// <summary>حقوق الملكية — طبيعة دائنة، تظهر في قائمة المركز المالي.</summary>
    Equity = 3,

    /// <summary>الإيرادات — طبيعة دائنة، تظهر في قائمة الدخل.</summary>
    Revenue = 4,

    /// <summary>المصروفات — طبيعة مدينة، تظهر في قائمة الدخل.</summary>
    Expense = 5,
}

public static class AccountTypeExtensions
{
    /// <summary>
    /// الطبيعة الافتراضية للحساب: مدين (true) أو دائن (false).
    /// الأصول والمصروفات مدينة؛ الالتزامات وحقوق الملكية والإيرادات دائنة.
    /// </summary>
    public static bool IsDebitNature(this AccountType type) =>
        type is AccountType.Asset or AccountType.Expense;

    /// <summary>هل يظهر الحساب في قائمة المركز المالي (الميزانية)؟</summary>
    public static bool IsBalanceSheet(this AccountType type) =>
        type is AccountType.Asset or AccountType.Liability or AccountType.Equity;

    /// <summary>هل يظهر الحساب في قائمة الدخل (يُقفل في نهاية الفترة)؟</summary>
    public static bool IsIncomeStatement(this AccountType type) =>
        type is AccountType.Revenue or AccountType.Expense;

    public static string ArabicName(this AccountType type) => type switch
    {
        AccountType.Asset => "الأصول",
        AccountType.Liability => "الالتزامات",
        AccountType.Equity => "حقوق الملكية",
        AccountType.Revenue => "الإيرادات",
        AccountType.Expense => "المصروفات",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };
}
