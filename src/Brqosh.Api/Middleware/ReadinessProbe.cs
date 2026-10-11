namespace Brqosh.Api.Middleware;

/// <summary>
/// <b>ما يقوله مسبارُ الجاهزيّة عن عطل القاعدة — ولمَن.</b>
///
/// <para>‏المسارُ <c>/health/ready</c> مفتوحٌ بلا مفتاح، ونصُّ خطأ المُشغِّل يحمل اسمَ المضيف واسمَ مستخدم القاعدة
/// («password authentication failed for user …»). <b>فالنصُّ الكاملُ إلى السجلّ، وفي الإنتاج يُردّ نوعُ الخطأ وحدَه</b>:
/// يُفرّق بين الشبكة والمصادقة دون أن يُعرِّف المهاجمَ بالبنية. وخارجَ الإنتاج يُردّ النصُّ كاملاً لأنّ أوّلَ دقيقةٍ في العطل أثمنُها.</para>
/// </summary>
internal static class ReadinessProbe
{
    /// <summary>نصُّ <c>databaseError</c> في ردّ المسبار — <b>والقرارُ هنا وحدَه</b>، يستدعيه المسارُ والاختبار.</summary>
    internal static string DatabaseErrorText(Exception error, bool isProduction)
    {
        ArgumentNullException.ThrowIfNull(error);
        var root = error.GetBaseException();
        return isProduction ? root.GetType().Name : root.Message;
    }
}
