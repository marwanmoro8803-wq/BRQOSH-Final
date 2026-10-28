using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views;

/// <summary>
/// (1.0.32) <b>صفحةُ الانتظار عند أوّل تشغيل.</b> بعد إنشاء المنشأة يُجهَّز البرنامجُ (قاعدةُ البيانات والجداول والحسابات والقوائم) وهذا يستغرق وقتاً —
/// فبدل شاشةٍ فارغةٍ يرى المستخدمُ الشعارَ يلمع وحلقاتٍ تنبض وشريطاً يتحرّك ورسائلَ تتبدّل، حتى يُفتح البرنامج.
/// لا حركةَ إن عطّل المستخدمُ تأثيراتِ ويندوز (<see cref="SystemParameters.ClientAreaAnimation"/>) — تبقى الرسائلُ وحدَها.
/// </summary>
public partial class StartupSplashWindow : Window
{
    private readonly DispatcherTimer _messages = new() { Interval = TimeSpan.FromSeconds(2.6) };
    private int _step;

    // الرسائلُ بترتيب ما يحدث فعلاً عند التهيئة الأولى.
    private static (string Ar, string En)[] Steps =>
    [
        ("جارٍ تهيئة قاعدة البيانات…", "Preparing the database…"),
        ("إنشاء الدفاتر وشجرة الحسابات…", "Creating the ledgers and chart of accounts…"),
        ("تجهيز الضرائب والقوالب الخاصة ببلدك…", "Setting up your country's tax and templates…"),
        ("ضبط القوائم والصلاحيات…", "Configuring menus and permissions…"),
        ("لمسات أخيرة… سيُفتح البرنامج بعد لحظات", "Final touches… opening in a moment"),
    ];

    public StartupSplashWindow()
    {
        InitializeComponent();
        FlowDirection = LocaleManager.Flow;
        HintText.Text = LocaleManager.T("التهيئة الأولى تستغرق وقتاً أطول من المعتاد — لن تتكرّر.", "First-time setup takes a little longer — it won't happen again.");
        ShowStep();
        Loaded += (_, _) => StartMotion();
        Closed += (_, _) => _messages.Stop();
    }

    /// <summary>يُغيّر الرسالةَ المعروضة صراحةً (حين يعرف المُستدعي المرحلة) ويُعيد دورةَ التبديل.</summary>
    public void SetStatus(string arabic, string english)
    {
        _messages.Stop();
        StatusText.Text = LocaleManager.T(arabic, english);
        _messages.Start();
    }

    /// <summary>
    /// ‏(طلبُ المالك) <b>حركةُ انتقالٍ بين خطوتين</b> — نافذةٌ وحدَها على الشاشة (لا مالكَ لها ولا شيءَ خلفها) نحو ثانيتين ونصف،
    /// وتُتخطّى بنقرة. لا تحجب الخيط: الانتظارُ <c>await</c> والرسائلُ تُضخّ.
    /// </summary>
    public static async Task PlayTransitionAsync(string arabic, string english, double seconds = 2.4)
    {
        var splash = new StartupSplashWindow();
        splash.SetStatus(arabic, english);
        splash.HintText.Text = LocaleManager.T("انقر للمتابعة", "Click to continue");
        var skipped = new TaskCompletionSource();
        splash.MouseLeftButtonDown += (_, _) => skipped.TrySetResult();
        splash.Show();
        splash.Activate();
        await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(seconds)), skipped.Task);
        await splash.FadeOutAsync();
        splash.Close();
    }

    /// <summary>‏يتلاشى ثمّ يعود (لا يُغلق) — ومن عطّل الرسومَ المتحرّكة لا ينتظر شيئاً.</summary>
    public Task FadeOutAsync()
    {
        if (!Motion.Enabled || !IsVisible)
            return Task.CompletedTask;

        var done = new TaskCompletionSource();
        var fade = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut },
        };
        fade.Completed += (_, _) => done.TrySetResult();
        BeginAnimation(OpacityProperty, fade);
        return done.Task;
    }

    private void ShowStep()
    {
        var (ar, en) = Steps[Math.Min(_step, Steps.Length - 1)];
        StatusText.Text = LocaleManager.T(ar, en);
    }

    private void StartMotion()
    {
        _messages.Tick += (_, _) =>
        {
            if (_step < Steps.Length - 1)
                _step++;
            if (!SystemParameters.ClientAreaAnimation)
            {
                ShowStep();
                return;
            }

            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220));
            fadeOut.Completed += (_, _) =>
            {
                ShowStep();
                StatusText.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320)));
            };
            StatusText.BeginAnimation(OpacityProperty, fadeOut);
        };
        _messages.Start();

        if (!SystemParameters.ClientAreaAnimation)
            return;

        var forever = RepeatBehavior.Forever;
        var sine = new SineEase { EasingMode = EasingMode.EaseInOut };

        // ‏التنفّس: يتّسع التوهّجُ ويضيق.
        var breathe = new DoubleAnimation(0.88, 1.08, TimeSpan.FromSeconds(2.4)) { AutoReverse = true, RepeatBehavior = forever, EasingFunction = sine };
        GlowScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, breathe);
        GlowScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, breathe);
        Glow.BeginAnimation(OpacityProperty, new DoubleAnimation(0.35, 0.8, TimeSpan.FromSeconds(2.4)) { AutoReverse = true, RepeatBehavior = forever, EasingFunction = sine });

        // ‏حلقتان بينهما نصفُ دورة: تتّسعان من خلف الشعار وتتلاشيان.
        Ring(RingA, RingAScale, TimeSpan.Zero);
        Ring(RingB, RingBScale, TimeSpan.FromSeconds(1.6));

        // ‏اللمعةُ تنزلق على مسار الشريط ذهاباً وإياباً.
        var run = new DoubleAnimation(-120, 310, TimeSpan.FromSeconds(1.5)) { RepeatBehavior = forever, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } };
        RunnerShift.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, run);
    }

    private static void Ring(System.Windows.Shapes.Ellipse ring, System.Windows.Media.ScaleTransform scale, TimeSpan delay)
    {
        var grow = new DoubleAnimation(0.6, 2.1, TimeSpan.FromSeconds(3.2)) { BeginTime = delay, RepeatBehavior = RepeatBehavior.Forever };
        scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, grow);
        scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, grow);
        var fade = new DoubleAnimationUsingKeyFrames { BeginTime = delay, RepeatBehavior = RepeatBehavior.Forever };
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0.55, TimeSpan.Zero));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, TimeSpan.FromSeconds(3.2)));
        ring.BeginAnimation(OpacityProperty, fade);
    }
}
