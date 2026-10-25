using System.IO;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// («دفتر الحبر والنحاس») كثافةُ الجداول مفتاحُ موردٍ واحد: تُحرس هنا قيمتاه، وأنّ DataGrid حقيقيّاً يلتقط التغيير (<c>DynamicResource</c>)
/// بلا لمس صفحة، وأنّ الاختيار يُحفظ ويعود. الاختبارُ يعيد الكثافةَ المريحة في <c>finally</c> فلا يلوّث اختباراً آخر يشارك التطبيقَ نفسه.
/// </summary>
public class DensityManagerTests
{
    [Theory]
    [InlineData(false, 40.0)]
    [InlineData(true, 32.0)]
    public void ApplyTo_WritesTheRowHeightKey(bool compact, double expected)
    {
        var resources = new ResourceDictionary();

        DensityManager.ApplyTo(resources, compact);

        Assert.Equal(expected, (double)resources["GridRowHeight"]);
    }

    [Fact]
    public void ARealDataGrid_FollowsTheDensity_WithoutAnyPageChange()
    {
        var (comfortable, compact, rowMin) = WpfTestHost.Invoke(() =>
        {
            try
            {
                // ‏نمطُ الثيم الضمنيّ يُمرَّر صراحةً: عنصرٌ بلا نافذةٍ لا يُحلّ له النمطُ الضمنيّ في الاختبار، والمقيسُ هنا هو DynamicResource نفسُه.
                static T Styled<T>(T element) where T : FrameworkElement
                {
                    element.Style = (Style)System.Windows.Application.Current.FindResource(typeof(T));
                    element.Measure(new Size(200, 200));
                    return element;
                }

                DensityManager.Apply(false);
                var a = Styled(new DataGrid()).RowHeight;
                DensityManager.Apply(true);
                var grid = Styled(new DataGrid());
                var row = Styled(new DataGridRow());
                return (a, grid.RowHeight, row.MinHeight);
            }
            finally
            {
                DensityManager.Apply(false);
            }
        });

        Assert.Equal(40.0, comfortable);
        Assert.Equal(32.0, compact);
        Assert.Equal(32.0, rowMin);
    }

    [Fact]
    public void Changed_FiresOnlyWhenTheDensityActuallyChanges()
    {
        var fired = WpfTestHost.Invoke(() =>
        {
            var count = 0;
            void Handler() => count++;
            DensityManager.Changed += Handler;
            try
            {
                DensityManager.Apply(false); // ‏لا تغيير
                DensityManager.Apply(true);  // ‏تغيير
                DensityManager.Apply(true);  // ‏لا تغيير
                DensityManager.Apply(false); // ‏تغيير
                return count;
            }
            finally
            {
                DensityManager.Changed -= Handler;
                DensityManager.Apply(false);
            }
        });

        Assert.Equal(2, fired);
    }

    [Fact]
    public void TheChoice_IsSavedAndComesBack()
    {
        var path = Path.Combine(Path.GetTempPath(), "brq-density-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            Assert.True(UiPreferencesStore.Save(new UiPreferences { Dark = false, English = true, Compact = true }, path));

            var loaded = UiPreferencesStore.Load(path);

            Assert.True(loaded.Compact);
            Assert.False(loaded.Dark);
            Assert.True(loaded.English);
            Assert.False(UiPreferencesStore.Load(Path.Combine(Path.GetTempPath(), "brq-absent-" + Guid.NewGuid().ToString("N"))).Compact);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
