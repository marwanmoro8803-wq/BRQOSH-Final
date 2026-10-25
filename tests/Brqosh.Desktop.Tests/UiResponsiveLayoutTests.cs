using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (UI-AUDIT · الجولةُ الثانية) <b>الواجهةُ تتّسع لمساحتها — مقيسةً لا مُفترَضة.</b>
///
/// <para><b>ما قيس بتشغيل المسبار على ١٨٤ نافذةً وصفحة:</b> اثنتا عشرة نافذةٌ ارتفاعُها المصمَّم أقلُّ من محتواها
/// (الأزرارُ تركب على آخر الحقول)، وسبعٌ وثمانون صفحةً أشرطةُ أدواتها <c>DockPanel</c> تعصر ما بعدها (حقلُ تاريخٍ بعرض ٠،
/// وزرُّ «عرض» خارج الشاشة بلا تمرير)، ولوحةُ المساعد تُفتح دائماً فتترك الصفحةَ ٤٥٢ بكسلاً عند الحدّ الأدنى.</para>
///
/// <para>الحراسُ هنا تُبقي الإصلاحَ: الصفحاتُ تُقاس بلا عرضٍ على نافذةٍ بنفس مضيف <see cref="PageViewport"/>، والقواعدُ
/// المنطقيّة (الالتفاف · النموّ إلى المحتوى · طيّ المساعد) تُقاس بلا نافذةٍ أصلاً.</para>
/// </summary>
public class UiResponsiveLayoutTests
{
    private static readonly string Root = FindRepoRoot();
    private static readonly string Desktop = Path.Combine(Root, "src", "Brqosh.Desktop");

    // ── أشرطةُ الأدوات تلتفّ ────────────────────────────────────────────────

    [Fact]
    public void ToolbarDockPanel_WithRoom_PlacesLeftItemsFromTheStartAndRightItemsFromTheEnd_LikeADockPanel()
    {
        WpfTestHost.Invoke(() =>
        {
            var bar = new ToolbarDockPanel();
            var title = Box(120, 30);
            var save = Box(80, 30);
            var run = Box(60, 30);
            DockPanel.SetDock(title, Dock.Left);
            DockPanel.SetDock(save, Dock.Right);
            DockPanel.SetDock(run, Dock.Right);
            bar.Children.Add(title);
            bar.Children.Add(save);
            bar.Children.Add(run);

            bar.Measure(new Size(600, 400));
            bar.Arrange(new Rect(0, 0, 600, 400));

            Assert.Equal(30, bar.DesiredSize.Height);           // ‏سطرٌ واحد
            Assert.Equal(0, Origin(title).X);                   // ‏اليسارُ من البداية
            Assert.Equal(520, Origin(save).X);                  // ‏أوّلُ اليمين عند الطرف: ٦٠٠ − ٨٠
            Assert.Equal(460, Origin(run).X);                   // ‏وما بعده يليه: ٥٢٠ − ٦٠
            return 0;
        });
    }

    [Fact]
    public void ToolbarDockPanel_WithoutRoom_WrapsInsteadOfSqueezing_AndNoItemLosesItsWidth()
    {
        WpfTestHost.Invoke(() =>
        {
            var bar = new ToolbarDockPanel { RowSpacing = 4 };
            var items = new[] { Box(200, 30), Box(150, 30), Box(120, 30), Box(100, 30) };
            foreach (var item in items)
            {
                DockPanel.SetDock(item, Dock.Right);
                bar.Children.Add(item);
            }

            bar.Measure(new Size(400, 400));      // ‏المجموع ٥٧٠ > ٤٠٠
            bar.Arrange(new Rect(0, 0, 400, 400));

            Assert.True(bar.DesiredSize.Height > 30, "لم يلتفّ الشريط إلى سطرٍ ثانٍ.");
            Assert.All(items, i => Assert.True(i.ActualWidth >= 99.9, "عنصرٌ ضاق عن عرضه الطبيعيّ."));
            Assert.All(items, i => Assert.True(Origin(i).X >= 0 && Origin(i).X + i.ActualWidth <= 400.5, "عنصرٌ خرج عن العرض."));
            return 0;
        });
    }

    [Fact]
    public void NoToolbarCard_StillUsesAPanelThatSqueezesOrOverflows()
    {
        var offenders = new List<string>();
        foreach (var file in XamlFiles())
        {
            var text = File.ReadAllText(file);
            foreach (Match m in Regex.Matches(text,
                         @"<Border[^>]*Style=""\{StaticResource ToolbarCard\}""[^>]*>\s*<(DockPanel LastChildFill=""False""|StackPanel Orientation=""Horizontal"")"))
                offenders.Add($"{Rel(file)} — {m.Groups[1].Value}");
        }

        Assert.True(offenders.Count == 0,
            "أشرطةُ أدواتٍ تعصر أو تفيض (استعمل brq:ToolbarDockPanel أو WrapPanel):\n" + string.Join("\n", offenders));
    }

