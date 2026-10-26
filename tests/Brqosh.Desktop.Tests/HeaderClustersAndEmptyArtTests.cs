using System.IO;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// («دفتر الحبر والنحاس» · قراراتُ التصميم المفوَّضة) المنشأةُ والفرعُ عنصرٌ واحدٌ بإطارٍ واحد، واللغةُ والمساعدُ والمظهرُ مجموعةٌ واحدة؛
/// وحالةُ الفراغ دفترٌ مرسومٌ بالفرشاة (لا مربّعُ حروف). وكلاهما قِيس بتصوير النافذة الحقيقيّة.
/// </summary>
public class HeaderClustersAndEmptyArtTests
{
    private static string Xaml(string file)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(dir!.FullName, "src", "Brqosh.Desktop", file));
    }

    [Fact]
    public void CompanyAndBranch_ShareOneBorderedCluster()
    {
        var xaml = Xaml("MainWindow.xaml");
        var company = xaml.IndexOf("x:Name=\"CompanyCombo\"", StringComparison.Ordinal);
        var branch = xaml.IndexOf("x:Name=\"BranchCombo\"", StringComparison.Ordinal);
        Assert.True(company > 0 && branch > company);
        var between = xaml[company..branch];
        Assert.DoesNotContain("</Border>\n                            <Border", between, StringComparison.Ordinal);   // ‏لا إطارٌ يُغلق ويُفتح آخرُ بينهما
        Assert.Contains("<Border Width=\"1\"", between, StringComparison.Ordinal);                                // ‏وفاصلٌ واحدٌ بينهما
    }

    [Fact]
    public void TheEmptyState_IsADrawnLedger_NotALetterBox()
    {
        var (isCanvas, paths) = WpfTestHost.Invoke(() =>
        {
            var border = new Border();
            var grid = new DataGrid { Name = "Zz" };
            border.Child = grid;
            border.Measure(new Size(400, 300));
            EmptyStateHelper.Sync(grid, 0);
            var host = (Grid)grid.Tag!;
            var overlay = host.Children.OfType<Border>().First();
            var stack = (StackPanel)overlay.Child;
            var holder = (Border)stack.Children[0];
            var canvas = holder.Child as Canvas;
            return (canvas is not null, canvas?.Children.OfType<System.Windows.Shapes.Path>().Count() ?? 0);
        });

        Assert.True(isCanvas);
        Assert.True(paths >= 2);
    }
}
