using System.Windows;
using System.Windows.Threading;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (CEEF-229 · م2-٥) <b>خيطُ واجهةٍ حقيقيّ بموارد الثيم — تُبنى عليه الشاشاتُ فعلاً.</b>
///
/// <para>كان هذا الجسمُ مكرَّراً حرفاً بحرف في ثلاثة ملفّات اختبار، وكلُّ نسخةٍ تُنشئ خيطاً
/// وتُدمج الثيم من جديد. وتوحيدُه ليس ترتيباً فحسب: <b>الثيمُ يُدمج مرّةً واحدة</b> في
/// <c>Application.Current.Resources</c>، فبناءُ أيّ صفحةٍ يرى الموارد نفسها التي يراها
/// التطبيقُ الحيّ — و<c>StaticResource</c> غيرُ موجود يرمي <c>XamlParseException</c> عند
/// البناء، وهو بالضبط ما نريد أن نراه.</para>
///
/// <para><b>ولماذا خيطٌ واحدٌ مشترك:</b> WPF يربط الكائن بالخيط الذي أنشأه، وخيطاً لكلّ
/// صنفِ اختبارٍ يعني تطبيقاتٍ متعدّدة و<c>Application.Current</c> متنازعاً عليه.</para>
/// </summary>
internal static class WpfTestHost
{
    private static readonly Lazy<Dispatcher> Instance = new(Start, isThreadSafe: true);

    /// <summary>خيطُ الواجهة المشترك — يبدأ عند أوّل استعمال ويبقى إلى نهاية التشغيل.</summary>
    internal static Dispatcher Dispatcher => Instance.Value;

    /// <summary>ينفّذ عملاً على خيط الواجهة ويُعيد نتيجته.</summary>
    internal static T Invoke<T>(Func<T> work) => Dispatcher.Invoke(work);

    /// <summary>ينفّذ عملاً على خيط الواجهة.</summary>
    internal static void Invoke(Action work) => Dispatcher.Invoke(work);

    private static Dispatcher Start()
    {
        Dispatcher? dispatcher = null;
        using var ready = new ManualResetEventSlim();

        var thread = new Thread(() =>
        {
            var app = System.Windows.Application.Current ?? new System.Windows.Application();

            // ‏افتراضُ WPF هو OnLastWindowClose: إغلاقُ آخر نافذةٍ يُنهي التطبيق. وأوّلُ حوارٍ
            // ‏يُغلقه الاختبار كان يُسقط التطبيق، فتفشل كلُّ شاشةٍ بعده بـ«The Application object
            // ‏is being shut down» — مئةُ فشلٍ سببُها الأداة لا المُقاس. والإغلاقُ الصريح يمنع ذلك.
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var theme = new ResourceDictionary
            {
                Source = new Uri(
                    "pack://application:,,,/Brqosh.Desktop;component/Themes/BrqoshTheme.xaml",
                    UriKind.Absolute),
            };
            if (!app.Resources.MergedDictionaries.Contains(theme))
                app.Resources.MergedDictionaries.Add(theme);

            dispatcher = Dispatcher.CurrentDispatcher;
            ready.Set();
            Dispatcher.Run();
        })
        { IsBackground = true };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        if (!ready.Wait(TimeSpan.FromSeconds(30)))
            throw new InvalidOperationException("خيط الواجهة لم يبدأ خلال ثلاثين ثانية.");

        return dispatcher!;
    }
}
