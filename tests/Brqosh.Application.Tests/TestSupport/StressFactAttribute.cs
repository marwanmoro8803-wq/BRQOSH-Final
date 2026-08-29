namespace Brqosh.Application.Tests.TestSupport;

/// <summary>
/// (CEEF-229 · م2-٤) بوّابةُ أداءٍ — <b>تُعلَن متخطّاةً، ولا تُحسب ناجحة.</b>
///
/// <para><b>ما كان:</b> كلُّ اختبارِ أداءٍ يبدأ بـ
/// <c>if (Environment.GetEnvironmentVariable("BRQOSH_SKIP_STRESS") == "1") return;</c>،
/// و<c>ci.yml</c> يضبط المتغيّر إلى <c>"1"</c>. فالبوّابتان لم تعملا في CI ولا مرّة، وتُحسبان
/// <b>ناجحتَين</b> في العدد. وهو نمطُ «العودة الصامتة» نفسه الذي أُغلق في اختبار PostgreSQL
/// (م2-٢): العددُ يطمئن والقياسُ لم يجرِ.</para>
///
/// <para><b>وقِيس الثمن قبل القرار:</b> <b>نحو أربعٍ وأربعين ثانية</b> — ٢٠٠ قيداً في ٠٫٩s
/// و٢٠٠٠ في ٤٢٫٨s. وأوّلُ رقمٍ قِسته كان «ثلاث عشرة ثانية»، وكان خطأً: قِيس على تشغيلٍ
/// <b>كانت البوّابتان فيه متخطَّاتَين أصلاً</b> — فقستُ زمنَ عدم تشغيلهما. والثمنُ مقبول
/// فأُزيل التخطّي، وبقيت السمة لمن يحتاجه محلياً: يظهر <c>Skipped</c> مُعلَناً لا
/// <c>Passed</c> كاذبة.</para>
/// </summary>
public sealed class StressFactAttribute : FactAttribute
{
    /// <summary>متغيّرُ البيئة الذي يتخطّى بوّابات الأداء — تخطٍّ معلَنٌ لا صامت.</summary>
    public const string SkipEnvironmentVariable = "BRQOSH_SKIP_STRESS";

    public StressFactAttribute()
    {
        if (Environment.GetEnvironmentVariable(SkipEnvironmentVariable) == "1")
            Skip = $"بوّابةُ أداءٍ متخطّاة بـ {SkipEnvironmentVariable}=1. "
                 + "أزِل المتغيّر لتعمل — تشغيلُها كلَّها نحو أربعٍ وأربعين ثانية.";
    }
}
