using Brqosh.Domain.Tax;

namespace Brqosh.Domain.Healthcare;

/// <summary>
/// مجموعةُ معالجةٍ ضريبية واحدة.
///
/// <para><b>وسببُ الإعفاء جزءٌ من الهويّة لا زينة</b>: النظامُ يرفض سطراً صفريّاً أو
/// معفىً بلا سببٍ نظاميّ — <b>وجمعُ سطرين بسببين مختلفين في واحدٍ يُضيّع أحدَهما</b>،
/// فيخرج المستندُ الإلكترونيّ بسببٍ لا يخصّ نصفَ مبلغه.</para>
/// </summary>
public readonly record struct TaxGroup(
    VatCategory Category, string? ExemptionReasonCode, string? ExemptionReason);

/// <summary>ما حدَّ حصّةَ شركة التأمين — ويُكتب على المطالبة فلا يُبحث عنه لاحقاً.</summary>
public enum CoverageBasis
{
    /// <summary>لا وثيقةَ سارية — الحسابُ على المريض كلُّه.</summary>
    SelfPay = 0,

    /// <summary>نسبةُ التغطية هي الحدّ.</summary>
    CoveragePercent = 1,

    /// <summary>سقفُ الوثيقة السنويّ هو الحدّ — <b>وما جاوزه على المريض</b>.</summary>
    AnnualLimit = 2,
}

/// <summary>
/// قسمةُ فاتورة الزيارة بين المريض وشركة التأمين.
/// </summary>
/// <param name="PatientShare">حصّةُ المريض — تحمّلٌ وغيرُ مغطّى وما جاوز السقف.</param>
/// <param name="PayerShare">حصّةُ شركة التأمين — وهي وحدها ما يُطالَب به.</param>
/// <param name="Basis">ما حدَّ حصّةَ الشركة.</param>
public readonly record struct CoverageSplit(decimal PatientShare, decimal PayerShare, CoverageBasis Basis);

