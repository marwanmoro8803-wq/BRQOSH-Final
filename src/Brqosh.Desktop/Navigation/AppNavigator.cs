namespace Brqosh.Desktop.Navigation;

/// <summary>
/// (أسئلةُ المالك 2026-10-24 · لوحة التحكم) <b>بابٌ تطلب منه الشاشاتُ فتحَ شاشةٍ أخرى بمفتاحها.</b>
///
/// <para><b>ما كان:</b> «إجراءات سريعة» في اللوحة أربعُ بطاقاتٍ تُرسَم ولا تفعل شيئاً — لا معالجَ نقرٍ ولا أمر. والشاشاتُ لا تملك
/// طريقاً إلى <c>MainWindow.Navigate</c> الخاصّ.</para>
///
/// <para><b>الآن:</b> النافذةُ الرئيسةُ تُسجّل معالجَها هنا، وتبقى كلُّ الفحوص (الصلاحيةُ والترخيصُ وانتهاءُ الجلسة) في
/// <c>Navigate</c> نفسِه — فلا يتجاوزها طالبُ التنقّل. بلا نافذة (اختبار) لا يحدث شيء.</para>
/// </summary>
public static class AppNavigator
{
    /// <summary>يضبطه <c>MainWindow</c> عند فتحه ويفرغه عند إغلاقه.</summary>
    public static Action<string>? Handler { get; set; }

    /// <summary>هل نافذةٌ جاهزةٌ لاستقبال الطلب؟</summary>
    public static bool IsAvailable => Handler is not null;

    public static void Go(string pageKey) => Handler?.Invoke(pageKey);

    /// <summary>يضبطه <c>MainWindow</c>: تبديلُ لغة الواجهة كاملةً (قوائم وصفحة ومساعد واتجاه) كما يفعل زرُّ الترويسة.</summary>
    public static Action<bool>? LanguageHandler { get; set; }

    public static void SetLanguage(bool english) => LanguageHandler?.Invoke(english);
}
