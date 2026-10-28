using System.IO;
using System.Text.RegularExpressions;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (إمكانية الوصول والفُرَش المجمّدة) يحرس أمرين:
/// <list type="bullet">
/// <item>ThemeManager يستبدل فُرَش الثيم في القاموس بفُرَش جديدة مجمَّدة ولا يعدّل فرشاة قائمة،
/// وكل استخدام لمفاتيح تلك الفُرَش في XAML صار DynamicResource فيلتقط البديل عند تبديل الثيم.</item>
/// <item>الأزرار التي بلا نصّ عربي والحقول في الواجهة الرئيسية وشاشات القوائم والمستندات لها اسم أتمتة يقرؤه قارئ الشاشة،
/// ونوافذ المستندات يدور فيها Tab داخل النافذة.</item>
/// </list>
/// </summary>
public class ThemeBrushesAndAccessibilityTests
{
    private static readonly string[] ListPages =
    {
        "MainWindow.xaml",
        "Views/Pages/SalesInvoicesPage.xaml", "Views/Pages/PurchaseInvoicesPage.xaml",
        "Views/Pages/PaymentVouchersPage.xaml", "Views/Pages/ReceiptVouchersPage.xaml",
        "Views/Pages/CustomersPage.xaml", "Views/Pages/ProductsPage.xaml",
    };

    private static readonly string[] Dialogs =
    {
        "Views/Pages/SalesInvoiceDialog.xaml", "Views/Pages/PurchaseInvoiceDialog.xaml",
        "Views/Pages/PaymentVoucherDialog.xaml", "Views/Pages/ReceiptVoucherDialog.xaml",
        "Views/Pages/CustomerDialog.xaml", "Views/Pages/ProductDialog.xaml",
    };

    private static readonly Regex Comment = new("<!--.*?-->", RegexOptions.Singleline);

    private static string DesktopRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
                return Path.Combine(dir.FullName, "src", "Brqosh.Desktop");
            dir = dir.Parent;
        }

        throw new InvalidOperationException("لم يُعثر على جذر المستودع (Brqosh.sln).");
    }

    private static string ThemeManagerSource() =>
        Regex.Replace(File.ReadAllText(Path.Combine(DesktopRoot(), "Themes", "ThemeManager.cs")), @"//.*", string.Empty);

    [Fact]
    public void ThemeManager_ReplacesBrushes_AndNeverMutatesOrThawsThem()
    {
        var src = ThemeManagerSource();
        Assert.DoesNotContain("brush.Color =", src);
        Assert.DoesNotContain("IsFrozen", src);
        Assert.DoesNotContain("EnsureMutableBrushes", src);
        Assert.Matches(@"r\[brushKey\]\s*=\s*brush;", src);
        Assert.Contains("Freeze()", src);
    }

    [Fact]
    public void ThemeBrushKeys_AreNeverBoundWithStaticResourceInXaml()
    {
        var keys = Regex.Matches(ThemeManagerSource(), @"SetColor\(r, ""\w+"", ""(\w+)""")
            .Select(m => m.Groups[1].Value).Append("SidebarGradientBrush").ToHashSet();
        Assert.True(keys.Count > 30, $"وُجد {keys.Count} مفتاح فرشاة فقط في ThemeManager — تغيّر شكله؟");

        var root = DesktopRoot();
        var staticRef = new Regex(@"\{StaticResource (\w+)\}");
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(root, "*.xaml", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                continue;
            var text = Comment.Replace(File.ReadAllText(file), string.Empty);
            foreach (Match m in staticRef.Matches(text))
                if (keys.Contains(m.Groups[1].Value))
                    offenders.Add($"{Path.GetRelativePath(root, file)}: {m.Value}");
        }

        Assert.True(offenders.Count == 0, "فُرَش ثيم مربوطة بـ StaticResource لا تتبع تبديل الثيم:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void ButtonsWithoutArabicText_AndInputs_HaveAnAutomationName()
    {
        var root = DesktopRoot();
        var button = new Regex(@"<(Button|ToggleButton|RepeatButton)\b([^>]*?)(/?)>", RegexOptions.Singleline);
        var input = new Regex(@"<(TextBox|ComboBox|DatePicker|PasswordBox)\b[^>]*>", RegexOptions.Singleline);
        var visible = new Regex(@"(?:Content|Text)=""([^""]*)""");
        var arabic = new Regex(@"[\u0600-\u06FF]");
        var missing = new List<string>();
        var scanned = 0;

        foreach (var rel in ListPages.Concat(Dialogs))
        {
            var text = Comment.Replace(File.ReadAllText(Path.Combine(root, rel)), string.Empty);
            foreach (Match m in button.Matches(text))
            {
                scanned++;
                if (HasName(m.Value))
                    continue;
                var body = m.Groups[3].Value == "/"
                    ? m.Value
                    : m.Value + SafeInner(text, m.Index + m.Length, "</" + m.Groups[1].Value);
                var shown = string.Join(" ", visible.Matches(body).Select(v => v.Groups[1].Value));
                if (!arabic.IsMatch(shown))
                    missing.Add($"{rel}: {Trim(m.Value)}");
            }

            foreach (Match m in input.Matches(text))
            {
                scanned++;
                if (!HasName(m.Value))
                    missing.Add($"{rel}: {Trim(m.Value)}");
            }
        }

        Assert.True(scanned > 60, $"فُحص {scanned} عنصراً فقط — تغيّر موضع الواجهة؟");
        Assert.True(missing.Count == 0, "عناصر بلا اسم أتمتة:\n" + string.Join("\n", missing));
    }

    [Fact]
    public void DocumentDialogs_CycleTabFocusInsideTheWindow()
    {
        var root = DesktopRoot();
        foreach (var rel in Dialogs)
        {
            var text = File.ReadAllText(Path.Combine(root, rel));
            var window = Regex.Match(text, @"<Window\b[^>]*>", RegexOptions.Singleline).Value;
            Assert.True(window.Contains(@"KeyboardNavigation.TabNavigation=""Cycle""", StringComparison.Ordinal),
                $"{rel}: نافذة المستند بلا KeyboardNavigation.TabNavigation=\"Cycle\"");
        }
    }

    private static bool HasName(string tag) =>
        tag.Contains("AutomationProperties.Name=", StringComparison.Ordinal)
        || tag.Contains("AutomationProperties.LabeledBy=", StringComparison.Ordinal);

    private static string SafeInner(string text, int from, string close)
    {
        var end = text.IndexOf(close, from, StringComparison.Ordinal);
        return end < 0 ? string.Empty : text[from..end];
    }

    private static string Trim(string tag) =>
        Regex.Replace(tag, @"\s+", " ") is var t && t.Length > 160 ? t[..160] : Regex.Replace(tag, @"\s+", " ");
}
