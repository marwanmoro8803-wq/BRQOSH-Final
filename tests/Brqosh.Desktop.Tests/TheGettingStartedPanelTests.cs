using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Navigation;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Reporting;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (أسئلةُ المالك 2026-10-24 · «صفحة ترحيب ومراحل تتبّع») «ابدأ هنا» في اللوحة: تظهر ما بقي شيءٌ يستطيعه الدور، وتختفي إن اكتمل
/// كلُّ شيء أو لم يكن للدور ما يفعله.
/// </summary>
public class TheGettingStartedPanelTests : IDisposable
{
    public void Dispose()
    {
        AppNavigator.Handler = null;
        AppServices.CurrentUser = null;
    }

    private static IReadOnlyList<OnboardingStep> Steps(bool customer, bool product, bool invoice) =>
    [
        new("company-data", "بيانات", "Company data", "company_settings", false),
        new("first-customer", "عميل", "Customer", "customers", customer),
        new("first-product", "صنف", "Product", "products", product),
        new("first-invoice", "فاتورة", "Invoice", "sales_invoices", invoice),
    ];

    [Fact]
    public void ItShowsProgress_AndOpensTheScreenOfAPendingStep()
    {
        var opened = new List<string>();
        var (progress, buttons) = WpfTestHost.Invoke(() =>
        {
            AppServices.CurrentUser = AppUser.Create(1, "admin", "hash", "مدير", UserRole.Administrator);
            AppNavigator.Handler = opened.Add;

            var panel = ExecutiveDashboardBuilder.BuildGettingStarted(new Grid(), Steps(customer: true, product: false, invoice: false));
            Assert.NotNull(panel);
            var texts = new List<string>();
            CollectTexts(panel!, texts);
            var openButtons = Descendants(panel!).OfType<Button>().ToList();
            foreach (var b in openButtons)
                b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            return (texts, openButtons.Count);
        });

        Assert.Contains(progress, t => t.Contains("1 من 4", StringComparison.Ordinal));
        Assert.Equal(3, buttons);                       // ‏الخطوةُ المنجزة بلا زرّ.
        Assert.Equal(["company_settings", "products", "sales_invoices"], opened);
    }

    [Fact]
    public void ItDisappearsWhenEverythingIsDone()
    {
        var panel = WpfTestHost.Invoke(() =>
        {
            AppServices.CurrentUser = AppUser.Create(1, "admin", "hash", "مدير", UserRole.Administrator);
            var allDone = Steps(true, true, true).Select(s => s with { Done = true }).ToList();
            return ExecutiveDashboardBuilder.BuildGettingStarted(new Grid(), allDone);
        });

        Assert.Null(panel);
    }

    [Fact]
    public void ARoleThatCannotOpenAnyPendingScreen_IsNotNagged()
    {
        var panel = WpfTestHost.Invoke(() =>
        {
            AppServices.CurrentUser = AppUser.Create(2, "viewer", "hash", "مشاهد", UserRole.Viewer);
            return ExecutiveDashboardBuilder.BuildGettingStarted(new Grid(), Steps(false, false, false));
        });

        Assert.Null(panel);
    }

    private static void CollectTexts(DependencyObject node, List<string> into)
    {
        if (node is TextBlock tb)
            into.Add(tb.Text);
        foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
            CollectTexts(child, into);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject node)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
        {
            yield return child;
            foreach (var inner in Descendants(child))
                yield return inner;
        }
    }
}