    // ── النافذةُ تنمو إلى محتواها ────────────────────────────────────────────

    [Fact]
    public void ContentOverflow_ReportsHowManyPixelsAFixedHeightWindowIsShort()
    {
        WpfTestHost.Invoke(() =>
        {
            var form = new StackPanel();
            for (var i = 0; i < 6; i++)
                form.Children.Add(Box(300, 50));

            Assert.Equal(100, WindowFit.ContentOverflow(form, 400, 200), 0.5);   // ‏٣٠٠ مطلوبة، ٢٠٠ متاحة
            Assert.Equal(0, WindowFit.ContentOverflow(form, 400, 320), 0.5);     // ‏تتّسع
            return 0;
        });
    }

    [Fact]
    public void ContentOverflow_LeavesAScrollingBodyAlone()
    {
        WpfTestHost.Invoke(() =>
        {
            var tall = new StackPanel();
            for (var i = 0; i < 20; i++)
                tall.Children.Add(Box(300, 50));
            var scrolling = new ScrollViewer { Content = tall };

            Assert.Equal(0d, WindowFit.ContentOverflow(scrolling, 400, 200));
            return 0;
        });
    }

    // ── لوحةُ المساعد ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1024, false)]
    [InlineData(1280, false)]
    [InlineData(1366, false)]    // ‏١٠٩٤ للصفحة بدل ٧٩٤
    [InlineData(1536, true)]     // ‏١٩٢٠ بتكبير ١٢٥٪
    [InlineData(1920, true)]
    public void TheAssistantPanel_OpensByDefaultOnlyWhereThePageKeepsItsRoom(double width, bool opens) =>
        Assert.Equal(opens, ShellLayout.AiPanelFits(width));

    // ── الصفحاتُ تُقاس كما يستضيفها التطبيق ─────────────────────────────────

    [Theory]
    [InlineData(1094, 638)]      // ‏١٣٦٦×٧٦٨ والمساعدُ مطويّ
    [InlineData(1008, 590)]      // ‏١٢٨٠×٧٢٠
    [InlineData(752, 430)]       // ‏١٠٢٤×٥٦٠ — الحدُّ الأدنى
    public void NoPage_HasAControlOutsideItsVisibleWidth(double width, double height)
    {
        var offenders = WpfTestHost.Invoke(() =>
        {
            var found = new List<string>();
            foreach (var type in PageTypes())
            {
                FrameworkElement page;
                try
                {
                    page = (FrameworkElement)Activator.CreateInstance(type)!;
                }
                catch (Exception)
                {
                    continue;   // ‏يحتاج خدمةً — يقيسه المسبارُ الحيّ لا هذا الحارس
                }

                var host = new ContentControl { VerticalAlignment = VerticalAlignment.Top, Content = page };
                var viewport = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    CanContentScroll = false,
                    Content = host,
                    FlowDirection = FlowDirection.RightToLeft,
                };
                if (PageViewport.HostsOwnScrolling(page))
                {
                    host.Height = Math.Max(height, PageViewport.MinPageHeight);
                }
                else
                {
                    host.MinHeight = height;
                }

                viewport.Measure(new Size(width, height));
                viewport.Arrange(new Rect(0, 0, width, height));
                viewport.UpdateLayout();

                foreach (var (name, bounds) in Controls(page))
                    if (bounds.Right > width + 2 || bounds.Left < -2)
                        found.Add($"{type.Name}: {name} {bounds.Left:0}..{bounds.Right:0} من {width:0}");

                // ‏الصفحةُ تشترك في أحداثٍ ساكنة (اللغة…) — تُحرَّر كما في PageConstructionTests فلا تُسرَّب إلى اختباراتٍ أخرى.
                host.Content = null;
                WpfTestHost.Release(page);
            }

            return found;
        });

