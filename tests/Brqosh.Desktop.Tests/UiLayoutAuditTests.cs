using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (UI-AUDIT) <b>حرّاسُ تدقيق الواجهة</b> — حتى لا تعود الشكاوى الثلاث:
/// خاناتٌ متراكبة · صفحاتٌ ونوافذُ مقصوصة · زرُّ «عرض» صامت.
/// </summary>
public class UiLayoutAuditTests
{
    private static readonly string Root = FindRepoRoot();
    private static readonly string Desktop = Path.Combine(Root, "src", "Brqosh.Desktop");
    private static readonly XNamespace P = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>
    /// خليّةُ Grid يتشاركها عنصران <b>بقصد</b> (تبديلُ إظهار/إخفاء أو طبقة) — بالملفّ والعدد.
    /// <para>والعددُ جزءٌ من الإقرار: مشاركةٌ جديدةٌ في ملفٍّ مُقَرّ تُسقط الاختبار.</para>
    /// </summary>
    private static readonly Dictionary<string, (int Count, string Reason)> SharedCellsByDesign =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["MainWindow.xaml"] = (1, "ToastOverlay فوق منطقة الصفحة — طبقةٌ لا تلتقط النقر"),
            ["Reporting/DocumentPdfPreviewWindow.xaml"] = (1, "WebView2 أو لوحةُ البديل — يُخفى أحدهما في الكود"),
            ["Themes/BrqoshTheme.xaml"] = (3, "أيقونتا العين، وشريطُ التحديد/خلفيّةُ القالب تحت ContentPresenter"),
            ["Views/ForgotPasswordWindow.xaml"] = (2, "PasswordBox/نصٌّ ظاهر/زرُّ الإظهار — حقلٌ واحدٌ بوجهين"),
            ["Views/LoginWindow.xaml"] = (4, "ثلاثةُ حقول كلمة مرور بوجهين، و«تذكّرني» يميناً و«نسيت» يساراً"),
            ["Views/Pages/CompanySettingsPage.xaml"] = (1, "صورةُ الشعار أو نصُّ «لا يوجد شعار»"),
        };

    [Fact]
    public void NoTwoSiblings_ShareAGridCell_UnlessDeclared()
    {
        var counts = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in XamlFiles())
        {
            var rel = Rel(file);
            foreach (var grid in XDocument.Load(file).Descendants(P + "Grid"))
            {
                var cells = grid.Elements()
                    .Where(e => !e.Name.LocalName.Contains('.'))
                    .GroupBy(e => ((string?)e.Attribute("Grid.Row") ?? "0", (string?)e.Attribute("Grid.Column") ?? "0"))
                    .Where(g => g.Count() > 1);
                foreach (var cell in cells)
                {
                    if (!counts.TryGetValue(rel, out var list)) counts[rel] = list = [];
                    list.Add($"({cell.Key.Item1},{cell.Key.Item2}): " + string.Join(" + ", cell.Select(Describe)));
                }
            }
        }

        var offenders = new List<string>();
        foreach (var (rel, list) in counts)
        {
            var allowed = SharedCellsByDesign.TryGetValue(rel, out var d) ? d.Count : 0;
            if (list.Count != allowed)
                offenders.Add($"{rel}: {list.Count} (المُقَرّ {allowed})\n    " + string.Join("\n    ", list));
        }
        offenders.AddRange(SharedCellsByDesign.Keys.Where(k => !counts.ContainsKey(k)).Select(k => $"{k}: مُقَرٌّ ولا مشاركة"));

        Assert.True(offenders.Count == 0, "عناصرُ متراكبةٌ في خليّة Grid واحدة:\n" + string.Join("\n", offenders));
    }

    /// <summary>كلُّ <c>Grid.Row</c> داخل الصفوف المعرَّفة — صفٌّ خارجها يُرمى فوق الأخير.</summary>
    [Fact]
    public void EveryGridRow_IsWithinTheDefinedRows()
    {
        var offenders = new List<string>();
        foreach (var file in XamlFiles())
            foreach (var grid in XDocument.Load(file).Descendants(P + "Grid"))
            {
                var rows = grid.Element(P + "Grid.RowDefinitions")?.Elements().Count() ?? 1;
                foreach (var child in grid.Elements().Where(e => !e.Name.LocalName.Contains('.')))
                    if (int.TryParse((string?)child.Attribute("Grid.Row"), out var r) && r >= Math.Max(rows, 1))
                        offenders.Add($"{Rel(file)}: {Describe(child)} Grid.Row={r} و الصفوف {rows}");
            }
        Assert.True(offenders.Count == 0, string.Join("\n", offenders));
    }

    /// <summary>
    /// شاشةُ 1366×768 بتكبير 125% تعطي ~614 وحدة. نافذةٌ حدُّها الأدنى أكبر لا تتّسع أبداً.
    /// </summary>
    [Fact]
    public void NoWindow_HasAMinHeightTallerThanASmallLaptop()
    {
        const double usable = 614;
        var offenders = XamlFiles()
            .Select(f => (f, root: XDocument.Load(f).Root!))
            .Where(x => x.root.Name == P + "Window")
            .Where(x => double.TryParse((string?)x.root.Attribute("MinHeight"), out var h) && h > usable)
            .Select(x => Rel(x.f)).ToList();
        Assert.True(offenders.Count == 0, "MinHeight أكبر من 614: " + string.Join(", ", offenders));
    }

    [Fact]
    public void PageHost_ScrollsInsideMainWindow_AndEveryWindowIsFitted()
    {
        var main = XDocument.Load(Path.Combine(Desktop, "MainWindow.xaml"));
        var host = main.Descendants(P + "ContentControl").Single(e => (string?)e.Attribute(X + "Name") == "PageHost");
        Assert.Equal(P + "ScrollViewer", host.Parent!.Name);

        var app = File.ReadAllText(Path.Combine(Desktop, "App.xaml.cs"));
        Assert.Contains("Ui.WindowFit.Register();", app, StringComparison.Ordinal);
        var mainCs = File.ReadAllText(Path.Combine(Desktop, "MainWindow.xaml.cs"));
        Assert.Contains("Ui.PageViewport.Attach(PageViewportScroll, PageHost);", mainCs, StringComparison.Ordinal);
    }

    public static TheoryData<string> ListPagesWithView() =>
    [
        "CreditNotesPage", "JournalEntriesPage", "ProgressBillingsPage", "PurchaseInvoicesPage",
        "PurchaseOrdersPage", "PurchaseRequestsPage", "StockMovementsPage", "SalesInvoicesPage",
    ];

    /// <summary>«عرض» لا يصمت: المعالجُ ملفوف، ولا <c>SingleAsync</c> يرمي داخل مسار العرض.</summary>
    [Theory]
    [MemberData(nameof(ListPagesWithView))]
    public void TheViewButton_NeverFailsSilently(string page)
    {
        var code = File.ReadAllText(Path.Combine(Desktop, "Views", "Pages", page + ".xaml.cs"));
        Assert.Matches(new Regex(@"void OnView\(object sender, RoutedEventArgs e\) =>\s*(await )?UiFeedback\.RunViewSafe(Async)?\("), code);

        var at = Regex.Match(code, @"(Task ViewSelectedAsync|void ViewSelected)\(\)").Index;
        Assert.True(at > 0, "لم يُعثر على مسار العرض في " + page);
        var body = code[at..];
        var next = body.IndexOf("\n    private ", StringComparison.Ordinal);
        if (next > 0) body = body[..next];
        Assert.DoesNotContain(".SingleAsync(", body, StringComparison.Ordinal);
        Assert.DoesNotContain("UiFeedback.ShowInfo(", body, StringComparison.Ordinal);
    }

    // ── سلوكُ WindowFit ─────────────────────────────────────────────────

    [Fact]
    public void WindowFit_MovesTheFooterOutOfTheScroll_AndKeepsTheBodyAtItsDesignedHeight()
    {
        WpfTestHost.Invoke(() =>
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var title = new TextBlock { Text = "t", Height = 40 };
            var table = new DataGrid();
            Grid.SetRow(table, 1);
            var footer = new StackPanel { Height = 50 };
            footer.Children.Add(new Button { Content = "حفظ" });
            Grid.SetRow(footer, 2);
            grid.Children.Add(title);
            grid.Children.Add(table);
            grid.Children.Add(footer);
            grid.Measure(new Size(600, 700));
            grid.Arrange(new Rect(0, 0, 600, 700));

            var result = Assert.IsType<Grid>(WindowFit.MakeScrollable(grid));

            var scroll = Assert.IsType<ScrollViewer>(result.Children[0]);
            Assert.Same(grid, scroll.Content);
            Assert.Equal(650, grid.Height, 1);              // ‏700 − صفُّ الأزرار
            Assert.Equal(2, grid.RowDefinitions.Count);
            Assert.Same(footer, result.Children[1]);
            Assert.Equal(1, Grid.GetRow(footer));           // ‏خارج التمرير — يبقى ظاهراً
            return 0;
        });
    }

    [Fact]
    public void WindowFit_StackPanelForms_KeepTheirButtonRowVisible()
    {
        WpfTestHost.Invoke(() =>
        {
            var stack = new StackPanel();
            stack.Children.Add(new TextBox());
            var buttons = new StackPanel();
            buttons.Children.Add(new Button());
            stack.Children.Add(buttons);

            var result = Assert.IsType<Grid>(WindowFit.MakeScrollable(stack));
            Assert.IsType<ScrollViewer>(result.Children[0]);
            Assert.Same(buttons, result.Children[1]);
            return 0;
        });
    }

    [Fact]
    public void PageViewport_TreatsGridPagesAsBounded_AndFormsAsNatural()
    {
        WpfTestHost.Invoke(() =>
        {
            var list = new Grid();
            list.Children.Add(new Border { Child = new DataGrid() });
            Assert.True(PageViewport.HostsOwnScrolling(list));

            var form = new StackPanel();
            form.Children.Add(new TextBox());
            form.Children.Add(new ComboBox());
            Assert.False(PageViewport.HostsOwnScrolling(form));
            return 0;
        });
    }

    // ── أدوات ─────────────────────────────────────────────────────────

    private static IEnumerable<string> XamlFiles() =>
        Directory.EnumerateFiles(Desktop, "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    private static string Rel(string file) => Path.GetRelativePath(Desktop, file).Replace('\\', '/');

    private static string Describe(XElement e) =>
        $"{e.Name.LocalName}[{(string?)e.Attribute(X + "Name") ?? (string?)e.Attribute("Name") ?? ""}]";

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("لم يُعثر على جذر المستودع.");
    }
}
