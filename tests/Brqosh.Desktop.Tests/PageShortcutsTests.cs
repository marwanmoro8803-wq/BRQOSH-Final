using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// («دفتر الحبر والنحاس») اختصاراتُ الصفحة تعمل على اصطلاحٍ مقيس: أوّلُ زرٍّ «＋ …» ظاهرٍ مفعَّل، وحقلُ البحث. يُحرس هنا المنطقُ نفسُه
/// وأنّ القائمة الفعليّة (٥٥ من ٩٥ صفحةً تبدأ بـ«＋») لم تفقد الاصطلاحَ — فإن غيّر أحدٌ نصَّ الأزرار فسقط الاختصارُ صامتاً يُرى هنا.
/// </summary>
public class PageShortcutsTests
{
    private static T OnUi<T>(Func<T> work) => WpfTestHost.Invoke(work);

    [Fact]
    public void FindAddButton_PicksTheFirstVisibleEnabledPlusButton()
    {
        var found = OnUi(() =>
        {
            var root = new StackPanel();
            root.Children.Add(new Button { Content = "تحديث" });
            root.Children.Add(new Button { Content = "＋ عميل جديد", IsEnabled = false });
            root.Children.Add(new Button { Content = "＋ مخفي", Visibility = Visibility.Collapsed });
            root.Children.Add(new Button { Content = "＋ فاتورة جديدة" });
            root.Children.Add(new Button { Content = "＋ ثانٍ" });
            return (PageShortcuts.FindAddButton(root)?.Content as string);
        });

        Assert.Equal("＋ فاتورة جديدة", found);
    }

    [Fact]
    public void FindAddButton_IgnoresAHiddenParent()
    {
        var found = OnUi(() =>
        {
            var hidden = new StackPanel { Visibility = Visibility.Collapsed };
            hidden.Children.Add(new Button { Content = "＋ داخل مخفي" });
            var root = new StackPanel();
            root.Children.Add(hidden);
            return PageShortcuts.FindAddButton(root);
        });

        Assert.Null(found);
    }

    [Fact]
    public void FindSearchBox_PrefersANamedSearchBox_ElseTheFirstWritableOne()
    {
        var (named, fallback, none) = OnUi(() =>
        {
            var root = new StackPanel();
            root.Children.Add(new TextBox { Name = "Other", IsReadOnly = true });
            root.Children.Add(new TextBox { Name = "NameBox" });
            root.Children.Add(new TextBox { Name = "SearchBox" });
            var named2 = PageShortcuts.FindSearchBox(root)?.Name;

            var plain = new StackPanel();
            plain.Children.Add(new TextBox { Name = "A", IsEnabled = false });
            plain.Children.Add(new TextBox { Name = "B" });
            var fallback2 = PageShortcuts.FindSearchBox(plain)?.Name;

            var empty = new StackPanel();
            empty.Children.Add(new Button { Content = "x" });
            return (named2, fallback2, PageShortcuts.FindSearchBox(empty));
        });

        Assert.Equal("SearchBox", named);
        Assert.Equal("B", fallback);
        Assert.Null(none);
    }

    [Theory]
    [InlineData("CustomersPage")]
    [InlineData("SalesInvoicesPage")]
    [InlineData("SuppliersPage")]
    public void RealListPages_ExposeThePlusButtonTheShortcutRelies_On(string pageType)
    {
        // ‏الصفحاتُ الحقيقيّة تُبنى في الاختبار كما في التطبيق (الثيمُ مدموج في WpfTestHost)؛ قاعدةٌ بلا ملفّ لا تمنع البناء.
        var label = OnUi(() =>
        {
            var type = typeof(Brqosh.Desktop.MainWindow).Assembly.GetType("Brqosh.Desktop.Views.Pages." + pageType)!;
            var page = (UserControl)Activator.CreateInstance(type)!;
            try
            {
                return PageShortcuts.FindAddButton(page)?.Content as string;
            }
            finally
            {
                WpfTestHost.Release(page);
            }
        });

        Assert.NotNull(label);
        Assert.StartsWith("＋", label);
    }
}