        Assert.True(offenders.Count == 0, "عناصرُ خارجَ العرض المرئيّ:\n" + string.Join("\n", offenders));
    }

    // ── الهويّةُ المولَّدة ───────────────────────────────────────────────────

    [Fact]
    public void TheBrandMarkGeometry_IsTheOneGeneratedFromTheSvg()
    {
        var svg = File.ReadAllText(Path.Combine(Root, "assets", "brand", "brqosh-mark.svg"));
        var xaml = File.ReadAllText(Path.Combine(Desktop, "Themes", "BrandMark.xaml"));
        var path = Regex.Match(svg, @" d=""([^""]+)""").Groups[1].Value;

        static List<double> Numbers(string s) =>
            Regex.Matches(s, @"-?\d+(?:\.\d+)?").Select(m => double.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture)).ToList();

        // ‏المسارُ يستعمل M/L/H/V/Z المطلقة (تصميمُ المالك)؛ والمولِّد يفرده نقاطاً صريحةً (x,y) لكلّ أمر — فيُقارن بعد التفريد نفسِه.
        static List<double> Points(string d)
        {
            var result = new List<double>();
            double x = 0, y = 0;
            var tokens = Regex.Matches(d, @"[MHVLZ]|-?\d+(?:\.\d+)?").Select(m => m.Value).ToList();
            string cmd = "";
            for (var i = 0; i < tokens.Count;)
            {
                if ("MHVLZ".Contains(tokens[i])) { cmd = tokens[i++]; continue; }
                double D(int k) => double.Parse(tokens[k], System.Globalization.CultureInfo.InvariantCulture);
                switch (cmd)
                {
                    case "M": case "L": x = D(i); y = D(i + 1); i += 2; cmd = "L"; break;
                    case "H": x = D(i); i++; break;
                    case "V": y = D(i); i++; break;
                    default: throw new InvalidOperationException("أمرُ مسارٍ غيرُ مدعوم: " + cmd);
                }
                result.Add(x); result.Add(y);
            }
            return result;
        }

        var figures = Regex.Match(xaml, @"Figures=""([^""]+)""").Groups[1].Value;
        Assert.Equal(Points(path), Numbers(figures));
        Assert.Equal(7, Regex.Matches(path, "Z").Count);
    }

    [Fact]
    public void TheProgramIcon_IsWiredIntoTheExecutableAndTheInstaller()
    {
        var icon = Path.Combine(Root, "assets", "brand", "brqosh.ico");
        Assert.True(File.Exists(icon), "brqosh.ico غير موجود — شغّل tools/brand/build_brand.py.");
        Assert.True(new FileInfo(icon).Length > 4000, "الأيقونةُ فارغةٌ تقريباً.");

        Assert.Contains(@"<ApplicationIcon>..\..\assets\brand\brqosh.ico</ApplicationIcon>",
            File.ReadAllText(Path.Combine(Desktop, "Brqosh.Desktop.csproj")), StringComparison.Ordinal);
        var wxs = File.ReadAllText(Path.Combine(Root, "installer", "brqosh.wxs"));
        Assert.Contains("ARPPRODUCTICON", wxs, StringComparison.Ordinal);
        Assert.Contains("BrandIcon", File.ReadAllText(Path.Combine(Root, "installer", "build-msi.ps1")), StringComparison.Ordinal);
    }

    // ── أدوات ────────────────────────────────────────────────────────────────

    private static FrameworkElement Box(double width, double height) =>
        new Border { Width = width, Height = height };

    private static Point Origin(FrameworkElement e) => new(VisualOffset(e).X, VisualOffset(e).Y);

    private static Vector VisualOffset(Visual v) => VisualTreeHelper.GetOffset(v);

    private static IEnumerable<Type> PageTypes() =>
        typeof(WindowFit).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(UserControl))
                        && t.Namespace?.StartsWith("Brqosh.Desktop.Views.Pages", StringComparison.Ordinal) == true)
            .OrderBy(t => t.FullName);

    private static IEnumerable<(string Name, Rect Bounds)> Controls(FrameworkElement root)
    {
        var all = new List<(string, Rect)>();
        void Walk(DependencyObject node)
        {
            foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<object>())
            {
                if (child is not FrameworkElement fe)
                    continue;
                if (fe.Visibility != Visibility.Visible)
                    continue;
                if (fe is TextBox or PasswordBox or ComboBox or DatePicker or Button or CheckBox)
                {
                    try
                    {
                        var t = fe.TransformToAncestor(root);
                        var rect = new Rect(t.Transform(new Point(0, 0)), t.Transform(new Point(fe.ActualWidth, fe.ActualHeight)));
                        if (fe.ActualWidth > 0)
                            all.Add(($"{fe.GetType().Name}#{fe.Name}", rect));
                    }
                    catch (InvalidOperationException)
                    {
                        // ‏غيرُ متّصلٍ بشجرة الرسم (قالبٌ لم يُطبَّق) — لا قياسَ له
                    }
                }

                Walk(fe);
            }
        }

        Walk(root);
        return all;
    }

    private static IEnumerable<string> XamlFiles() =>
        Directory.EnumerateFiles(Desktop, "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    private static string Rel(string file) => Path.GetRelativePath(Desktop, file).Replace('\\', '/');

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("لم يُعثر على جذر المستودع.");
    }
}
