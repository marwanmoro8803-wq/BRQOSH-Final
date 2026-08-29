namespace Brqosh.Application.Tests.TestSupport;

/// <summary>
/// (CEEF-229 · م2-٢) اختبارٌ يتطلّب PostgreSQL — <b>يُعلَن متخطّىً، ولا يُحسب ناجحاً.</b>
///
/// <para>الفرق بين هذا و<c>if (!available) return;</c> هو الفرقُ بين بوّابةٍ وزينة: العودةُ
/// الصامتة تجعل الاختبار يُحسب ضمن «٩٧٣ خضراء» وهو لم يُشغَّل، فيصير العددُ كاذباً بقدر ما
/// هو مطمئن. أمّا <c>Skip</c> فيظهر في التقرير سطراً بسببه، فيُرى ويُحاسَب.</para>
///
/// <para>وبضبط <c>BRQOSH_REQUIRE_PG=1</c> لا يُتخطّى شيء: يعمل الاختبار ويفشل بغياب الخادم.
/// وهذا ما ينبغي أن تضبطه أيّ بوّابةٍ تدّعي أنها تحرس مسار الإنتاج.</para>
/// </summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (PostgresTestDatabase.SkipReason is string reason)
            Skip = reason;
    }
}
