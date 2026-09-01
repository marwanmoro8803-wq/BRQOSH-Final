using Brqosh.Domain.Accounting;

namespace Brqosh.Domain.Payroll;

/// <summary>سببُ انتهاء الخدمة — <b>وهو ما يُحدّد النسبةَ المستحقّة، لا المدّةُ وحدها</b>.</summary>
public enum EndOfServiceReason
{
    /// <summary>إنهاءٌ من صاحب العمل أو انتهاءُ مدّة العقد — المستحقُّ كاملٌ.</summary>
    EmployerTermination = 0,

    /// <summary>استقالةٌ — وللمدّة سُلَّمٌ خاصّ (نظام العمل، المادة 85).</summary>
    Resignation = 1,
}

/// <summary>
/// (CEEF-230 · ط2-٣) <b>مكافأةُ نهاية الخدمة — نظامُ العمل السعوديّ، المادتان 84 و85.</b>
///
/// <para><b>ما كان:</b> حسابُ المخصّص <c>2202</c> موجودٌ في شجرة الحسابات <b>ولا تكتب فيه
/// شيفرةٌ قطّ</b>. فالمنشأةُ تحمل التزاماً نظامياً يتراكم كلَّ شهر <b>ولا يظهر في ميزانيّتها
/// بريالٍ واحد</b> — وأوّلُ استحقاقٍ يُفاجئها بمبلغٍ لم تُخصّص له.</para>
///
/// <para><b>والقاعدة (المادة 84):</b> نصفُ أجرِ شهرٍ عن كلٍّ من السنوات الخمس الأولى،
/// وأجرُ شهرٍ كاملٍ عن كلّ سنةٍ بعدها. <b>والكسورُ بنسبتها</b> — فلا تُهدَر أشهرُ السنة
/// الأخيرة.</para>
///
/// <para><b>وسُلَّمُ الاستقالة (المادة 85):</b> دون سنتين لا شيء · ومن سنتين إلى خمسٍ
/// الثلث · ومن خمسٍ إلى عشرٍ الثلثان · وعشرٌ فأكثر المستحقُّ كاملاً. <b>وحسابٌ يتجاهل
/// السببَ يُخصّص أضعافَ الواجب لمنشأةٍ موظّفوها يستقيلون</b>.</para>
/// </summary>
public static class EndOfServiceCalculator
{
    /// <summary>السنواتُ التي يُحتسب عنها نصفُ شهر.</summary>
    private const decimal HalfWageYears = 5m;

    /// <summary>
    /// المستحقُّ قبل تطبيق سُلَّم السبب — <b>وهو ما يُخصَّص شهرياً</b> بغضّ النظر عن
    /// كيفية الانتهاء، إذ لا تُعرف يومَ التخصيص.
    /// </summary>
    public static decimal FullEntitlement(decimal monthlyWage, decimal serviceYears)
    {
        if (monthlyWage < 0m)
            throw new AccountingException("الأجر الشهري لا يكون سالباً.");
        if (serviceYears <= 0m) return 0m;

        var firstFive = Math.Min(serviceYears, HalfWageYears);
        var beyond = Math.Max(0m, serviceYears - HalfWageYears);

        var amount = monthlyWage * (firstFive * 0.5m + beyond);
        return Math.Round(amount, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// نسبةُ الاستحقاق حسب السبب والمدّة (المادة 85).
    ///
    /// <para><b>والحدودُ شاملةٌ للأدنى</b>: مَن أتمّ سنتين بالضبط يستحقّ الثلث — والنظامُ
    /// يقول «من أمضى سنتين»، فاستثناءُ المتمِّ يحرمه حقّاً بيومٍ واحد.</para>
    /// </summary>
    public static decimal EntitlementFactor(EndOfServiceReason reason, decimal serviceYears) =>
        reason == EndOfServiceReason.EmployerTermination
            ? 1m
            : serviceYears switch
            {
                < 2m => 0m,
                < 5m => 1m / 3m,
                < 10m => 2m / 3m,
                _ => 1m,
            };

    /// <summary>المستحقُّ فعلاً عند الانتهاء — الكاملُ مضروباً في نسبة السبب.</summary>
    public static decimal Benefit(decimal monthlyWage, decimal serviceYears, EndOfServiceReason reason) =>
        Math.Round(FullEntitlement(monthlyWage, serviceYears) * EntitlementFactor(reason, serviceYears),
            2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// المستحقُّ لموظّفٍ حتى تاريخ.
    ///
    /// <para><b>والأجرُ هو الأخير لا المتوسّط</b> — وهو ما يفرضه النظام. فترقيةٌ في الشهر
    /// الأخير تُعيد تقدير الالتزام كلِّه، <b>وهذا سببٌ كافٍ لتخصيصٍ شهريٍّ يُعاد حسابه</b>
    /// لا تراكمٍ أعمى.</para>
    /// </summary>
    public static decimal BenefitFor(Employee employee, DateOnly asOf, EndOfServiceReason reason) =>
        Benefit(employee.GrossPay, employee.ServiceYearsAt(asOf), reason);
}
