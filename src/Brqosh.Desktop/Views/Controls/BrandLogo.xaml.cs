using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Brqosh.Desktop.Views.Controls;

/// <summary>
/// شعارُ BRQOSH — درعٌ متّجهيٌّ و«BRQOSH» تحته. يُستعمل في الدخول والشريط الجانبيّ ونوافذ الإعداد،
/// <b>ولا يُرسَم الشعارُ يدويّاً في أيّ موضعٍ آخر</b>: مصدرُه الوحيدُ <c>assets/brand/brqosh-mark.svg</c>.
/// </summary>
public partial class BrandLogo : UserControl
{
    private static readonly Brush DefaultMark = Freeze(new SolidColorBrush(Color.FromRgb(0xC9, 0xA2, 0x27)));
    private static readonly Brush DefaultName = Freeze(new SolidColorBrush(Colors.White));

    public static readonly DependencyProperty MarkBrushProperty =
        DependencyProperty.Register(nameof(MarkBrush), typeof(Brush), typeof(BrandLogo), new PropertyMetadata(DefaultMark));

    public static readonly DependencyProperty NameBrushProperty =
        DependencyProperty.Register(nameof(NameBrush), typeof(Brush), typeof(BrandLogo), new PropertyMetadata(DefaultName));

    public static readonly DependencyProperty MarkHeightProperty =
        DependencyProperty.Register(nameof(MarkHeight), typeof(double), typeof(BrandLogo), new PropertyMetadata(96d));

    public static readonly DependencyProperty NameSizeProperty =
        DependencyProperty.Register(nameof(NameSize), typeof(double), typeof(BrandLogo), new PropertyMetadata(18d));

    public static readonly DependencyProperty ShowNameProperty =
        DependencyProperty.Register(nameof(ShowName), typeof(bool), typeof(BrandLogo),
            new PropertyMetadata(true, (d, _) => ((BrandLogo)d).OnShowNameChanged()));

    public static readonly DependencyProperty NameVisibilityProperty =
        DependencyProperty.Register(nameof(NameVisibility), typeof(Visibility), typeof(BrandLogo),
            new PropertyMetadata(Visibility.Visible));

    public static readonly DependencyProperty AnimateProperty =
        DependencyProperty.Register(nameof(Animate), typeof(bool), typeof(BrandLogo), new PropertyMetadata(false));

    private const string ShineKey = "ShineBoard";   // ‏في Resources لا حقلٌ: حارسُ الأسماء المعلَنة يعدّ كلَّ حقلٍ من نوع Freezable عنصراً مسمّى.

    public BrandLogo()
    {
        InitializeComponent();
        Loaded += (_, _) => StartAnimation();
        Unloaded += (_, _) => { if (Resources[ShineKey] is Storyboard board) { board.Stop(this); Resources.Remove(ShineKey); } };
    }

    /// <summary>حركةُ الشعار: دخولٌ هادئ (تكبيرٌ وظهور) ثمّ لمعةٌ تمرّ على الدرع كلَّ بضع ثوانٍ. تُحترم «تقليل الحركة» في ويندوز.</summary>
    public bool Animate { get => (bool)GetValue(AnimateProperty); set => SetValue(AnimateProperty, value); }

    private void StartAnimation()
    {
        if (!Animate || !SystemParameters.ClientAreaAnimation) return;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var enter = new Storyboard();
        AddTo(enter, new DoubleAnimation(0.82, 1, TimeSpan.FromMilliseconds(800)) { EasingFunction = ease }, MarkScale, ScaleTransform.ScaleXProperty);
        AddTo(enter, new DoubleAnimation(0.82, 1, TimeSpan.FromMilliseconds(800)) { EasingFunction = ease }, MarkScale, ScaleTransform.ScaleYProperty);
        AddTo(enter, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(650)), MarkBox, OpacityProperty);
        AddTo(enter, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(600)) { BeginTime = TimeSpan.FromMilliseconds(350) }, NameText, OpacityProperty);
        enter.Begin(this);

        // ‏اللمعةُ: تظهر وتُزاح من يسار الدرع إلى يمينه خلال ١٫٢ ث ثمّ تختفي وتنتظر؛ وتتكرّر كلَّ ٦ ث.
        var shine = new Storyboard { RepeatBehavior = RepeatBehavior.Forever, Duration = TimeSpan.FromSeconds(6) };
        AddTo(shine, new DoubleAnimation(-1.4, 1.4, TimeSpan.FromMilliseconds(1200)) { BeginTime = TimeSpan.FromMilliseconds(1600), EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } },
              ShineShift, TranslateTransform.XProperty);
        var fade = new DoubleAnimationUsingKeyFrames { BeginTime = TimeSpan.FromMilliseconds(1600) };
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, TimeSpan.FromMilliseconds(100)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, TimeSpan.FromMilliseconds(1100)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, TimeSpan.FromMilliseconds(1200)));
        AddTo(shine, fade, Shine, OpacityProperty);
        shine.Begin(this, true);
        Resources[ShineKey] = shine;
    }

    private static void AddTo(Storyboard board, Timeline animation, DependencyObject target, DependencyProperty property)
    {
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, new PropertyPath(property));
        board.Children.Add(animation);
    }

    /// <summary>لونُ الدرع — ذهبيٌّ على الخلفيّة الداكنة، وكحليٌّ على الفاتحة.</summary>
    public Brush MarkBrush { get => (Brush)GetValue(MarkBrushProperty); set => SetValue(MarkBrushProperty, value); }

    /// <summary>لونُ الاسم تحت الدرع.</summary>
    public Brush NameBrush { get => (Brush)GetValue(NameBrushProperty); set => SetValue(NameBrushProperty, value); }

    /// <summary>ارتفاعُ الدرع بوحدات الشاشة (العرضُ يتبعه بنسبة ٧٥٢ : ١٠٠٠).</summary>
    public double MarkHeight { get => (double)GetValue(MarkHeightProperty); set => SetValue(MarkHeightProperty, value); }

    public double NameSize { get => (double)GetValue(NameSizeProperty); set => SetValue(NameSizeProperty, value); }

    /// <summary>إظهارُ «BRQOSH» تحت الدرع. يُخفى حيث لا يتّسع (أيقونةٌ صغيرة)؛ <b>والاسمُ هو الأصل</b> فلا يُخفى بلا سبب.</summary>
    public bool ShowName { get => (bool)GetValue(ShowNameProperty); set => SetValue(ShowNameProperty, value); }

    public Visibility NameVisibility => (Visibility)GetValue(NameVisibilityProperty);

    private void OnShowNameChanged() =>
        SetValue(NameVisibilityProperty, ShowName ? Visibility.Visible : Visibility.Collapsed);

    private static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
