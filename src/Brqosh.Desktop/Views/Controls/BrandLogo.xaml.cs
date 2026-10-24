using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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

    public BrandLogo() => InitializeComponent();

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
