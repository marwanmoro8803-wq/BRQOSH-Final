using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Brqosh.Desktop.Navigation;
using Brqosh.Desktop.Support;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Reporting;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (أسئلةُ المالك 2026-10-24) <b>ما ظهر حين سُئل البرنامجُ عن نفسه — وكلُّ واحدٍ منه كان عطلاً حقيقياً لا رأياً.</b>
/// <list type="bullet">
/// <item>لوحةُ التحكم تعرض رقمَين لكلّ مؤشّر (بطاقاتٌ من قائمة الدخل وملخّصُ أداءٍ من إجماليّات المستندات).</item>
/// <item>«إجراءات سريعة» بطاقاتٌ لا تفعل شيئاً.</item>
/// <item>المظهرُ لا يُحفظ ولا موضعَ له في أيّ إعدادات.</item>
/// <item>الدعمُ بلا طلبات: جدولُ تذاكر فارغٌ دائماً.</item>
/// </list>
/// </summary>
public class TheOwnerQuestionsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "brqosh-owner-q-" + Guid.NewGuid().ToString("N"));

    public TheOwnerQuestionsTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        AppNavigator.Handler = null;
        AppServices.CurrentUser = null;
        CurrencyLabel.Set("SAR");
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private static void Texts(DependencyObject node, List<string> into)
    {
        if (node is TextBlock tb)
            into.Add(tb.Text);
        foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
            Texts(child, into);
    }

    private static IEnumerable<FrameworkElement> Descendants(DependencyObject node)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
        {
            if (child is FrameworkElement fe)
                yield return fe;
            foreach (var inner in Descendants(child))
                yield return inner;
        }
    }

    // ───────────────────────── لوحة التحكم ─────────────────────────

    /// <summary>أرقامٌ تفرّق المصدرين: المستنداتُ (شاملةً الضريبة) مقابل قائمة الدخل.</summary>
    private static DashboardKpis Kpis() => new(
        SalesThisMonth: 115_000m, PurchasesThisMonth: 46_000m, CollectedThisMonth: 80_000m, PaidThisMonth: 30_000m,
        Receivables: 70_000m, Payables: 20_000m, NetVatThisMonth: 11_500m, InventoryValue: 5_000m,
        OpenPeriods: 3, ClosedPeriods: 9,
        RevenueThisMonth: 100_000m, ExpensesThisMonth: 40_000m, NetProfitThisMonth: 60_000m);

    [Fact]
    public void ThePerformanceSummary_ShowsTheSameFiguresAsTheKpiCards()
    {
        var texts = WpfTestHost.Invoke(() =>
        {
            CurrencyLabel.Set("SAR");
            var element = ExecutiveDashboardBuilder.BuildPerformanceSummary(new Grid(), Kpis());
            var found = new List<string>();
            Texts(element, found);
            return found;
        });

        // ‏الإيراد والمصروف والربح من قائمة الدخل — كالبطاقات.
        Assert.Contains(texts, t => t.Contains("100,000.00", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.Contains("40,000.00", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.Contains("60,000.00", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.Contains("50,000.00", StringComparison.Ordinal)); // رأس المال العامل = ذمم − دائنون

        // ‏وليس الرقمَ القديم: مبيعات المستندات شاملةً الضريبة، ولا مشتريات + سدادات.
        Assert.DoesNotContain(texts, t => t.Contains("115,000.00", StringComparison.Ordinal));
        Assert.DoesNotContain(texts, t => t.Contains("76,000.00", StringComparison.Ordinal)); // 46 + 30
        Assert.DoesNotContain(texts, t => t.Contains("69,000.00", StringComparison.Ordinal)); // 115 − 46
    }

    [Fact]
    public void TheDashboardCurrencyFollowsTheCompanyBooks()
    {
        var texts = WpfTestHost.Invoke(() =>
        {
            CurrencyLabel.Set("USD");
            var element = ExecutiveDashboardBuilder.BuildPerformanceSummary(new Grid(), Kpis());
            var found = new List<string>();
            Texts(element, found);
            return found;
        });

        Assert.Contains(texts, t => t.Contains("100,000.00 $", StringComparison.Ordinal));
        Assert.DoesNotContain(texts, t => t.Contains("ر.س", StringComparison.Ordinal));
    }

    [Fact]
    public void TheQuickActions_OpenTheirScreens_ForAUserWhoMayUseThem()
    {
        var opened = new List<string>();
        WpfTestHost.Invoke(() =>
        {
            AppServices.CurrentUser = AppUser.Create(1, "admin", "hash", "مدير", UserRole.Administrator);
            AppNavigator.Handler = opened.Add;

            var panel = ExecutiveDashboardBuilder.BuildQuickActions(new Grid());
            var chips = Descendants(panel)
                .Where(e => System.Windows.Automation.AutomationProperties.GetAutomationId(e).StartsWith("QuickAction_", StringComparison.Ordinal))
                .ToList();
            Assert.Equal(4, chips.Count);

            foreach (var chip in chips)
                chip.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
        });

        Assert.Equal(["sales_invoices", "journal_entries", "receipt_vouchers", "vat_return"], opened);
    }

    [Fact]
    public void TheQuickActions_AreHiddenFromARoleThatMayNotOpenThem()
    {
        var count = WpfTestHost.Invoke(() =>
        {
            AppServices.CurrentUser = AppUser.Create(2, "viewer", "hash", "مشاهد", UserRole.Viewer);
            var panel = ExecutiveDashboardBuilder.BuildQuickActions(new Grid());
            return Descendants(panel)
                .Count(e => System.Windows.Automation.AutomationProperties.GetAutomationId(e).StartsWith("QuickAction_", StringComparison.Ordinal));
        });

        Assert.Equal(0, count);
    }

    // ───────────────────────── العملة ─────────────────────────

    [Fact]
    public void TheCurrencyLabel_FollowsTheCompanyCurrency_AndLanguage()
    {
        CurrencyLabel.Set("AED");
        Assert.Equal("AED", CurrencyLabel.Code);
        Assert.Equal("د.إ", CurrencyLabel.Short);

        CurrencyLabel.Set("not-a-currency");
        Assert.Equal("SAR", CurrencyLabel.Code);
        Assert.Equal("1,250.50 ر.س", CurrencyLabel.Format(1250.5m));
    }

    [Fact]
    public void NoScreenWritesTheRiyalInItsCode()
    {
        // ‏ما عدا قوالبَ فيها العملةُ نفسُها (CurrencyLabel) أو سجلَّ الترجمة المولَّد: «ر.س» في شيفرة شاشةٍ عطلٌ لمنشأةٍ بعملةٍ أخرى.
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Brqosh.Desktop"));
        var offenders = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !f.EndsWith(".g.cs", StringComparison.Ordinal))
            .Where(f => File.ReadAllText(f).Contains("ر.س", StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(root, f))
            .ToList();

        Assert.True(offenders.Count == 0, "«ر.س» مكتوبةٌ في شاشات: " + string.Join("، ", offenders));
    }

    // ───────────────────────── التفضيلات ─────────────────────────

    [Fact]
    public void ThePreferences_AreRemembered_AndAMissingFileMeansTheDefaults()
    {
        var path = Path.Combine(_dir, "ui-prefs.json");

        var fresh = UiPreferencesStore.Load(path);
        Assert.True(fresh.Dark);
        Assert.False(fresh.English);

        Assert.True(UiPreferencesStore.Save(new UiPreferences { Dark = false, English = true }, path));
        var again = UiPreferencesStore.Load(path);
        Assert.False(again.Dark);
        Assert.True(again.English);
    }

    [Fact]
    public void ACorruptPreferencesFile_NeverStopsTheProgram()
    {
        var path = Path.Combine(_dir, "ui-prefs.json");
        File.WriteAllText(path, "{ not json");

        var prefs = UiPreferencesStore.Load(path);

        Assert.True(prefs.Dark);
        Assert.False(prefs.English);
    }

    [Fact]
    public void ThePreferencesPage_ShowsThemeLanguageAndTheProgramsIdentity()
    {
        var (texts, themeItems, languageItems) = WpfTestHost.Invoke(() =>
        {
            var page = new Views.Pages.PreferencesPage();
            page.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            var found = new List<string>();
            Texts(page, found);
            var theme = (page.FindName("ThemeBox") as ComboBox)!.Items.Count;
            var language = (page.FindName("LanguageBox") as ComboBox)!.Items.Count;
            var about = (page.FindName("AboutList") as ItemsControl)!.ItemsSource!.Cast<Views.Pages.PreferencesPage.AboutRow>()
                .Select(r => $"{r.Label}: {r.Value}").ToList();
            found.AddRange(about);
            WpfTestHost.Release(page);
            return (found, theme, language);
        });

        Assert.Equal(2, themeItems);
        Assert.Equal(2, languageItems);
        Assert.Contains(texts, t => t.StartsWith("المورّد: BRQOSH", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.StartsWith("الإصدار:", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.StartsWith("الفئة العمرية:", StringComparison.Ordinal) && t.Contains("١٨", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.StartsWith("نوع الاستخدام:", StringComparison.Ordinal));
    }

    // ───────────────────────── الدعم والشكاوى ─────────────────────────

    [Fact]
    public void ASupportRequest_IsRecorded_WithASequentialNumber_AndCanBeClosed()
    {
        var path = Path.Combine(_dir, "support-requests.json");

        var first = SupportRequestStore.Add(SupportRequestKind.Complaint, "بطء", "الشاشة تتأخر", "1.0.5", "شركة", "admin", path);
        var second = SupportRequestStore.Add(SupportRequestKind.Question, "سؤال", "كيف أرحّل؟", "1.0.5", "شركة", "admin", path);

        Assert.Equal("SR-0001", first.Id);
        Assert.Equal("SR-0002", second.Id);
        Assert.Equal(["SR-0002", "SR-0001"], SupportRequestStore.Load(path).Select(r => r.Id).ToArray());

        Assert.True(SupportRequestStore.Close("SR-0001", path));
        Assert.Equal(SupportRequestStatus.Closed, SupportRequestStore.Load(path).Single(r => r.Id == "SR-0001").Status);
        Assert.False(SupportRequestStore.Close("SR-9999", path));

        // ‏رقمٌ لا يتكرّر ولو أُغلق ما قبله.
        Assert.Equal("SR-0003", SupportRequestStore.Add(SupportRequestKind.Problem, "ثالث", "تفاصيل", "1.0.5", "شركة", "admin", path).Id);
    }

    [Theory]
    [InlineData("", "تفاصيل")]
    [InlineData("موضوع", "   ")]
    public void ARequestWithoutASubjectOrDetails_IsRefused(string subject, string details)
    {
        var path = Path.Combine(_dir, "support-requests.json");

        Assert.Throws<ArgumentException>(() =>
            SupportRequestStore.Add(SupportRequestKind.Problem, subject, details, "1.0", "شركة", "u", path));
        Assert.Empty(SupportRequestStore.Load(path));
    }

    [Fact]
    public void ACorruptRequestsFile_ReadsAsEmpty_NotAsACrash()
    {
        var path = Path.Combine(_dir, "support-requests.json");
        File.WriteAllText(path, "[ {broken");

        Assert.Empty(SupportRequestStore.Load(path));
    }

    [Fact]
    public void TheMailLink_CarriesTheRequest_AndStaysWithinWhatMailProgramsAccept()
    {
        var path = Path.Combine(_dir, "support-requests.json");
        var longDetails = string.Concat(Enumerable.Repeat("هذه جملةٌ عربيةٌ طويلةٌ تصف المشكلة بالتفصيل. ", 60));
        var request = SupportRequestStore.Add(SupportRequestKind.Problem, "تعطّل", longDetails, "1.0.5", "شركة", "admin", path);

        var uri = SupportRequestStore.MailtoUri("support@example.com", request, "مشكلة");

        Assert.StartsWith("mailto:support@example.com?subject=", uri, StringComparison.Ordinal);
        Assert.Contains(Uri.EscapeDataString("[SR-0001] تعطّل"), uri, StringComparison.Ordinal);
        var body = uri[(uri.IndexOf("&body=", StringComparison.Ordinal) + 6)..];
        Assert.True(body.Length <= SupportRequestStore.MailBodyEncodedLimit, $"جسمُ الرسالة المرمَّز {body.Length} حرفاً.");
        // ‏المقصوصُ يقول ذلك، والنصُّ الكاملُ في السجلّ.
        Assert.Contains(Uri.EscapeDataString("النص كاملاً في سجل الطلبات"), body, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSupportPage_ListsRecordedRequests_AndTellsTheTruthAboutSending()
    {
        // ‏لا نكتب في ملفّ المستخدم الحقيقيّ: الاختبارُ يقرأ صفحةً بلا طلبات ما دام ملفُّه غائباً — ويتحقّق من الصدق.
        var texts = WpfTestHost.Invoke(() =>
        {
            var page = new Views.Pages.CustomerSupportPage();
            page.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            var found = new List<string>();
            Texts(page, found);
            WpfTestHost.Release(page);
            return found;
        });

        Assert.Contains(texts, t => t.Contains("لا يُرسل", StringComparison.Ordinal) || t.Contains("لا يرسل", StringComparison.Ordinal)
                                    || t.Contains("يُسجَّل الطلب على هذا الجهاز", StringComparison.Ordinal));
        Assert.DoesNotContain(texts, t => t.Contains("لا تدار من داخل البرنامج", StringComparison.Ordinal));
    }
}