/// <summary>
/// (CEEF-230 · ط3-٢) <b>قسمةُ فاتورة الزيارة — وهي كلُّ الفرق بين عيادةٍ تُحصّل وعيادةٍ تخسر.</b>
///
/// <para><b>ما كان:</b> ملفُّ «الصحّة» أحدُ ثمانية ملفّاتِ عملٍ في شاشة التأسيس، <b>ولا
/// مريضَ في النظام ولا زيارةَ ولا وثيقةَ تأمين</b>. فالعيادةُ تكتب فاتورةً واحدةً باسم
/// المريض <b>بالمبلغ كلِّه</b> — والحقيقةُ أنّ ثمانين بالمئة منها ذمّةٌ على شركة التأمين
/// تُحصَّل بعد ستّين يوماً بمطالبةٍ ومستندات. <b>فتخرج أعمارُ الديون كلُّها خطأً</b>،
/// ويُطالَب المريضُ بما لا يلزمه.</para>
///
/// <para><b>والترتيبُ معنى لا شكل:</b> التحمّلُ الثابت يُخصم <b>أوّلاً</b> ثمّ تُطبَّق
/// النسبةُ على الباقي. وعكسُه — نسبةٌ على الكلّ ثمّ تحمّلٌ — يُعطي المريضَ رقماً آخر:
/// بفاتورةِ 1,000 وتحمّلٍ 50 وتغطيةِ 80٪ تكون حصّةُ الشركة <b>760</b> لا 800، <b>والفرقُ
/// أربعون ريالاً في كلّ زيارة</b> تُطالَب بها الشركةُ فترفضها.</para>
///
/// <para><b>وحصّةُ المريض تُشتقّ طرحاً لا حساباً مستقلاً</b>: حسابُ الطرفين كلٌّ على حدةٍ
/// ثمّ تقريبُ كلٍّ منهما يُنتج هللةً ضائعةً أو زائدة — <b>ومجموعُ الفاتورتين لا يساوي
/// الزيارة</b>، فتبقى ذمّةٌ معلّقةٌ بهللةٍ لا يُغلقها سداد.</para>
/// </summary>
public static class CoverageCalculator
{
    /// <summary>
    /// يقسم إجماليَّ الزيارة.
    /// </summary>
    /// <param name="coveredTotal">مجموعُ الخدمات المشمولة بالوثيقة.</param>
    /// <param name="uncoveredTotal">
    /// مجموعُ ما لا تشمله — <b>وهو على المريض كلُّه مهما بلغت نسبةُ التغطية</b>: تجميلٌ
    /// أو خدمةٌ مستثناةٌ نصّاً. <b>وإدخالُه في القسمة يُطالِب الشركةَ بما لا تدفعه.</b>
    /// </param>
    /// <param name="coveragePercent">نسبةُ تحمّل الشركة بعد التحمّل الثابت.</param>
    /// <param name="copayFixed">التحمّلُ الثابت للزيارة — يُخصم قبل النسبة.</param>
    /// <param name="remainingLimit">
    /// ما بقي من سقف الوثيقة — <c>null</c> يعني بلا سقف. <b>وتجاهلُه أخطرُ ما في الباب</b>:
    /// العيادةُ تُثبت ذمّةً على شركةٍ استُنفد سقفُها، <b>وتكتشف الرفضَ بعد أشهر</b> حين
    /// يكون المريضُ قد غادر.
    /// </param>
    public static CoverageSplit Split(decimal coveredTotal, decimal uncoveredTotal,
        decimal coveragePercent, decimal copayFixed, decimal? remainingLimit)
    {
        var total = Round(coveredTotal + uncoveredTotal);

        if (coveredTotal <= 0m || coveragePercent <= 0m)
            return new CoverageSplit(total, 0m, CoverageBasis.SelfPay);

        // ‏**والتحمّلُ لا يتجاوز المشمول**: زيارةٌ بثلاثين ريالاً وتحمّلٌ خمسون لا تُنتج
        // ‏باقياً سالباً تُطالَب الشركةُ بنصيبٍ منه — أي **تدفع الشركةُ للعيادة عن زيارةٍ
        // ‏دفع المريضُ كلَّها**.
        var copay = Math.Min(Round(copayFixed), Round(coveredTotal));
        var afterCopay = Round(coveredTotal) - copay;

        var byPercent = Round(afterCopay * coveragePercent / 100m);

        var basis = CoverageBasis.CoveragePercent;
        var payer = byPercent;

        if (remainingLimit is decimal limit)
        {
            var capped = Math.Max(0m, Round(limit));
            if (capped < payer)
            {
                payer = capped;
                basis = CoverageBasis.AnnualLimit;
            }
        }

        // ‏**الطرحُ هو ما يضمن التطابق** — لا حسابٌ ثانٍ للمريض.
        return new CoverageSplit(total - payer, payer, payer == 0m ? CoverageBasis.SelfPay : basis);
    }

    /// <summary>
    /// يوزّع حصّةَ الشركة على فئات الضريبة <b>بالتناسب</b>.
    ///
    /// <para><b>ولولا التوزيع لضاعت المعالجةُ الضريبية</b>: دواءٌ صفريُّ النسبة وكشفٌ
    /// خاضعٌ في زيارةٍ واحدة — وجمعُهما في سطرٍ واحدٍ على المطالبة <b>يُخضع الدواءَ
    /// للضريبة أو يُعفي الكشف</b>، وكلاهما إقرارٌ خاطئ للهيئة.</para>
    ///
    /// <para><b>والفئةُ الأخيرة تأخذ الباقي</b> — فمجموعُ الأنصبة يساوي النصيبَ تماماً،
    /// ولا تضيع هللةُ التقريب.</para>
    /// </summary>
    public static IReadOnlyList<(TaxGroup Group, decimal Amount)> Allocate(
        IReadOnlyList<(TaxGroup Group, decimal Amount)> groups, decimal share)
    {
        if (groups.Count == 0 || share <= 0m) return [];

        var basis = groups.Sum(g => g.Amount);
        if (basis <= 0m) return [];

        var result = new List<(TaxGroup, decimal)>(groups.Count);
        var allocated = 0m;

        for (var i = 0; i < groups.Count; i++)
        {
            var amount = i == groups.Count - 1
                ? Round(share) - allocated
                : Round(share * groups[i].Amount / basis);

            allocated += amount;
            if (amount != 0m) result.Add((groups[i].Group, amount));
        }

        return result;
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
