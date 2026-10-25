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

    /// <summary>
    /// (CEEF-229 · م2-٦) <b>يُنشئ هذا المضيفُ التطبيقَ قبل أيّ اختبار — عمداً.</b>
    ///
    /// <para>WPF يسمح بتطبيقٍ واحدٍ في العملية كلّها، ومن أنشأه أوّلاً ملكه. وفي هذه الحزمة
    /// ستةُ أصنافٍ تُنشئ خيوطَ واجهةٍ خاصّةً بها، فأيُّها سبق صار <c>Application.Current</c>
    /// له — و<c>ShutdownMode</c> عندئذٍ غيرُ قابلٍ للضبط من خيطنا: يرمي
    /// <c>InvalidOperationException</c> <b>يُسقط مُضيفَ الاختبارات كلَّه</b>، فتُجهض الجولة
    /// ويظهر «٢٠ ناجحاً» بدل ٢٣٤ وكأنّ كلّ شيءٍ بخير.</para>
    ///
    /// <para>ومُهيّئُ الوحدة يعمل عند تحميل التجميع قبل أوّل اختبار، فيضمن أنّ التطبيق تطبيقُنا
    /// وأنّ وضعَ إغلاقه صريح — فلا يُسقطه إغلاقُ آخر نافذة.</para>
    /// </summary>
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void ClaimTheApplicationBeforeAnyTest()
    {
        // ‏تفضيلاتُ الدخول لكلّ تشغيلٍ للاختبارات في ملفٍّ مؤقّت — فلا يكتب أيُّ اختبارٍ (ولا نافذةُ دخولٍ تُبنى فيه) في %APPDATA%\BRQOSH الحقيقيّ،
        // ‏وقد كان اسمُ «demo» يعود إلى شاشة دخول المالك بعد كلّ تشغيل.
        Brqosh.Desktop.Security.LoginPreferencesStore.FilePathOverride =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "brq-tests-" + Environment.ProcessId, "login-prefs.json");
        _ = Instance.Value;
    }

    /// <summary>خيطُ الواجهة المشترك — يبدأ عند أوّل استعمال ويبقى إلى نهاية التشغيل.</summary>
    internal static Dispatcher Dispatcher => Instance.Value;

    /// <summary>ينفّذ عملاً على خيط الواجهة ويُعيد نتيجته.</summary>
    internal static T Invoke<T>(Func<T> work) => Dispatcher.Invoke(work);

    /// <summary>ينفّذ عملاً على خيط الواجهة.</summary>
    internal static void Invoke(Action work) => Dispatcher.Invoke(work);

    /// <summary>
    /// (CEEF-229 · م2-٦) <b>يُسرِّح شاشةً بُنيت للقياس — عبر مسار التفريغ الذي كتبه المشروع نفسه.</b>
    ///
    /// <para><b>لماذا لا يكفي تركُها للجامع:</b> بانيَ <c>DashboardPage</c> — وغيرِه — يشترك في
    /// حدثٍ <b>ساكن</b> (<c>LocaleManager.Changed</c>) ويُلغي اشتراكَه عند <c>Unloaded</c>. وشاشةٌ
    /// تُبنى ولا تُعرض لا يصلها <c>Unloaded</c> أبداً، فتبقى مشتركةً إلى نهاية العملية. ومسحُ
    /// كلِّ الشاشات يترك مشتركاً لكلّ واحدةٍ منها، فأوّلُ تبديلِ لغةٍ في اختبارٍ آخر يُنادي عليها جميعاً —
    /// فيرمي «The calling thread cannot access this object» ويُسقط اختباراً لا علاقة له بالمسح.
    /// أي أنّ أداةَ القياس تُلوّث ما تقيسه.</para>
    ///
    /// <para>ورفعُ <c>Unloaded</c> صراحةً يُشغّل إلغاءَ الاشتراك الذي كتبه المشروع — فلا حاجة
    /// إلى معرفةِ ما اشتركت فيه كلُّ شاشة، ويُقاس مسارُ التفريغ نفسه ضمناً.</para>
    /// </summary>
    internal static void Release(object? instance)
    {
        if (instance is FrameworkElement element)
            element.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));

        if (instance is Window window)
            window.Close();
    }

    private static Dispatcher Start()
    {
        Dispatcher? dispatcher = null;
        using var ready = new ManualResetEventSlim();

        var thread = new Thread(() =>
        {
            // ‏(CEEF-229 · م2-٦) <b>ضبطُ ShutdownMode على تطبيقٍ يملكه خيطٌ آخر يُسقط مُضيفَ
            // ‏الاختبارات كلَّه</b> — لا يرمي داخل الاختبار بل يُنهي العملية، فتُجهض الجولة
            // ‏ويظهر عددٌ مبتور (٢٠ من ٢٣٤) وكأنّ الاختبارات نجحت. ولذلك يُضبط **فقط** حين
            // ‏نكون نحن من أنشأ التطبيق على هذا الخيط.
            var existing = System.Windows.Application.Current;
            var app = existing ?? new System.Windows.Application();

            if (existing is null)
            {
                // ‏افتراضُ WPF هو OnLastWindowClose: إغلاقُ آخر نافذةٍ يُنهي التطبيق. وأوّلُ حوارٍ
                // ‏يُغلقه الاختبار كان يُسقط التطبيق، فتفشل كلُّ شاشةٍ بعده بـ«The Application object
                // ‏is being shut down» — مئةُ فشلٍ سببُها الأداة لا المُقاس.
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            }

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
